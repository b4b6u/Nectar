import {
  buildAppInstaller,
  feedVersionFromTag,
  isSecureFeedUrl,
  parseMainPackage
} from "../../../../lib/appinstaller";
import { RateLimiter, clientKey, fingerprint, newRequestId } from "../../../../lib/feedGuard";

/**
 * The live .appinstaller feed.
 *
 * Windows polls this URL on launch (and roughly every 8 hours in the background) and installs
 * whatever signed .msix it names. Serving it from a route rather than a committed file means the
 * release workflow never has to push to `main`, so branch protection stays on.
 *
 * Source of truth is the *latest GitHub release*: its .msix asset provides the installer URL, and
 * its .appinstaller asset provides the Publisher/Version identity. Nothing here is hard-coded, so
 * a release cannot disagree with the feed it publishes.
 *
 * Abuse and observability:
 *   - per-client sliding window (FEED_RATE_LIMIT per minute, default 60) with a 429 page;
 *   - short in-process cache (FEED_CACHE_SECONDS, default 30) plus 60s CDN caching, so a burst of
 *     update checks costs GitHub one lookup rather than thousands;
 *   - ETag/304 so a client that already has the feed transfers nothing;
 *   - one structured JSON log line per request, an X-Nectar-Request-Id on every response, and an
 *     optional FEED_ALERT_WEBHOOK POST for 5xx incidents.
 *
 * Configuration (Vercel → Environment Variables):
 *   NECTAR_GITHUB_REPO  owner/repo, e.g. "nectar-app/nectar"  (falls back to GITHUB_REPOSITORY)
 *   GITHUB_TOKEN        optional; required only for a private repository (raises the API limit)
 *   MSIX_PUBLISHER      optional fallback if a release has no .appinstaller asset
 *   FEED_RATE_LIMIT     requests per minute per client (default 60)
 *   FEED_CACHE_SECONDS  in-process feed cache lifetime (default 30)
 *   FEED_ALERT_WEBHOOK  optional https endpoint that receives JSON on 5xx
 *
 * Before the first tagged release the route answers 503 with a styled page rather than an
 * unrendered template: an .appinstaller naming a 404 .msix is worse than "not yet".
 */

export const dynamic = "force-dynamic";
export const revalidate = 0;

const PACKAGE_NAME = "Nectar.Desktop";
const GITHUB_API = "https://api.github.com";
const FETCH_TIMEOUT_MS = 6000;

const RATE_LIMIT = Math.max(1, Number(process.env.FEED_RATE_LIMIT ?? 60));
const RATE_WINDOW_MS = 60_000;
const FEED_TTL_MS = Math.max(0, Number(process.env.FEED_CACHE_SECONDS ?? 30)) * 1000;

const limiter = new RateLimiter(RATE_LIMIT, RATE_WINDOW_MS);

interface CachedFeed {
  xml: string;
  version: string;
  etag: string;
  expiresAt: number;
}

const feedCache = new Map<string, CachedFeed>();

// Tests set NODE_ENV=test to keep the runner output readable; production always logs.
const LOG_ENABLED = process.env.NODE_ENV !== "test";

interface ReleaseAsset {
  name: string;
  browser_download_url: string;
}

interface Release {
  tag_name: string;
  draft: boolean;
  prerelease: boolean;
  assets: ReleaseAsset[];
}

function log(event: string, fields: Record<string, unknown>, level: "info" | "warn" | "error" = "info"): void {
  if (!LOG_ENABLED) {
    return;
  }

  const line = JSON.stringify({ ts: new Date().toISOString(), level, event, ...fields });
  if (level === "error") {
    console.error(line);
  } else if (level === "warn") {
    console.warn(line);
  } else {
    console.log(line);
  }
}

/** Best effort: a slow alarm endpoint must never slow down or fail a download. */
function reportIncident(requestId: string, event: string, fields: Record<string, unknown>): void {
  const webhook = process.env.FEED_ALERT_WEBHOOK;
  if (!webhook || !isSecureFeedUrl(webhook)) {
    return;
  }

  void fetch(webhook, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ text: `Nectar feed incident: ${event}`, requestId, ...fields }),
    signal: AbortSignal.timeout(2000)
  }).catch(() => undefined);
}

function repoSlug(): string | null {
  const raw = process.env.NECTAR_GITHUB_REPO ?? process.env.GITHUB_REPOSITORY ?? "";
  const trimmed = raw.trim();
  return /^[\w.-]+\/[\w.-]+$/.test(trimmed) ? trimmed : null;
}

function publicUrl(request: Request): string {
  const incoming = new URL(request.url);
  const host = incoming.hostname.toLowerCase();
  const loopback = host === "localhost" || host === "127.0.0.1" || host === "[::1]";

  // Vercel terminates TLS, and a proxy that forwards http:// must not leak into the feed.
  incoming.protocol = loopback ? incoming.protocol : "https:";
  return `${incoming.origin}${incoming.pathname}`;
}

async function githubFetch(url: string, accept: string): Promise<Response> {
  const headers: Record<string, string> = {
    Accept: accept,
    "User-Agent": "nectar-site",
    "X-GitHub-Api-Version": "2022-11-28"
  };

  const token = process.env.GITHUB_TOKEN ?? process.env.GH_TOKEN;
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

  try {
    return await fetch(url, { headers, signal: controller.signal, cache: "no-store" });
  } finally {
    clearTimeout(timer);
  }
}

async function latestRelease(repo: string): Promise<Release | null | "unavailable"> {
  let response: Response;
  try {
    response = await githubFetch(`${GITHUB_API}/repos/${repo}/releases/latest`, "application/vnd.github+json");
  } catch {
    return "unavailable";
  }

  // 404 means the repository has no published, non-prerelease release yet — a normal state.
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    return "unavailable";
  }

  return (await response.json()) as Release;
}

/** Publisher from the release's own .appinstaller asset, if the workflow attached one. */
async function publisherFromReleaseAsset(release: Release): Promise<string | null> {
  const asset = release.assets.find((candidate) => candidate.name.toLowerCase().endsWith(".appinstaller"));
  if (!asset || !isSecureFeedUrl(asset.browser_download_url)) {
    return null;
  }

  try {
    const response = await githubFetch(asset.browser_download_url, "application/xml;q=0.9,*/*;q=0.8");
    if (!response.ok) {
      return null;
    }

    return parseMainPackage(await response.text())?.publisher ?? null;
  } catch {
    return null;
  }
}

function htmlPage(
  status: number,
  title: string,
  message: string,
  extraHeaders: Record<string, string>,
  requestId: string
): Response {
  const body = `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex">
<title>${title} — Nectar</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,400..800&family=IBM+Plex+Mono:wght@400;500&display=swap" rel="stylesheet">
<style>
  :root { color-scheme: dark; }
  body {
    margin: 0; min-height: 100vh; display: grid; place-items: center;
    background: #0b0b0e; color: #f3ede4;
    font-family: "Bricolage Grotesque", ui-sans-serif, system-ui, sans-serif;
  }
  main {
    max-width: 34rem; margin: 2rem; padding: 2.75rem 2.5rem;
    border: 1px solid rgba(255,255,255,0.1); border-radius: 1.5rem;
    background: linear-gradient(180deg, rgba(255,255,255,0.05), transparent);
  }
  p.eyebrow {
    margin: 0 0 1rem; font-family: "IBM Plex Mono", ui-monospace, monospace;
    font-size: 11px; letter-spacing: 0.18em; text-transform: uppercase; color: #f79a63;
  }
  h1 { margin: 0 0 1rem; font-size: 2rem; line-height: 1.15; font-weight: 640; }
  p.copy { margin: 0 0 1.75rem; color: rgba(243,237,228,0.68); line-height: 1.65; font-size: 15px; }
  a {
    color: #201109; background: #f79a63; text-decoration: none;
    padding: 0.85rem 1.6rem; border-radius: 999px; font-weight: 640; font-size: 15px;
    display: inline-block;
  }
  a:hover { background: #ffa970; }
  code { font-family: "IBM Plex Mono", ui-monospace, monospace; font-size: 13px; }
  p.ref { margin: 1.5rem 0 0; font-family: "IBM Plex Mono", ui-monospace, monospace; font-size: 11px; color: rgba(243,237,228,0.4); }
</style>
</head>
<body>
<main>
  <p class="eyebrow">Nectar for Windows</p>
  <h1>${title}</h1>
  <p class="copy">${message}</p>
  <a href="/">Back to Nectar</a>
  <p class="ref">Request ${requestId}</p>
</main>
</body>
</html>`;

  return new Response(body, {
    status,
    headers: {
      "Content-Type": "text/html; charset=utf-8",
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff",
      "X-Nectar-Request-Id": requestId,
      ...extraHeaders
    }
  });
}

function feedResponse(
  request: Request,
  xml: string,
  version: string,
  etag: string,
  feedUrl: string,
  requestId: string,
  cacheState: "MISS" | "HIT",
  remaining: number,
  startedAt: number
): Response {
  const headers: Record<string, string> = {
    "Content-Type": "application/appinstaller",
    "Content-Disposition": 'inline; filename="nectar.appinstaller"',
    // Short CDN cache: a new release should reach clients within a minute, and Windows compares
    // the Version inside the feed against the installed package.
    "Cache-Control": "public, max-age=0, s-maxage=60, stale-while-revalidate=300",
    ETag: etag,
    "X-Nectar-Cache": cacheState,
    "X-Nectar-Feed-Url": feedUrl,
    "X-Nectar-Feed-Version": version,
    "X-Nectar-Request-Id": requestId,
    "X-RateLimit-Limit": String(RATE_LIMIT),
    "X-RateLimit-Remaining": String(remaining),
    "X-Content-Type-Options": "nosniff",
    "Server-Timing": `feed;dur=${(performance.now() - startedAt).toFixed(1)}`
  };

  if (request.headers.get("if-none-match") === etag) {
    return new Response(null, { status: 304, headers });
  }

  return new Response(xml, { status: 200, headers });
}

export async function GET(request: Request): Promise<Response> {
  const startedAt = performance.now();
  const requestId = newRequestId();
  const feedUrl = publicUrl(request);
  const client = clientKey(request);

  const limit = limiter.take(client);
  if (!limit.allowed) {
    log("feed.rate_limited", { requestId, client, retryAfter: limit.retryAfterSeconds }, "warn");

    return htmlPage(
      429,
      "Too many download requests.",
      "Nectar's installer feed is being polled faster than expected. Wait a moment and try again — an installed copy checks on its own and is unaffected.",
      {
        "Retry-After": String(limit.retryAfterSeconds),
        "X-RateLimit-Limit": String(RATE_LIMIT),
        "X-RateLimit-Remaining": "0",
        "X-RateLimit-Reset": String(limit.retryAfterSeconds)
      },
      requestId
    );
  }

  const repo = repoSlug();
  if (!repo) {
    log("feed.misconfigured", { requestId, client }, "error");
    reportIncident(requestId, "feed.misconfigured", { repo: null });

    return htmlPage(
      500,
      "The download feed is not configured.",
      "This deployment has no <code>NECTAR_GITHUB_REPO</code> set, so it cannot look up a release. Set it to <code>owner/repo</code> and redeploy.",
      {},
      requestId
    );
  }

  const cacheKey = `${repo}|${feedUrl}`;
  const cached = feedCache.get(cacheKey);
  if (cached && cached.expiresAt > Date.now()) {
    return feedResponse(
      request,
      cached.xml,
      cached.version,
      cached.etag,
      feedUrl,
      requestId,
      "HIT",
      limit.remaining,
      startedAt
    );
  }

  const release = await latestRelease(repo);

  if (release === "unavailable") {
    log("feed.upstream_unavailable", { requestId, repo }, "warn");
    reportIncident(requestId, "feed.upstream_unavailable", { repo });

    return htmlPage(
      502,
      "GitHub is not answering.",
      "The release feed could not be read just now. Please try again in a minute — the installer itself is unaffected.",
      { "Retry-After": "300", "X-Nectar-Feed-Url": feedUrl },
      requestId
    );
  }

  if (!release) {
    log("feed.no_release", { requestId, repo }, "warn");

    return htmlPage(
      503,
      "Nectar is not released yet.",
      "The first signed build has not been published. This page is the live installer feed — it will serve the download the moment the release is out.",
      { "Retry-After": "3600", "X-Nectar-Feed-Url": feedUrl },
      requestId
    );
  }

  const msix = release.assets.find((asset) => asset.name.toLowerCase().endsWith(".msix"));
  if (!msix || !isSecureFeedUrl(msix.browser_download_url)) {
    log("feed.release_without_installer", { requestId, repo, tag: release.tag_name }, "error");
    reportIncident(requestId, "feed.release_without_installer", { repo, tag: release.tag_name });

    return htmlPage(
      503,
      "That release has no installer yet.",
      `Release <code>${release.tag_name}</code> is published but has no signed <code>.msix</code> attached. Nothing is installable until it does.`,
      { "Retry-After": "900", "X-Nectar-Feed-Url": feedUrl },
      requestId
    );
  }

  const publisher = (await publisherFromReleaseAsset(release)) ?? process.env.MSIX_PUBLISHER ?? null;
  const version = feedVersionFromTag(release.tag_name);

  if (!publisher || !version) {
    log("feed.identity_incomplete", { requestId, repo, tag: release.tag_name, hasPublisher: Boolean(publisher) }, "error");
    reportIncident(requestId, "feed.identity_incomplete", {
      repo,
      tag: release.tag_name,
      hasPublisher: Boolean(publisher)
    });

    return htmlPage(
      500,
      "The feed identity is incomplete.",
      !publisher
        ? "No publisher could be determined. Attach the rendered <code>.appinstaller</code> to the release, or set <code>MSIX_PUBLISHER</code> to the certificate subject."
        : `The tag <code>${release.tag_name}</code> is not a MAJOR.MINOR.PATCH version, so the four-part identity cannot be derived.`,
      { "X-Nectar-Feed-Url": feedUrl },
      requestId
    );
  }

  let xml: string;
  try {
    xml = buildAppInstaller({
      feedUrl,
      msixUrl: msix.browser_download_url,
      packageName: PACKAGE_NAME,
      version,
      publisher
    });
  } catch (error) {
    // Fail closed: a feed with a bad URI or identity must not reach Windows at all.
    log("feed.build_failed", { requestId, repo, tag: release.tag_name, error: String(error) }, "error");
    reportIncident(requestId, "feed.build_failed", { repo, tag: release.tag_name });

    return htmlPage(
      502,
      "The installer feed is being repaired.",
      "The published release metadata is inconsistent, so no feed is being served. This is deliberate: Windows must never be pointed at a build we cannot vouch for.",
      { "Retry-After": "600", "X-Nectar-Feed-Url": feedUrl },
      requestId
    );
  }

  const etag = `"${fingerprint(xml)}"`;
  if (FEED_TTL_MS > 0) {
    feedCache.set(cacheKey, { xml, version, etag, expiresAt: Date.now() + FEED_TTL_MS });
  }

  log("feed.served", {
    requestId,
    repo,
    tag: release.tag_name,
    version,
    cache: "MISS",
    ms: Number((performance.now() - startedAt).toFixed(1))
  });

  return feedResponse(request, xml, version, etag, feedUrl, requestId, "MISS", limit.remaining, startedAt);
}
