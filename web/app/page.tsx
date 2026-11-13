/**
 * The one landing page. Everything the standalone static site used to say — the workspace tour, the
 * three ways in, calendar, customize, insights, the local-first facts and the install steps — lives
 * here now, in the same design as /download-steps, /privacy and /licence.
 */

import Image from "next/image";
import SiteFooter from "../components/SiteFooter";
import SiteHeader from "../components/SiteHeader";

const SITE_URL = "https://nectar.app";

/** Same SoftwareApplication block the standalone page carried, minus the placeholder repo link. */
const STRUCTURED_DATA = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Nectar",
  alternateName: "Nectar for Windows",
  url: `${SITE_URL}/`,
  description:
    "A top-edge overlay for Windows: tasks, a focus timer, a scratchpad and today's calendar drop out of the notch. Local-first, no account, WinUI 3.",
  applicationCategory: "ProductivityApplication",
  applicationSubCategory: "Desktop productivity overlay",
  operatingSystem: "Windows 10, Windows 11",
  softwareVersion: "1.0.1",
  isAccessibleForFree: true,
  inLanguage: "en",
  offers: {
    "@type": "Offer",
    price: "0",
    priceCurrency: "USD",
    availability: "https://schema.org/InStock"
  },
  image: `${SITE_URL}/og-image.png`,
  screenshot: [
    `${SITE_URL}/docs/nectar-expanded.png`,
    `${SITE_URL}/docs/nectar-customize.png`,
    `${SITE_URL}/docs/nectar-insights.png`
  ],
  featureList: [
    "Non-activating top-edge overlay opened by a shortcut, the top-edge hover band or the collapsed notch pill",
    "Tasks with one-line capture, a completion ring and a cleared section that keeps its date",
    "Deadline-derived focus timer with extend chips, pause on suspend and a completion chime",
    "Self-saving scratchpad that lifts a line into Tasks with Ctrl+Enter",
    "Today's calendar from one linked Google or Microsoft account, with a reminder tray and snooze",
    "Seven-day Insights chart drawn natively from a local SQLite file",
    "Local-first storage: no account, no telemetry, DPAPI-encrypted calendar tokens"
  ]
};

const FEATURES = [
  {
    title: "Tasks",
    copy: "Type a line, press Enter. The ring on the left completes it, completed rows sink to the bottom, and every check-off feeds the weekly chart.",
    points: ["One-line capture", "Strikethrough, then re-sort", "Cleared tasks keep their date"],
    dot: "#8DBA98"
  },
  {
    title: "Focus timer",
    copy: "A 25-minute countdown derived from a deadline, not from ticks — a busy machine cannot make it drift. Sleeping the PC pauses it.",
    points: ["5 / 10 / 15 min extend chips", "Pauses on suspend", "Bird call at 00:00"],
    dot: "#A39CD0"
  },
  {
    title: "Notepad",
    copy: "A scratch page with no title, no file and no save button. It writes itself 700 ms after you stop typing.",
    points: ["Word count and save state", "Ctrl + Enter lifts a line into Tasks", "Saves on quit"],
    dot: "#D2CA72"
  },
  {
    title: "Calendar",
    copy: "Today's events, one row each, each with a bell. Link Google or Microsoft — one at a time, from your own OAuth client.",
    points: ["Today only, on purpose", "Reminders at the start or before", "Snooze 5 min, or Okay"],
    dot: "#8CB1CD"
  }
] as const;

const ENTRIES = [
  {
    key: "Alt + N",
    title: "Your shortcut",
    copy: "Click the chord in Customize and press a new one. If Windows already owns it, Nectar keeps the old one and tells you why. Esc cancels."
  },
  {
    key: "Top edge",
    title: "The hover band",
    copy: "A 240 × 6 px strip at the top of the work area opens the workspace as the pointer crosses it. There is a switch for that too."
  },
  {
    key: "Pill",
    title: "The collapsed notch",
    copy: "Folded away, Nectar is a small black pill with the name and the countdown. Hide the pill and it disappears completely — the shortcut and the top edge still open it."
  }
] as const;

const FACTS = [
  {
    value: "0",
    title: "Accounts",
    copy: "No sign-up, no profile, no sync service. The calendar uses your own OAuth client, not ours."
  },
  {
    value: "1",
    title: "SQLite file",
    copy: "%LOCALAPPDATA%\\Nectar\\nectar.db holds the tasks, the note and every focus session."
  },
  {
    value: "DPAPI",
    title: "Encrypted tokens",
    copy: "Calendar credentials are sealed to your Windows user, so another account on the machine cannot read them."
  }
] as const;

/** A titled text column beside a screenshot; `flip` puts the shot on the left for the rhythm. */
function SplitSection({
  id,
  eyebrow,
  title,
  paragraphs,
  children,
  flip = false
}: {
  id: string;
  eyebrow: string;
  title: string;
  paragraphs: readonly string[];
  children: React.ReactNode;
  flip?: boolean;
}) {
  return (
    <section
      id={id}
      className="mx-auto grid max-w-shell scroll-mt-16 items-center gap-10 px-6 py-16 lg:grid-cols-2 lg:gap-16"
    >
      <div className={flip ? "lg:order-2" : undefined}>
        <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">{eyebrow}</p>
        <h2 className="font-display text-3xl font-semibold leading-tight sm:text-4xl">{title}</h2>
        {paragraphs.map((paragraph) => (
          <p key={paragraph} className="mt-4 text-[15px] leading-relaxed text-muted">
            {paragraph}
          </p>
        ))}
      </div>
      <div className={flip ? "lg:order-1" : undefined}>{children}</div>
    </section>
  );
}

export default function Home() {
  return (
    <div className="relative z-10">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(STRUCTURED_DATA) }}
      />
      <SiteHeader />

      <main>
        {/* Hero */}
        <section className="mx-auto max-w-shell px-6 pb-16 pt-10 text-center sm:pt-16">
          <p className="mb-7 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
            Windows 10 &amp; 11 · WinUI 3 · Free
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
              href="/download-steps"
            >
              Download Nectar — free
            </a>
            <a
              className="rounded-full border border-white/10 px-7 py-3.5 text-[15px] font-semibold text-cream transition hover:border-white/30"
              href="#workspace"
            >
              See what&apos;s inside
            </a>
          </div>

          <p className="mt-5 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            Free to use · Unlimited updates · No account
          </p>

          {/* The overlay itself, with its collapsed pill on top */}
          <figure className="mx-auto mt-16 max-w-[1000px]">
            <div className="overflow-hidden rounded-2xl border border-white/10 bg-shell shadow-[0_50px_140px_-50px_rgba(0,0,0,0.95)]">
              <div className="flex items-center gap-2.5 border-b border-white/10 px-4 py-2.5 text-[11px]">
                <span className="h-2 w-2 rounded-full bg-nectar" />
                <span className="font-mono tracking-[0.18em]">NECTAR</span>
                <span className="ml-auto font-mono text-muted">25:00</span>
              </div>
              <Image
                alt="The Nectar workspace open on Windows: four pastel cards for tasks, focus timer, notepad and events, with Insights and Customize tabs in the header."
                className="h-auto w-full"
                height={340}
                priority
                src="/docs/nectar-expanded.png"
                width={1000}
              />
            </div>
            <figcaption className="mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
              <span className="inline-flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-sage" /> Tasks
              </span>
              <span className="inline-flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-lavender" /> Focus
              </span>
              <span className="inline-flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-mustard" /> Notepad
              </span>
              <span className="inline-flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-steel" /> Events
              </span>
            </figcaption>
          </figure>
        </section>

        {/* The workspace */}
        <section id="workspace" className="mx-auto max-w-shell scroll-mt-16 px-6 py-20">
          <div className="mx-auto mb-14 max-w-xl text-center">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
              The workspace
            </p>
            <h2 className="font-display text-4xl font-semibold sm:text-5xl">
              Four cards. Nothing to arrange.
            </h2>
            <p className="mt-5 text-[15px] leading-relaxed text-muted">
              They arrive in one 1000 px strip that never covers the app underneath. Hide the ones
              you do not use, drag them into your own order.
            </p>
          </div>

          <div className="grid grid-cols-1 gap-px overflow-hidden rounded-3xl sm:grid-cols-2 lg:grid-cols-4">
            {FEATURES.map((feature) => (
              <article
                key={feature.title}
                className="group relative bg-[#0b0b0e] p-8 transition-colors duration-300 hover:bg-[#101014]"
              >
                <span
                  aria-hidden
                  className="absolute inset-x-0 top-0 h-px"
                  style={{ backgroundColor: feature.dot, opacity: 0.85 }}
                />
                <span
                  aria-hidden
                  className="mb-6 block h-2.5 w-2.5 rounded-full"
                  style={{ backgroundColor: feature.dot }}
                />
                <h3 className="mb-3 font-display text-xl font-semibold">{feature.title}</h3>
                <p className="text-[14.5px] leading-relaxed text-muted">{feature.copy}</p>
                <ul className="mt-5 list-none space-y-2 border-t border-white/[0.07] p-0 pt-4 text-[13px] text-muted">
                  {feature.points.map((point) => (
                    <li key={point} className="flex gap-2">
                      <span aria-hidden style={{ color: feature.dot }}>
                        ·
                      </span>
                      {point}
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>

          <p className="mt-10 text-center font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            Local-first · No account · Your calendar tokens never leave the PC
          </p>
        </section>

        {/* Three ways in */}
        <section className="mx-auto max-w-shell px-6 py-16">
          <div className="max-w-2xl">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
              Getting in
            </p>
            <h2 className="font-display text-3xl font-semibold leading-tight sm:text-4xl">
              Three ways in. Pick one.
            </h2>
            <p className="mt-5 text-[15px] leading-relaxed text-muted">
              The overlay is deliberately non-activating: clicking a card never moves your keyboard
              focus out of the editor you are in. Only a text field asks for it, and only when you
              click.
            </p>
          </div>

          <ol className="mt-10 grid list-none grid-cols-1 gap-px overflow-hidden rounded-3xl p-0 sm:grid-cols-3">
            {ENTRIES.map((entry) => (
              <li key={entry.key} className="bg-[#0b0b0e] p-7">
                <span className="mb-5 inline-block rounded-full border border-white/15 px-3 py-1 font-mono text-[11px] text-nectar">
                  {entry.key}
                </span>
                <h3 className="mb-2 font-display text-lg font-semibold">{entry.title}</h3>
                <p className="m-0 text-sm leading-relaxed text-muted">{entry.copy}</p>
              </li>
            ))}
          </ol>
        </section>

        {/* Calendar */}
        <SplitSection
          eyebrow="Calendar & reminders"
          id="calendar"
          title="A reminder that looks like the timer."
          paragraphs={[
            "Link Google Calendar or Microsoft Calendar through OAuth with PKCE. Tokens are encrypted with DPAPI for your Windows user and never leave the machine; only the public client ID and tenant are written to settings.",
            "Ring the bell on an event — at the start, 5 or 10 minutes before. When it comes due, the same tray that ends a focus session drops down: the hummingbird, the event, the time, and two buttons."
          ]}
        >
          <figure className="m-0 rounded-2xl border border-white/10 bg-[#0b0b0e] p-6">
            <Image
              alt="The Nectar reminder tray: hummingbird mark, event title, a muted Reminder line, and two pills — Snooze 5 min and Okay."
              className="h-auto w-full"
              height={92}
              src="/docs/nectar-reminder.png"
              width={560}
            />
            <figcaption className="mt-5 text-[13px] leading-relaxed text-muted">
              Snooze pushes it five minutes out. Okay dismisses it. <kbd>Esc</kbd> counts as Okay.
            </figcaption>
          </figure>
        </SplitSection>

        {/* Customize */}
        <SplitSection
          eyebrow="Customize"
          flip
          id="customize"
          title="Set once, then forget it."
          paragraphs={[
            "General holds the shortcut, the hover band, the pill switch and the calendar. Focus holds the session length, the completion sound, the chime volume and the expiry tray. Workspace holds card order and visibility.",
            "Reset restores the layout and the focus defaults — your shortcut, your hover setting and your calendar link stay exactly where they were."
          ]}
        >
          <figure className="m-0">
            <div className="overflow-hidden rounded-2xl border border-white/10 bg-shell">
              <Image
                alt="Nectar's Customize view: a mint General card with shortcut, pill and calendar controls, a lilac Focus card, and a sky card for card order and visibility."
                className="h-auto w-full"
                height={480}
                src="/docs/nectar-customize.png"
                width={1000}
              />
            </div>
            <figcaption className="mt-5 text-[13px] leading-relaxed text-muted">
              Three cards, one file: everything lands in <code>settings.json</code>.
            </figcaption>
          </figure>
        </SplitSection>

        {/* Insights */}
        <SplitSection
          eyebrow="Insights"
          id="insights"
          title="Seven days, two ways to read them."
          paragraphs={[
            "The week you just had, beside the week before it: tasks completed or minutes focused, whichever you are in the mood to face. Every bar is drawn natively from the same SQLite file the cards write to — no charting library, no cloud round trip.",
            "If nothing has been logged yet, it says so instead of drawing an empty chart."
          ]}
        >
          <figure className="m-0">
            <div className="overflow-hidden rounded-2xl border border-white/10 bg-shell">
              <Image
                alt="The Insights tab: a sage momentum card with the week's headline and count, beside a lilac bar chart of the last seven days."
                className="h-auto w-full"
                height={450}
                src="/docs/nectar-insights.png"
                width={1000}
              />
            </div>
            <figcaption className="mt-5 text-[13px] leading-relaxed text-muted">
              Hover a column for the day&apos;s numbers.
            </figcaption>
          </figure>
        </SplitSection>

        {/* Local first */}
        <section className="mx-auto max-w-shell px-6 py-16">
          <div className="mx-auto max-w-2xl text-center">
            <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
              Where your data lives
            </p>
            <h2 className="font-display text-3xl font-semibold leading-tight sm:text-4xl">
              On this PC. That is the whole story.
            </h2>
          </div>

          <dl className="m-0 mt-12 grid grid-cols-1 gap-px overflow-hidden rounded-3xl sm:grid-cols-3">
            {FACTS.map((fact) => (
              <div key={fact.title} className="bg-[#0b0b0e] p-8">
                <dt className="font-display text-3xl font-semibold text-nectar">{fact.value}</dt>
                <dd className="m-0 mt-3 font-display text-lg font-semibold">{fact.title}</dd>
                <p className="m-0 mt-2 text-[13.5px] leading-relaxed text-muted">{fact.copy}</p>
              </div>
            ))}
          </dl>

          <p className="mt-10 text-center font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
            No telemetry · no analytics · no Electron runtime — a WinUI 3 app with a Win32 overlay
            layer
          </p>
        </section>

        {/* Install */}
        <section
          id="install"
          className="mx-auto max-w-shell scroll-mt-16 px-6 pb-28 pt-10"
        >
          <div className="grid gap-10 rounded-3xl border border-white/10 bg-gradient-to-b from-white/[0.05] to-transparent p-8 sm:p-12 lg:grid-cols-[1.05fr_1fr] lg:gap-16">
            <div>
              <p className="mb-4 font-mono text-[11px] uppercase tracking-[0.18em] text-nectar">
                Install
              </p>
              <h2 className="font-display text-3xl font-semibold leading-tight sm:text-4xl">
                Download it, run it, pin it.
              </h2>
              <p className="mt-5 text-[15px] leading-relaxed text-muted">
                One setup file and nothing to hunt down: Nectar brings its own .NET runtime and
                Windows App SDK. It installs for your Windows user, so there is no administrator
                prompt — Windows only asks once whether you trust the file;{" "}
                <a className="text-cream underline decoration-white/30 underline-offset-4" href="/download-steps">
                  here is exactly what to click
                </a>
                .
              </p>

              <ol className="mt-7 list-none space-y-3 p-0 text-[15px] text-muted">
                <li className="flex gap-3">
                  <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full border border-white/15 font-mono text-[11px] text-nectar">
                    1
                  </span>
                  Download <code className="font-mono text-[13px] text-cream/90">Nectar-Setup</code>{" "}
                  and run it.
                </li>
                <li className="flex gap-3">
                  <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full border border-white/15 font-mono text-[11px] text-nectar">
                    2
                  </span>
                  More info, then Run anyway — the SmartScreen prompt, explained.
                </li>
                <li className="flex gap-3">
                  <span className="grid h-6 w-6 shrink-0 place-items-center rounded-full border border-white/15 font-mono text-[11px] text-nectar">
                    3
                  </span>
                  Press Alt + N. It starts hidden in the notch, and a desktop shortcut is waiting.
                </li>
              </ol>

              <a
                className="mt-9 inline-block rounded-full bg-nectar px-8 py-3.5 text-[15px] font-semibold text-[#201109] transition hover:-translate-y-px hover:bg-[#ffa970]"
                href="/download-steps"
              >
                Install on Windows
              </a>
            </div>

            <div className="flex flex-col justify-center gap-4 rounded-2xl border border-white/10 bg-black/40 p-7">
              <p className="m-0 font-mono text-[11px] uppercase tracking-[0.14em] text-muted">
                What you need
              </p>
              <ul className="m-0 list-none space-y-3 p-0 text-[14px] leading-relaxed text-muted">
                <li>Windows 10 build 19041 or later, or Windows 11.</li>
                <li>A ~70 MB download, about 280 MB once installed.</li>
                <li>No administrator rights — it installs to your user folder.</li>
                <li>Nothing else — the .NET runtime and Windows App SDK travel inside.</li>
                <li>No account, no card, no trial clock.</li>
              </ul>
              <p className="m-0 border-t border-white/10 pt-4 text-[13px] leading-relaxed text-muted">
                Prefer to build it yourself? The source needs the .NET 8 SDK:{" "}
                <code className="font-mono text-[12.5px] text-cream/90">
                  dotnet build Nectar/Nectar.csproj -c Release
                </code>
              </p>
            </div>
          </div>
        </section>
      </main>

      <SiteFooter />
    </div>
  );
}
