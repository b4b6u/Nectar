import type { Metadata } from "next";
import LegalLayout from "../../components/LegalLayout";

export const metadata: Metadata = {
  title: "Privacy",
  description:
    "Nectar keeps tasks, notes, focus sessions and calendar tokens on your own PC. No account, no telemetry, no analytics — and this site sets no cookies.",
  alternates: { canonical: "/privacy" },
  openGraph: { title: "Privacy — Nectar", type: "article", url: "/privacy" }
};

const TOC = [
  { id: "short", label: "The short version" },
  { id: "stored", label: "What is stored" },
  { id: "calendar", label: "Calendar connections" },
  { id: "network", label: "What goes online" },
  { id: "site", label: "This website" },
  { id: "control", label: "Your control" },
  { id: "contact", label: "Questions" }
] as const;

export default function PrivacyPage() {
  return (
    <LegalLayout
      title="Privacy"
      lede="Nectar has no account, no server and no analytics. What you type into it stays in one folder on your own PC — this page lists exactly what that folder holds, and what leaves the machine when you link a calendar."
      updated="Last updated 13 November 2026 · Covers Nectar 1.0.0 and this website"
      toc={TOC}
    >
      <section id="short">
        <h2>The short version</h2>
        <p>
          There is no Nectar account and no Nectar backend. The app does not send usage data, crash
          reports or diagnostics anywhere, because there is nowhere for them to go: the only network
          requests it makes are the calendar calls you set up yourself, plus the update check against
          this site&rsquo;s installer feed.
        </p>
        <p>
          <strong>No data is sold, shared or mined.</strong> There is no server holding your tasks,
          and no third-party analytics library in the app.
        </p>
      </section>

      <section id="stored">
        <h2>What is stored, and where</h2>
        <p>
          Everything lives in <code>%LOCALAPPDATA%\Nectar</code>. The folder is yours: copy it, back
          it up, or delete it.
        </p>
        <table>
          <thead>
            <tr>
              <th>File</th>
              <th>Contents</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>nectar.db</td>
              <td>
                An SQLite database: your tasks and their completion dates, the scratchpad note, and
                one row per focus session.
              </td>
            </tr>
            <tr>
              <td>settings.json</td>
              <td>
                Plain text preferences — the shortcut, the hover band and pill switches, card order
                and visibility, focus length, sound settings, and the calendar client ID and tenant
                you entered.
              </td>
            </tr>
            <tr>
              <td>Token file</td>
              <td>
                The calendar access and refresh tokens, encrypted with DPAPI for your Windows user.
                Another account on the same machine cannot read them.
              </td>
            </tr>
            <tr>
              <td>nectar.log</td>
              <td>
                A diagnostic log the app appends to while it runs. It can include the title of a
                reminder that fired, so treat it as personal if your event names are.
              </td>
            </tr>
          </tbody>
        </table>
        <p>
          There is no cloud sync and no hidden copy elsewhere. Pinning Nectar to another machine
          means copying the folder yourself.
        </p>
      </section>

      <section id="calendar">
        <h2>Calendar connections</h2>
        <p>
          Linking a calendar is optional and uses OAuth with PKCE against <em>your own</em> client
          ID: you create the app registration with Google or Microsoft, and paste its ID into Nectar.
          There is no shared Nectar client credential, and no intermediary server sitting between the
          app and the provider.
        </p>
        <p>The scopes requested are the narrowest that still show a day:</p>
        <ul>
          <li>
            <strong>Google</strong> — <code>calendar.readonly</code>, read access only.
          </li>
          <li>
            <strong>Microsoft</strong> — <code>offline_access</code> and <code>Calendars.Read</code>,
            so the app can refresh a token and read events.
          </li>
        </ul>
        <p>
          Tokens are sealed with DPAPI (user scope) and never leave the machine. Nectar cannot write
          to, move or delete anything in your calendar. Google or Microsoft still see the requests
          the app makes to them, under their own privacy policies — that part is between you and
          whichever provider you link.
        </p>
      </section>

      <section id="network">
        <h2>What actually goes online</h2>
        <p>The app talks to these hosts and no others:</p>
        <ul>
          <li>
            <code>accounts.google.com</code>, <code>oauth2.googleapis.com</code> and{" "}
            <code>www.googleapis.com</code> for Google sign-in and events.
          </li>
          <li>
            <code>login.microsoftonline.com</code> and <code>graph.microsoft.com</code> for
            Microsoft sign-in and events.
          </li>
          <li>
            <code>nectar.app</code> for the installer feed, which the App Installer uses to fetch
            updates. It is a plain file request; no identifier is attached to it.
          </li>
        </ul>
        <p>
          There is no licence check, no heartbeat and no telemetry endpoint. Unlink the calendar and
          the only remaining request is that update check.
        </p>
      </section>

      <section id="site">
        <h2>This website</h2>
        <p>
          The site sets no cookies, runs no analytics and loads no third-party scripts. There is no
          consent banner because there is nothing to consent to.
        </p>
        <p>Two things are outside the page&rsquo;s control:</p>
        <ul>
          <li>
            <strong>Fonts</strong> are served by Google Fonts, which receives the request — your IP
            address, browser and the page you came from — under Google&rsquo;s privacy policy.
          </li>
          <li>
            <strong>Hosting</strong> is Vercel, which keeps standard server logs (IP address, user
            agent, requested path) as any web host does.
          </li>
        </ul>
        <p>
          No form on this site collects anything, and the site has no database of its own. The
          installer download redirects to this site&rsquo;s own API route, which reads the latest
          release from GitHub to build the feed — it stores nothing about you.
        </p>
      </section>

      <section id="control">
        <h2>Your control</h2>
        <ul>
          <li>
            <strong>Unlink a calendar</strong> in Customize → General; the stored tokens are
            discarded.
          </li>
          <li>
            <strong>Revoke access</strong> at{" "}
            <a href="https://myaccount.google.com/permissions" rel="noreferrer" target="_blank">
              Google
            </a>{" "}
            or{" "}
            <a href="https://account.live.com/consent/Manage" rel="noreferrer" target="_blank">
              Microsoft
            </a>
            , which cuts Nectar off even if a token were left behind.
          </li>
          <li>
            <strong>Delete everything</strong> by removing <code>%LOCALAPPDATA%\Nectar</code> while
            the app is closed. There is no remote copy to clean up.
          </li>
          <li>
            <strong>Uninstall</strong> from Windows Settings → Apps; the folder above is left in
            place, so remove it by hand if you want the data gone too.
          </li>
        </ul>
      </section>

      <section id="contact">
        <h2>Questions</h2>
        <p>
          Read the source: the calendar client and the token store are small enough to check the
          claims on this page yourself. Licence terms are on the{" "}
          <a href="/licence">licence page</a>.
        </p>
      </section>
    </LegalLayout>
  );
}
