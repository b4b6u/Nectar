# Changelog

What changed in each released build of Nectar. Dates are the day the release was published.
Downloads live at <https://github.com/b4b6u/Nectar/releases>.

## [Unreleased]

### Added

- **Categories, three of them free.** A task can now be filed under a category: the tag on any task
  row opens a menu with "No category", every category you have, and "New category…". The name rides
  along on the row's meta line. Categories are made, renamed and removed in Settings, and the free
  build stops at three - the panel says so before you hit the wall, and the row menu says so instead
  of hiding the entry. A key lifts the limit entirely.
- **Existing databases upgrade in place.** The category table and the task column are created on
  the next launch without touching a single task.

- **Nectar Pro, sold through Dodo Payments.** A licence key is the account: Dodo holds the customer,
  the subscription and the billing portal, and Nectar holds a key, an activation id and the last
  time it checked in. Activation and validation use Dodo's public license endpoints, so the app
  carries no merchant secret and there is nothing in the binary worth extracting.
- **Three palettes are free** — Paper, Meadow and Sunset. Midnight and any palette added later are
  Pro, marked and disabled in Settings for a free install. A Pro palette saved before a downgrade
  resolves to Paper on the next launch instead of repainting oddly.
- **A seven-day grace window.** Pro stays on without a check-in; only an explicit "invalid" from
  Dodo turns it off, so a captive portal or a dead Wi-Fi never costs someone what they paid for.
- **A Nectar Pro panel in Settings › Look**: the plan, the masked key, one field and one button,
  plus "forget this key on this PC". Nothing else about the app needs an account.

### Notes

- Keys are bought at the site and activated here; billing changes are made in Dodo's customer
  portal. `licence.json` is sealed with DPAPI like `db.key`.

## [1.0.3] — 2026-11-14

### Added

- **A settings window, behind a gear beside the tabs.** The three tabs are now only about the notch;
  everything that is plumbing moved into its own window: the calendar link (provider, OAuth client
  ID, tenant, connect, disconnect, status), privacy & data (microphone state with a re-check and a
  jump to Windows' mic privacy page, the database state, the data folder), permissions & updates
  (calendar access, notifications, the running version, the latest release), a link to
  `nectarnotch.vercel.app`, the data folder, and the reset. One window at a time — a second click
  raises the one already open — and it closes with the overlay.
- **Four themes** in Settings › Look: **Paper** (the shipping pastel-on-black look), **Meadow**
  (greener cards, mossy brand), **Sunset** (warm cards, browned ink) and **Midnight** (dark cards
  with light ink). Each is shown with its four card colours as swatches. Picking one repaints the
  notch and the settings window immediately, with no restart, and the choice is remembered.
- The theme is applied from `settings.json` before the first frame, so the overlay never flashes
  one look and then swaps to another on launch.

### Changed

- **Customize is down to what it does best:** General (hover band, pill switch, shortcut), Focus
  (session length, sounds, chime volume, expiry tray) and Customize workspace (card order,
  visibility). The calendar block, the privacy block and the reset button left the card.
- The Customize card's **Look** line now names the current theme and links into Settings.
- The Events card's empty state opens Settings on the calendar section instead of switching to
  Customize with a panel open underneath.
- `settings.json` gains `Theme`. An unknown, empty or hand-edited value is normalized to `paper` on
  load, so the app can never start half-themed; **Reset to default** restores the shipping look
  along with the cards and sounds, while the shortcut, the calendar link and your data stay.
- The README, the site's Customize section and its feature list describe the new split, and the
  site's copy no longer sends people to Customize for the calendar.

### Notes

- `Settings/ThemeIds.cs` holds the four ids without any WinUI types, so `dotnet test` covers the
  normalization rules; `Services/AppTheme.cs` owns the colours and repaints the existing brushes in
  place rather than rebuilding the windows.

### The website, rebuilt around the app

- **Fig. 01 is the workspace itself now.** The hero demo renders the app's real 1000 × 340 surface
  at its own measurements — the 34 px header, four 239 px cards with 13/10 padding and 18 px
  corners, the XAML type scale (13.5 titles, 12 rows, 10.5 chips, 9.5 meta, 48 Consolas countdown)
  and the brushed card colours — and scales as one piece, so its proportions never drift. Tasks,
  the timer, the note, the event bells and the three tabs are all live.
- The story page has the hummingbird's flock, the branches, the drifting leaves, a night palette
  and three densities, so the forest stands back on a phone instead of crowding the type.
- Removed the hero's platform line, the promises ticker, the "Vol. 01 / The workspace" label and
  the bird caption: decoration that repeated what the cards already say. The footer now carries
  the app mark beside the wordmark, like the masthead.
- The dark palette's accent ink is darker, so links and step numerals stay legible on cream.

### Download and API hardening

- The setup-exe redirect (`/download/windows`) was an unauthenticated GitHub API call per request.
  It now has its own per-client budget and a 30-second resolved-asset cache, so a loop against the
  button cannot drain the quota the installer feed also needs.
- Release tags are escaped before they reach the feed's HTML status pages.
- The feed rate limiter keys on `x-vercel-forwarded-for` and the rightmost forwarded hop, so a
  proxy in front of the site cannot turn the per-client budget into a client-chosen one.

### Notes

- 1.0.2 was stamped in the project and superseded before a release was cut, so this is the first
  published build that carries the settings window and the four themes.

## [1.0.1] — 2026-11-13

### Fixed

- **The overlay stays above other apps.** It was pinned to the top of the z-order once at launch,
  and the window calls WinUI makes when the overlay shows, moves or activates could drop it back
  into the normal band — so the notch strip ended up behind whatever was open. Every z-order
  request is now rewritten to keep Nectar on top, and a resize re-pins it, which also covers DPI
  changes, moving between displays, and collapse/expand. Measured over a maximised window, the
  overlay used to sit below it and now sits above it.
- **The `.appinstaller` feed reports its own version.** The feed's `Version` attribute was fixed at
  `1.0.0.0` regardless of the package, and Windows compares that attribute to tell one feed from
  the next; it now follows the package identity (`1.0.1.0` for this release).

### Changed

- The site's download button links straight at the version-less release asset
  `releases/latest/download/Nectar-Setup.exe`, so it needs no GitHub API call, no environment
  variable and no redeploy to keep working.
- The install guide no longer sends people to a domain the project does not own, checks the
  published SHA-256 instead of a package signer that only exists for MSIX installs, and says
  plainly that an update is a newer setup run over the top rather than a silent background update.
- Site canonical, Open Graph and sitemap URLs follow the domain the project actually serves.

### Release tooling

- CI restores before building the MSIX; a clean checkout has no `obj/project.assets.json` and the
  build failed with `NETSDK1004`.
- The signature is verified by signer identity rather than chain status, because a fresh runner has
  never trusted this self-signed certificate and reports `UnknownError` for a correctly signed
  package.
- The NSIS download uses `curl` with a ZIP magic-byte check, so a blocked mirror fails with a
  readable message instead of an unformatted exception.
- CI builds the setup exe and the portable zip alongside the MSIX and refreshes the version-less
  alias asset on every tag, so the download can never lag the release.

## [1.0.0] — 2026-11-13

First release.

- Offline Windows workspace overlay: tasks, an ephemeral note, insights, a focus timer,
  reminders, and an optional calendar link (Google or Microsoft), with all data in SQLite under
  `%LOCALAPPDATA%\Nectar`.
- Three ways to install: a per-user **setup exe** that needs no administrator rights and no
  certificate, a **portable zip**, and a **signed MSIX** with an `.appinstaller` feed for
  automatic updates.
- The MSIX is signed with a self-signed certificate (`CN=Nectar`), so Windows shows a "Windows
  protected your PC" prompt on first run; the setup exe is not code-signed at all. Every asset
  ships with a `.sha256` sidecar.
- The website moved into the `web/` Next app: landing page, install walk-through, privacy and
  licence pages, and the routes that serve the MSIX feed and the download redirect.

[1.0.3]: https://github.com/b4b6u/Nectar/releases/tag/v1.0.3
[1.0.1]: https://github.com/b4b6u/Nectar/releases/tag/v1.0.1
[1.0.0]: https://github.com/b4b6u/Nectar/releases/tag/v1.0.0
