# PrayerTray

A small prayer-time slot for the Windows taskbar.

PrayerTray keeps the next prayer beside the clock. Left-click opens today's prayers. Right-click opens settings. On Windows 11, Windhawk gives it a real taskbar slot, so the clock, network, and volume icons keep their normal spacing.

## Install

One-line install:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 | iex"
```

Read-first install:

```powershell
iwr https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 -OutFile .\install-prayertray.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-prayertray.ps1
```

The installer downloads the latest release, checks the SHA256 file, installs Windhawk when needed, installs the taskbar slot mod, adds startup, restarts Explorer, and starts PrayerTray.

PrayerTray ships self-contained. You do not need to install .NET.

## Uninstall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/uninstall.ps1 | iex"
```

## What It Does

- Next prayer on the taskbar
- Prayer time, countdown, or auto text
- 12-hour and 24-hour clock formats
- Automatic location with manual city fallback
- Arabic, English, French, Indonesian, Turkish, and Urdu
- Optional low-volume prayer notification, off by default
- Startup toggle
- Hidden taskbar item during fullscreen apps
- Tray icon toggle for people who still want one

## How It Stays Native

PrayerTray is the app. It handles location, prayer times, settings, cache, language, and notification sound.

Windhawk owns the taskbar slot. The mod reads PrayerTray's small state file and draws the text beside the system clock. The taskbar keeps its own layout, hover states, and spacing.

Windows 10 and non-Windhawk setups still use the fallback taskbar window.

## Requirements

- Windows 11 for the native Windhawk taskbar slot
- Windows 10 or 11 for fallback mode
- Internet access for install, location, and prayer time updates
- PowerShell for install scripts

## Settings

Open settings with a right-click on the taskbar prayer time.

You can change:

- language
- time format
- taskbar text mode
- location
- startup
- taskbar visibility
- prayer notification sound
- tray icon

Language changes apply after Save.

## Development

Run the app:

```powershell
dotnet run --project src\PrayerTray\PrayerTray.csproj
```

Run tests:

```powershell
dotnet test PrayerTray.slnx
```

Install the local Windhawk mod:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-prayertray-windhawk-mod.ps1
```

Project layout:

```text
src/PrayerTray      WPF app
windhawk/           taskbar slot mod
scripts/            install, uninstall, release helpers
tests/              xUnit tests
```

## Release

Build release assets:

```powershell
.\scripts\build-release.ps1
```

Upload these files from `artifacts\release` to a GitHub Release:

```text
install.ps1
uninstall.ps1
PrayerTray-win-x64.zip
PrayerTray-win-x64.zip.sha256
prayertray-taskbar-slot.wh.cpp
```

## Troubleshooting

Slot missing:

- Make sure Windhawk is enabled.
- Restart Explorer.
- Run the installer again.

Text not updating:

- Start PrayerTray.
- Open settings and press Save.
- Wait a few seconds for `%APPDATA%\PrayerTray\taskbar-slot.txt` to refresh.

Windhawk install failed:

- Install Windhawk with winget or from the Windhawk website.
- Run the PrayerTray installer again.

## Contributing

Keep it small. Keep it native. Keep it quiet by default.

Taste guide:

- taskbar first
- no loud defaults
- native controls over custom chrome
- readable code over clever code

## Support

Like the project? Consider supporting the developer.

https://www.patreon.com/n0tmar
