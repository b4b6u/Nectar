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
 * The shell the two legal pages share: a display title block, a sticky contents rail, and one prose
 * column. The child markup is plain h2/p/ul/table, styled here instead of at every element.
 */
export default function LegalLayout({ title, lede, updated, toc, children }: Props) {
  return (
    <div className="relative z-10">
      <SiteHeader />

      <main className="mx-auto max-w-shell px-5 pb-24 sm:px-6">
        <header className="max-w-3xl pt-12 sm:pt-16">
          <p className="mb-5 inline-block rotate-[-1.5deg] border-2 border-ink bg-sun px-2.5 py-1 text-[10.5px] font-bold uppercase tracking-[0.24em]">
            Legal
          </p>
          <h1 className="font-display text-[clamp(2.6rem,7vw,5.2rem)] uppercase leading-[0.86]">
            {title}
          </h1>
          <p className="mt-6 text-[17px] leading-relaxed text-inkSoft">{lede}</p>
          <p className="mt-5 text-[11px] font-bold uppercase tracking-[0.18em] text-muted">
            {updated}
          </p>
        </header>

        <div className="mt-14 grid gap-10 lg:grid-cols-[190px_1fr] lg:gap-14">
          <nav aria-label="On this page" className="lg:sticky lg:top-8 lg:self-start">
            <p className="mb-3 border-b-2 border-ink pb-2 text-[10.5px] font-bold uppercase tracking-[0.2em]">
              On this page
            </p>
            <ol className="m-0 list-none space-y-2 p-0 text-[13.5px]">
              {toc.map((item) => (
                <li key={item.id}>
                  <a
                    className="text-muted transition-colors hover:text-orange"
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
              "[&>section]:scroll-mt-10 [&>section+section]:mt-12",
              "[&>section]:border-t-2 [&>section]:border-ink/15 [&>section]:pt-8",
              "[&_h2]:font-display [&_h2]:text-[clamp(1.6rem,3vw,2.2rem)] [&_h2]:uppercase [&_h2]:leading-none",
              "[&_p]:mt-4 [&_p]:text-[15px] [&_p]:leading-relaxed [&_p]:text-muted",
              "[&_ul]:mt-4 [&_ul]:list-disc [&_ul]:space-y-2 [&_ul]:pl-5 [&_ul]:text-[15px] [&_ul]:leading-relaxed [&_ul]:text-muted",
              "[&_li]:pl-1",
              "[&_strong]:font-bold [&_strong]:text-ink",
              "[&_a]:text-ink [&_a]:underline [&_a]:decoration-orange [&_a]:decoration-2 [&_a]:underline-offset-4 hover:[&_a]:text-orange",
              "[&_code]:rounded [&_code]:border [&_code]:border-ink/20 [&_code]:bg-paperDeep [&_code]:px-1.5 [&_code]:py-0.5 [&_code]:font-mono [&_code]:text-[12.5px] [&_code]:text-ink",
              "[&_pre]:mt-5 [&_pre]:overflow-x-auto [&_pre]:rounded-sticker [&_pre]:border-2 [&_pre]:border-ink [&_pre]:bg-ink [&_pre]:p-5 [&_pre]:font-mono [&_pre]:text-[12.5px] [&_pre]:leading-relaxed [&_pre]:text-paper/90",
              "[&_table]:mt-5 [&_table]:w-full [&_table]:border-collapse [&_table]:text-left [&_table]:text-[14px] [&_table]:text-muted",
              "[&_th]:border-b-2 [&_th]:border-ink [&_th]:py-3 [&_th]:pr-4 [&_th]:align-bottom [&_th]:text-[10.5px] [&_th]:uppercase [&_th]:tracking-[0.16em] [&_th]:text-ink",
              "[&_td]:border-b [&_td]:border-ink/15 [&_td]:py-3 [&_td]:pr-4 [&_td]:align-top"
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
