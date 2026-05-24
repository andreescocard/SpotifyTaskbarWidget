<div align="center">

# 🎵 Spotify Taskbar Widget

**A sleek, always-on-top Spotify controller that lives above your Windows taskbar.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D4?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-22c55e?style=flat-square)](LICENSE)
[![Buy Me a Coffee](https://img.shields.io/badge/support-Buy%20Me%20a%20Coffee-FFDD00?style=flat-square&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/andreescocard)

</div>

---

## What it is

A tiny 480×48 WPF overlay that anchors itself above the Windows taskbar — album art, track info, and playback controls always one glance away. No browser extension, no Spotify login, no setup. Just run and go.

```
┌────────────────────────────────────────────────────────────┐
│  🎵  Blinding Lights          1:23  ⏮ ⏸ ⏭  ♡  3:20  ████░░│
│      The Weeknd                                            │
└────────────────────────────────────────────────────────────┘
          ↑ sits right above your taskbar
```

## Features

- **Album art** with rounded corners
- **Track title & artist** with ellipsis trimming
- **Playback controls** — previous, play/pause, next
- **Progress bar** with elapsed / total time
- **Always on top** — survives taskbar clicks and fullscreen apps
- **Drag to reposition**, right-click for context menu
- **Auto-snaps** after display settings change
- **Zero dependencies** — single self-contained `.exe`

## Requirements

| | |
|---|---|
| OS | Windows 10 1903+ (build 19041+) |
| Runtime | Bundled — nothing to install |
| Spotify | Desktop app **or** Microsoft Store version |

## Download

Grab the latest release from [Releases](../../releases) — single `.exe`, run and done.

## Build from source

```powershell
git clone https://github.com/andreescocard/spotify-taskbar-widget
cd spotify-taskbar-widget/src/SpotifyTaskbarWidget
dotnet publish -c Release
```

Output: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SpotifyTaskbarWidget.exe`

## How it works

Uses the Windows **SMTC** (`GlobalSystemMediaTransportControlsSession`) API — the same one that powers the volume flyout media controls. No Spotify API keys, no OAuth, no network calls. Reads playback state and sends transport commands entirely through the OS.

A `WinEventHook` on `EVENT_SYSTEM_FOREGROUND` re-asserts `HWND_TOPMOST` whenever the taskbar gains focus, keeping the widget visible at all times.

## Usage

1. Run `SpotifyTaskbarWidget.exe`
2. Open Spotify and play something
3. Widget appears above the taskbar

**Drag** — reposition anywhere  
**Right-click** — open Spotify · snap to taskbar · exit

## Contributing

PRs welcome. Open an issue first for anything beyond a small fix.

## License

MIT — do whatever you want, just keep the attribution.

---

<div align="center">

If this saved you a click or two, a coffee would be awesome ☕

[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-support%20the%20project-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/andreescocard)

</div>
