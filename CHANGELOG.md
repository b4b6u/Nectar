# Changelog

What changed in each released build of Nectar. Dates are the day the release was published.
Downloads live at <https://github.com/b4b6u/Nectar/releases>.

## [1.0.2] — 2026-11-14

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

[1.0.1]: https://github.com/b4b6u/Nectar/releases/tag/v1.0.1
[1.0.0]: https://github.com/b4b6u/Nectar/releases/tag/v1.0.0
