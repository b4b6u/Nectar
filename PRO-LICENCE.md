# Nectar Pro licence

The repository is open source under the MIT licence with one carve-out: **the Nectar Pro tier is not.**

MIT applies to the free application, the website and everything else in this repository, exactly as
`LICENSE` says. It does **not** apply to the paths listed below, and it does not apply to any Pro
build produced from them. Those are copyright © 2026 b4b6u, all rights reserved.

## What is proprietary

| Path | What it is |
|---|---|
| `Nectar/Services/Licence/**` | Dodo key activation, validation and the local entitlement state |
| any future `Nectar/Pro/**` | Pro feature modules (categories, NLP, planner, MCP, clips, focus guard, …) |
| Pro palette data and Pro assets | anything marked `Pro` in the UI or in `ThemeIds` |
| `dist/Nectar-Setup-*.exe` built with the Pro modules | the paid binary itself |

## What you may not do with it

- Copy, publish, sublicense or redistribute the proprietary source, in whole or in part.
- Ship a Pro build to anyone, or host one, without a licence from the copyright holder.
- Use the proprietary parts to produce a competing paid build of Nectar.

## What you may still do

- Read this repository, build the **free** application, and do anything MIT allows with it.
- Activate a key you bought, and run Nectar Pro on the machines your key covers.
- Quote short excerpts for review, comment or security reporting.

## How the split is built

The public repository compiles the free tier. `Nectar/Pro/**` is not compiled unless the build is
told to include it:

```bash
dotnet build Nectar/Nectar.csproj                      # free: no Pro source compiled
dotnet build Nectar/Nectar.csproj -p:NectarPro=true    # Pro: needs Nectar/Pro/** present
```

`NectarPro` defines `NECTAR_PRO`, which is the only thing that switches
`Nectar/Services/Entitlement/Entitlements.cs` from `FreeEntitlementService` to the Dodo-backed
service. The free binary contains no activation code, no HTTP client and no key handling; the
release pipeline asserts this before it packages anything. The Pro sources are developed and built
in a private repository that pulls this one in and passes `-p:NectarPro=true`.

## Distribution

Pro builds are distributed **only** to licensees, through the channels the seller controls (a
Dodo-issued licence plus a download link). They are **never** attached to a public GitHub release,
and the public release channel carries the free build alone. Buying a key buys a licence to run the
Pro build, not ownership of it and not the right to redistribute it.

## Termination

A licence ends when the subscription ends or the key is refunded or revoked. At that point the app
keeps working as Nectar Free; the proprietary tier stops being enabled. Nothing here takes away
rights you already have under MIT to the free application.

Questions about licensing: open an issue on the public repository, or write from the address the
licence was bought with.
