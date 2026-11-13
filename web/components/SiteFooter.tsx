import Link from "next/link";

/** The footer every page shares: product line, then the three places people actually go. */
export default function SiteFooter() {
  return (
    <footer className="mx-auto flex max-w-shell flex-col items-center justify-between gap-4 border-t border-white/10 px-6 py-10 text-[13px] text-muted sm:flex-row">
      <span className="font-mono text-[11.5px] uppercase tracking-[0.12em] text-cream/80">
        Nectar v1
      </span>
      <p className="m-0">A top-edge workspace for Windows · WinUI 3 · handmade, no web view.</p>
      <div className="flex gap-5">
        <Link className="transition-colors hover:text-cream" href="/download-steps">
          Install
        </Link>
        <Link className="transition-colors hover:text-cream" href="/privacy">
          Privacy
        </Link>
        <Link className="transition-colors hover:text-cream" href="/licence">
          Licence
        </Link>
      </div>
    </footer>
  );
}
