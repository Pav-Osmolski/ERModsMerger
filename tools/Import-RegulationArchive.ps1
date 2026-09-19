param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$assetsRoot = Join-Path $repoRoot 'Assets'
$manifestPath = Join-Path $assetsRoot 'Regulations\manifest.json'
$destinationRoot = Join-Path $assetsRoot 'Regulations'
$assetsZipPath = Join-Path $assetsRoot 'Assets.zip'

if (-not (Test-Path -LiteralPath $ArchivePath)) {
    throw "Archive not found: $ArchivePath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)

try {
    foreach ($regulation in $manifest.regulations) {
        $entryPath = "ER Regulation Archive/$($regulation.archiveLabel) ($($regulation.rawVersion))/regulation.bin"
        $entry = $zip.Entries | Where-Object { $_.FullName -eq $entryPath } | Select-Object -First 1

        if ($null -eq $entry) {
            throw "Missing expected archive entry: $entryPath"
        }

        if ($entry.Length -ne [int64]$regulation.size) {
            throw "Unexpected size for $entryPath. Expected $($regulation.size), found $($entry.Length)."
        }

        $targetDirectory = Join-Path $destinationRoot $regulation.assetFolder
        $targetPath = Join-Path $targetDirectory 'regulation.bin'
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null

        $sourceStream = $entry.Open()
        try {
            $targetStream = [System.IO.File]::Create($targetPath)
            try {
                $sourceStream.CopyTo($targetStream)
            }
            finally {
                $targetStream.Dispose()
            }
        }
        finally {
            $sourceStream.Dispose()
        }

        $actualHash = (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $regulation.sha256.ToLowerInvariant()) {
            throw "SHA-256 mismatch for $targetPath. Expected $($regulation.sha256), found $actualHash."
        }

        Write-Host ("Imported {0,-7} raw {1} -> {2}" -f $regulation.assetFolder, $regulation.rawVersion, $targetPath)
    }
}
finally {
    $zip.Dispose()
}

Write-Host ""
Write-Host "Imported and verified $($manifest.regulations.Count) regulation files."
Write-Host "Rebuilding Assets.zip..."

$tempZipPath = Join-Path ([System.IO.Path]::GetTempPath()) ("ERModsMerger.Assets.{0}.zip" -f [guid]::NewGuid().ToString('N'))
$archive = [System.IO.Compression.ZipFile]::Open($tempZipPath, [System.IO.Compression.ZipArchiveMode]::Create)

try {
    Get-ChildItem -LiteralPath $assetsRoot -File -Recurse |
        Where-Object { $_.FullName -ne $assetsZipPath } |
        Sort-Object FullName |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($assetsRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
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

$rebuilt = [System.IO.Compression.ZipFile]::OpenRead($assetsZipPath)
try {
    $entryNames = @{}
    foreach ($entry in $rebuilt.Entries) {
        $entryNames[$entry.FullName] = $true
    }

    foreach ($regulation in $manifest.regulations) {
        $expected = "Regulations/$($regulation.assetFolder)/regulation.bin"
        if (-not $entryNames.ContainsKey($expected)) {
            throw "Rebuilt Assets.zip is missing expected entry: $expected"
        }
    }
}
finally {
    $rebuilt.Dispose()
}

Write-Host "Rebuilt and verified Assets.zip: $assetsZipPath"
Write-Host ""
& (Join-Path $PSScriptRoot 'Test-RegulationAssets.ps1')
