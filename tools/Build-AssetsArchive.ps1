param(
    [string]$AssetsRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($AssetsRoot)) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $AssetsRoot = Join-Path $repoRoot 'Assets'
}
else {
    $AssetsRoot = (Resolve-Path -LiteralPath $AssetsRoot).Path
}

$assetsZipPath = Join-Path $AssetsRoot 'Assets.zip'
$tempZipPath = Join-Path ([System.IO.Path]::GetTempPath()) ("ERModsMerger.Assets.{0}.zip" -f [guid]::NewGuid().ToString('N'))

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::Open(
    $tempZipPath,
    [System.IO.Compression.ZipArchiveMode]::Create)

try {
    Get-ChildItem -LiteralPath $AssetsRoot -File -Recurse |
        Where-Object { $_.FullName -ne $assetsZipPath } |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($AssetsRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $_.FullName,
                $relativePath,
                [System.IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
}
finally {
    $archive.Dispose()
}

Move-Item -LiteralPath $tempZipPath -Destination $assetsZipPath -Force
Write-Host "Generated Assets.zip: $assetsZipPath"
