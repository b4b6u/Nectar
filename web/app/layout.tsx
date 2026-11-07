import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL("https://nectar.app"),
  title: "Nectar — your day, perfectly anchored",
  description:
    "A top-anchored workspace for Windows: tasks, a focus timer, a scratchpad and today's calendar drop out of the notch. $9 a year.",
  openGraph: {
    title: "Nectar — your day, perfectly anchored",
    description:
      "Tasks, focus timer, notepad and calendar in a Windows overlay that never steals focus.",
    url: "https://nectar.app",
    siteName: "Nectar",
    images: [{ url: "/og.png", width: 1200, height: 630, alt: "Nectar for Windows" }],
    type: "website"
  },
  twitter: { card: "summary_large_image" },
  icons: { icon: "/favicon.ico", apple: "/apple-touch-icon.png" }
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Bricolage+Grotesque:opsz,wght@12..96,400..800&family=Instrument+Sans:ital,wght@0,400..700;1,400&family=IBM+Plex+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="bg-ink font-sans text-cream antialiased">{children}</body>
    </html>
  );
}
