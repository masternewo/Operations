# Operations — client plugin

BepInEx plugin for **SPT 3.11.x / EFT 0.16.x** that shows the server-side *Operations* (missions) module of
EconomyGuard 1.6.0 inside the game: rank and points, active missions, the military-letter briefing, and a banner
when a new mission appears.

* Press **F10** (configurable) in the main menu or in raid.
* Install: put `Operations.Client.dll` in `BepInEx/plugins/`. The server mod `SPT-EconomyGuard` ≥ 1.6.0 must be installed.
* Build: GitHub Actions builds the DLL on every push (Actions → build → artifact), or locally `dotnet build -c Release`.

## Status
v0.1.0 — **not yet tested in a real game.** It is a plain IMGUI overlay (no EFT-internal UI hooks), so it should not
conflict with other mods, but check `BepInEx/LogOutput.log` for `Operations.Client loaded`.
Not included yet: video/audio/image briefings, the multiplayer lobby, one-click Fika join.
Text uses Unity's default font, which cannot shape Persian/Arabic: keep in-game mission text in English
(the admin's *In-game text language* setting).
