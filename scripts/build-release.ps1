[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $RepoRoot 'src\PrayerTray\PrayerTray.csproj'
$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$PublishDir = Join-Path $ArtifactsDir "publish\$Runtime"
$ReleaseDir = Join-Path $ArtifactsDir 'release'
$ZipName = "PrayerTray-$Runtime.zip"
$ZipPath = Join-Path $ReleaseDir $ZipName
$HashPath = Join-Path $ReleaseDir "$ZipName.sha256"

if (-not $SkipTests) {
    $testOutDir = Join-Path $env:TEMP ('PrayerTrayTest_' + [guid]::NewGuid().ToString('N'))
    dotnet test (Join-Path $RepoRoot 'PrayerTray.slnx') -v:minimal -p:OutDir="$testOutDir\"
}

Remove-Item -LiteralPath $PublishDir, $ReleaseDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $PublishDir, $ReleaseDir -Force | Out-Null

dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $PublishDir

if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $ZipPath -Force

$hash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToUpperInvariant()
Set-Content -LiteralPath $HashPath -Value "$hash  $ZipName" -Encoding ASCII

Copy-Item -LiteralPath (Join-Path $RepoRoot 'scripts\install.ps1') -Destination (Join-Path $ReleaseDir 'install.ps1') -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot 'scripts\uninstall.ps1') -Destination (Join-Path $ReleaseDir 'uninstall.ps1') -Force
Copy-Item -LiteralPath (Join-Path $RepoRoot 'windhawk\prayertray-taskbar-slot.wh.cpp') -Destination (Join-Path $ReleaseDir 'prayertray-taskbar-slot.wh.cpp') -Force

Write-Host ''
Write-Host 'Release assets ready:'
Get-ChildItem -LiteralPath $ReleaseDir | Select-Object Name, Length
