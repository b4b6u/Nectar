const test = require("node:test");
const assert = require("node:assert/strict");

const {
  buildAppInstaller,
  feedVersionFromTag,
  isSecureFeedUrl,
  parseMainPackage
} = require("../.test-build/lib/appinstaller.js");

const FEED = {
  feedUrl: "https://nectar.app/download",
  msixUrl: "https://github.com/o/n/releases/download/v2.0.1/Nectar-2.0.1-x64.msix",
  packageName: "Nectar.Desktop",
  version: "2.0.1.0",
  publisher: "CN=Nectar Ltd, O=Nectar Ltd, C=GB"
};

test("builds the App Installer shape Windows expects", () => {
  const xml = buildAppInstaller(FEED);

  assert.ok(xml.startsWith('<?xml version="1.0" encoding="utf-8"?>'), "ships an XML declaration");
  assert.ok(xml.includes('xmlns="http://schemas.microsoft.com/appx/appinstaller/2018"'));
  assert.ok(xml.includes('Uri="https://nectar.app/download"'), "root Uri is the feed itself");
  assert.ok(xml.includes('<MainPackage'), "uses MainPackage, not MainBundle");
  assert.ok(xml.includes('Name="Nectar.Desktop"'));
  assert.ok(xml.includes('Version="2.0.1.0"'));
  assert.ok(xml.includes('ProcessorArchitecture="x64"'));
  assert.ok(xml.includes('Uri="https://github.com/o/n/releases/download/v2.0.1/Nectar-2.0.1-x64.msix"'));
  assert.ok(xml.includes('<OnLaunch HoursBetweenUpdateChecks="0" ShowPrompt="false" UpdateMode="Automatic" />'));
  assert.ok(xml.includes("<AutomaticBackgroundTask />"));
  assert.ok(xml.includes("<ForceUpdateFromAnyVersion>false</ForceUpdateFromAnyVersion>"));
  assert.ok(xml.trimEnd().endsWith("</AppInstaller>"));
  assert.equal(xml.match(/<MainPackage/g).length, 1, "exactly one MainPackage element");
});

test("escapes XML attribute values and round-trips through the parser", () => {
  const publisher = 'CN=Nectar & Sons, O=A <"quoted"> Ltd';
  const xml = buildAppInstaller({ ...FEED, publisher });
  const msix = "https://github.com/o/n/r.msix?x=1&y=2";

  assert.ok(xml.includes("CN=Nectar &amp; Sons"));
  assert.ok(xml.includes("&lt;"));
  assert.ok(xml.includes("&quot;"));
  assert.ok(!xml.includes('publisher="CN=Nectar & Sons'), "no raw ampersand survives");

  const parsed = parseMainPackage(buildAppInstaller({ ...FEED, publisher, msixUrl: msix }));
  assert.equal(parsed.publisher, publisher);
  assert.equal(parsed.uri, msix);
});

test("refuses a non-https installer URL", () => {
  assert.throws(() => buildAppInstaller({ ...FEED, msixUrl: "http://github.com/o/n/x.msix" }), /non-https MSIX URL/);
  assert.throws(() => buildAppInstaller({ ...FEED, msixUrl: "ftp://example.com/x.msix" }), /non-https MSIX URL/);
});

test("refuses a public http feed URL, but allows loopback for local testing", () => {
  assert.throws(() => buildAppInstaller({ ...FEED, feedUrl: "http://nectar.app/download" }), /non-https feed URL/);
  assert.doesNotThrow(() => buildAppInstaller({ ...FEED, feedUrl: "http://localhost:3000/download" }));
  assert.doesNotThrow(() => buildAppInstaller({ ...FEED, feedUrl: "http://127.0.0.1:3000/download" }));
});

test("refuses a version that is not four-part", () => {
  assert.throws(() => buildAppInstaller({ ...FEED, version: "2.0.1" }), /four-part/);
  assert.throws(() => buildAppInstaller({ ...FEED, version: "v2.0.1.0" }), /four-part/);
});

test("refuses an empty publisher", () => {
  assert.throws(() => buildAppInstaller({ ...FEED, publisher: "" }), /publisher is required/);
});

test("derives the four-part identity version from a tag", () => {
  assert.equal(feedVersionFromTag("v2.0.1"), "2.0.1.0");
  assert.equal(feedVersionFromTag("v2.0.1.4"), "2.0.1.4");
  assert.equal(feedVersionFromTag("V2.0.1"), "2.0.1.0");
  assert.equal(feedVersionFromTag("2.0.1"), "2.0.1.0");
  assert.equal(feedVersionFromTag("release-candidate"), null);
  assert.equal(feedVersionFromTag("v2.1"), null);
  assert.equal(feedVersionFromTag("v2.0.1.4.5"), null);
});

test("accepts only https or loopback feed URLs", () => {
  assert.equal(isSecureFeedUrl("https://nectar.app/download"), true);
  assert.equal(isSecureFeedUrl("http://localhost:3000/download"), true);
  assert.equal(isSecureFeedUrl("http://127.0.0.1/download"), true);
  assert.equal(isSecureFeedUrl("http://nectar.app/download"), false);
  assert.equal(isSecureFeedUrl("//nectar.app/download"), false);
  assert.equal(isSecureFeedUrl("not a url"), false);
});

test("parses MainPackage attributes regardless of order, case or whitespace", () => {
  const upstream = `<?xml version="1.0"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2018" Uri="https://nectar.app/download">
  <MainPackage
      version="2.0.1.0"
      NAME="Nectar.Desktop"
      Publisher="CN=Nectar &amp; Sons"
      Uri="https://github.com/o/n/x.msix" />
</AppInstaller>`;

  const parsed = parseMainPackage(upstream);
  assert.equal(parsed.name, "Nectar.Desktop");
  assert.equal(parsed.version, "2.0.1.0");
  assert.equal(parsed.publisher, "CN=Nectar & Sons");
  assert.equal(parsed.uri, "https://github.com/o/n/x.msix");
});

test("returns null when there is no MainPackage element", () => {
  assert.equal(parseMainPackage("<AppInstaller/>"), null);
  assert.equal(parseMainPackage(""), null);
});
