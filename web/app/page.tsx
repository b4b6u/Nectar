/**
 * Landing page. The only interactive element is the CTA, which is a plain anchor to a Stripe
 * Payment Link — no client-side checkout code, no publishable key in the bundle.
 */

const FEATURES = [
  {
    title: "Tasks",
    copy: "Type a line, press Enter. The ring completes it, done rows sink, and every check-off feeds the weekly chart.",
    dot: "#8DBA98"
  },
  {
    title: "Focus timer",
    copy: "A 25-minute countdown derived from a deadline, not from ticks — a busy machine cannot make it drift.",
    dot: "#A39CD0"
  },
  {
    title: "Notepad",
    copy: "A scratch page with no title, no file and no save button. It writes itself 700 ms after you stop typing.",
    dot: "#D2CA72"
  },
  {
    title: "Calendar",
    copy: "Today's events in one row each, with a reminder tray that snoozes five minutes or dismisses with Esc.",
    dot: "#8CB1CD"
  }
] as const;

const PASTELS = {
  sage: "#8DBA98",
  lavender: "#A39CD0",
  mustard: "#D2CA72",
  steel: "#8CB1CD"
} as const;

function paletteFor(index: number) {
  return [PASTELS.sage, PASTELS.lavender, PASTELS.mustard, PASTELS.steel][index % 4];
}

const CHECKOUT_URL =
  process.env.NEXT_PUBLIC_STRIPE_PAYMENT_LINK ?? "https://buy.stripe.com/replace-with-your-payment-link";

export default function Home() {
  return (
    <div className="relative z-10">
      {/* Nav */}
      <header className="mx-auto flex max-w-shell items-center justify-between px-6 py-7">
        <div className="flex items-center gap-3">
          <span className="grid h-8 w-8 place-items-center rounded-lg bg-nectar text-sm font-bold text-[#201109]">
            N
          </span>
          <span className="font-mono text-xs tracking-[0.22em]">NECTAR</span>
          <span className="rounded-full bg-nectar px-1.5 py-0.5 font-mono text-[10px] text-[#201109]">
            v2
          </span>
        </div>
        <nav className="hidden items-center gap-7 text-sm text-muted sm:flex">
          <a className="transition-colors hover:text-cream" href="#features">
            Features
          </a>
          <a className="transition-colors hover:text-cream" href="#pricing">
            Pricing
          </a>
          <a
            className="rounded-full border border-white/10 px-4 py-2 text-cream transition-colors hover:border-white/30"
            href="/downloads/nectar.appinstaller"
          >
            Download
          </a>
        </nav>
      </header>

      {/* Hero */}
      <main>
        <section className="mx-auto max-w-shell px-6 pb-24 pt-16 text-center sm:pt-24">
          <p className="mb-7 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
            Windows 10 &amp; 11 · WinUI 3 · $9 / year
          </p>
          <h1 className="mx-auto max-w-4xl font-display text-hero font-bold">
            Your day,{" "}
            <em className="font-medium not-italic text-nectar">perfectly anchored.</em>
          </h1>
          <p className="mx-auto mt-7 max-w-xl text-lg text-muted">
            Nectar hangs off the top edge of your screen. Pull it down and your tasks, a focus
            timer, a scratchpad and today&apos;s calendar are already open — without stealing focus
            from the app you are working in.
          </p>

          <div className="mt-10 flex flex-wrap items-center justify-center gap-3">
            <a
              className="rounded-full bg-nectar px-7 py-3.5 text-[15px] font-semibold text-[#201109] shadow-[0_14px_40px_-18px_rgba(247,154,99,0.9)] transition hover:-translate-y-px hover:bg-[#ffa970]"
              href={CHECKOUT_URL}
            >
              Buy Nectar — $9 / year
            </a>
            <a
              className="rounded-full border border-white/10 px-7 py-3.5 text-[15px] font-semibold text-cream transition hover:border-white/30"
              href="/downloads/nectar.appinstaller"
            >
              Free trial install
            </a>
          </div>

          <p className="mt-5 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            One machine, unlimited updates · Cancel any time
          </p>

          {/* The notch, collapsed */}
          <div className="mx-auto mt-16 w-[260px] rounded-2xl border border-white/10 bg-shell px-4 py-3 shadow-[0_40px_120px_-40px_rgba(0,0,0,0.9)]">
            <div className="flex items-center gap-2.5 text-[11px]">
              <span className="h-4 w-4 rounded bg-nectar" />
              <span className="font-mono tracking-[0.18em]">NECTAR</span>
              <span className="ml-auto font-mono text-muted">25:00</span>
            </div>
          </div>
        </section>

        {/* Features */}
        <section id="features" className="mx-auto max-w-shell px-6 py-20">
          <div className="mx-auto mb-14 max-w-xl text-center">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
              The workspace
            </p>
            <h2 className="font-display text-4xl font-semibold sm:text-5xl">
              Four cards. Nothing to arrange.
            </h2>
          </div>

          {/* Borderless cards with a hairline top rule in each card's own colour. */}
          <div className="grid grid-cols-1 gap-px overflow-hidden rounded-3xl sm:grid-cols-2 lg:grid-cols-4">
            {FEATURES.map((feature, index) => {
              const colour = paletteFor(index);
              return (
                <article
                  key={feature.title}
                  className="group relative bg-[#0b0b0e] p-8 transition-colors duration-300 hover:bg-[#101014]"
                >
                  <span
                    aria-hidden
                    className="absolute inset-x-0 top-0 h-px"
                    style={{ backgroundColor: colour, opacity: 0.85 }}
                  />
                  <span
                    aria-hidden
                    className="mb-6 block h-2.5 w-2.5 rounded-full"
                    style={{ backgroundColor: colour }}
                  />
                  <h3 className="mb-3 font-display text-xl font-semibold">{feature.title}</h3>
                  <p className="text-[14.5px] leading-relaxed text-muted">{feature.copy}</p>
                </article>
              );
            })}
          </div>

          <p className="mt-10 text-center font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            Local-first · No account · Your calendar tokens never leave the PC
          </p>
        </section>

        {/* Pricing */}
        <section id="pricing" className="mx-auto max-w-shell px-6 pb-28">
          <div className="mx-auto max-w-2xl rounded-3xl border border-white/10 bg-gradient-to-b from-white/[0.05] to-transparent p-10 text-center">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
              One plan
            </p>
            <p className="font-display text-6xl font-bold tracking-tight">
              $9<span className="ml-1 align-top font-sans text-lg font-medium text-muted">/year</span>
            </p>
            <ul className="mx-auto mt-7 max-w-sm space-y-2 text-left text-sm text-muted">
              <li>Every feature — tasks, timer, notepad, calendar, insights</li>
              <li>All updates, including new major versions</li>
              <li>Signed MSIX installs and updates itself from the notch</li>
              <li>Cancel any time; the app keeps working until the year ends</li>
            </ul>
            <a
              className="mt-9 inline-block rounded-full bg-nectar px-8 py-3.5 text-[15px] font-semibold text-[#201109] transition hover:-translate-y-px hover:bg-[#ffa970]"
              href={CHECKOUT_URL}
            >
              Buy $9 / year
            </a>
            <p className="mt-4 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
              Secure checkout by Stripe · VAT calculated at checkout
            </p>
          </div>
        </section>
      </main>

      <footer className="mx-auto flex max-w-shell flex-col items-center justify-between gap-4 border-t border-white/10 px-6 py-10 text-[13px] text-muted sm:flex-row">
        <span className="font-mono text-[11.5px] uppercase tracking-[0.12em] text-cream/80">
          Nectar v2
        </span>
        <p className="m-0">A top-edge workspace for Windows · handmade, no web view.</p>
        <div className="flex gap-5">
          <a className="transition-colors hover:text-cream" href="/privacy.html">
            Privacy
          </a>
          <a className="transition-colors hover:text-cream" href="/licence.html">
            Licence
          </a>
          <a className="transition-colors hover:text-cream" href="/downloads/nectar.appinstaller">
            Download
          </a>
        </div>
      </footer>
    </div>
  );
}
