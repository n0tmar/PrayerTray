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
$ModVersion = '1.0.0'
$ZipName = 'PrayerTray-win-x64.zip'
$HashName = "$ZipName.sha256"
$ModSourceName = 'prayertray-taskbar-slot.wh.cpp'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\PrayerTray'
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

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

    Write-Host 'PrayerTray needs administrator rights to install the Windhawk taskbar slot.'
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

function Install-Windhawk {
    if (Get-WindhawkRoot) {
        return
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw 'Windhawk is not installed, and winget was not found. Install Windhawk, then run this installer again.'
    }

    Write-Host 'Installing Windhawk...'
    & winget install --id RamenSoftware.Windhawk --exact --accept-package-agreements --accept-source-agreements --silent
    if ($LASTEXITCODE -ne 0) {
        throw 'winget failed to install Windhawk.'
    }

    Start-Sleep -Seconds 2
    if (-not (Get-WindhawkRoot)) {
        throw 'Windhawk install finished, but windhawk.exe was not found.'
    }
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

    Install-Windhawk

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
        '-DWH_MOD_VERSION=L\"1.0.0\"',
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
        cmd /c "$compileCmd 1>`"$compileStdout`" 2>`"$compileStderr`""
        $compilerOutput = Get-Content -LiteralPath $compileStderr -ErrorAction SilentlyContinue
        if ($compilerOutput) {
            $compilerOutput | Write-Host
        }

        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dllPath)) {
            throw 'Windhawk mod compilation failed.'
        }
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
}

function Restart-WindhawkAndExplorer {
    if ($NoExplorerRestart) {
        Write-Host 'Skipping Explorer restart. Restart Explorer later to load the taskbar slot.'
        return
    }

    $windhawkRoot = Get-WindhawkRoot
    if ($windhawkRoot) {
        $windhawkExe = Join-Path $windhawkRoot 'windhawk.exe'
        Start-Process -FilePath $windhawkExe -ArgumentList '-restart' -WindowStyle Hidden -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

    Write-Host 'Restarting Explorer...'
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Start-Process explorer.exe
    Start-Sleep -Seconds 2
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

function Download-ReleaseAsset {
    param(
        [string]$Name,
        [string]$Destination
    )

    $base = $ReleaseBaseUrl.TrimEnd('/')
    $url = "$base/$Name"
    Write-Host "Downloading $Name..."
    Invoke-WebRequest -Uri $url -OutFile $Destination -UseBasicParsing
}

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

    Download-ReleaseAsset -Name $ZipName -Destination $zipPath
    Download-ReleaseAsset -Name $HashName -Destination $hashPath
    Download-ReleaseAsset -Name $ModSourceName -Destination $modPath
    Download-ReleaseAsset -Name 'uninstall.ps1' -Destination $uninstallPath

    $expectedHash = Get-ExpectedHash -Path $hashPath
    $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA256 mismatch for $ZipName"
    }

    Stop-Process -Name PrayerTray -Force -ErrorAction SilentlyContinue

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

    Install-WindhawkMod -ModSourcePath $modPath
    Restart-WindhawkAndExplorer

    Start-Process -FilePath $exePath

    Write-Host ''
    Write-Host 'PrayerTray installed.'
    Write-Host "App: $exePath"
    Write-Host 'Right-click the taskbar prayer time to open settings.'
}
finally {
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
}
