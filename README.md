# CCL Smoke Fix

A small Unity Mod Manager / Harmony mod for **Derail Valley** that makes stack smoke on
**Custom Car Loader (CCL)** steam locomotives look like the vanilla S282.

## The problem

CCL steam locos copy the vanilla S282 smoke particle systems, but the colour readers that
drive them arrive with one wrong value. On the `SteamSmokeThick` exhaust-flow opacity reader,
the zero-flow end is set to alpha **1.0**; the vanilla S282 uses **0.0**.

As a result, at startup and idle the thick smoke on CCL locos is about 96% opaque, while on
vanilla locos it is about 1%. That causes:

- a dense black cloud right after startup that never thins out,
- that near-opaque smoke drawing over other smoke and steam,
- smoke that barely fades into distance fog.

Every other render setting (materials, sort order, particle counts, sizes, speeds) already
matches vanilla. This was checked with the in-game report described below.

## What the mod does

Just before a CCL loco's `ParticlesPortReadersController` initialises, a Harmony prefix copies
the vanilla S282 colour/opacity settings onto the matching CCL stack-smoke readers
(`SteamSmoke` / `SteamSmokeThick`, matched by reader type and simulation port). Vanilla locos
are never touched. Each correction is written to the UMM log.

The fix can be toggled in the mod's UMM settings. It applies to locos spawned or loaded after
the setting changes.

## Diagnostics

The settings page has a **Write particle report** button. It writes every live loco particle
system (renderer sort settings, bounds, materials and per-property material diffs, start
colours, colour-reader configuration) to `particle_report_*.txt` in the mod folder.

## Install

Extract the release zip into `Derail Valley\Mods` so you get `Mods\CCLSmokeFix\`.
Requires Unity Mod Manager. Works with any CCL steam loco; no changes to the locos are needed.

## Build

```powershell
.\build.ps1 -DVRoot "<path to Derail Valley>"
```

Requires the .NET SDK. It references the game's managed assemblies, UMM and Harmony from your
Derail Valley install and produces `CCLSmokeFix_v<version>.zip`. It never installs into the game.
