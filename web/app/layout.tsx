import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL("https://nectarnotch.vercel.app"),
  title: "Nectar — your day, perfectly anchored",
  description:
    "A top-anchored workspace for Windows: tasks, a focus timer, a scratchpad and today's calendar drop out of the notch. Free.",
  openGraph: {
    title: "Nectar — your day, perfectly anchored",
    description:
      "Tasks, focus timer, notepad and calendar in a Windows overlay that never steals focus.",
    url: "https://nectarnotch.vercel.app",
    siteName: "Nectar",
    images: [{ url: "/og-image.png", width: 1200, height: 630, alt: "Nectar for Windows" }],
    type: "website"
  },
  twitter: { card: "summary_large_image", images: ["/og-image.png"] },
  manifest: "/manifest.webmanifest",
  icons: {
    icon: [
      { url: "/favicon.ico", sizes: "48x48" },
      { url: "/favicon-32x32.png", type: "image/png", sizes: "32x32" },
      { url: "/favicon-16x16.png", type: "image/png", sizes: "16x16" }
    ],
    apple: { url: "/apple-touch-icon.png", sizes: "180x180" }
  }
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Londrina+Solid:wght@400;900&family=DM+Sans:opsz,wght@9..40,400;9..40,500;9..40,700&family=IBM+Plex+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="bg-paper font-sans text-ink antialiased">{children}</body>
    </html>
  );
}
