/**
 * App Installer feed primitives.
 *
 * Kept free of Next.js and Node APIs so it can be unit-checked with plain `tsc` + `node` and so the
 * XML shape lives in exactly one place. The route (`app/api/download/appinstaller/route.ts`) adds
 * the HTTP concern; everything here is pure string work.
 */

export interface AppInstallerFeed {
  /** Absolute URL of the feed itself; Windows re-checks this URI for updates. */
  feedUrl: string;
  /** Absolute https URL of the signed .msix. */
  msixUrl: string;
  /** Identity Name from Package.appxmanifest. */
  packageName: string;
  /** Four-part identity version, e.g. 2.0.1.0. */
  version: string;
  /** Identity Publisher — must equal the signing certificate subject exactly. */
  publisher: string;
}

export interface MainPackageIdentity {
  name: string | null;
  version: string | null;
  publisher: string | null;
  uri: string | null;
}

const ATTR = /([A-Za-z_:][\w:.-]*)\s*=\s*"([^"]*)"/g;

function decodeEntities(value: string): string {
  return value
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

export function escapeXml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

/**
 * Reads the <MainPackage> attributes out of a rendered .appinstaller. The release workflow uploads
 * that file next to the .msix, so it is the authoritative source for Publisher and Version — which
 * means the site never needs those values configured separately (and cannot drift from them).
 */
export function parseMainPackage(xml: string): MainPackageIdentity | null {
  const tag = /<MainPackage\b([^>]*?)\/?>/i.exec(xml);
  if (!tag) {
    return null;
  }

  const attributes = new Map<string, string>();
  for (const match of tag[1].matchAll(ATTR)) {
    attributes.set(match[1].toLowerCase(), decodeEntities(match[2]));
  }

  return {
    name: attributes.get("name") ?? null,
    version: attributes.get("version") ?? null,
    publisher: attributes.get("publisher") ?? null,
    uri: attributes.get("uri") ?? null
  };
}

/** v2.0.1 -> 2.0.1.0; already four-part tags pass through. Returns null for anything else. */
export function feedVersionFromTag(tag: string): string | null {
  const bare = tag.replace(/^v/i, "");
  if (!/^\d+\.\d+\.\d+(\.\d+)?$/.test(bare)) {
    return null;
  }

  return bare.split(".").length === 3 ? `${bare}.0` : bare;
}

/** Windows only follows https update URIs; loopback http is allowed so localhost testing works. */
export function isSecureFeedUrl(value: string): boolean {
  let parsed: URL;
  try {
    parsed = new URL(value);
  } catch {
    return false;
  }

  if (parsed.protocol === "https:") {
    return true;
  }

  const host = parsed.hostname.toLowerCase();
  const loopback = host === "localhost" || host === "127.0.0.1" || host === "[::1]";
  return parsed.protocol === "http:" && loopback;
}

/**
 * Builds the feed Windows reads. Refuses to emit anything with a non-https installer URL: a feed
 * that points at http:// is a man-in-the-middle who gets to choose which binary installs.
 */
export function buildAppInstaller(feed: AppInstallerFeed): string {
  if (!isSecureFeedUrl(feed.msixUrl)) {
    throw new Error(`Refusing to build an .appinstaller with a non-https MSIX URL: ${feed.msixUrl}`);
  }
  if (!isSecureFeedUrl(feed.feedUrl)) {
    throw new Error(`Refusing to build an .appinstaller with a non-https feed URL: ${feed.feedUrl}`);
  }
  if (!/^\d+\.\d+\.\d+\.\d+$/.test(feed.version)) {
    throw new Error(`Version '${feed.version}' is not a four-part identity version.`);
  }
  if (!feed.publisher) {
    throw new Error("A publisher is required; the manifest and certificate must agree.");
  }

  // The feed document's own Version tracks the package: Windows compares that attribute to
  // decide whether the feed changed, so a frozen 1.0.0.0 would make every later release look
  // identical to the first.
  return `<?xml version="1.0" encoding="utf-8"?>
<AppInstaller
    xmlns="http://schemas.microsoft.com/appx/appinstaller/2018"
    Version="${escapeXml(feed.version)}"
    Uri="${escapeXml(feed.feedUrl)}">

  <MainPackage
      Name="${escapeXml(feed.packageName)}"
      Version="${escapeXml(feed.version)}"
      Publisher="${escapeXml(feed.publisher)}"
      ProcessorArchitecture="x64"
      Uri="${escapeXml(feed.msixUrl)}" />

  <UpdateSettings>
    <!-- Check on every launch, install silently, no prompt. -->
    <OnLaunch HoursBetweenUpdateChecks="0" ShowPrompt="false" UpdateMode="Automatic" />

    <!-- Windows also polls in the background (its own cadence, roughly every 8 hours). -->
    <AutomaticBackgroundTask />

    <!-- Never block a user who is ahead of the feed. -->
    <ForceUpdateFromAnyVersion>false</ForceUpdateFromAnyVersion>
  </UpdateSettings>

  <Properties>
    <IsAutoRepairEnabled>true</IsAutoRepairEnabled>
    <UpdateBlocksActivation>false</UpdateBlocksActivation>
  </Properties>
</AppInstaller>
`;
}
