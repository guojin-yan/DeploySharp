[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MetadataPath,

    [Parameter(Mandatory = $true)]
    [string]$AnnotationPath,

    [string]$OutputJson,

    [string]$OutputMarkdown
)

$ErrorActionPreference = 'Stop'

function Resolve-InputFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label does not exist: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-GzipJson([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $gzip = [System.IO.Compression.GzipStream]::new(
            $stream,
            [System.IO.Compression.CompressionMode]::Decompress)
        try {
            $reader = [System.IO.StreamReader]::new($gzip)
            try {
                return ($reader.ReadToEnd() | ConvertFrom-Json)
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $gzip.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Add-Count([hashtable]$Table, [string]$Key) {
    if ([string]::IsNullOrWhiteSpace($Key)) {
        $Key = '<blank>'
    }

    if (-not $Table.ContainsKey($Key)) {
        $Table[$Key] = 0
    }

    $Table[$Key]++
}

$metadata = Resolve-InputFile $MetadataPath 'HierText rotation metadata CSV'
$annotations = Resolve-InputFile $AnnotationPath 'HierText annotation gzip'

$metadataRows = @{}
foreach ($row in (Import-Csv -LiteralPath $metadata)) {
    if ([string]::IsNullOrWhiteSpace([string]$row.ImageID)) {
        continue
    }

    $metadataRows[[string]$row.ImageID] = [string]$row.Rotation
}

$annotationDocument = Read-GzipJson $annotations
$annotationIds = @(
    $annotationDocument.annotations |
        ForEach-Object { [string]$_.image_id } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
)

$allRotationCounts = @{}
foreach ($id in $metadataRows.Keys) {
    Add-Count $allRotationCounts $metadataRows[$id]
}

$annotatedRotationCounts = @{}
$annotatedIdsMissingMetadata = New-Object System.Collections.Generic.List[string]
$annotatedNonZeroRotationIds = New-Object System.Collections.Generic.List[string]
foreach ($id in $annotationIds) {
    if (-not $metadataRows.ContainsKey($id)) {
        $annotatedIdsMissingMetadata.Add($id)
        continue
    }

    $rotation = $metadataRows[$id]
    Add-Count $annotatedRotationCounts $rotation
    if ($rotation -in @('90.0', '180.0', '270.0')) {
        $annotatedNonZeroRotationIds.Add($id)
    }
}

$nonZeroMetadataIds = @(
    $metadataRows.Keys |
        Where-Object { $metadataRows[$_] -in @('90.0', '180.0', '270.0') } |
        Sort-Object
)

$repoRevision = ''
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
try {
    $repoRevision = (git -C $repoRoot.Path rev-parse HEAD).Trim()
}
catch {
    $repoRevision = '<unavailable>'
}

$result = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceRevision = $repoRevision
    annotationSource = 'google-research-datasets/HierText validation annotation cache'
    metadataPath = $metadata
    metadataSha256 = Get-Sha256 $metadata
    annotationPath = $annotations
    annotationSha256 = Get-Sha256 $annotations
    metadataImageCount = $metadataRows.Count
    annotationImageCount = $annotationIds.Count
    metadataNonZeroRotationImageCount = $nonZeroMetadataIds.Count
    annotatedIdsMissingMetadataCount = $annotatedIdsMissingMetadata.Count
    annotatedRotationCounts = [ordered]@{}
    allMetadataRotationCounts = [ordered]@{}
    annotatedNonZeroRotationImageCount = $annotatedNonZeroRotationIds.Count
    annotatedNonZeroRotationImageIds = @($annotatedNonZeroRotationIds)
    representativeUnmatchedNonZeroRotationImageIds = @($nonZeroMetadataIds | Select-Object -First 20)
    status = if ($annotatedNonZeroRotationIds.Count -eq 0) {
        'blocked-no-annotated-nonzero-rotation'
    }
    else {
        'annotated-nonzero-rotation-present'
    }
    interpretation = 'Metadata-only nonzero rotation rows cannot be used as OCR quality samples without a matching HierText annotation record.'
}
foreach ($key in ($annotatedRotationCounts.Keys | Sort-Object)) {
    $result.annotatedRotationCounts[$key] = $annotatedRotationCounts[$key]
}
foreach ($key in ($allRotationCounts.Keys | Sort-Object)) {
    $result.allMetadataRotationCounts[$key] = $allRotationCounts[$key]
}

$json = $result | ConvertTo-Json -Depth 10
if (-not [string]::IsNullOrWhiteSpace($OutputJson)) {
    $jsonPath = [System.IO.Path]::GetFullPath($OutputJson)
    $jsonDirectory = Split-Path -Parent $jsonPath
    $null = New-Item -ItemType Directory -Force -Path $jsonDirectory
    Set-Content -LiteralPath $jsonPath -Value $json -Encoding utf8
    Write-Host "HIER_TEXT_ROTATION_JSON=$jsonPath"
}

if (-not [string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $markdownPath = [System.IO.Path]::GetFullPath($OutputMarkdown)
    $markdownDirectory = Split-Path -Parent $markdownPath
    $null = New-Item -ItemType Directory -Force -Path $markdownDirectory
    $metadataRotationText = ($allRotationCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "- $($_.Name): $($_.Value)" }) -join "`n"
    $annotatedRotationText = if ($annotatedRotationCounts.Count -eq 0) {
        '- No annotation IDs map to a rotation value in the metadata CSV.'
    }
    else {
        ($annotatedRotationCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "- $($_.Name): $($_.Value)" }) -join "`n"
    }
    $sampleIds = if ($nonZeroMetadataIds.Count -gt 0) {
        ($nonZeroMetadataIds | Select-Object -First 20 | ForEach-Object { $_ }) -join ', '
    }
    else {
        'none'
    }
    $markdownLines = @(
        '# HierText rotation coverage audit (A3) (2026-10-02)',
        '',
        'This audit compares the local HierText validation annotation cache with its Open Images rotation metadata. It is a provenance and coverage check, not an OCR accuracy result. No image or annotation is copied into the repository or a Release.',
        '',
        '## Inputs',
        '',
        "- Metadata rows: $($metadataRows.Count) ($(Get-Sha256 $metadata))",
        "- HierText annotation image IDs: $($annotationIds.Count) ($(Get-Sha256 $annotations))",
        "- DeploySharp source revision: $repoRevision",
        '',
        '## Findings',
        '',
        "- Metadata rows with nonzero source rotation (`90.0`, `180.0`, `270.0`): $($nonZeroMetadataIds.Count).",
        "- Annotation IDs that map to a nonzero rotation row: $($annotatedNonZeroRotationIds.Count).",
        "- Annotation IDs without a metadata row: $($annotatedIdsMissingMetadata.Count).",
        "- A3 coverage status: **$($result.status)**.",
        '',
        'All metadata rotation distribution:',
        $metadataRotationText,
        '',
        'Rotation distribution among annotation IDs that do map to metadata:',
        $annotatedRotationText,
        '',
        "Representative metadata-only nonzero-rotation IDs (not valid OCR ground-truth samples): $sampleIds",
        '',
        '## Boundary',
        '',
        'The cached annotation file and rotation metadata do not provide a usable intersection for natural nonzero-rotation OCR evaluation. The 476 metadata-only rows cannot be downloaded and scored as HierText samples because they have no matching text annotation in this cache. The existing controlled SROIE transformations and known-quadrilateral rectification therefore remain geometry-contract evidence only; A3 still needs a legally usable natural or explicitly labeled angle dataset before repair rate, CER/WER, rejection rate and coordinate error can be claimed.',
        '',
        'The audit is reproducible with:',
        '',
        '```powershell',
        '.\eng\models\paddle-ocr\scripts\Audit-HierTextRotationCoverage.ps1 `',
        '  -MetadataPath ''F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation-images-with-rotation.csv'' `',
        '  -AnnotationPath ''F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation.jsonl.gz'' `',
        '  -OutputJson ''eng\models\paddle-ocr\verification\hiertext-rotation-coverage-a3-20261002.json'' `',
        '  -OutputMarkdown ''eng\models\paddle-ocr\verification\hiertext-rotation-coverage-a3-20261002.md''',
        '```'
    )
    $markdown = $markdownLines -join [Environment]::NewLine
    Set-Content -LiteralPath $markdownPath -Value $markdown -Encoding utf8
    Write-Host "HIER_TEXT_ROTATION_MARKDOWN=$markdownPath"
}

$json
