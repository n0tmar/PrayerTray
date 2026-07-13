[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$AppName = 'PrayerTray'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\PrayerTray'
$ExePath = Join-Path $InstallDir 'PrayerTray.exe'
$AppDataDir = Join-Path $env:APPDATA 'PrayerTray'
$SettingsPath = Join-Path $AppDataDir 'settings.json'
$SlotFilePath = Join-Path $AppDataDir 'taskbar-slot.txt'
$SlotStatusPath = Join-Path $AppDataDir 'taskbar-slot-status.txt'
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ModId = 'prayertray-taskbar-slot'

function Write-Title {
    param([string]$Text)
    Write-Host ''
    Write-Host "== $Text ==" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Text)
    Write-Host "[!] $Text" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Text)
    Write-Host "    $Text" -ForegroundColor DarkGray
}

function Get-WindhawkRoot {
    $programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Windhawk'),
        $(if ($programFilesX86) { Join-Path $programFilesX86 'Windhawk' })
    ) | Where-Object { $_ }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'windhawk.exe')) {
            return $candidate
        }
    }

    return $null
}

function Get-WindhawkModRegistry {
    param([Microsoft.Win32.RegistryView]$View)

    try {
        $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
            [Microsoft.Win32.RegistryHive]::LocalMachine,
            $View)
        $key = $baseKey.OpenSubKey("SOFTWARE\Windhawk\Engine\Mods\$ModId")
        if (-not $key) {
            $baseKey.Dispose()
            return $null
        }

        $values = [pscustomobject]@{
            View = $View
            Disabled = $key.GetValue('Disabled')
            Version = $key.GetValue('Version')
            LibraryFileName = $key.GetValue('LibraryFileName')
            Include = $key.GetValue('Include')
            Architecture = $key.GetValue('Architecture')
        }
        $key.Dispose()
        $baseKey.Dispose()
        return $values
    }
    catch {
        return $null
    }
}

function Get-RecentFileSummary {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Warn "Missing: $Path"
        return
    }

    $item = Get-Item -LiteralPath $Path
    $age = [DateTime]::UtcNow - $item.LastWriteTimeUtc
    Write-Ok "$Path"
    Write-Info ("Updated {0:n0}s ago" -f [Math]::Max(0, $age.TotalSeconds))

    try {
        Get-Content -LiteralPath $Path -Encoding UTF8 -TotalCount 8 | ForEach-Object {
            Write-Host "    $_"
        }
    }
    catch {
        Write-Warn "Could not read $Path"
    }
}

Write-Host ''
Write-Host 'PrayerTray doctor' -ForegroundColor Cyan
Write-Host 'Copy this whole report when asking for help.' -ForegroundColor DarkGray

Write-Title 'Windows'
$os = [Environment]::OSVersion.Version
$isWindows11 = $os.Build -ge 22000
Write-Info "Version: $os"
Write-Info "Windows 11: $isWindows11"
Write-Info "CPU arch: $env:PROCESSOR_ARCHITECTURE"
Write-Info "PowerShell arch: $([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)"
if ($env:PROCESSOR_ARCHITECTURE -notin @('AMD64', 'ARM64')) {
    Write-Warn 'Unsupported CPU architecture. PrayerTray release is win-x64.'
}

Write-Title 'App'
if (Test-Path -LiteralPath $ExePath) {
    Write-Ok "Installed: $ExePath"
    $version = (Get-Item -LiteralPath $ExePath).VersionInfo.FileVersion
    if ($version) {
        Write-Info "File version: $version"
    }
}
else {
    Write-Warn "Not installed at $ExePath"
}

$process = Get-Process -Name PrayerTray -ErrorAction SilentlyContinue
if ($process) {
    Write-Ok "Running: pid $($process.Id -join ', ')"
}
else {
    Write-Warn 'PrayerTray is not running.'
}

if (Test-Path -LiteralPath $RunKeyPath) {
    $runValue = (Get-ItemProperty -LiteralPath $RunKeyPath -Name $AppName -ErrorAction SilentlyContinue).$AppName
    if ($runValue) {
        Write-Ok "Startup: $runValue"
    }
    else {
        Write-Warn 'Startup entry is missing.'
    }
}

Write-Title 'Settings'
if (Test-Path -LiteralPath $SettingsPath) {
    Write-Ok $SettingsPath
    try {
        $settings = Get-Content -LiteralPath $SettingsPath -Encoding UTF8 -Raw | ConvertFrom-Json
        Write-Info "showWidget: $($settings.showWidget)"
        Write-Info "taskbarDisplayMode: $($settings.taskbarDisplayMode)"
        Write-Info "taskbarContentMode: $($settings.taskbarContentMode)"
        Write-Info "showNotificationIcon: $($settings.showNotificationIcon)"
        Write-Info "language: $($settings.language)"
    }
    catch {
        Write-Warn 'Settings file is not valid JSON.'
    }
}
else {
    Write-Warn "Missing: $SettingsPath"
}

Write-Title 'Taskbar Files'
Get-RecentFileSummary -Path $SlotFilePath
Get-RecentFileSummary -Path $SlotStatusPath

Write-Title 'Windhawk'
$windhawkRoot = Get-WindhawkRoot
if ($windhawkRoot) {
    Write-Ok "Installed: $windhawkRoot"
    $windhawkExe = Join-Path $windhawkRoot 'windhawk.exe'
    $version = (Get-Item -LiteralPath $windhawkExe).VersionInfo.ProductVersion
    if ($version) {
        Write-Info "Version: $version"
    }
}
else {
    Write-Warn 'Windhawk is not installed or was not found.'
}

$mod64 = Get-WindhawkModRegistry -View ([Microsoft.Win32.RegistryView]::Registry64)
$mod32 = Get-WindhawkModRegistry -View ([Microsoft.Win32.RegistryView]::Registry32)
if ($mod64) {
    Write-Ok "Mod registry: HKLM:\SOFTWARE\Windhawk\Engine\Mods\$ModId"
    Write-Info "Disabled: $($mod64.Disabled)"
    Write-Info "Version: $($mod64.Version)"
    Write-Info "LibraryFileName: $($mod64.LibraryFileName)"
    Write-Info "Include: $($mod64.Include)"

    if ($mod64.LibraryFileName) {
        $dllPath = Join-Path $env:PROGRAMDATA "Windhawk\Engine\Mods\64\$($mod64.LibraryFileName)"
        if (Test-Path -LiteralPath $dllPath) {
            Write-Ok "Mod DLL: $dllPath"
        }
        else {
            Write-Warn "Mod DLL missing: $dllPath"
        }
    }
}
else {
    Write-Warn 'PrayerTray Windhawk mod is not registered in the 64-bit registry view.'
}

if ($mod32) {
    Write-Warn 'A 32-bit Windhawk registry entry exists. Windhawk may ignore it.'
    Write-Info "32-bit Version: $($mod32.Version)"
    Write-Info "32-bit LibraryFileName: $($mod32.LibraryFileName)"
}

Write-Title 'Network'
try {
    $release = Invoke-RestMethod `
        -Uri 'https://api.github.com/repos/n0tmar/PrayerTray/releases/latest' `
        -Headers @{ 'User-Agent' = 'PrayerTray-doctor' } `
        -TimeoutSec 15
    Write-Ok "GitHub latest release: $($release.tag_name)"
}
catch {
    Write-Warn 'Could not reach GitHub releases API.'
    Write-Info $_.Exception.Message
}

Write-Title 'Logs'
Get-ChildItem -LiteralPath $env:TEMP -Filter 'PrayerTray-*' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 8 |
    ForEach-Object {
        Write-Info "$($_.FullName)  $($_.LastWriteTime)"
    }

Write-Host ''
