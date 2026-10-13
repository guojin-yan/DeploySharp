[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestRoot,

    [string]$OutputJson,

    [string]$OutputMarkdown
)

$ErrorActionPreference = 'Stop'

function Resolve-Directory([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Label does not exist: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Add-Count([hashtable]$Table, [string]$Key) {
    if ([string]::IsNullOrWhiteSpace($Key)) { $Key = '<blank>' }
    if (-not $Table.ContainsKey($Key)) { $Table[$Key] = 0 }
    $Table[$Key]++
}

function Convert-ToOrdered([hashtable]$Table) {
    $ordered = [ordered]@{}
    foreach ($key in ($Table.Keys | Sort-Object)) { $ordered[$key] = $Table[$key] }
    return $ordered
}

function Get-Text([object]$Value) {
    if ($null -eq $Value) { return '' }
    return ([string]$Value).Trim()
}

function Test-ExplicitNaturalLabel([object]$DirectionLabel, [object]$Attributes) {
    if ($null -eq $DirectionLabel) { return $false }
    $label = Get-Text $DirectionLabel.label
    $class = Get-Text $DirectionLabel.class
    $source = Get-Text $DirectionLabel.label_source
    if ([string]::IsNullOrWhiteSpace($label) -and [string]::IsNullOrWhiteSpace($class)) { return $false }
    if ($source -match 'unavailable|unknown|inferred|synthetic|transform|oracle|not_checked') { return $false }
    if ($label -match 'unknown|unavailable|not[_ -]?checked') { return $false }
    if ($class -match '^(null|unknown)$') { return $false }
    # A field is useful only when it is explicitly sourced by the dataset/annotator.
    return $true
}

function Test-NonZeroAngle([object]$Value) {
    if ($null -eq $Value) { return $false }
    $text = Get-Text $Value
    if ([string]::IsNullOrWhiteSpace($text) -or $text -match '^(unknown|null|none)$') { return $false }
    $number = 0.0
    if ([double]::TryParse($text, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$number)) {
        return [Math]::Abs($number) -gt 0.000001
    }
    return $text -match '90|180|270|rotat|tilt|skew'
}

$root = Resolve-Directory $ManifestRoot 'PaddleOCR benchmark manifest root'
$files = @(Get-ChildItem -LiteralPath $root -File -Filter '*.jsonl' | Sort-Object Name)
if ($files.Count -eq 0) { throw "No JSONL manifests found under $root" }

$manifestResults = New-Object System.Collections.Generic.List[object]
$aggregate = [ordered]@{
    manifestCount = 0
    imageRecordCount = 0
    instanceCount = 0
    nonEmptyTextInstanceCount = 0
    explicitNaturalDirectionLabelCount = 0
    explicitNaturalDirectionImageCount = 0
    unavailableDirectionLabelCount = 0
    unknownOrientationImageCount = 0
    verticalTrueInstanceCount = 0
    nonZeroAngleMetadataInstanceCount = 0
    nonZeroAngleMetadataImageCount = 0
    labelSourceCounts = @{}
    directionClassCounts = @{}
    topLevelOrientationCounts = @{}
    datasets = @{}
}

foreach ($file in $files) {
    $images = 0
    $instances = 0
    $nonEmpty = 0
    $explicitLabels = 0
    $explicitLabelImages = 0
    $unavailable = 0
    $unknownOrientation = 0
    $vertical = 0
    $nonZeroAngleInstances = 0
    $nonZeroAngleImages = 0
    $labelSources = @{}
    $directionClasses = @{}
    $orientations = @{}
    $datasets = @{}
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $record = $line | ConvertFrom-Json
        if ((Get-Text $record.record_type) -ne 'image') { continue }
        $images++
        $dataset = Get-Text $record.source_dataset
        if (-not [string]::IsNullOrWhiteSpace($dataset)) { Add-Count $datasets $dataset; Add-Count $aggregate.datasets $dataset }
        $orientation = Get-Text $record.dimensions.orientation
        Add-Count $orientations $orientation
        Add-Count $aggregate.topLevelOrientationCounts $orientation
        if ($orientation -match 'unknown|unavailable|not[_ -]?checked') {
            $unknownOrientation++
            $aggregate.unknownOrientationImageCount++
        }
        $imageHasExplicit = $false
        if (Test-NonZeroAngle $record.rotation -or Test-NonZeroAngle $record.angle -or Test-NonZeroAngle $record.source_rotation) {
            $nonZeroAngleImages++
            $aggregate.nonZeroAngleMetadataImageCount++
        }
        foreach ($instance in @($record.instances)) {
            if ($null -eq $instance) { continue }
            $instances++
            $aggregate.instanceCount++
            $text = Get-Text $instance.text
            if (-not [string]::IsNullOrWhiteSpace($text)) { $nonEmpty++; $aggregate.nonEmptyTextInstanceCount++ }
            $direction = $instance.direction_label
            $source = Get-Text $direction.label_source
            $class = Get-Text $direction.class
            if (-not [string]::IsNullOrWhiteSpace($source)) { Add-Count $labelSources $source; Add-Count $aggregate.labelSourceCounts $source }
            Add-Count $directionClasses $class
            Add-Count $aggregate.directionClassCounts $class
            if ($source -match 'unavailable|unknown|not[_ -]?checked' -or $class -match '^(null|unknown)$') { $unavailable++; $aggregate.unavailableDirectionLabelCount++ }
            if (Test-ExplicitNaturalLabel $direction $instance.attributes) {
                $explicitLabels++; $aggregate.explicitNaturalDirectionLabelCount++; $imageHasExplicit = $true
            }
            if ($instance.attributes.text_direction -eq 'vertical' -or $record.dimensions.orientation -eq 'vertical') {
                $vertical++; $aggregate.verticalTrueInstanceCount++
            }
            if (Test-NonZeroAngle $instance.rotation -or Test-NonZeroAngle $instance.angle -or Test-NonZeroAngle $instance.source_rotation) {
                $nonZeroAngleInstances++; $aggregate.nonZeroAngleMetadataInstanceCount++
            }
        }
        if ($imageHasExplicit) { $explicitLabelImages++; $aggregate.explicitNaturalDirectionImageCount++ }
    }
    $aggregate.imageRecordCount += $images
    $aggregate.manifestCount++
    $manifestResults.Add([ordered]@{
        manifest = $file.Name
        path = $file.FullName
        sha256 = Get-Sha256 $file.FullName
        imageRecordCount = $images
        instanceCount = $instances
        nonEmptyTextInstanceCount = $nonEmpty
        explicitNaturalDirectionLabelCount = $explicitLabels
        explicitNaturalDirectionImageCount = $explicitLabelImages
        unavailableDirectionLabelCount = $unavailable
        unknownOrientationImageCount = $unknownOrientation
        verticalTrueInstanceCount = $vertical
        nonZeroAngleMetadataInstanceCount = $nonZeroAngleInstances
        nonZeroAngleMetadataImageCount = $nonZeroAngleImages
        datasets = Convert-ToOrdered $datasets
        labelSourceCounts = Convert-ToOrdered $labelSources
        directionClassCounts = Convert-ToOrdered $directionClasses
        topLevelOrientationCounts = Convert-ToOrdered $orientations
    })
}

$status = if ($aggregate.explicitNaturalDirectionLabelCount -gt 0) { 'natural-direction-labels-present' } else { 'blocked-no-natural-direction-labels' }
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
$repoRevision = '<unavailable>'
try { $repoRevision = (git -C $repoRoot.Path rev-parse HEAD).Trim() } catch { }
$result = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceRevision = $repoRevision
    manifestRoot = $root
    manifestCount = $aggregate.manifestCount
    aggregate = $aggregate
    status = $status
    interpretation = 'Only explicitly sourced direction/angle labels can be used for natural A3 quality. Unknown, unavailable, vertical-only, geometry-inferred and generated transform fields are not upright/inverted ground truth.'
    manifests = $manifestResults.ToArray()
}
$json = $result | ConvertTo-Json -Depth 12
if (-not [string]::IsNullOrWhiteSpace($OutputJson)) {
    $jsonPath = [System.IO.Path]::GetFullPath($OutputJson)
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonPath)
    Set-Content -LiteralPath $jsonPath -Value $json -Encoding utf8
    Write-Host "PADDLEOCR_ORIENTATION_JSON=$jsonPath"
}
if (-not [string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $markdownPath = [System.IO.Path]::GetFullPath($OutputMarkdown)
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownPath)
    $rows = foreach ($item in $manifestResults) {
        '| ' + $item.manifest + ' | ' + $item.imageRecordCount + ' | ' + $item.instanceCount + ' | ' + $item.explicitNaturalDirectionLabelCount + ' | ' + $item.unknownOrientationImageCount + ' | ' + $item.verticalTrueInstanceCount + ' | ' + $item.nonZeroAngleMetadataInstanceCount + ' |'
    }
    $rowsText = $rows -join [Environment]::NewLine
    $markdown = @(
        '# PaddleOCR orientation coverage audit (A3) (2026-10-14)',
        '',
        'This audit scans every local OCR benchmark manifest for explicitly sourced natural direction/angle labels. It is a provenance/coverage audit, not an OCR accuracy result. Unknown, unavailable, vertical-only, geometry-inferred and generated transform fields are not treated as upright/inverted ground truth.',
        '',
        '## Aggregate result',
        '',
        "- Manifests: $($aggregate.manifestCount); image records: $($aggregate.imageRecordCount); instances: $($aggregate.instanceCount).",
        "- Non-empty text instances: $($aggregate.nonEmptyTextInstanceCount).",
        "- Explicit natural direction labels: $($aggregate.explicitNaturalDirectionLabelCount) on $($aggregate.explicitNaturalDirectionImageCount) images.",
        "- Unknown orientation images: $($aggregate.unknownOrientationImageCount); vertical instances: $($aggregate.verticalTrueInstanceCount).",
        "- Non-zero angle metadata: $($aggregate.nonZeroAngleMetadataInstanceCount) instances on $($aggregate.nonZeroAngleMetadataImageCount) images.",
        "- A3 data status: **$status**.",
        '',
        '## Per-manifest counts',
        '',
        '| Manifest | Images | Instances | Explicit natural labels | Unknown orientation images | Vertical instances | Non-zero angle metadata |',
        '|---|---:|---:|---:|---:|---:|---:|',
        $rowsText,
        '',
        '## Boundary',
        '',
        'The current local manifests do not provide an explicitly sourced natural upright/inverted/angle label when the status is blocked. Controlled SROIE rotations, vertical-text candidates and retry contracts remain useful execution/geometry evidence, but they cannot close natural A3 repair rate, rejection rate, CER/WER or coordinate-error gates. A future natural-angle dataset must retain source image identity, crop identity, annotator/source label, angle convention and license/provenance fields.',
        '',
        '## Reproduction',
        '',
        '```powershell',
        '.\eng\models\paddle-ocr\scripts\Audit-PaddleOcrOrientationCoverage.ps1 `',
        "  -ManifestRoot 'F:\OCRBenchmarkTesting\data\annotations\manifests'",
        "  -OutputJson 'eng\models\paddle-ocr\verification\paddleocr-orientation-coverage-a3-20261014.json'",
        "  -OutputMarkdown 'eng\models\paddle-ocr\verification\paddleocr-orientation-coverage-a3-20261014.md'",
        '```'
    ) -join [Environment]::NewLine
    Set-Content -LiteralPath $markdownPath -Value $markdown -Encoding utf8
    Write-Host "PADDLEOCR_ORIENTATION_MARKDOWN=$markdownPath"
}
$json
