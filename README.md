# CCL Smoke Fix

A small Unity Mod Manager / Harmony mod for **Derail Valley** that fixes the dark,
near-opaque chimney smoke on **Custom Car Loader (CCL)** steam locomotives so it behaves
like the smoke on the vanilla S282 and S060.

Works with every CCL steam loco. The locos themselves don't need any changes.

---

## The symptoms

On CCL steam locos, and not on the vanilla ones:

- the moment the loco is started, the chimney gives off a big, dark, dense cloud, where a
  vanilla loco gives a light grey wisp;
- the cloud stays heavy even when the loco is idling or working gently;
- this smoke draws over other smoke and steam, so plumes look like they render through
  each other;
- it hardly fades into the distance haze, so far-off CCL locos still show a solid
  black column.

## What we found

It looks like a rendering bug, but it isn't one.

Each steam loco's chimney smoke is made of two particle systems:

| System | Look | Opacity is driven by |
|---|---|---|
| `SteamSmoke` | light grey plume | exhaust flow |
| `SteamSmokeThick` | dark, dense smoke | exhaust flow (and colour by firebox smoke density) |

CCL locos don't ship their own smoke. They copy these systems straight from the vanilla S282,
and we confirmed in-game that the copies match vanilla exactly: same materials and shader,
same sort order and sort mode, same bounds, same simulation space, same particle counts,
sizes and speeds.

The difference is in the **colour readers**, the small controllers that set each smoke
system's colour and opacity from the loco's simulation. The `SteamSmokeThick` opacity reader
blends between two alpha values as exhaust flow goes from 0 to 1:

| | Alpha at zero exhaust flow | Alpha at full exhaust flow |
|---|---|---|
| Vanilla S282 | **0.0** (invisible) | 0.31 |
| CCL locos | **1.0** (fully opaque) | 0.31 |

So at startup and idle, vanilla thick smoke is effectively invisible (measured alpha about 1%),
while CCL thick smoke is about 96% opaque. It only gets *more* transparent as the loco works
harder, the reverse of vanilla. Every effect in the list above follows from that one number:

- **Dark cloud at startup:** about 60 nearly solid dark particles per chimney.
- **Rendering through other smoke:** the game always draws thick smoke after ordinary steam.
  Vanilla does the same, but there the thick smoke is see-through at idle, so you never
  notice. With opaque thick smoke, the draw order becomes visible.
- **No distance fade:** fog can only lighten a near-black, nearly solid plume so much.

The value comes from CCL's own **Steam Template** particle wizard
(`GameObject > CCL > Particles > Steam Template`, `CCL.Creator/Wizards/ParticleWizards.cs`).
The last colour reader it creates (thick smoke, `TOTAL_FLOW_NORMALIZED`, `ALPHA_ONLY`) sets
`startColorMin` alpha to `1` instead of vanilla's `0`. Any loco built with that template has
the value baked into its bundle. We checked an RNF-2, an H6-F and a Big Boy (both chimneys),
and all of them had it.

Even if the template is fixed in a future CCL release, locos that were already built keep the
old value until their authors rebuild them. This mod corrects them at runtime in the meantime.

## What the mod does

- One Harmony **prefix** on the game's `ParticlesPortReadersController.Init`, which runs once
  when a loco's particle controller starts up.
- If the loco is a **CCL** loco (vanilla locos are never touched), the prefix finds its chimney
  smoke colour readers. It matches each one to the vanilla S282's equivalent by smoke system
  (`SteamSmoke` / `SteamSmokeThick`), reader type and simulation value, then copies the
  vanilla colours, opacity and blend curve onto it.
- The vanilla values are read live from the game's own S282, so nothing is hard-coded and the
  fix follows any future vanilla changes.
- Readers that already match vanilla are left alone. Each correction is written to the
  UMM log, for example:
  `RR2DV_RLW_2_8_2_UKL SteamSmokeThick exhaust.TOTAL_FLOW_NORMALIZED ALPHA_ONLY: min RGBA(0.500, 0.500, 0.500, 1.000) -> RGBA(0.500, 0.500, 0.500, 0.000) ...`
- Nothing else is changed: no materials, no render settings, no particle counts, and no files
  on disk.

The fix can be switched off in the mod's UMM settings (Ctrl+F10). The setting applies to
locos spawned or loaded after it changes.

## Diagnostics

The settings page has a **Write particle report** button. It writes every live loco's particle
systems to `particle_report_<date>_<time>.txt` in the mod folder: renderer sort settings,
bounds, simulation space, start colours and sizes, colour-reader setup, and a per-property
comparison of any material copies against the game's originals. This is the report that
was used to find the problem, and it's handy if a loco still looks wrong.

## Install

1. Install [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) for Derail Valley.
2. Extract the release zip into `Derail Valley\Mods`, so you end up with `Mods\CCLSmokeFix\`.
3. Start the game. It works with CCL as-is; no load-order setup is needed.

To uninstall, delete the `Mods\CCLSmokeFix` folder.

## Build

```powershell
.\build.ps1 -DVRoot "<path to Derail Valley>"
```

Requires the .NET SDK. The script references the game's managed assemblies, UMM and Harmony
from your Derail Valley install and produces `CCLSmokeFix_v<version>.zip`. It never installs
anything into the game.

## License

[MIT](LICENSE)
