param(
    [string]$VersionLabel = 'dev',
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\release'
}

$publishRoot = Join-Path $OutputRoot 'publish'
$consolePublish = Join-Path $publishRoot 'console'
$managerPublish = Join-Path $publishRoot 'manager'
$packageRoot = Join-Path $OutputRoot 'package'
$assetsZip = Join-Path $repoRoot 'Assets\Assets.zip'

Remove-Item -LiteralPath $OutputRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $consolePublish, $managerPublish, $packageRoot -Force | Out-Null

& (Join-Path $PSScriptRoot 'Build-AssetsArchive.ps1')

dotnet publish (Join-Path $repoRoot 'ERModsMerger\ERModsMerger.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    --output $consolePublish
if ($LASTEXITCODE -ne 0) { throw 'Console publish failed.' }

dotnet publish (Join-Path $repoRoot 'ERModsManager\ERModsManager.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    --output $managerPublish
if ($LASTEXITCODE -ne 0) { throw 'Manager publish failed.' }

$consoleExe = Join-Path $consolePublish 'ERModsMerger.exe'
$managerExe = Join-Path $managerPublish 'ERModsManager.exe'

foreach ($required in @($consoleExe, $managerExe, $assetsZip)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Expected release file was not produced: $required"
    }
}

Copy-Item -LiteralPath $consoleExe -Destination $packageRoot
Copy-Item -LiteralPath $managerExe -Destination $packageRoot
Copy-Item -LiteralPath $assetsZip -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'Documentation\ChangeLog.md') -Destination (Join-Path $packageRoot 'ChangeLog.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageRoot

$zipPath = Join-Path $OutputRoot ("ERModsMerger-{0}-win-x64.zip" -f $VersionLabel)
Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

Write-Host "Release package: $zipPath"
