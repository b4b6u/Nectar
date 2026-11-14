import Image from "next/image";
import Link from "next/link";

/**
 * The masthead: a cream bar with an ink rule under it, the wordmark set in the display face, and
 * one outlined pill on the right for the only action that matters. Short on purpose — the landing
 * page carries the tour, this bar gets you back to it or straight to the installer.
 */
export default function SiteHeader() {
  return (
    <header className="relative z-20 border-b-2 border-ink bg-paper">
      <div className="mx-auto flex max-w-shell items-center justify-between gap-4 px-5 py-4 sm:px-6">
        <Link className="group flex items-center gap-2" href="/">
          <Image
            alt="Nectar"
            className="h-[30px] w-[30px] rounded-[9px] border-2 border-ink transition-transform group-hover:-rotate-6"
            height={30}
            priority
            src="/Nectar/Assets/nectar-mark.scale-400.png"
            width={30}
          />
          <span className="font-display text-[26px] uppercase leading-none tracking-[0.01em]">
            nectar
          </span>
          <span className="text-[15px] text-orange transition-transform group-hover:scale-110">
            &hearts;
          </span>
        </Link>

        <nav
          aria-label="Sections"
          className="hidden items-center gap-7 text-[11px] font-medium uppercase tracking-[0.16em] md:flex"
        >
          <Link className="transition-colors hover:text-orange" href="/#workspace">
            Workspace
          </Link>
          <Link className="transition-colors hover:text-orange" href="/#install">
            Install
          </Link>
          <Link className="transition-colors hover:text-orange" href="/download-steps">
            The prompt
          </Link>
        </nav>

        <Link
          className="rounded-full border-2 border-ink bg-sun px-4 py-1.5 text-inkSolid text-[11px] font-bold uppercase tracking-[0.16em] shadow-sticker transition-transform hover:-translate-y-0.5 hover:bg-leaf"
          href="/download-steps"
        >
          Download <span aria-hidden>&darr;</span>
        </Link>
      </div>
    </header>
  );
}
