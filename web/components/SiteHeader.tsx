import Link from "next/link";

/**
 * One header for every page on the site. The nav is deliberately short — the landing page carries
 * the tour, this bar only needs to get someone back to it or straight to the installer.
 */
export default function SiteHeader() {
  return (
    <header className="mx-auto flex max-w-shell items-center justify-between px-6 py-7">
      <Link className="flex items-center gap-3" href="/">
        <span className="grid h-8 w-8 place-items-center rounded-lg bg-nectar text-sm font-bold text-[#201109]">
          N
        </span>
        <span className="font-mono text-xs tracking-[0.22em]">NECTAR</span>
        <span className="rounded-full bg-nectar px-1.5 py-0.5 font-mono text-[10px] text-[#201109]">
          v2
        </span>
      </Link>

      <nav aria-label="Sections" className="hidden items-center gap-7 text-sm text-muted sm:flex">
        <Link className="transition-colors hover:text-cream" href="/#workspace">
          Workspace
        </Link>
        <Link className="transition-colors hover:text-cream" href="/#install">
          Install
        </Link>
        <Link
          className="rounded-full border border-white/10 px-4 py-2 text-cream transition-colors hover:border-white/30"
          href="/download-steps"
        >
          Download
        </Link>
      </nav>
    </header>
  );
}
