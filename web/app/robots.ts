import type { MetadataRoute } from "next";

/** Same host as `metadataBase` in app/layout.tsx; change both together. */
const SITE_URL = "https://nectar.app";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: "*", allow: "/" }],
    sitemap: `${SITE_URL}/sitemap.xml`
  };
}
