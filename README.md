# Bongo Cat Chat Merger

**English** · [한국어](README.ko.md) · [日本語](README.ja.md)

A lightweight Windows companion that collects chat from your current **Steam Bongo Cat** room in one window. Built with C# / WPF and a BepInEx bridge. Other players do not need to install it.

![English preview](docs/assets/preview.png)

## Download and install

Download **BongoCatChatMerger-0.3.0.zip** from [Releases](https://github.com/RATOY-Jeong/bongo-cat-chat-merger/releases/latest). The automatically generated **Source code** archives are not ready-to-run packages. Requires Windows x64 and .NET Framework 4.x.

1. Extract the ZIP and fully close Bongo Cat.
2. Run `Install.cmd`. It finds the Steam game folder, or asks you to select `BongoCat.exe`.
3. Start Bongo Cat from your Steam library and join a room.
4. Run `BongoCatChatMerger.exe`, choose your language, and continue to chat.

The installer stops if it detects an existing mod loader. Do not overwrite another loader. `Uninstall.cmd` removes only files listed in the installation manifest; recorded conversations are preserved.

## Languages — new in 0.3.0

- On first launch, choose **English / 한국어 / 日本語**. Subsequent launches open the chat window directly.
- Change language using the dropdown in the top right. Selection is saved as `language.txt` beside the EXE. A missing or invalid preference opens the language picker again.
- Buttons, connection/error notices, date/time display, and export labels follow your selection. Chat messages, player names, room names, and emote names supplied by the game stay in their original language.
- Switching language keeps your draft, search, history, pending send request, and reconnect attempt. It never resends a message.
- Read these GitHub instructions in any of the three languages using the links above.

## Updating

**From 0.2.2–0.2.4:** Close the companion and copy the new `BongoCatChatMerger.exe` into your existing app folder. Use this file and update any shortcut pointing to `BongoChat.exe`. Keeping the same folder preserves theme, always-on-top, emote visibility, and game-path preferences. No game restart or bridge reinstall is required.

**From 0.2.1 or earlier:** Close game and companion, run the old package's `Uninstall.cmd`, then the new package's `Install.cmd`, and use the new EXE. Records are preserved. If Steam was already running with a failed connection, fully exit and restart it once.

The bridge remains the verified **0.2.2** binary. `BongoChatBridge.dll`, the installation manifest name, and `BepInEx\BongoChat` paths retain their names for compatibility.

## Features

- Send chat from the bottom input: **Enter** to send, **Shift+Enter** for a new line. Active IME composition is completed before sending. Messages use the game's existing chat filter and send function.
- Display emotes fired in the game with their original images. **Show emotes** hides their bubbles without deleting records or excluding them from exports.
- Dark/light themes, always-on-top, opacity, message/name search, and text export of the current room's most recent 500 messages.
- **Reconnect** rechecks the game folder, recent records, and connection heartbeat for up to 12 seconds. It keeps drafts and records, cancels uncertain sends, and never replays them. It cannot load a missing bridge into a running game; follow the restart instructions if needed.
- The bridge fixes inherited loader flags when the game starts Steam and restarts. Version 0.2.2 was verified on game build **25936063** through that startup path. Future game updates work while the internal chat methods and data format remain compatible; structural changes can require an app update.

## Records and privacy

Only messages received after installation are recorded; earlier server conversations cannot be restored. Records include room IDs, Steam IDs, names, message content, and timestamps. Review them before sharing.

- Conversations: game folder `BepInEx\BongoChat\room_<room ID>.jsonl`
- Connection state: `BepInEx\BongoChat\state.json`
- Emote cache: `BepInEx\BongoChat\emotes`
- Local send requests/results: `BepInEx\BongoChat\commands`

The companion reads the most recent 2 MB and displays up to 500 messages; the full local journal remains on disk. Recording continues while the game runs, even if the companion is closed. This app does not send conversations to a separate server. Release packages contain no real chat journals, personal preferences, or cached game emotes. Nickname initials are used as avatars; Steam profile photos are not included.

## Troubleshooting and verification

If disconnected, confirm the game is running in a room and try **Reconnect**. If needed, close Bongo Cat and launch it from Steam. If the problem continues, fully exit Steam too and restart both. Technical details are in `BepInEx\LogOutput.log` and the state file above. Game updates can require a compatibility fix.

Version 0.3.0 passed the app build, **25 localization checks**, and **33 existing feature/reconnect checks**. Three languages were rendered in dark/light themes. Fresh user confirmation of live game sends for 0.3.0 is still pending. [Detailed validation notes (Korean)](docs/VALIDATION.md).

## Source and licenses

`src/app` contains the WPF app, `src/bridge` the game bridge, `src/shared` the send protocol, `scripts` build/install scripts, and `docs` usage/validation notes and screenshots. Copy `runtime` from a release package into the source root, then run `./scripts/Build.ps1` in Windows PowerShell; optional `-GamePath` selects the game installation. Output is in `build/`. The compiler uses installed game assemblies; those assemblies are not redistributed.

The package includes BepInEx 5.4.23.5, Doorstop, HarmonyX and Windows Mono libraries. See [third-party notices and licenses](licenses/THIRD-PARTY-NOTICES.md). This is an unofficial companion.

Contact / bug reports: [parabellum0208@gmail.com](mailto:parabellum0208@gmail.com)
