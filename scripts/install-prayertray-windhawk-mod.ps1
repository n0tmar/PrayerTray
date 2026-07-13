#Requires -RunAsAdministrator
param(
    [switch]$Disable
)

$ErrorActionPreference = 'Stop'

$ModId = 'prayertray-taskbar-slot'
$ModVersion = '1.0.0'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$SourceFile = Join-Path $RepoRoot 'windhawk\prayertray-taskbar-slot.wh.cpp'

$WindhawkDir = 'C:\PROGRA~1\Windhawk'
$CompilerDir = Join-Path $WindhawkDir 'Compiler'
$EngineVersionDir = Join-Path $WindhawkDir 'Engine\1.7.3'
$ProgramDataWindhawk = Join-Path $env:PROGRAMDATA 'Windhawk'
$ModsSourceDir = Join-Path $ProgramDataWindhawk 'ModsSource'
$ModsOutputDir = Join-Path $ProgramDataWindhawk 'Engine\Mods\64'

if (-not (Test-Path $SourceFile)) {
    throw "Mod source not found: $SourceFile"
}

if (-not (Test-Path (Join-Path $WindhawkDir 'windhawk.exe'))) {
    Write-Host 'Windhawk not found. Installing via winget...'
    winget install RamenSoftware.Windhawk --accept-package-agreements --accept-source-agreements --silent | Out-Host
}

New-Item -ItemType Directory -Path $ModsSourceDir -Force | Out-Null
New-Item -ItemType Directory -Path $ModsOutputDir -Force | Out-Null

$TargetSource = Join-Path $ModsSourceDir "$ModId.wh.cpp"
Copy-Item -Path $SourceFile -Destination $TargetSource -Force
Write-Host "Copied mod source to $TargetSource"

$randomSuffix = Get-Random -Minimum 100000 -Maximum 999999
$DllName = "${ModId}_${ModVersion}_${randomSuffix}.dll"
$DllPath = Join-Path $ModsOutputDir $DllName

$Clang = 'C:\PROGRA~1\Windhawk\Compiler\bin\clang++.exe'
$WindhawkLib = 'C:\PROGRA~1\Windhawk\Engine\1.7.3\64\windhawk.lib'
if (-not (Test-Path $Clang)) { throw "Compiler not found: $Clang" }
if (-not (Test-Path $WindhawkLib)) { throw "windhawk.lib not found: $WindhawkLib" }

Write-Host 'Compiling mod...'
$CompileStdout = Join-Path $env:TEMP 'PrayerTray-Windhawk-compile-stdout.txt'
$CompileStderr = Join-Path $env:TEMP 'PrayerTray-Windhawk-compile-stderr.txt'
$compileCmd = @(
    "`"$Clang`"",
    '-std=c++23 -O2 -shared',
    '-DUNICODE -D_UNICODE',
    '-DWINVER=0x0A00 -D_WIN32_WINNT=0x0A00 -D_WIN32_IE=0x0A00 -DNTDDI_VERSION=0x0A000008',
    '-D__USE_MINGW_ANSI_STDIO=0 -DWH_MOD',
    '-DWH_MOD_ID=L\"prayertray-taskbar-slot\"',
    '-DWH_MOD_VERSION=L\"1.0.0\"',
    "`"$WindhawkLib`"",
    "`"$TargetSource`"",
    '-include windhawk_api.h',
    '-target x86_64-w64-mingw32',
    '-Wl,--export-all-symbols',
    "-o `"$DllPath`"",
    '-lole32 -loleaut32 -lruntimeobject -lshlwapi'
) -join ' '

Push-Location 'C:\PROGRA~1\Windhawk\Compiler'
try {
    cmd /c "$compileCmd 1>`"$CompileStdout`" 2>`"$CompileStderr`""
    Get-Content $CompileStderr -ErrorAction SilentlyContinue | Write-Host
    if (-not (Test-Path $DllPath)) {
        throw 'Compilation failed; DLL was not produced.'
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $DllPath)) {
    throw "Compiled DLL not found: $DllPath"
}

Write-Host "Compiled: $DllPath"

# Copy runtime deps expected by Windhawk mods
$RuntimeLibs = @(
    @('libc++.dll', 'libc++.dll'),
    @('libunwind.dll', 'libunwind.dll'),
    @('windhawk-mod-shim.dll', 'windhawk-mod-shim.dll')
)
$CompilerBin = Join-Path $CompilerDir 'x86_64-w64-mingw32\bin'
foreach ($pair in $RuntimeLibs) {
    $src = Join-Path $CompilerBin $pair[0]
    $dst = Join-Path $ModsOutputDir $pair[1]
    if (Test-Path $src) {
        Copy-Item $src $dst -Force
    }
}

$RegPath = "HKLM:\SOFTWARE\Windhawk\Engine\Mods\$ModId"
New-Item -Path $RegPath -Force | Out-Null
New-ItemProperty -Path $RegPath -Name 'LibraryFileName' -Value $DllName -PropertyType String -Force | Out-Null
New-ItemProperty -Path $RegPath -Name 'Disabled' -Value ($(if ($Disable) { 1 } else { 0 })) -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $RegPath -Name 'Include' -Value 'explorer.exe' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $RegPath -Name 'Architecture' -Value 'x86-64' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $RegPath -Name 'Version' -Value $ModVersion -PropertyType String -Force | Out-Null

if (-not $Disable) {
    Write-Host 'Registered and enabled mod in registry.'
}
else {
    Write-Host 'Registered mod in registry, left disabled. Re-run without -Disable to inject into Explorer.'
}

$WindhawkExe = Join-Path $WindhawkDir 'windhawk.exe'
if (-not $Disable -and (Test-Path $WindhawkExe)) {
    Write-Host 'Restarting Windhawk engine...'
    Start-Process -FilePath $WindhawkExe -ArgumentList '-restart' -WindowStyle Hidden -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

if (-not $Disable) {
    Write-Host 'Restarting Explorer to load the mod into the taskbar...'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-Process explorer
    Start-Sleep -Seconds 3
}

Stop-Process -Name PrayerTray -Force -ErrorAction SilentlyContinue

$SlotFile = Join-Path $env:APPDATA 'PrayerTray\taskbar-slot.txt'
$PrayerTrayExe = $null
if (Test-Path $SlotFile) {
    $exeLine = Get-Content $SlotFile | Where-Object { $_ -like 'EXE=*' } | Select-Object -First 1
    if ($exeLine) {
        $PrayerTrayExe = $exeLine.Substring(4).Trim()
    }
}
if (-not $PrayerTrayExe -or -not (Test-Path $PrayerTrayExe)) {
    $PrayerTrayExe = Join-Path $RepoRoot 'src\PrayerTray\bin\Debug\net10.0-windows\PrayerTray.exe'
}
if (-not (Test-Path $PrayerTrayExe)) {
    $PrayerTrayExe = Join-Path $RepoRoot 'src\PrayerTray\bin\Release\net10.0-windows\PrayerTray.exe'
}
if (Test-Path $PrayerTrayExe) {
    Write-Host "Starting PrayerTray: $PrayerTrayExe"
    Start-Process -FilePath $PrayerTrayExe
}
else {
    Write-Host 'PrayerTray executable not found. Start PrayerTray manually after this script completes.'
}

if (-not (Test-Path $SlotFile)) {
    Write-Host 'PrayerTray slot file not found yet. Start PrayerTray after this script completes.'
}
else {
    Write-Host "Slot file present: $SlotFile"
}

Write-Host ''
Write-Host 'PrayerTray Windhawk mod installed and enabled.'
Write-Host "Mod ID: $ModId"
Write-Host "DLL: $DllName"
