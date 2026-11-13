import type { Metadata } from "next";
import Link from "next/link";
import SiteFooter from "../../components/SiteFooter";
import SiteHeader from "../../components/SiteHeader";

export const metadata: Metadata = {
  title: "Installing Nectar — one extra click, explained",
  description:
    "Windows will ask whether you trust Nectar the first time you install it. Here is what that prompt means, why it appears, and the two clicks that get past it.",
  alternates: { canonical: "/download-steps" }
};

/**
 * A stable alias GitHub keeps pointing at the newest release that contains it, so this link survives
 * every version and works from any host — no API route, no environment variable, no redeploy.
 */
const DOWNLOAD_URL = "https://github.com/b4b6u/Nectar/releases/latest/download/Nectar-Setup.exe";

const STEPS = [
  {
    number: "01",
    title: "Download the installer",
    copy: "One file, Nectar-Setup. Your browser may ask you to keep the download first — the same check, one step earlier. It installs for your Windows user only, so there is no administrator prompt."
  },
  {
    number: "02",
    title: "Click “More info”",
    copy: "The blue Windows prompt hides its details behind that link — including who signed the file. Clicking it is not agreeing to anything; it only reveals the buttons below."
  },
  {
    number: "03",
    title: "Click “Run anyway”",
    copy: "That is the only way past a Windows prompt for an app Microsoft has not seen before. Nectar installs, and the prompt never appears again."
  }
] as const;

const QUESTIONS = [
  {
    question: "Is something wrong with my download?",
    answer:
      "No. That screen is Windows saying it has not seen this app often enough to vouch for it yet — not that it found a problem. New software from small developers gets it by default, and so does every fresh build of an app that has not been around for years."
  },
  {
    question: "What am I actually clicking?",
    answer:
      "“More info” reveals the details Windows already has. “Run anyway” tells Windows you have decided to install this specific app. Both are local decisions on your PC; nothing is reported to us."
  },
  {
    question: "Will it ask again?",
    answer:
      "Windows remembers the choice for that file, so reinstalling the same download is quiet. A newer version arrives as a new file: run its setup over the top and your tasks, note and settings stay exactly where they are."
  }
] as const;

const CHECKS = [
  {
    label: "Publisher",
    body: (
      <>
        Today it will say <em className="not-italic">Unknown publisher</em>: this build is not
        code-signed yet, and signing certificates cost money we have not spent. Expected — but the
        line should never show another company&rsquo;s name.
      </>
    ),
    after: (
      <>
        When a certificate is added it will read{" "}
        <strong className="font-bold">Nectar</strong>. Until then the checksum below is the check
        that does not depend on reputation.
      </>
    )
  },
  {
    label: "Address",
    body: (
      <>
        The download starts on this site and hands you to{" "}
        <strong className="font-bold">github.com/b4b6u/Nectar</strong>, the repository&rsquo;s own
        release page. If a link in an email or a chat window sends you anywhere else, close it and
        come back here.
      </>
    )
  },
  {
    label: "Checksum",
    body: <>Every release publishes a SHA-256 beside the file. Compare yours with that line:</>,
    command: "Get-FileHash .\\Nectar-Setup.exe"
  }
] as const;

/** A calm, faithful sketch of the Windows SmartScreen dialog — greys from the OS, not the site. */
function SmartScreenMock({
  state,
  label
}: {
  state: "collapsed" | "expanded";
  label: string;
}) {
  return (
    <figure className="m-0 flex h-full flex-col">
      <div
        aria-hidden
        className="rounded-md border border-black/15 bg-[#f3f3f3] p-5 text-[#1b1b1b] shadow-lift sm:p-6"
        style={{ fontFamily: "'Segoe UI', system-ui, sans-serif" }}
      >
        <div className="flex items-start gap-4">
          <span className="mt-0.5 grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[#0f6cbd]/10 text-[#0f6cbd]">
            <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M12 3l7 3v5.5c0 4.4-2.9 8.1-7 9.5-4.1-1.4-7-5.1-7-9.5V6l7-3z" strokeLinejoin="round" />
              <path d="M12 8.5v4" strokeLinecap="round" />
              <circle cx="12" cy="15.6" r="0.9" fill="currentColor" stroke="none" />
            </svg>
          </span>

          <div className="min-w-0">
            <p className="m-0 text-[17px] font-semibold leading-snug text-[#1b1b1b]">
              Windows protected your PC
            </p>
            <p className="m-0 mt-2 text-[13px] leading-relaxed text-[#4a4a4a]">
              Microsoft Defender SmartScreen prevented an unrecognised app from starting. Running
              this app might put your PC at risk.
            </p>

            {state === "expanded" && (
              <dl className="mt-4 space-y-1 border-t border-black/10 pt-3 text-[12.5px] text-[#4a4a4a]">
                <div className="flex gap-2">
                  <dt className="w-20 shrink-0 text-[#6b6b6b]">App</dt>
                  <dd className="m-0 font-medium text-[#1b1b1b]">Nectar</dd>
                </div>
                <div className="flex gap-2">
                  <dt className="w-20 shrink-0 text-[#6b6b6b]">Publisher</dt>
                  <dd className="m-0 font-medium text-[#1b1b1b]">Unknown publisher</dd>
                </div>
              </dl>
            )}

            <div className="mt-5 flex flex-wrap items-center justify-end gap-2">
              <span className="whitespace-nowrap rounded-[3px] border border-black/15 bg-white px-3.5 py-1.5 text-[12.5px] text-[#1b1b1b] sm:px-4">
                Don&rsquo;t run
              </span>

              {state === "collapsed" ? (
                <span className="inline-flex items-center gap-1 whitespace-nowrap text-[12.5px] font-medium text-[#0f6cbd] underline decoration-[#0f6cbd]/40 underline-offset-2">
                  More info
                </span>
              ) : (
                <span className="relative whitespace-nowrap rounded-[3px] bg-[#0f6cbd] px-3.5 py-1.5 text-[12.5px] font-semibold text-white sm:px-4">
                  Run anyway
                  <span
                    aria-hidden
                    className="absolute -inset-2 rounded-[6px] ring-[3px] ring-orange ring-offset-2 ring-offset-[#f3f3f3]"
                  />
                </span>
              )}
            </div>
          </div>
        </div>
      </div>

      <figcaption className="mt-auto pt-5 text-[10.5px] font-bold uppercase tracking-[0.18em] text-muted">
        {label}
      </figcaption>
    </figure>
  );
}

export default function DownloadStepsPage() {
  return (
    <div className="relative z-10">
      <SiteHeader />

      <main className="mx-auto max-w-shell px-5 pb-24 sm:px-6">
        <section className="mx-auto max-w-3xl pt-12 text-center sm:pt-16">
          <p className="mb-6 inline-block rotate-[-1.5deg] border-2 border-ink bg-blush px-3 py-1 text-[10.5px] font-bold uppercase tracking-[0.24em]">
            Before you install
          </p>
          <h1 className="font-display text-[clamp(2.4rem,6.4vw,5rem)] uppercase leading-[0.94]">
            <span className="display-shadow inline-block">Windows will</span>{" "}
            <span className="display-shadow inline-block -rotate-1">ask once.</span>
            <br />
            <span className="inline-block text-orange">Two clicks and you&rsquo;re in.</span>
          </h1>
          <p className="mx-auto mt-7 max-w-xl text-[17px] leading-relaxed text-inkSoft">
            The first time you install Nectar, Windows shows a blue &ldquo;Windows protected your
            PC&rdquo; screen. Nothing is wrong — it says that about every app Microsoft has not seen
            enough of yet. Here is exactly what to click.
          </p>
        </section>

        {/* The prompt, in both states */}
        <section className="sticker-card mt-14 grid gap-10 bg-paperDeep p-6 sm:p-10 lg:grid-cols-2 lg:gap-12">
          <SmartScreenMock state="collapsed" label="What you will see" />
          <SmartScreenMock state="expanded" label="What you click" />
        </section>

        {/* Steps */}
        <section className="mt-20">
          <h2 className="font-display text-shout uppercase leading-[0.9]">Three steps</h2>
          <ol className="mt-8 grid list-none grid-cols-1 gap-6 p-0 sm:grid-cols-3">
            {STEPS.map((step, index) => (
              <li
                key={step.number}
                className="sticker-card bg-paper p-6"
                style={{ transform: `rotate(${index === 1 ? "0.9deg" : "-0.8deg"})` }}
              >
                <span className="font-display text-[36px] uppercase leading-none text-orange">
                  {step.number}
                </span>
                <h3 className="mt-2 font-display text-[24px] uppercase leading-none">
                  {step.title}
                </h3>
                <p className="m-0 mt-3 text-[13.5px] leading-relaxed text-muted">{step.copy}</p>
              </li>
            ))}
          </ol>
        </section>

        {/* Reassurance */}
        <section className="mt-20 grid gap-10 lg:grid-cols-[1fr_1.1fr] lg:gap-14">
          <div>
            <p className="mb-4 inline-block rotate-[-1.5deg] border-2 border-ink bg-sun px-2.5 py-1 text-[10.5px] font-bold uppercase tracking-[0.24em]">
              Why you are seeing this
            </p>
            <h2 className="font-display text-[clamp(1.9rem,4vw,3rem)] uppercase leading-[0.9]">
              A missing reputation is not a warning.
            </h2>
            <p className="mt-5 text-[15.5px] leading-relaxed text-muted">
              SmartScreen is a popularity check, not a virus scan. Microsoft builds a reputation for
              an installer as more people download and run it; until Nectar has one, the prompt is
              what Windows does by default. It is the same screen that greets small utilities,
              open-source tools and brand-new releases of well-known apps.
            </p>
            <p className="mt-4 text-[15.5px] leading-relaxed text-muted">
              You are not switching off a protection or widening a setting — you are answering one
              question, about one file, once.
            </p>
          </div>

          <dl className="m-0 space-y-0 rounded-sticker border-2 border-ink bg-paper p-6 shadow-sticker">
            {QUESTIONS.map((item) => (
              <div key={item.question} className="border-t-2 border-ink/15 py-4 first:border-0 first:pt-0">
                <dt className="font-display text-[20px] uppercase leading-none">{item.question}</dt>
                <dd className="m-0 mt-2 text-[13.5px] leading-relaxed text-muted">{item.answer}</dd>
              </div>
            ))}
          </dl>
        </section>

        {/* Verification */}
        <section className="mt-20 rounded-sticker border-2 border-ink bg-paperDeep p-6 shadow-sticker sm:p-10">
          <p className="mb-4 inline-block rotate-[-1.5deg] border-2 border-ink bg-leaf px-2.5 py-1 text-[10.5px] font-bold uppercase tracking-[0.24em]">
            Check it is really us
          </p>
          <h2 className="font-display text-shout uppercase leading-[0.9]">
            Before you click Run anyway
          </h2>
          <p className="mt-4 max-w-2xl text-[15.5px] leading-relaxed text-muted">
            Teaching someone to click past a warning only makes sense if they can also check who
            signed the file. Three things are worth a glance:
          </p>

          <ul className="mt-8 grid list-none grid-cols-1 gap-5 p-0 sm:grid-cols-3">
            {CHECKS.map((check) => (
              <li key={check.label} className="rounded-sticker border-2 border-ink bg-paper p-5">
                <p className="mb-2 text-[10.5px] font-bold uppercase tracking-[0.2em] text-ink/60">
                  {check.label}
                </p>
                <p className="m-0 text-[13.5px] leading-relaxed text-ink">
                  {check.body}
                </p>
                {"after" in check && check.after ? (
                  <p className="m-0 mt-3 text-[12.5px] leading-relaxed text-muted">{check.after}</p>
                ) : null}
                {"command" in check && check.command ? (
                  <code className="mt-3 block overflow-x-auto rounded border-2 border-ink bg-ink px-2.5 py-2 font-mono text-[11.5px] text-paper">
                    {check.command}
                  </code>
                ) : null}
              </li>
            ))}
          </ul>

          <p className="mt-6 max-w-2xl text-[12.5px] leading-relaxed text-muted">
            &ldquo;Unknown publisher&rdquo; means Windows has no record of the signer yet. A
            different <em className="not-italic">company</em> in that line means the file did not
            come from us — close the window and tell us, because someone is passing off a copy of
            the app.
          </p>
        </section>

        {/* CTA */}
        <section className="mx-auto mt-20 max-w-2xl text-center">
          <h2 className="font-display text-shout uppercase leading-[0.9]">Ready when you are.</h2>
          <p className="mx-auto mt-4 max-w-lg text-[15px] leading-relaxed text-muted">
            The download is small, the install takes under a minute, and Nectar opens quietly in the
            notch — press{" "}
            <kbd className="rounded border-2 border-ink bg-sun px-1.5 py-0.5 font-mono text-[12px]">
              Alt
            </kbd>{" "}
            +{" "}
            <kbd className="rounded border-2 border-ink bg-sun px-1.5 py-0.5 font-mono text-[12px]">
              N
            </kbd>{" "}
            to bring it down.
          </p>

          <a
            className="mt-9 inline-block rounded-full border-2 border-ink bg-orange px-8 py-3.5 text-[12px] font-bold uppercase tracking-[0.16em] text-paper shadow-sticker transition-transform hover:-translate-y-0.5 hover:bg-red"
            href={DOWNLOAD_URL}
            download
          >
            Download Nectar
          </a>

          <p className="mt-5 text-[10.5px] font-bold uppercase tracking-[0.2em] text-muted">
            Windows 10 &amp; 11 &middot; one setup file &middot; no account needed
          </p>

          <p className="mt-8 text-[13px] text-muted">
            Stuck on a step? The{" "}
            <Link className="underline decoration-orange decoration-2 underline-offset-4" href="/">
              home page
            </Link>{" "}
            has the rest of the story, and the{" "}
            <Link
              className="underline decoration-orange decoration-2 underline-offset-4"
              href="/privacy"
            >
              privacy page
            </Link>{" "}
            lists everything Nectar stores on your PC.
          </p>
        </section>
      </main>

      <SiteFooter />
    </div>
  );
}
