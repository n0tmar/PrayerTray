[CmdletBinding()]
param(
    [switch]$Elevated,
    [switch]$NoExplorerRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$AppName = 'PrayerTray'
$ModId = 'prayertray-taskbar-slot'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\PrayerTray'
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$RepoUninstallScriptUrl = 'https://github.com/n0tmar/PrayerTray/releases/latest/download/uninstall.ps1'

function Write-Banner {
    Write-Host ''
    Write-Host '============================================================' -ForegroundColor DarkGray
    Write-Host ' PrayerTray uninstaller' -ForegroundColor Cyan
    Write-Host '============================================================' -ForegroundColor DarkGray
}

function Write-Step {
    param([string]$Message)
    Write-Host ''
    Write-Host "[>] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Yellow
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-ElevatedUninstaller {
    $tempScript = Join-Path $env:TEMP 'uninstall-prayertray.ps1'

    if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) {
        Copy-Item -LiteralPath $PSCommandPath -Destination $tempScript -Force
    }
    else {
        Invoke-WebRequest -Uri $RepoUninstallScriptUrl -OutFile $tempScript -UseBasicParsing
    }

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        ('"{0}"' -f $tempScript),
        '-Elevated'
    )

    if ($NoExplorerRestart) {
        $arguments += '-NoExplorerRestart'
    }

    Write-Warn 'Administrator rights needed to remove the Windhawk taskbar slot.'
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
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

function Restart-WindhawkAndExplorer {
    if ($NoExplorerRestart) {
        Write-Warn 'Explorer restart skipped.'
        return
    }

    $windhawkRoot = Get-WindhawkRoot
    if ($windhawkRoot) {
        $windhawkExe = Join-Path $windhawkRoot 'windhawk.exe'
        Start-Process -FilePath $windhawkExe -ArgumentList '-restart' -WindowStyle Hidden -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-Process explorer.exe
    Write-Ok 'Taskbar reloaded.'
}

Write-Banner

if (-not (Test-Administrator)) {
    Invoke-ElevatedUninstaller
}

Write-Step 'Stop PrayerTray'
Stop-Process -Name PrayerTray -Force -ErrorAction SilentlyContinue
Write-Ok 'PrayerTray stopped.'

Write-Step 'Remove startup entry'
if (Test-Path -LiteralPath $RunKeyPath) {
    Remove-ItemProperty -Path $RunKeyPath -Name $AppName -ErrorAction SilentlyContinue
}
Write-Ok 'Startup entry removed.'

Write-Step 'Remove Windhawk taskbar slot'
$regPath = "HKLM:\SOFTWARE\Windhawk\Engine\Mods\$ModId"
$libraryFileName = $null
if (Test-Path -LiteralPath $regPath) {
    $properties = Get-ItemProperty -LiteralPath $regPath -ErrorAction SilentlyContinue
    $libraryProperty = $properties.PSObject.Properties['LibraryFileName']
    if ($libraryProperty) {
        $libraryFileName = $libraryProperty.Value
    }
    Remove-Item -LiteralPath $regPath -Recurse -Force
}

$programDataWindhawk = Join-Path $env:PROGRAMDATA 'Windhawk'
$modsSourcePath = Join-Path $programDataWindhawk "ModsSource\$ModId.wh.cpp"
Remove-Item -LiteralPath $modsSourcePath -Force -ErrorAction SilentlyContinue

$modsOutputDir = Join-Path $programDataWindhawk 'Engine\Mods\64'
if ($libraryFileName) {
    Remove-Item -LiteralPath (Join-Path $modsOutputDir $libraryFileName) -Force -ErrorAction SilentlyContinue
}
Get-ChildItem -LiteralPath $modsOutputDir -Filter "$ModId*.dll" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
Write-Ok 'Windhawk taskbar slot removed.'

Write-Step 'Reload taskbar'
Restart-WindhawkAndExplorer

Write-Step 'Remove app files'
if (Test-Path -LiteralPath $InstallDir) {
    if ($PSCommandPath -and $PSCommandPath.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase)) {
        $cleanupScript = Join-Path $env:TEMP 'cleanup-prayertray-install.ps1'
        @"
Start-Sleep -Seconds 2
Remove-Item -LiteralPath '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue
"@ | Set-Content -LiteralPath $cleanupScript -Encoding UTF8
        Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $cleanupScript))
    }
    else {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }
}
Write-Ok 'App files removed.'

Write-Host ''
Write-Host '============================================================' -ForegroundColor DarkGray
Write-Host ' PrayerTray removed' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor DarkGray
