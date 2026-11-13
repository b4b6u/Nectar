import type { MetadataRoute } from "next";

const SITE_URL = "https://nectar.app";

export default function sitemap(): MetadataRoute.Sitemap {
  return [
    {
      url: `${SITE_URL}/`,
      lastModified: "2026-11-13",
      changeFrequency: "monthly",
      priority: 1
    },
    {
      url: `${SITE_URL}/download-steps`,
      lastModified: "2026-11-13",
      changeFrequency: "monthly",
      priority: 0.8
    },
    {
      url: `${SITE_URL}/privacy`,
      lastModified: "2026-11-07",
      changeFrequency: "yearly",
      priority: 0.3
    },
    {
      url: `${SITE_URL}/licence`,
      lastModified: "2026-11-07",
      changeFrequency: "yearly",
      priority: 0.3
    }
  ];
}
