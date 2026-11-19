# Operations Client (v0.4.0)

BepInEx client plugin for SPT 3.11.1 / Fika. Pairs with the EconomyGuard Enterprise server mod (Operations module).

- **OPERATIONS tab** replaces Fika's "Download Profile" tab (managed by the game's own menu bar) with an unread badge.
- **Top-right HUD tag** for priority orders / an assembled squad; click opens the order.
- **Mission screen**: CONFIDENTIAL until OPEN FILE; dossier with objectives, rewards, equipment, briefing image, status, and ACCEPT TASK / CLAIM REWARD buttons (through the game's own quest controller).
- **Squad briefing**: when all members are online the commander starts a synchronized countdown; members are told to join voice chat with the commander first.
- No hotkey: open it from the tab or the alert icon; close it with the yellow-framed CLOSE window at the top right (ESC is not used, so it can never fight the game menu). Menu only; hidden in raid.
- Interface style (classic / modern) follows the admin's setting. Briefing images are cached in BepInEx/cache/Operations/media by hash and downloaded only once.

Install: put `Operations.Client.dll` in `BepInEx/plugins/`. Download it from the latest GitHub Actions run (artifact `Operations.Client`).
