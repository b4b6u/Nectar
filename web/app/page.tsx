/**
 * The landing page. Everything the site says about the app — the workspace tour, the three ways in,
 * calendar, customize, insights, the local-first facts and the install steps — set in the poster
 * language: cream stock, green ink, one loud orange, pastel cut-out cards and display type.
 */

import Image from "next/image";
import SiteFooter from "../components/SiteFooter";
import SiteHeader from "../components/SiteHeader";

const SITE_URL = "https://nectarnotch.vercel.app";

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
    "A settings window behind a gear beside the tabs, with four themes that repaint the app as you pick them",
    "Local-first storage: no account, no telemetry, DPAPI-encrypted calendar tokens"
  ]
};

/** The ticker under the hero: the promises, set as a strip of stamps. */
const TICKER = [
  "Alt + N opens the notch",
  "No account",
  "No telemetry",
  "No server",
  "No Electron",
  "No web view",
  "C# · .NET 8 · WinUI 3"
] as const;

/** The four beats of the story the hummingbird carries across the page. */
const STORY = [
  {
    number: "01",
    tag: "The dart",
    title: "Attention darts.",
    copy: "A notification, an open tab, a chime from a calendar you forgot you linked. Every ping is a flower worth checking, and none of them are the thing you sat down to do."
  },
  {
    number: "02",
    tag: "The branch",
    title: "Find one place to land.",
    copy: "Alt + N drops a single strip out of the top edge: tasks, a timer, a note, today. It never covers the app you are working in and never takes your keyboard away from it."
  },
  {
    number: "03",
    tag: "The hold",
    title: "Twenty-five minutes, one task.",
    copy: "The countdown runs from a deadline rather than from ticks, so a busy machine cannot make it drift. Nothing in the strip rewards you for checking something else."
  },
  {
    number: "04",
    tag: "The return",
    title: "The branch stays.",
    copy: "Tick the rows off, close the note, fold the notch back into the edge. Nothing to maintain, nothing to log into — the next dithering spell already has somewhere to go."
  }
] as const;

const FEATURES = [
  {
    title: "Tasks",
    copy: "Type a line, press Enter. The ring on the left completes it, completed rows sink to the bottom, and every check-off feeds the weekly chart.",
    points: ["One-line capture", "Strikethrough, then re-sort", "Cleared tasks keep their date"],
    fill: "#b8cd49",
    rotate: "-1.2deg"
  },
  {
    title: "Focus timer",
    copy: "A 25-minute countdown derived from a deadline, not from ticks — a busy machine cannot make it drift. Sleeping the PC pauses it.",
    points: ["5 / 10 / 15 min extend chips", "Pauses on suspend", "Bird call at 00:00"],
    fill: "#c9c2ea",
    rotate: "0.9deg"
  },
  {
    title: "Notepad",
    copy: "A scratch page with no title, no file and no save button. It writes itself 700 ms after you stop typing.",
    points: ["Word count and save state", "Ctrl + Enter lifts a line into Tasks", "Saves on quit"],
    fill: "#ffcf4a",
    rotate: "-0.7deg"
  },
  {
    title: "Calendar",
    copy: "Today's events, one row each, each with a bell. Link Google or Microsoft — one at a time, from your own OAuth client.",
    points: ["Today only, on purpose", "Reminders at the start or before", "Snooze 5 min, or Okay"],
    fill: "#f5b6bf",
    rotate: "1.1deg"
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

/** A small tracked label — the editorial standing head over a headline. */
function Kicker({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <p
      className={`m-0 text-[10.5px] font-bold uppercase tracking-[0.24em] ${className}`}
    >
      {children}
    </p>
  );
}

/** A titled text column beside a screenshot; `flip` puts the shot on the left for the rhythm. */
function SplitSection({
  id,
  eyebrow,
  title,
  paragraphs,
  children,
  flip = false,
  caption
}: {
  id: string;
  eyebrow: string;
  title: string;
  paragraphs: readonly string[];
  children: React.ReactNode;
  flip?: boolean;
  caption?: React.ReactNode;
}) {
  return (
    <section
      id={id}
      className="mx-auto grid max-w-shell scroll-mt-24 items-center gap-10 px-5 py-16 sm:px-6 lg:grid-cols-2 lg:gap-16"
    >
      <div className={flip ? "lg:order-2" : undefined}>
        <Kicker className="mb-4 inline-block rotate-[-1.5deg] border-2 border-ink bg-sun px-2.5 py-1 text-inkSolid">
          {eyebrow}
        </Kicker>
        <h2 className="m-0 font-display text-[clamp(2rem,4.6vw,3.4rem)] uppercase leading-[0.9]">
          {title}
        </h2>
        {paragraphs.map((paragraph) => (
          <p key={paragraph} className="mt-4 text-[15.5px] leading-relaxed text-muted">
            {paragraph}
          </p>
        ))}
      </div>
      <figure className={flip ? "m-0 lg:order-1" : "m-0"}>{children}</figure>
      {caption ? (
        <figcaption className="order-last text-[12px] uppercase tracking-[0.14em] text-muted lg:col-span-2">
          {caption}
        </figcaption>
      ) : null}
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
        <section className="relative overflow-hidden px-5 pb-10 pt-12 sm:px-6 sm:pt-16">
          <div className="mx-auto max-w-shell text-center">
            <Kicker className="text-ink/70">Windows 10 &amp; 11 &middot; WinUI 3 &middot; Free</Kicker>

            <h1 className="mx-auto mt-6 max-w-5xl font-display text-hero uppercase">
              <span className="display-shadow -rotate-1 inline-block text-orange">Your day,</span>{" "}
              <span className="display-shadow inline-block">perfectly</span>{" "}
              <span className="display-shadow rotate-1 inline-block">anchored.</span>
            </h1>

            <p className="mx-auto mt-8 max-w-xl text-[17px] leading-relaxed text-inkSoft">
              Nectar hangs off the top edge of your screen. Pull it down and your tasks, a focus
              timer, a scratchpad and today&rsquo;s calendar are already open — without stealing
              focus from the app you are working in.
            </p>

            <div className="mt-9 flex flex-wrap items-center justify-center gap-4">
              <a
                className="rounded-full border-2 border-ink bg-orange px-7 py-3 text-[12px] font-bold uppercase tracking-[0.16em] text-paper shadow-sticker transition-transform hover:-translate-y-0.5 hover:bg-red"
                href="/download-steps"
              >
                Download Nectar &mdash; free
              </a>
              <a
                className="rounded-full border-2 border-ink bg-paper px-7 py-3 text-[12px] font-bold uppercase tracking-[0.16em] text-ink transition-transform hover:-translate-y-0.5 hover:bg-sun"
                href="#workspace"
              >
                See what&rsquo;s inside
              </a>
            </div>

            <p className="mt-6 text-[11px] font-medium uppercase tracking-[0.18em] text-muted">
              Free to use &middot; Unlimited updates &middot; No account
            </p>
          </div>

          {/* The overlay itself, in a cut-out frame */}
          <figure className="relative mx-auto mt-14 max-w-[1000px]">
            <div className="sticker-card overflow-hidden bg-ink shadow-stickerLg">
              <div className="flex items-center gap-2.5 border-b-2 border-ink bg-paper px-4 py-2.5 text-[10.5px] font-bold uppercase tracking-[0.18em]">
                <span className="h-2.5 w-2.5 rounded-full border border-ink bg-orange" />
                <span>Nectar</span>
                <span className="ml-auto font-mono text-[11px] tracking-normal">25:00</span>
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
            <figcaption className="mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 text-[10.5px] font-bold uppercase tracking-[0.16em] text-muted">
              <span>Fig. 01 &mdash; the workspace, 1000 &times; 340 px</span>
              <span aria-hidden className="text-orange">
                &darr;&darr;&darr;
              </span>
            </figcaption>
          </figure>
        </section>

        {/* Ticker */}
        <div className="overflow-hidden border-y-2 border-ink bg-leaf py-2.5 text-inkSolid">
          <div className="marquee">
            {[0, 1].map((copy) => (
              <div
                key={copy}
                aria-hidden={copy === 1}
                className="flex shrink-0 items-center whitespace-nowrap text-[12px] font-bold uppercase tracking-[0.2em]"
              >
                {TICKER.map((item) => (
                  <span key={item} className="flex items-center">
                    <span className="px-5">{item}</span>
                    <span className="text-orange">&hearts;</span>
                  </span>
                ))}
              </div>
            ))}
          </div>
        </div>

        {/* The story the bird is telling */}
        <section id="story" className="px-5 py-20 sm:px-6">
          <div className="mx-auto max-w-shell">
            <div className="mx-auto max-w-3xl text-center">
              <Kicker className="inline-block rotate-[-1.5deg] border-2 border-ink bg-leaf px-2.5 py-1 text-inkSolid">
                The story
              </Kicker>
              <h2 className="mt-5 font-display text-shout uppercase leading-[0.94]">
                Reclaiming focus, one branch at a time.
              </h2>
              <p className="mx-auto mt-5 max-w-lg text-[16px] leading-relaxed text-muted">
                A hummingbird never holds still for long — it darts, hovers, darts again. So does
                attention. Nectar is not a cage for it; it is one branch worth coming back to, and
                this bird is on its way across the page to prove it.
              </p>
            </div>

            <ol className="mt-14 grid list-none grid-cols-1 gap-6 p-0 md:grid-cols-2">
              {STORY.map((beat, index) => (
                <li
                  key={beat.number}
                  className="reveal sticker-card bg-paper p-7"
                  style={{ transform: index % 2 ? "rotate(0.6deg)" : "rotate(-0.6deg)" }}
                >
                  <div className="flex items-baseline gap-3">
                    <span className="font-display text-[40px] uppercase leading-none text-orange">
                      {beat.number}
                    </span>
                    <span className="text-[10px] font-bold uppercase tracking-[0.22em] text-ink/50">
                      {beat.tag}
                    </span>
                  </div>
                  <h3 className="mt-3 font-display text-[27px] uppercase leading-none">
                    {beat.title}
                  </h3>
                  <p className="m-0 mt-3 text-[14px] leading-relaxed text-muted">{beat.copy}</p>
                </li>
              ))}
            </ol>

            <p className="mt-12 text-center text-[10.5px] font-bold uppercase tracking-[0.2em] text-muted">
              The bird flies left to right as you read &middot; the forest button turns the sound on
            </p>
          </div>
        </section>

        {/* The workspace — the loud orange scene */}
        <section id="workspace" className="scroll-mt-24 bg-orange px-5 py-20 text-paper sm:px-6">
          <div className="mx-auto max-w-shell">
            <div className="mx-auto max-w-2xl text-center">
              <Kicker className="text-paper/80">Vol. 01 / The workspace</Kicker>
              <h2 className="mt-5 font-display text-scene uppercase">
                <span className="display-shadow inline-block -rotate-1">Four cards.</span>{" "}
                <span className="display-shadow inline-block">Nothing</span>{" "}
                <span className="display-shadow inline-block rotate-1">to arrange.</span>
              </h2>
              <p className="mx-auto mt-6 max-w-lg text-[16px] leading-relaxed text-paper/90">
                They arrive in one 1000 px strip that never covers the app underneath. Hide the ones
                you do not use, drag them into your own order.
              </p>
            </div>

            <div className="mt-14 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
              {FEATURES.map((feature, index) => (
                <article
                  key={feature.title}
                  className="sticker-card flex flex-col p-6 text-inkSolid transition-transform duration-200 hover:-translate-y-1"
                  style={{ backgroundColor: feature.fill, transform: `rotate(${feature.rotate})` }}
                >
                  <span className="font-mono text-[11px] tracking-[0.1em] opacity-60">
                    0{index + 1}
                  </span>
                  <h3 className="mt-3 font-display text-[30px] uppercase leading-none">
                    {feature.title}
                  </h3>
                  <p className="mt-3 text-[13.5px] leading-relaxed">{feature.copy}</p>
                  <ul className="mt-4 list-none space-y-1.5 border-t-2 border-inkSolid/25 p-0 pt-3 text-[12.5px]">
                    {feature.points.map((point) => (
                      <li key={point} className="flex gap-2">
                        <span aria-hidden className="text-inkSolid/60">
                          &rarr;
                        </span>
                        {point}
                      </li>
                    ))}
                  </ul>
                </article>
              ))}
            </div>

            <p className="mt-12 text-center text-[11px] font-bold uppercase tracking-[0.2em] text-paper/80">
              Local-first &middot; No account &middot; Your calendar tokens never leave the PC
            </p>
          </div>
        </section>

        {/* Three ways in */}
        <section className="px-5 py-20 sm:px-6">
          <div className="mx-auto max-w-shell">
            <div className="max-w-2xl">
              <Kicker className="mb-4 inline-block rotate-[-1.5deg] border-2 border-ink bg-blush px-2.5 py-1 text-inkSolid">
                Getting in
              </Kicker>
              <h2 className="font-display text-shout uppercase leading-[0.9]">
                Three ways in. Pick one.
              </h2>
              <p className="mt-5 text-[15.5px] leading-relaxed text-muted">
                The overlay is deliberately non-activating: clicking a card never moves your keyboard
                focus out of the editor you are in. Only a text field asks for it, and only when you
                click.
              </p>
            </div>

            <ol className="mt-10 grid list-none grid-cols-1 gap-6 p-0 sm:grid-cols-3">
              {ENTRIES.map((entry, index) => (
                <li key={entry.key} className="sticker-card bg-paper p-6">
                  <span className="rounded-full border-2 border-ink bg-sun px-3 py-1 text-inkSolid font-mono text-[11px]">
                    {entry.key}
                  </span>
                  <h3 className="mt-4 font-display text-[26px] uppercase leading-none">
                    {entry.title}
                  </h3>
                  <p className="m-0 mt-2.5 text-[13.5px] leading-relaxed text-muted">{entry.copy}</p>
                  <span className="mt-4 block text-[11px] font-bold uppercase tracking-[0.18em] text-ink/40">
                    0{index + 1}
                  </span>
                </li>
              ))}
            </ol>
          </div>
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
          caption="Snooze pushes it five minutes out. Okay dismisses it. Esc counts as Okay."
        >
          <div className="sticker-card bg-paperDeep p-6">
            <Image
              alt="The Nectar reminder tray: hummingbird mark, event title, a muted Reminder line, and two pills — Snooze 5 min and Okay."
              className="h-auto w-full"
              height={92}
              src="/docs/nectar-reminder.png"
              width={560}
            />
          </div>
        </SplitSection>

        {/* Customize */}
        <SplitSection
          eyebrow="Customize"
          flip
          id="customize"
          title="Set once, then forget it."
          paragraphs={[
            "General holds the shortcut, the hover band and the pill switch. Focus holds the session length, the completion sound, the chime volume and the expiry tray. Workspace holds card order and visibility.",
            "The gear beside the tabs opens Settings: the calendar link, privacy and data, permissions and updates, a link to this page, and four themes — Paper, Meadow, Sunset and Midnight — that repaint both windows the moment you pick one. Reset to default restores the shipped cards, sounds and colours, and keeps your shortcut, your calendar link and your data."
          ]}
          caption={
            <>
              Three cards in the notch, one settings window behind the gear — and one file: everything
              lands in <code className="font-mono">settings.json</code>.
            </>
          }
        >
          <div className="sticker-card overflow-hidden bg-ink">
            <Image
              alt="Nectar's Customize view: a mint General card with the shortcut, hover and pill switches, a lilac Focus card, and a sky card for card order and visibility."
              className="h-auto w-full"
              height={480}
              src="/docs/nectar-customize.png"
              width={1000}
            />
          </div>
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
          caption="Hover a column for the day's numbers."
        >
          <div className="sticker-card overflow-hidden bg-ink">
            <Image
              alt="The Insights tab: a sage momentum card with the week's headline and count, beside a lilac bar chart of the last seven days."
              className="h-auto w-full"
              height={450}
              src="/docs/nectar-insights.png"
              width={1000}
            />
          </div>
        </SplitSection>

        {/* Local first */}
        <section className="border-y-2 border-ink bg-paperDeep px-5 py-20 sm:px-6">
          <div className="mx-auto max-w-shell">
            <div className="mx-auto max-w-2xl text-center">
              <Kicker className="text-ink/60">Where your data lives</Kicker>
              <h2 className="mt-5 font-display text-shout uppercase leading-[0.9]">
                On this PC. That is the whole story.
              </h2>
            </div>

            <dl className="m-0 mt-12 grid grid-cols-1 gap-6 sm:grid-cols-3">
              {FACTS.map((fact, index) => (
                <div
                  key={fact.title}
                  className="sticker-card bg-paper p-6"
                  style={{ transform: `rotate(${index === 1 ? "0.8deg" : "-0.7deg"})` }}
                >
                  <dt className="font-display text-[44px] uppercase leading-none text-orange">
                    {fact.value}
                  </dt>
                  <dd className="m-0 mt-2 font-display text-[24px] uppercase leading-none">
                    {fact.title}
                  </dd>
                  <p className="m-0 mt-3 text-[13.5px] leading-relaxed text-muted">{fact.copy}</p>
                </div>
              ))}
            </dl>

            <p className="mt-10 text-center text-[11px] font-bold uppercase tracking-[0.18em] text-muted">
              No telemetry &middot; no analytics &middot; no Electron runtime — a WinUI 3 app with a
              Win32 overlay layer
            </p>
          </div>
        </section>

        {/* Install */}
        <section id="install" className="scroll-mt-24 bg-ink px-5 py-20 text-paper sm:px-6">
          <div className="mx-auto grid max-w-shell gap-10 lg:grid-cols-[1.05fr_1fr] lg:gap-14">
            <div>
              <Kicker className="inline-block rotate-[-1.5deg] border-2 border-paper bg-orange px-2.5 py-1 text-paper">
                Install
              </Kicker>
              <h2 className="mt-5 font-display text-shout uppercase leading-[0.9] text-paper">
                Download it, run it, pin it.
              </h2>
              <p className="mt-5 text-[15.5px] leading-relaxed text-paper/80">
                One setup file and nothing to hunt down: Nectar brings its own .NET runtime and
                Windows App SDK. It installs for your Windows user, so there is no administrator
                prompt — Windows only asks once whether you trust the file;{" "}
                <a className="text-sun underline decoration-sun/40 underline-offset-4" href="/download-steps">
                  here is exactly what to click
                </a>
                .
              </p>

              <ol className="mt-8 grid list-none grid-cols-1 gap-4 p-0 sm:grid-cols-3">
                {[
                  "Download Nectar-Setup and run it.",
                  "More info, then Run anyway — the SmartScreen prompt, explained.",
                  "Press Alt + N. It starts hidden in the notch."
                ].map((step, index) => (
                  <li key={step} className="border-t-2 border-paper/25 pt-3 text-[13px] leading-relaxed text-paper/80">
                    <span className="block font-display text-[32px] uppercase leading-none text-leaf">
                      0{index + 1}
                    </span>
                    <span className="mt-2 block">{step}</span>
                  </li>
                ))}
              </ol>

              <a
                className="mt-9 inline-block rounded-full border-2 border-paper bg-orange px-8 py-3.5 text-[12px] font-bold uppercase tracking-[0.16em] text-paper shadow-[4px_4px_0_rgb(var(--paper-rgb))] transition-transform hover:-translate-y-0.5 hover:bg-red"
                href="/download-steps"
              >
                Install on Windows
              </a>
            </div>

            <div className="rounded-sticker border-2 border-paper/30 bg-inkSoft p-6">
              <Kicker className="text-paper/60">What you need</Kicker>
              <ul className="mt-4 list-none space-y-3 p-0 text-[14px] leading-relaxed text-paper/85">
                <li>Windows 10 build 19041 or later, or Windows 11.</li>
                <li>A ~70 MB download, about 280 MB once installed.</li>
                <li>No administrator rights — it installs to your user folder.</li>
                <li>Nothing else — the .NET runtime and Windows App SDK travel inside.</li>
                <li>No account, no card, no trial clock.</li>
              </ul>
              <p className="m-0 mt-5 border-t-2 border-paper/20 pt-4 text-[13px] leading-relaxed text-paper/70">
                Prefer to build it yourself? The source needs the .NET 8 SDK:{" "}
                <code className="mt-2 block overflow-x-auto rounded bg-black/40 px-3 py-2 font-mono text-[12px] text-paper/90">
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
