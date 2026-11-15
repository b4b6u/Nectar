import type { Metadata } from "next";
import LegalLayout from "../../components/LegalLayout";

export const metadata: Metadata = {
  title: "Licence",
  description:
    "Nectar Free is MIT licensed; the Pro tier is proprietary. Read the MIT text, where the open part ends, and the licences of the Microsoft, SQLite and type components it ships with.",
  alternates: { canonical: "/licence" },
  openGraph: { title: "Licence — Nectar", type: "article", url: "/licence" }
};

const TOC = [
  { id: "mit", label: "The MIT licence" },
  { id: "pro", label: "Nectar Pro" },
  { id: "means", label: "What it lets you do" },
  { id: "third-party", label: "Third-party components" },
  { id: "brand", label: "The name and the bird" },
  { id: "contributions", label: "Contributions" }
] as const;

const MIT_TEXT = `MIT License

Copyright (c) 2026 Nectar contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.`;

export default function LicencePage() {
  return (
    <LegalLayout
      title="Licence"
      lede="Nectar Free is open source under the MIT licence: use it, read it, change it, ship it inside something else. Nectar Pro is not — it is proprietary, sold as a licence key. Below is the MIT text in full, what it lets you do, and exactly where the open part ends."
      updated="Last updated 14 November 2026 · Applies to Nectar 1.0.3"
      toc={TOC}
    >
      <section id="mit">
        <h2>The MIT licence</h2>
        <p>
          The free application, this website and the source are released under MIT. One carve-out:
          the Nectar Pro tier, which has its own section below.
        </p>
        <pre>{MIT_TEXT}</pre>
      </section>

      <section id="pro">
        <h2>Nectar Pro</h2>
        <p>
          <strong>The Pro tier is proprietary.</strong> Its licence activation code, its feature
          modules and its extra palettes are copyright &copy; 2026 b4b6u, all rights reserved. MIT
          does not apply to them, and buying a key buys a licence to run the Pro build — not
          ownership of it and not the right to redistribute it.
        </p>
        <ul>
          <li>
            <strong>Free stays free.</strong> Everything MIT covers keeps working exactly as before,
            with no account and no expiry.
          </li>
          <li>
            <strong>One binary, gated features.</strong> Pro is enabled on your machine by the
            licence key you activate in Settings; the gate is in the app, not in a separate download
            you have to chase.
          </li>
          <li>
            <strong>No public Pro downloads.</strong> Pro builds are never attached to the
            repository&rsquo;s releases — the public release channel carries the free build alone.
          </li>
          <li>
            <strong>When a licence ends</strong> the app keeps running as Nectar Free. Nothing takes
            away the rights MIT already gave you.
          </li>
        </ul>
        <p>
          The full terms live in <code>PRO-LICENCE.md</code> in the repository. Forking the free
          build is fine; publishing the Pro modules is not.
        </p>
      </section>

      <section id="means">
        <h2>What it lets you do</h2>
        <ul>
          <li>
            <strong>Run it anywhere.</strong> Personal, academic or commercial use, on as many
            machines as you like, at no cost.
          </li>
          <li>
            <strong>Change it.</strong> Fork the source, strip the calendar card out, rewrite the
            timer — no permission needed.
          </li>
          <li>
            <strong>Pass it on.</strong> Bundle Nectar into another product, or sell a build, as long
            as the copyright notice and the permission notice travel with it.
          </li>
        </ul>
        <p>
          What it does not give you is a warranty: if the timer misses a meeting, the licence says
          the authors are not liable. Keep the notices, and you are done — MIT has no copyleft
          clause, so your own changes can stay closed if you want them to.
        </p>
        <p>
          <strong>Redistributing a build?</strong> Ship the licence text beside the binary. A
          self-signed or missing code signature will still make Windows warn about the publisher —
          that is separate from the licence.
        </p>
      </section>

      <section id="third-party">
        <h2>Third-party components</h2>
        <p>
          Nectar is native C# rather than a bundled browser, so the list is short. These are the
          pieces that end up in a published build, with their own terms:
        </p>
        <table>
          <thead>
            <tr>
              <th>Component</th>
              <th>Licence</th>
              <th>Notes</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>Windows App SDK · WinUI 3</td>
              <td>MIT</td>
              <td>Microsoft. Shipped inside the installer.</td>
            </tr>
            <tr>
              <td>.NET 8 runtime</td>
              <td>MIT</td>
              <td>Microsoft. Bundled into the package, so nothing needs installing first.</td>
            </tr>
            <tr>
              <td>Entity Framework Core + SQLite provider</td>
              <td>MIT</td>
              <td>
                Microsoft. Reads and writes <code>nectar.db</code>.
              </td>
            </tr>
            <tr>
              <td>Microsoft.Win32.SystemEvents</td>
              <td>MIT</td>
              <td>Microsoft. Notices when the PC suspends or the display changes.</td>
            </tr>
            <tr>
              <td>SQLite</td>
              <td>Public domain</td>
              <td>The SQLite authors dedicated it to the public domain.</td>
            </tr>
            <tr>
              <td>Bricolage Grotesque · Instrument Sans · IBM Plex Mono</td>
              <td>SIL Open Font License 1.1</td>
              <td>Headings, body and code on this site, served through Google Fonts.</td>
            </tr>
          </tbody>
        </table>
        <p>
          The Windows SDK build tools are used to compile and sign but are not redistributed, and
          there are no other third-party libraries in the app — no Electron, no web view, no charting
          package.
        </p>
      </section>

      <section id="brand">
        <h2>The name and the bird</h2>
        <p>
          The MIT licence covers the code. It does not hand over the Nectar name or the hummingbird
          mark, which identify this project: a fork is welcome, but it should ship under its own name
          and icon so people can tell the two apart. The logo itself is included in the repository
          for building and distributing Nectar, not for unrelated branding.
        </p>
      </section>

      <section id="contributions">
        <h2>Contributions</h2>
        <p>
          Bug reports, fixes and features are welcome in the repository. Anything merged is accepted
          under the same MIT terms, so the project stays equally free for everyone who picks it up
          next.
        </p>
        <p>
          How the app handles your data is on the <a href="/privacy">privacy page</a>.
        </p>
      </section>
    </LegalLayout>
  );
}
