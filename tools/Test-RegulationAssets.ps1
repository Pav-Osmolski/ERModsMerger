$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$assetsRoot = Join-Path $repoRoot 'Assets'
$regulationsRoot = Join-Path $assetsRoot 'Regulations'
$manifestPath = Join-Path $regulationsRoot 'manifest.json'
$assetsZipPath = Join-Path $assetsRoot 'Assets.zip'

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Regulation manifest not found: $manifestPath"
}

if (-not (Test-Path -LiteralPath $assetsZipPath)) {
    throw "Assets archive not found: $assetsZipPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$expectedFolders = @($manifest.regulations | ForEach-Object { $_.assetFolder })
$actualFolders = @(Get-ChildItem -LiteralPath $regulationsRoot -Directory | Select-Object -ExpandProperty Name)

$missingFolders = @($expectedFolders | Where-Object { $_ -notin $actualFolders })
$extraFolders = @($actualFolders | Where-Object { $_ -notin $expectedFolders })

if ($missingFolders.Count -gt 0) {
    throw "Missing regulation asset folders: $($missingFolders -join ', ')"
}

if ($extraFolders.Count -gt 0) {
    throw "Unexpected regulation asset folders: $($extraFolders -join ', ')"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead($assetsZipPath)

try {
    $entries = @{}
    foreach ($entry in $zip.Entries) {
        $entries[$entry.FullName] = $entry
    }

    foreach ($regulation in $manifest.regulations) {
        $loosePath = Join-Path (Join-Path $regulationsRoot $regulation.assetFolder) 'regulation.bin'
        if (-not (Test-Path -LiteralPath $loosePath)) {
            throw "Missing loose regulation asset: $loosePath"
        }

        $looseFile = Get-Item -LiteralPath $loosePath
        if ($looseFile.Length -ne [int64]$regulation.size) {
            throw "Loose asset size mismatch for $($regulation.assetFolder). Expected $($regulation.size), found $($looseFile.Length)."
        }

        $looseHash = (Get-FileHash -LiteralPath $loosePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($looseHash -ne $regulation.sha256.ToLowerInvariant()) {
            throw "Loose asset SHA-256 mismatch for $($regulation.assetFolder). Expected $($regulation.sha256), found $looseHash."
        }

        $zipPath = "Regulations/$($regulation.assetFolder)/regulation.bin"
        if (-not $entries.ContainsKey($zipPath)) {
            throw "Assets.zip is missing expected regulation: $zipPath"
        }

        $zipEntry = $entries[$zipPath]
        if ($zipEntry.Length -ne [int64]$regulation.size) {
            throw "Assets.zip size mismatch for $zipPath. Expected $($regulation.size), found $($zipEntry.Length)."
        }

        $stream = $zipEntry.Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $zipHashBytes = $sha.ComputeHash($stream)
            $zipHash = ([System.BitConverter]::ToString($zipHashBytes)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha.Dispose()
            $stream.Dispose()
        }

        if ($zipHash -ne $regulation.sha256.ToLowerInvariant()) {
            throw "Assets.zip SHA-256 mismatch for $zipPath. Expected $($regulation.sha256), found $zipHash."
        }

        Write-Host ("Verified {0,-7} raw {1}" -f $regulation.assetFolder, $regulation.rawVersion)
    }
}
finally {
    $zip.Dispose()
}

Write-Host ""
Write-Host "Regulation assets verified: $($manifest.regulations.Count) loose files and matching Assets.zip entries."
