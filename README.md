# PrayerTray

PrayerTray shows the next prayer time in the Windows taskbar.

It runs as a small Windows app for prayer times, location, settings, and cache. On Windows 11, a Windhawk mod adds a real taskbar slot beside the clock, so the tray keeps its normal spacing.

## Install

Open PowerShell and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 | iex"
```

Prefer to read the script first?

```powershell
iwr https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 -OutFile .\install-prayertray.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\install-prayertray.ps1
```

The installer:

- downloads the latest PrayerTray release
- checks the SHA256 hash
- installs Windhawk if needed
- installs the taskbar slot mod
- adds PrayerTray to startup
- restarts Explorer
- starts PrayerTray

PrayerTray ships with its own runtime. You do not need to install .NET.

## Uninstall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/uninstall.ps1 | iex"
```

## Features

- Next prayer in the taskbar
- Prayer time, countdown, and auto text modes
- 12-hour and 24-hour time formats
- Left-click prayer times flyout
- Right-click settings
- Automatic location
- Manual city selection
- Arabic, English, French, Indonesian, Turkish, and Urdu
- Starts with Windows
- Hides during fullscreen apps

## Requirements

- Windows 11 for the Windhawk taskbar slot
- Windows 10 or 11 for the app fallback mode
- Internet access for install and prayer time updates

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

## Release

Build release assets:

```powershell
.\scripts\build-release.ps1
```

Upload files from `artifacts\release` to a GitHub Release:

- `install.ps1`
- `uninstall.ps1`
- `PrayerTray-win-x64.zip`
- `PrayerTray-win-x64.zip.sha256`
- `prayertray-taskbar-slot.wh.cpp`

## Troubleshooting

Slot does not show:

- Restart Explorer.
- Rerun the installer.
- Make sure Windhawk is enabled.

Slot is empty:

- Start PrayerTray.
- Open settings and save once.
- Wait a few seconds for the taskbar file to update.

Windhawk cannot install:

- Install Windhawk manually.
- Run the PrayerTray installer again.

## Support

Like the project? Consider supporting the developer:

https://www.patreon.com/n0tmar
