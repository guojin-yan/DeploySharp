[CmdletBinding()]
param(
    [string]$ImageRoot = 'E:\Data\image',
    [string]$OcrRoot = 'E:\Data\ocr',
    [string]$OutputRoot = 'artifacts\test-assets',
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$catalogPath = Join-Path $PSScriptRoot 'test-image-catalog.json'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$stageDirectory = Join-Path $repositoryRoot (Join-Path $OutputRoot ([string]$catalog.release.tag))

function Resolve-SourcePath {
    param([string]$FileName)
    $candidates = switch ($FileName) {
        'bus.jpg' { @((Join-Path $ImageRoot 'bus.jpg')) }
        'demo_7.jpg' { @((Join-Path $ImageRoot 'demo_7.jpg'), (Join-Path $ImageRoot 'demo\_7.jpg')) }
        'demo_9.jpg' { @((Join-Path $ImageRoot 'demo_9.jpg'), (Join-Path $ImageRoot 'demo\_9.jpg')) }
        'plane.png' { @((Join-Path $ImageRoot 'plane.png')) }
        'ocr-demo_1.jpg' { @((Join-Path $OcrRoot 'demo_1.jpg'), (Join-Path $OcrRoot 'demo\_1.jpg')) }
        default { throw "No local source mapping exists for test asset '$FileName'." }
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    throw "Test image source not found for '$FileName'. Checked: $($candidates -join ', ')"
}

function Assert-Asset {
    param([object]$Expected, [string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Staged test image is missing: $Path" }
    $item = Get-Item -LiteralPath $Path
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($item.Length -ne [long]$Expected.sizeBytes -or $hash -ne [string]$Expected.sha256) {
        throw "Test image integrity mismatch for $($Expected.fileName): expected $($Expected.sizeBytes) bytes/$($Expected.sha256), actual $($item.Length) bytes/$hash"
    }
}

if ($Check) {
    $planPath = Join-Path $stageDirectory 'test-image-assets.json'
    if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) { throw "Missing staged asset plan: $planPath" }
    foreach ($asset in @($catalog.assets)) { Assert-Asset $asset (Join-Path $stageDirectory ([string]$asset.fileName)) }
    Write-Output "DEPLOYSHARP_TEST_IMAGE_STAGE_OK assets=$(@($catalog.assets).Count) stage=$stageDirectory"
    return
}

New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null
$records = [System.Collections.Generic.List[object]]::new()
foreach ($asset in @($catalog.assets)) {
    $source = Resolve-SourcePath ([string]$asset.fileName)
    $destination = Join-Path $stageDirectory ([string]$asset.fileName)
    Copy-Item -LiteralPath $source -Destination $destination -Force
    Assert-Asset $asset $destination
    $item = Get-Item -LiteralPath $destination
    $records.Add([ordered]@{
        name = $item.Name
        sizeBytes = [long]$item.Length
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

$checksumPath = Join-Path $stageDirectory 'SHA256SUMS'
$checksumLines = @($records | Sort-Object name | ForEach-Object { $_.sha256 + '  ' + $_.name })
[IO.File]::WriteAllText($checksumPath, (($checksumLines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
$records.Add([ordered]@{ name = 'SHA256SUMS'; sizeBytes = (Get-Item -LiteralPath $checksumPath).Length; sha256 = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256).Hash.ToLowerInvariant() })

$releaseReadme = @'
# DeploySharp test images

This asset collection is the default input set for the DeploySharp visual and PaddleOCR examples.

| File | Default task |
| --- | --- |
| `bus.jpg` | Detection, segmentation, anomaly, background removal, visual-language, promptable segmentation |
| `demo_7.jpg` | Classification |
| `demo_9.jpg` | Pose estimation |
| `plane.png` | Oriented bounding-box detection |
| `ocr-demo_1.jpg` | PaddleOCR |

The files are versioned by the `test-assets.1` release and each SHA-256 is recorded in `test-image-catalog.json` and `SHA256SUMS`. Future images can be appended to this release with the same naming and checksum rules.
'@
[IO.File]::WriteAllText((Join-Path $stageDirectory 'README.md'), $releaseReadme.TrimStart() + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
[IO.File]::Copy($catalogPath, (Join-Path $stageDirectory 'test-image-catalog.json'), $true)
foreach ($metadataName in @('README.md', 'test-image-catalog.json')) {
    $metadataPath = Join-Path $stageDirectory $metadataName
    $metadataItem = Get-Item -LiteralPath $metadataPath
    $records.Add([ordered]@{ name = $metadataName; sizeBytes = [long]$metadataItem.Length; sha256 = (Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256).Hash.ToLowerInvariant() })
}
$plan = [ordered]@{
    schemaVersion = 1
    repository = [string]$catalog.release.repository
    tag = [string]$catalog.release.tag
    assets = @($records)
    defaults = $catalog.defaults
}
[IO.File]::WriteAllText((Join-Path $stageDirectory 'test-image-assets.json'), (($plan | ConvertTo-Json -Depth 8) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
Write-Output "DEPLOYSHARP_TEST_IMAGE_STAGE_READY assets=$(@($records).Count) stage=$stageDirectory"
