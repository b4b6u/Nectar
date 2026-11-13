import type { ReactNode } from "react";
import SiteFooter from "./SiteFooter";
import SiteHeader from "./SiteHeader";

type Props = {
  title: string;
  lede: string;
  updated: string;
  toc: readonly { id: string; label: string }[];
  children: ReactNode;
};

/**
 * The shell the two legal pages share: a quiet title block, a sticky contents rail, and one prose
 * column. The child markup is plain h2/p/ul/table, styled here instead of at every element.
 */
export default function LegalLayout({ title, lede, updated, toc, children }: Props) {
  return (
    <div className="relative z-10">
      <SiteHeader />

      <main className="mx-auto max-w-shell px-6 pb-28">
        <header className="max-w-2xl pt-10 sm:pt-14">
          <p className="mb-5 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">Legal</p>
          <h1 className="font-display text-4xl font-semibold leading-[1.05] sm:text-6xl">{title}</h1>
          <p className="mt-6 text-lg leading-relaxed text-muted">{lede}</p>
          <p className="mt-5 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            {updated}
          </p>
        </header>

        <div className="mt-14 grid gap-10 lg:grid-cols-[190px_1fr] lg:gap-16">
          <nav aria-label="On this page" className="lg:sticky lg:top-10 lg:self-start">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-muted">
              On this page
            </p>
            <ol className="m-0 list-none space-y-2 p-0 text-sm">
              {toc.map((item) => (
                <li key={item.id}>
                  <a
                    className="text-muted transition-colors hover:text-cream"
                    href={`#${item.id}`}
                  >
                    {item.label}
                  </a>
                </li>
              ))}
            </ol>
          </nav>

          <div
            className={[
              "max-w-3xl scroll-mt-10",
              "[&>section]:scroll-mt-10 [&>section+section]:mt-14",
              "[&_h2]:font-display [&_h2]:text-2xl [&_h2]:font-semibold [&_h2]:leading-snug",
              "[&_p]:mt-4 [&_p]:text-[15px] [&_p]:leading-relaxed [&_p]:text-muted",
              "[&_ul]:mt-4 [&_ul]:list-disc [&_ul]:space-y-2 [&_ul]:pl-5 [&_ul]:text-[15px] [&_ul]:leading-relaxed [&_ul]:text-muted",
              "[&_li]:pl-1",
              "[&_strong]:font-semibold [&_strong]:text-cream",
              "[&_a]:text-cream [&_a]:underline [&_a]:decoration-white/30 [&_a]:underline-offset-4 hover:[&_a]:decoration-nectar",
              "[&_code]:rounded [&_code]:bg-white/[0.06] [&_code]:px-1.5 [&_code]:py-0.5 [&_code]:font-mono [&_code]:text-[13px] [&_code]:text-cream/90",
              "[&_pre]:mt-5 [&_pre]:overflow-x-auto [&_pre]:rounded-2xl [&_pre]:border [&_pre]:border-white/10 [&_pre]:bg-black/50 [&_pre]:p-6 [&_pre]:font-mono [&_pre]:text-[12.5px] [&_pre]:leading-relaxed [&_pre]:text-cream/85",
              "[&_table]:mt-5 [&_table]:w-full [&_table]:border-collapse [&_table]:text-left [&_table]:text-[14px] [&_table]:text-muted",
              "[&_th]:border-b [&_th]:border-white/15 [&_th]:py-3 [&_th]:pr-4 [&_th]:align-bottom [&_th]:font-mono [&_th]:text-[11px] [&_th]:uppercase [&_th]:tracking-[0.12em] [&_th]:text-cream/80",
              "[&_td]:border-b [&_td]:border-white/[0.08] [&_td]:py-3 [&_td]:pr-4 [&_td]:align-top"
            ].join(" ")}
          >
            {children}
          </div>
        </div>
      </main>

      <SiteFooter />
    </div>
  );
}
