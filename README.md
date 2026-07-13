# PrayerTray

PrayerTray adds the next prayer to the Windows taskbar, directly beside the clock.

It uses a small Windows app for prayer times, settings, location, and caching. A companion Windhawk mod gives Windows 11 a real taskbar slot, so network, volume, and clock controls keep their normal layout.

## Install

Fast install:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 | iex"
```

Safer install:

```powershell
iwr https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 -OutFile .\install-prayertray.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-prayertray.ps1
```

The installer downloads the latest self-contained PrayerTray build, checks its SHA256 hash, installs Windhawk if needed, enables the taskbar slot mod, restarts Explorer, and starts PrayerTray.

## Uninstall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/uninstall.ps1 | iex"
```

## Features

- Next prayer in the Windows taskbar
- Time, countdown, and smart taskbar text modes
- Left-click prayer schedule flyout
- Right-click settings
- Automatic location with manual city override
- Prayer times from Islamic.app with Aladhan fallback
- Starts with Windows
- Hides during fullscreen apps

## Requirements

- Windows 11 for the native Windhawk taskbar slot
- Windows 10/11 for the app fallback mode
- Internet during first install

PrayerTray ships as a self-contained Windows build, so users do not need to install .NET.

## Release Build

Create release assets:

```powershell
.\scripts\build-release.ps1
```

Upload these files from `artifacts\release` to a GitHub Release:

- `install.ps1`
- `uninstall.ps1`
- `PrayerTray-win-x64.zip`
- `PrayerTray-win-x64.zip.sha256`
- `prayertray-taskbar-slot.wh.cpp`

## Development

Run from source:

```powershell
dotnet run --project src\PrayerTray\PrayerTray.csproj
```

Run tests:

```powershell
dotnet test PrayerTray.slnx
```

Install the local Windhawk mod during development:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-prayertray-windhawk-mod.ps1
```

## Troubleshooting

- If the taskbar slot does not appear, restart Explorer or rerun the installer.
- If Windhawk is blocked by policy, install Windhawk manually and rerun the installer.
- If the app opens but the slot is empty, wait a few seconds or open PrayerTray settings and save once.
