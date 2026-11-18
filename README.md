# Operations Client (v0.2.0)

BepInEx client plugin for SPT 3.11.1 / Fika. Pairs with the EconomyGuard Enterprise server mod (Operations module).

- **OPERATIONS tab** in the bottom bar (replaces Fika's "Download Profile" tab) with an unread badge.
- **Top-right alert icon** for priority orders / an assembled squad; click opens the order directly.
- **Mission screen**: orders are shown as CONFIDENTIAL until you press OPEN FILE; dossier with objectives, rewards, issued equipment, briefing image.
- **Squad briefing**: when all members are online the commander starts a synchronized countdown; members are told to join voice chat with the commander first.
- Fallback hotkey F10 (configurable). Menu only; hidden in raid.

Install: put `Operations.Client.dll` in `BepInEx/plugins/`. Download it from the latest GitHub Actions run (artifact `Operations.Client`).
