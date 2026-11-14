import Link from "next/link";

/** The footer every page shares: ink band, the wordmark, the product line and the three links. */
export default function SiteFooter() {
  return (
    <footer className="relative z-10 border-t-2 border-ink bg-ink text-paper">
      {/* Extra bottom room: the forest-sound control floats in the bottom-right corner. */}
      <div className="mx-auto flex max-w-shell flex-col gap-6 px-5 pb-20 pt-10 sm:px-6 md:flex-row md:items-center md:justify-between">
        <div className="flex items-baseline gap-1.5">
          <span className="font-display text-[30px] uppercase leading-none">nectar</span>
          <span className="text-[15px] text-orange">&hearts;</span>
        </div>

        <p className="m-0 max-w-sm text-[13px] leading-relaxed text-paper/70">
          Built, signed and run locally &middot; C# &middot; .NET 8 &middot; WinUI 3. A top-edge
          workspace for Windows, handmade — no web view.
        </p>

        <nav aria-label="Footer" className="flex flex-wrap gap-5 text-[11px] uppercase tracking-[0.16em]">
          <Link className="transition-colors hover:text-sun" href="/download-steps">
            Install
          </Link>
          <Link className="transition-colors hover:text-sun" href="/privacy">
            Privacy
          </Link>
          <Link className="transition-colors hover:text-sun" href="/licence">
            Licence
          </Link>
        </nav>
      </div>
    </footer>
  );
}
