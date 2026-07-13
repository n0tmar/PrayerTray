[CmdletBinding()]
param(
    [string]$ReleaseBaseUrl = 'https://github.com/n0tmar/PrayerTray/releases/latest/download',
    [switch]$Elevated,
    [switch]$NoExplorerRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$AppName = 'PrayerTray'
$RepoInstallScriptUrl = 'https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1'
$ModId = 'prayertray-taskbar-slot'
$ModVersion = '1.1.2'
$ZipName = 'PrayerTray-win-x64.zip'
$HashName = "$ZipName.sha256"
$ModSourceName = 'prayertray-taskbar-slot.wh.cpp'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\PrayerTray'
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$AppDataDir = Join-Path $env:APPDATA 'PrayerTray'
$SettingsPath = Join-Path $AppDataDir 'settings.json'
$SlotFilePath = Join-Path $AppDataDir 'taskbar-slot.txt'
$SlotStatusPath = Join-Path $AppDataDir 'taskbar-slot-status.txt'
$script:ReleaseAssetUrls = $null

function Write-PrayerTrayLogo {
    Write-Host '                                             ' -ForegroundColor Cyan
    Write-Host ' _____                     _____             ' -ForegroundColor Cyan
    Write-Host '|  _  |___ ___ _ _ ___ ___|_   _|___ ___ _ _ ' -ForegroundColor Cyan
    Write-Host "|   __|  _| .'| | | -_|  _| | | |  _| .'| | |" -ForegroundColor Cyan
    Write-Host '|__|  |_| |__,|_  |___|_|   |_| |_| |__,|_  |' -ForegroundColor Cyan
    Write-Host '              |___|                     |___|' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '---------' -ForegroundColor DarkGray
}

function Write-Banner {
    Write-Host ''
    Write-PrayerTrayLogo
    Write-Host ' PrayerTray installer' -ForegroundColor Cyan
    Write-Host ' Small prayer-time slot for the Windows taskbar.' -ForegroundColor DarkGray
    Write-Host '---------' -ForegroundColor DarkGray
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

function Write-Info {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
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

function Invoke-ElevatedInstaller {
    $tempScript = Join-Path $env:TEMP 'install-prayertray.ps1'

    if ($PSCommandPath -and (Test-Path -LiteralPath $PSCommandPath)) {
        Copy-Item -LiteralPath $PSCommandPath -Destination $tempScript -Force
    }
    else {
        Invoke-WebRequest -Uri $RepoInstallScriptUrl -OutFile $tempScript -UseBasicParsing
    }

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        ('"{0}"' -f $tempScript),
        '-Elevated',
        '-ReleaseBaseUrl',
        ('"{0}"' -f $ReleaseBaseUrl)
    )

    if ($NoExplorerRestart) {
        $arguments += '-NoExplorerRestart'
    }

    Write-Warn 'Administrator rights needed for the Windhawk taskbar slot.'
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

function Install-WindhawkWithWinget {
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Warn 'Winget was not found.'
        return $false
    }

    $wingetLog = Join-Path $env:TEMP 'PrayerTray-Windhawk-winget-install.log'
    Remove-Item -LiteralPath $wingetLog -Force -ErrorAction SilentlyContinue

    Write-Info 'Installing Windhawk with Winget...'
    & $winget.Source install `
        --id RamenSoftware.Windhawk `
        --exact `
        --source winget `
        --accept-package-agreements `
        --accept-source-agreements `
        --silent `
        --disable-interactivity *> $wingetLog

    if ($LASTEXITCODE -ne 0) {
        Write-Warn 'Winget could not install Windhawk.'
        Write-Info "Winget log: $wingetLog"
        return $false
    }

    Start-Sleep -Seconds 2
    if (Get-WindhawkRoot) {
        return $true
    }

    Write-Warn 'Winget finished, but Windhawk was not found.'
    Write-Info "Winget log: $wingetLog"
    return $false
}

function Install-WindhawkFromGitHub {
    $setupPath = Join-Path $env:TEMP 'windhawk_setup.exe'
    Remove-Item -LiteralPath $setupPath -Force -ErrorAction SilentlyContinue

    try {
        Write-Info 'Downloading Windhawk installer from GitHub...'
        $release = Invoke-RestMethod `
            -Uri 'https://api.github.com/repos/ramensoftware/windhawk/releases/latest' `
            -Headers @{ 'User-Agent' = 'PrayerTray-installer' }
        $asset = @($release.assets) |
            Where-Object { $_.name -eq 'windhawk_setup.exe' } |
            Select-Object -First 1

        if (-not $asset) {
            throw 'Windhawk installer asset was not found.'
        }

        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $setupPath -UseBasicParsing

        Write-Info 'Installing Windhawk...'
        $process = Start-Process -FilePath $setupPath -ArgumentList @('/S', '/STANDARD') -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Windhawk installer exited with code $($process.ExitCode)."
        }

        Start-Sleep -Seconds 3
        return [bool](Get-WindhawkRoot)
    }
    catch {
        Write-Warn 'Direct Windhawk install did not finish.'
        Write-Info $_.Exception.Message
        return $false
    }
}

function Install-Windhawk {
    if (Get-WindhawkRoot) {
        Write-Ok 'Windhawk found.'
        return $true
    }

    if (Install-WindhawkWithWinget) {
        Write-Ok 'Windhawk installed.'
        return $true
    }

    if (Install-WindhawkFromGitHub) {
        Write-Ok 'Windhawk installed.'
        return $true
    }

    Write-Warn 'Windhawk was not installed.'
    Write-Warn 'PrayerTray will use the fallback taskbar item.'
    Write-Info 'Install Windhawk manually from https://windhawk.net, then run this installer again for the native slot.'
    return $false
}

function Get-WindhawkEngineDir {
    param([string]$WindhawkRoot)

    $engineRoot = Join-Path $WindhawkRoot 'Engine'
    if (-not (Test-Path -LiteralPath $engineRoot)) {
        throw "Windhawk engine folder not found: $engineRoot"
    }

    $engineDir = Get-ChildItem -LiteralPath $engineRoot -Directory |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName '64\windhawk.lib') } |
        Sort-Object -Property @{ Expression = { try { [version]$_.Name } catch { [version]'0.0' } } } -Descending |
        Select-Object -First 1

    if (-not $engineDir) {
        throw 'No 64-bit Windhawk engine folder with windhawk.lib was found.'
    }

    return $engineDir.FullName
}

function Install-WindhawkMod {
    param(
        [string]$ModSourcePath
    )

    if (-not (Install-Windhawk)) {
        return $false
    }

    $windhawkRoot = Get-WindhawkRoot
    $engineDir = Get-WindhawkEngineDir -WindhawkRoot $windhawkRoot
    $compilerDir = Join-Path $windhawkRoot 'Compiler'
    $clang = Join-Path $compilerDir 'bin\clang++.exe'
    $windhawkLib = Join-Path $engineDir '64\windhawk.lib'
    $includeDir = Join-Path $compilerDir 'include'

    if (-not (Test-Path -LiteralPath $clang)) { throw "Windhawk compiler not found: $clang" }
    if (-not (Test-Path -LiteralPath $windhawkLib)) { throw "windhawk.lib not found: $windhawkLib" }
    if (-not (Test-Path -LiteralPath (Join-Path $includeDir 'windhawk_api.h'))) { throw "windhawk_api.h not found: $includeDir" }

    $programDataWindhawk = Join-Path $env:PROGRAMDATA 'Windhawk'
    $modsSourceDir = Join-Path $programDataWindhawk 'ModsSource'
    $modsOutputDir = Join-Path $programDataWindhawk 'Engine\Mods\64'
    New-Item -ItemType Directory -Path $modsSourceDir, $modsOutputDir -Force | Out-Null

    $targetSource = Join-Path $modsSourceDir "$ModId.wh.cpp"
    Copy-Item -LiteralPath $ModSourcePath -Destination $targetSource -Force
    Write-Info 'Copied Windhawk mod source.'

    $dllName = '{0}_{1}_{2}.dll' -f $ModId, $ModVersion, (Get-Random -Minimum 100000 -Maximum 999999)
    $dllPath = Join-Path $modsOutputDir $dllName
    $compileStdout = Join-Path $env:TEMP 'PrayerTray-Windhawk-compile-stdout.txt'
    $compileStderr = Join-Path $env:TEMP 'PrayerTray-Windhawk-compile-stderr.txt'

    $compileCmd = @(
        ('"{0}"' -f $clang),
        '-std=c++23 -O2 -shared',
        '-DUNICODE -D_UNICODE',
        '-DWINVER=0x0A00 -D_WIN32_WINNT=0x0A00 -D_WIN32_IE=0x0A00 -DNTDDI_VERSION=0x0A000008',
        '-D__USE_MINGW_ANSI_STDIO=0 -DWH_MOD',
        '-DWH_MOD_ID=L\"prayertray-taskbar-slot\"',
        '-DWH_MOD_VERSION=L\"1.1.2\"',
        ('"{0}"' -f $windhawkLib),
        ('"{0}"' -f $targetSource),
        ('-I "{0}"' -f $includeDir),
        '-include windhawk_api.h',
        '-target x86_64-w64-mingw32',
        '-Wl,--export-all-symbols',
        ('-o "{0}"' -f $dllPath),
        '-lole32 -loleaut32 -lruntimeobject -lshlwapi'
    ) -join ' '

    Push-Location $compilerDir
    try {
        Write-Info 'Compiling Windhawk taskbar slot...'
        cmd /c "$compileCmd 1>`"$compileStdout`" 2>`"$compileStderr`""
        $compilerOutput = Get-Content -LiteralPath $compileStderr -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dllPath)) {
            Write-Warn 'Windhawk could not compile the taskbar slot.'
            Write-Warn 'PrayerTray is installed, but the taskbar slot was not added.'
            Write-Info "Compiler log: $compileStderr"
            throw 'Update Windhawk, then run the PrayerTray installer again.'
        }

        if ($compilerOutput) {
            Write-Info 'Windhawk compiler reported a warning. The mod compiled and will still be enabled.'
            Write-Info "Compiler log: $compileStderr"
        }

        Write-Ok 'Windhawk taskbar slot compiled.'
    }
    finally {
        Pop-Location
    }

    $runtimeLibs = @('libc++.dll', 'libunwind.dll', 'windhawk-mod-shim.dll')
    $compilerBin = Join-Path $compilerDir 'x86_64-w64-mingw32\bin'
    foreach ($name in $runtimeLibs) {
        $source = Join-Path $compilerBin $name
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $modsOutputDir $name) -Force
        }
    }

    $regPath = "HKLM:\SOFTWARE\Windhawk\Engine\Mods\$ModId"
    New-Item -Path $regPath -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'LibraryFileName' -Value $dllName -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'Disabled' -Value 0 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'Include' -Value 'explorer.exe' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'Architecture' -Value 'x86-64' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $regPath -Name 'Version' -Value $ModVersion -PropertyType String -Force | Out-Null
    Write-Ok 'Windhawk taskbar slot enabled.'
    return $true
}

function Restart-WindhawkAndExplorer {
    if ($NoExplorerRestart) {
        Write-Host 'Skipping Explorer restart. Restart Explorer later to load the taskbar slot.'
        return
    }

    $windhawkRoot = Get-WindhawkRoot
    if ($windhawkRoot) {
        $windhawkExe = Join-Path $windhawkRoot 'windhawk.exe'
        Write-Info 'Restarting Windhawk...'
        Start-Process -FilePath $windhawkExe -ArgumentList '-restart' -WindowStyle Hidden -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    Write-Info 'Restarting Explorer...'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-Process explorer.exe
    Start-Sleep -Seconds 2
    Write-Ok 'Taskbar reloaded.'
}

function Get-ExpectedHash {
    param([string]$Path)

    $text = Get-Content -LiteralPath $Path -Raw
    $match = [regex]::Match($text, '(?i)\b[a-f0-9]{64}\b')
    if (-not $match.Success) {
        throw "No SHA256 hash found in $Path"
    }

    return $match.Value.ToUpperInvariant()
}

function Get-ReleaseAssetUrl {
    param([string]$Name)

    $base = $ReleaseBaseUrl.TrimEnd('/')
    if ($base -notmatch '^https://github\.com/n0tmar/PrayerTray/releases/latest/download$') {
        return "$base/$Name"
    }

    if ($null -eq $script:ReleaseAssetUrls) {
        $script:ReleaseAssetUrls = @{}
        try {
            $release = Invoke-RestMethod `
                -Uri 'https://api.github.com/repos/n0tmar/PrayerTray/releases/latest' `
                -Headers @{ 'User-Agent' = 'PrayerTray-installer' }

            foreach ($asset in @($release.assets)) {
                $script:ReleaseAssetUrls[$asset.name] = $asset.browser_download_url
            }

            Write-Info "Latest release: $($release.tag_name)"
        }
        catch {
            Write-Warn 'Could not read the latest GitHub release.'
            Write-Info $_.Exception.Message
        }
    }

    if ($script:ReleaseAssetUrls.ContainsKey($Name)) {
        return $script:ReleaseAssetUrls[$Name]
    }

    return "$base/$Name"
}

function Invoke-DownloadWithCurl {
    param(
        [string]$Url,
        [string]$Destination,
        [string]$LogPath
    )

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if (-not $curl) {
        return $false
    }

    & $curl.Source `
        --fail `
        --location `
        --silent `
        --show-error `
        --retry 3 `
        --retry-delay 2 `
        --connect-timeout 20 `
        --output $Destination `
        $Url *> $LogPath

    return $LASTEXITCODE -eq 0
}

function Download-ReleaseAsset {
    param(
        [string]$Name,
        [string]$Destination
    )

    $url = Get-ReleaseAssetUrl -Name $Name
    $logPath = Join-Path $env:TEMP "PrayerTray-download-$Name.log"
    Write-Info "Downloading $Name"

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        Remove-Item -LiteralPath $Destination, $logPath -Force -ErrorAction SilentlyContinue

        if (Invoke-DownloadWithCurl -Url $url -Destination $Destination -LogPath $logPath) {
            return
        }

        try {
            Invoke-WebRequest -Uri $url -OutFile $Destination -UseBasicParsing -ErrorAction Stop
            return
        }
        catch {
            $message = $_.Exception.Message
            if ($_.Exception.Response) {
                $message = "$($_.Exception.Response.StatusCode.value__) $($_.Exception.Response.StatusDescription)"
            }

            if ($attempt -eq 5) {
                Write-Warn "Could not download $Name."
                Write-Info "GitHub said: $message"
                Write-Info "Download log: $logPath"
                throw 'GitHub did not return the release file. Try the installer again in a minute.'
            }

            Write-Warn "Download failed for $Name. Trying again..."
            Write-Info "GitHub said: $message"
            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

function Set-JsonProperty {
    param(
        [pscustomobject]$Object,
        [string]$Name,
        [object]$Value
    )

    if ($Object.PSObject.Properties[$Name]) {
        $Object.$Name = $Value
    }
    else {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Enable-TaskbarWidgetInSettings {
    New-Item -ItemType Directory -Path $AppDataDir -Force | Out-Null

    $settings = [pscustomobject]@{}
    if (Test-Path -LiteralPath $SettingsPath) {
        try {
            $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
        }
        catch {
            $backupPath = "$SettingsPath.bak"
            Copy-Item -LiteralPath $SettingsPath -Destination $backupPath -Force -ErrorAction SilentlyContinue
            Write-Warn "Settings file was invalid. Backup saved to $backupPath"
        }
    }

    Set-JsonProperty -Object $settings -Name 'showWidget' -Value $true
    $displayMode = if ([Environment]::OSVersion.Version.Build -ge 22000) { 'NativeSlot' } else { 'Embed' }
    Set-JsonProperty -Object $settings -Name 'taskbarDisplayMode' -Value $displayMode
    Set-JsonProperty -Object $settings -Name 'startWithWindows' -Value $true

    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $SettingsPath -Encoding UTF8
    Write-Ok 'Taskbar visibility enabled.'
}

function Start-PrayerTray {
    param([string]$ExePath)

    Stop-Process -Name PrayerTray -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -LiteralPath $ExePath)
    Write-Ok 'PrayerTray started.'
}

function Test-SlotFileVisible {
    if (-not (Test-Path -LiteralPath $SlotFilePath)) {
        return $false
    }

    $lines = Get-Content -LiteralPath $SlotFilePath -ErrorAction SilentlyContinue
    if (-not $lines) {
        return $false
    }

    $visible = $lines | Where-Object { $_ -eq 'VISIBLE=1' } | Select-Object -First 1
    $top = $lines | Where-Object { $_ -like 'TOP=*' } | Select-Object -First 1
    $bottom = $lines | Where-Object { $_ -like 'BOTTOM=*' } | Select-Object -First 1
    return [bool]($visible -and $top -and $bottom)
}

function Test-NativeSlotVisible {
    if (-not (Test-Path -LiteralPath $SlotStatusPath)) {
        return $false
    }

    $lines = Get-Content -LiteralPath $SlotStatusPath -ErrorAction SilentlyContinue
    if (-not $lines) {
        return $false
    }

    $visible = $lines | Where-Object { $_ -eq 'VISIBLE=1' } | Select-Object -First 1
    if (-not $visible) {
        return $false
    }

    $lastWriteUtc = (Get-Item -LiteralPath $SlotStatusPath).LastWriteTimeUtc
    return (((Get-Date).ToUniversalTime() - $lastWriteUtc).TotalSeconds -lt 8)
}

function Wait-PrayerTraySlotFile {
    param([int]$TimeoutSeconds = 25)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-SlotFileVisible) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Wait-NativeSlot {
    param([int]$TimeoutSeconds = 25)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-NativeSlotVisible) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

Write-Banner

if (-not (Test-Administrator)) {
    Invoke-ElevatedInstaller
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$workDir = Join-Path $env:TEMP ('PrayerTrayInstall_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

try {
    $zipPath = Join-Path $workDir $ZipName
    $hashPath = Join-Path $workDir $HashName
    $modPath = Join-Path $workDir $ModSourceName
    $uninstallPath = Join-Path $workDir 'uninstall.ps1'

    Write-Step 'Download release files'
    Download-ReleaseAsset -Name $ZipName -Destination $zipPath
    Download-ReleaseAsset -Name $HashName -Destination $hashPath
    Download-ReleaseAsset -Name $ModSourceName -Destination $modPath
    Download-ReleaseAsset -Name 'uninstall.ps1' -Destination $uninstallPath

    Write-Step 'Verify package'
    $expectedHash = Get-ExpectedHash -Path $hashPath
    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA256 mismatch for $ZipName"
    }
    Write-Ok 'SHA256 matched.'

    Stop-Process -Name PrayerTray -Force -ErrorAction SilentlyContinue

    Write-Step 'Install app'
    $extractDir = Join-Path $workDir 'app'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Copy-Item -Path (Join-Path $extractDir '*') -Destination $InstallDir -Recurse -Force
    Copy-Item -LiteralPath $uninstallPath -Destination (Join-Path $InstallDir 'uninstall.ps1') -Force

    $exePath = Join-Path $InstallDir 'PrayerTray.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "PrayerTray.exe was not found after extraction: $exePath"
    }

    New-Item -Path $RunKeyPath -Force | Out-Null
    New-ItemProperty -Path $RunKeyPath -Name $AppName -Value ('"{0}"' -f $exePath) -PropertyType String -Force | Out-Null
    Write-Ok "Installed to $InstallDir"
    Write-Ok 'Startup enabled.'

    Enable-TaskbarWidgetInSettings

    Write-Step 'Start PrayerTray'
    Remove-Item -LiteralPath $SlotStatusPath -Force -ErrorAction SilentlyContinue
    Start-PrayerTray -ExePath $exePath
    if (Wait-PrayerTraySlotFile -TimeoutSeconds 25) {
        Write-Ok 'Taskbar text file ready.'
    }
    else {
        Write-Warn 'Taskbar text file was not ready yet. The app may still be resolving location.'
    }

    Write-Step 'Install taskbar slot'
    $nativeSlotInstalled = $false
    try {
        $nativeSlotInstalled = Install-WindhawkMod -ModSourcePath $modPath
    }
    catch {
        Write-Warn 'Native taskbar slot was not installed.'
        Write-Info $_.Exception.Message
        Write-Warn 'PrayerTray will use the fallback taskbar item.'
    }

    if ($nativeSlotInstalled) {
        Write-Step 'Reload taskbar'
        Restart-WindhawkAndExplorer
    }

    if (-not (Get-Process PrayerTray -ErrorAction SilentlyContinue)) {
        Start-PrayerTray -ExePath $exePath
    }

    if (Wait-PrayerTraySlotFile -TimeoutSeconds 20) {
        Write-Ok 'Prayer time is publishing to the taskbar.'
    }
    else {
        Write-Warn 'PrayerTray installed, but the taskbar slot did not publish in time.'
        Write-Warn 'Open PrayerTray settings, press Save, or restart Explorer once.'
    }

    if ($nativeSlotInstalled -and [Environment]::OSVersion.Version.Build -ge 22000) {
        if (Wait-NativeSlot -TimeoutSeconds 25) {
            Write-Ok 'Native taskbar slot is visible.'
        }
        else {
            Write-Warn 'Native taskbar slot did not report back.'
            Write-Warn 'PrayerTray will show the fallback taskbar item if Windhawk is blocked.'
        }
    }
    elseif (-not $nativeSlotInstalled) {
        Write-Ok 'Fallback taskbar item is active.'
    }

    Write-Host ''
    Write-Host '============================================================' -ForegroundColor DarkGray
    Write-Host ' PrayerTray installed' -ForegroundColor Green
    Write-Host '============================================================' -ForegroundColor DarkGray
    Write-Host " App: $exePath"
    Write-Host ' Left-click taskbar prayer time: prayer times'
    Write-Host ' Right-click taskbar prayer time: settings'
    Write-Host ''
}
finally {
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
