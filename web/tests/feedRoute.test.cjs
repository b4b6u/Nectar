process.env.NODE_ENV = "test";
process.env.NECTAR_GITHUB_REPO = "o/n";
process.env.FEED_RATE_LIMIT = "5";
process.env.FEED_CACHE_SECONDS = "30";
delete process.env.MSIX_PUBLISHER;
delete process.env.FEED_ALERT_WEBHOOK;

const test = require("node:test");
const assert = require("node:assert/strict");

const route = require("../.test-build/app/api/download/appinstaller/route.js");

const realFetch = global.fetch;
const MSIX_URL = "https://github.com/o/n/releases/download/v2.0.1/Nectar-2.0.1-x64.msix";

const RELEASE = {
  tag_name: "v2.0.1",
  draft: false,
  prerelease: false,
  assets: [
    { name: "Nectar-2.0.1-x64.msix", browser_download_url: MSIX_URL },
    {
      name: "nectar.appinstaller",
      browser_download_url: "https://github.com/o/n/releases/download/v2.0.1/nectar.appinstaller"
    }
  ]
};

const UPSTREAM_FEED = `<?xml version="1.0" encoding="utf-8"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2018" Version="1.0.0.0" Uri="https://nectar.app/download">
  <MainPackage Name="Nectar.Desktop" Version="2.0.1.0" Publisher="CN=Nectar Ltd, O=Nectar Ltd, C=GB" ProcessorArchitecture="x64" Uri="${MSIX_URL}" />
</AppInstaller>`;

let caseId = 0;

/** A distinct repository per test keeps the route's in-process feed cache from leaking between cases. */
function freshRepo() {
  caseId += 1;
  process.env.NECTAR_GITHUB_REPO = `o/case-${caseId}`;
  return process.env.NECTAR_GITHUB_REPO;
}

let ipSuffix = 10;
function nextIp() {
  ipSuffix += 1;
  return `198.51.100.${ipSuffix}`;
}

function call(path = "/download", ip = nextIp(), headers = {}) {
  return route.GET(
    new Request(`https://nectar.app${path}`, {
      headers: { "x-forwarded-for": ip, ...headers }
    })
  );
}

function stubGithub({ release = RELEASE, feed = UPSTREAM_FEED, failLatest = false, latestStatus } = {}) {
  const calls = [];
  global.fetch = async (url) => {
    const target = String(url);
    calls.push(target);

    if (target.endsWith("/releases/latest")) {
      if (failLatest) {
        throw new Error("network unreachable");
      }
      if (latestStatus) {
        return new Response("{}", { status: latestStatus });
      }
      return new Response(JSON.stringify(release), {
        status: 200,
        headers: { "content-type": "application/json" }
      });
    }

    if (target.endsWith(".appinstaller")) {
      return new Response(feed, { status: 200, headers: { "content-type": "application/xml" } });
    }

    return new Response("not found", { status: 404 });
  };

  return calls;
}

test.afterEach(() => {
  global.fetch = realFetch;
  process.env.NECTAR_GITHUB_REPO = "o/n";
  delete process.env.MSIX_PUBLISHER;
});

test("serves the generated feed with the right MIME type, caching and identity", async () => {
  freshRepo();
  const calls = stubGithub();

  const response = await call();
  const xml = await response.text();

  assert.equal(response.status, 200);
  assert.equal(response.headers.get("content-type"), "application/appinstaller");
  assert.match(response.headers.get("cache-control"), /s-maxage=60/);
  assert.match(response.headers.get("etag"), /^"[0-9a-f]{8}"$/);
  assert.equal(response.headers.get("x-nectar-cache"), "MISS");
  assert.equal(response.headers.get("x-nectar-feed-version"), "2.0.1.0");
  assert.ok(response.headers.get("x-nectar-request-id"));
  assert.equal(response.headers.get("x-ratelimit-limit"), "5");
  assert.match(response.headers.get("server-timing"), /^feed;dur=/);

  assert.ok(xml.includes('Publisher="CN=Nectar Ltd, O=Nectar Ltd, C=GB"'), "publisher came from the release asset");
  assert.ok(xml.includes(`Uri="${MSIX_URL}"`), "installer points at the release asset");
  assert.ok(xml.includes('Uri="https://nectar.app/download"'), "root Uri is the feed");
  assert.ok(!/__[A-Z_]+__/.test(xml), "no template placeholders survive");
  assert.equal(calls.length, 2, "one release lookup and one .appinstaller asset fetch");
});

test("answers 304 when the client already has the feed", async () => {
  freshRepo();
  stubGithub();

  const first = await call();
  await first.text();
  const etag = first.headers.get("etag");

  const second = await call("/download", undefined, { "if-none-match": etag });
  assert.equal(second.status, 304);
  assert.equal(await second.text(), "");
});

test("serves a repeat request from the in-process cache without touching GitHub", async () => {
  freshRepo();
  const calls = stubGithub();

  const first = await call();
  await first.text();
  const second = await call();
  await second.text();

  assert.equal(second.status, 200);
  assert.equal(second.headers.get("x-nectar-cache"), "HIT");
  assert.equal(calls.length, 2, "upstream is not called twice");
});

test("keeps the legacy update path in the feed URL it returns", async () => {
  freshRepo();
  stubGithub();

  const response = await call("/downloads/nectar.appinstaller");
  await response.text();

  assert.equal(response.status, 200);
  assert.equal(
    response.headers.get("x-nectar-feed-url"),
    "https://nectar.app/downloads/nectar.appinstaller"
  );
});

test("upgrades a proxied http request to https in the feed", async () => {
  freshRepo();
  stubGithub();

  const response = await route.GET(
    new Request("http://nectar.app/api/download/appinstaller", {
      headers: { "x-forwarded-for": nextIp() }
    })
  );
  const xml = await response.text();

  assert.ok(xml.includes('Uri="https://nectar.app/api/download/appinstaller"'));
});

test("returns a styled 503 before the first release exists", async () => {
  freshRepo();
  stubGithub({ latestStatus: 404 });

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 503);
  assert.equal(response.headers.get("content-type"), "text/html; charset=utf-8");
  assert.equal(response.headers.get("cache-control"), "no-store");
  assert.equal(response.headers.get("retry-after"), "3600");
  assert.match(body, /not released yet/);
  assert.match(body, /Request [0-9a-f-]+/);
});

test("returns 502 when GitHub cannot be reached", async () => {
  freshRepo();
  stubGithub({ failLatest: true });

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 502);
  assert.equal(response.headers.get("retry-after"), "300");
  assert.match(body, /GitHub is not answering/);
});

test("returns 503 when the release has no MSIX attached", async () => {
  freshRepo();
  stubGithub({ release: { ...RELEASE, assets: [] } });

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 503);
  assert.match(body, /no installer yet/);
});

test("returns 500 when neither the release asset nor the environment names a publisher", async () => {
  freshRepo();
  stubGithub({
    release: {
      ...RELEASE,
      assets: [{ name: "Nectar-2.0.1-x64.msix", browser_download_url: MSIX_URL }]
    }
  });

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 500);
  assert.match(body, /identity is incomplete/);
});

test("falls back to MSIX_PUBLISHER when the release has no .appinstaller asset", async () => {
  freshRepo();
  process.env.MSIX_PUBLISHER = "CN=Fallback Publisher";
  stubGithub({
    release: {
      ...RELEASE,
      assets: [{ name: "Nectar-2.0.1-x64.msix", browser_download_url: MSIX_URL }]
    }
  });

  const response = await call();
  const xml = await response.text();

  assert.equal(response.status, 200);
  assert.ok(xml.includes('Publisher="CN=Fallback Publisher"'));
});

test("returns 500 for a tag that is not MAJOR.MINOR.PATCH", async () => {
  freshRepo();
  stubGithub({ release: { ...RELEASE, tag_name: "nightly" } });

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 500);
  assert.match(body, /MAJOR.MINOR.PATCH/);
});

test("returns 500 when no repository is configured", async () => {
  delete process.env.NECTAR_GITHUB_REPO;
  delete process.env.GITHUB_REPOSITORY;
  stubGithub();

  const response = await call();
  const body = await response.text();

  assert.equal(response.status, 500);
  assert.match(body, /not configured/);
});

test("rate limits a client after the configured budget and leaves other clients alone", async () => {
  freshRepo();
  stubGithub();
  const busyClient = "203.0.113.77";

  const statuses = [];
  for (let index = 0; index < 6; index++) {
    const response = await call("/download", busyClient);
    await response.text();
    statuses.push(response.status);
  }

  assert.deepEqual(statuses.slice(0, 5), [200, 200, 200, 200, 200]);
  assert.equal(statuses[5], 429);

  const limited = await call("/download", busyClient);
  const body = await limited.text();
  assert.equal(limited.status, 429);
  assert.equal(limited.headers.get("cache-control"), "no-store");
  assert.ok(Number(limited.headers.get("retry-after")) >= 1);
  assert.equal(limited.headers.get("x-ratelimit-remaining"), "0");
  assert.match(body, /Too many download requests/);

  const otherClient = await call("/download", "203.0.113.78");
  await otherClient.text();
  assert.equal(otherClient.status, 200, "a different client is unaffected");
});
