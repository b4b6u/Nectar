import { isSecureFeedUrl } from "../../../lib/appinstaller";

/**
 * The Windows download button.
 *
 * It resolves the *latest* GitHub release and redirects to its setup .exe, so the site never has to
 * be redeployed when a version ships and the button never points at a file that has been replaced.
 * The MSIX feed (`/downloads/nectar.appinstaller`) stays for people already installed that way, but
 * the plain setup exe is the one that installs without importing a certificate first.
 *
 * Configuration mirrors the feed route: NECTAR_GITHUB_REPO (or GITHUB_REPOSITORY), and an optional
 * GITHUB_TOKEN for a private repository.
 */

export const dynamic = "force-dynamic";
export const revalidate = 0;

const GITHUB_API = "https://api.github.com";

function repoSlug(): string | null {
  const raw = process.env.NECTAR_GITHUB_REPO ?? process.env.GITHUB_REPOSITORY ?? "";
  const trimmed = raw.trim();
  return /^[\w.-]+\/[\w.-]+$/.test(trimmed) ? trimmed : null;
}

function textPage(status: number, title: string, message: string): Response {
  return new Response(`${title}\n\n${message}\n`, {
    status,
    headers: {
      "Content-Type": "text/plain; charset=utf-8",
      "Cache-Control": "no-store"
    }
  });
}

interface ReleaseAsset {
  name: string;
  browser_download_url: string;
}

export async function GET(): Promise<Response> {
  const repo = repoSlug();
  if (!repo) {
    return textPage(
      500,
      "The download is not configured.",
      "This deployment has no NECTAR_GITHUB_REPO set, so it cannot find a release."
    );
  }

  const headers: Record<string, string> = {
    Accept: "application/vnd.github+json",
    "User-Agent": "nectar-site",
    "X-GitHub-Api-Version": "2022-11-28"
  };
  const token = process.env.GITHUB_TOKEN ?? process.env.GH_TOKEN;
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  let response: Response;
  try {
    response = await fetch(`${GITHUB_API}/repos/${repo}/releases/latest`, {
      headers,
      cache: "no-store",
      signal: AbortSignal.timeout(6000)
    });
  } catch {
    return textPage(502, "GitHub is not answering.", "Try again in a minute.");
  }

  if (response.status === 404) {
    return textPage(503, "Nectar is not released yet.", "The first build has not been published.");
  }
  if (!response.ok) {
    return textPage(502, "GitHub is not answering.", `GitHub returned ${response.status}.`);
  }

  const release = (await response.json()) as { assets?: ReleaseAsset[] };
  const assets = release.assets ?? [];

  // Prefer the setup exe; fall back to the portable zip for a release published without one.
  const asset =
    assets.find((candidate) => /^Nectar-Setup-.*\.exe$/i.test(candidate.name)) ??
    assets.find((candidate) => /\.zip$/i.test(candidate.name));

  if (!asset || !isSecureFeedUrl(asset.browser_download_url)) {
    return textPage(
      503,
      "That release has no Windows installer yet.",
      "The release is published but has no setup .exe attached."
    );
  }

  return new Response(null, {
    status: 302,
    headers: {
      Location: asset.browser_download_url,
      "Cache-Control": "no-store",
      "X-Nectar-Asset": asset.name
    }
  });
}
