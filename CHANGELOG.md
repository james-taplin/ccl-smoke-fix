# Changelog

## 0.1.0 (2026-10-06)

First release.

- Fix: CCL steam locos' chimney smoke now matches the vanilla S282. A Harmony prefix on
  `ParticlesPortReadersController.Init` copies the vanilla S282 `SteamSmoke` /
  `SteamSmokeThick` colour readers onto CCL locos. This corrects the thick-smoke opacity
  reader, whose zero-flow alpha is 1.0 on CCL locos instead of vanilla's 0.0. That value
  caused the dark cloud at startup, smoke drawing over other smoke, and poor distance fading.
- Corrections are logged per loco and chimney. Vanilla locos are never modified.
- Added a UMM setting to switch the fix on or off.
- Added a **Write particle report** diagnostics button.

## 0.0.2 (2026-10-06, unreleased diagnostic build)

- The particle report now also records start colours, colour-reader setup and
  material-instance diffs.

## 0.0.1 (2026-10-06, unreleased diagnostic build)

- Particle report with renderer sort settings, bounds, simulation space and materials.
