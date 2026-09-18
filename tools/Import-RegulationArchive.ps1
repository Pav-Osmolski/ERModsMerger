param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot 'Assets\Regulations\manifest.json'
$destinationRoot = Join-Path $repoRoot 'Assets\Regulations'

if (-not (Test-Path -LiteralPath $ArchivePath)) {
    throw "Archive not found: $ArchivePath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
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
