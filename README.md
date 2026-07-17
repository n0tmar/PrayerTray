# PrayerTray

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=flat-square)
![Windhawk](https://img.shields.io/badge/Windhawk-mod-5B5BD6?style=flat-square)

<p align="center">
  <img src="docs/assets/taskbar-time.png" width="330" alt="PrayerTray taskbar prayer time">
  <img src="docs/assets/taskbar-countdown.png" width="330" alt="PrayerTray taskbar countdown">
</p>

Prayer time belongs near the clock.

One [Windhawk](https://windhawk.net/) mod that puts the next prayer beside the system clock. No separate app to install.

## Install

1. Install Windhawk
2. Create a new mod and paste [`windhawk/prayertray.wh.cpp`](windhawk/prayertray.wh.cpp)
3. Compile and enable

Left-click opens today's times. Right-click refreshes location or opens Windhawk settings.

## Features

- Next prayer on the taskbar (Windows 10 and 11)
- Countdown, prayer time, or auto text
- 12-hour and 24-hour clocks
- Windows Location when allowed, IP geolocation otherwise, or manual city/coordinates
- Arabic, English, French, Indonesian, Turkish, Urdu
- Optional sound shortly after prayer time
- Hides during true fullscreen

## Notes

Prayer times come from islamic.app (Aladhan if needed) and are cached under `%APPDATA%\PrayerTray`.

Keep defaults quiet.

## Support

If this helps you, support me on Patreon :)
https://www.patreon.com/n0tmar
