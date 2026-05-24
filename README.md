# Spotify Taskbar Widget

Minimal WPF widget that sits above the Windows taskbar showing the current Spotify track with playback controls.

## Features

- Album art, track title, artist
- Play/pause, previous, next
- Progress bar with elapsed / duration
- Always-on-top above taskbar (survives taskbar clicks)
- Right-click menu: open Spotify, snap to taskbar, exit
- Auto-repositions on display settings change

## Requirements

- Windows 10 1903+ (build 19041+)
- .NET 8
- Spotify (desktop app or Microsoft Store)

## Build

```powershell
dotnet publish -c Release
```

Output: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\SpotifyTaskbarWidget.exe`

Single self-contained executable, no installer needed.

## How it works

Uses the Windows `GlobalSystemMediaTransportControlsSession` API (SMTC) to read Spotify playback state and send transport commands. A `WinEventHook` on `EVENT_SYSTEM_FOREGROUND` re-asserts `HWND_TOPMOST` whenever the taskbar gains focus.

## Usage

Run the exe. Widget appears above the taskbar on the primary display. Drag to reposition; right-click for options.
