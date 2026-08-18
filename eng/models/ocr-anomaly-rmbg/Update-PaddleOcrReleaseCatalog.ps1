[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ReleaseCommit,
    [string]$CatalogPath = 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json',
    [string]$Tag = 'models-20260818.ppocrv5.1',
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$catalogFile = Join-Path $repositoryRoot $CatalogPath
$manifestDirectory = Join-Path $PSScriptRoot 'releases'
$releaseBase = 'https://github.com/guojin-yan/DeploySharp/releases/download/' + $Tag + '/'
$licenseSize = 11376L
$licenseSha256 = '3840c5c0c61c294264d2dd77b8777be6ddd90121ef4e0e64abcd22edea581d6e'
$licenseAssetName = 'paddleocr.LICENSE.txt'
$dictionaryAssetName = 'ppocrv5_dict.txt'
$dictionarySha256 = 'd1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b'
$dictionarySize = 74012L

$catalog = Get-Content -Raw -LiteralPath $catalogFile | ConvertFrom-Json
$manifests = @(Get-ChildItem -LiteralPath $manifestDirectory -Filter '*.modelpack.json' -File | Sort-Object Name)
if ($manifests.Count -ne 6) { throw "Expected six PP-OCRv5 release manifests, found $($manifests.Count)." }

function Get-AssetKind([string]$role) {
    if ($role -eq 'license') { return 'license' }
    if ($role -eq 'model') { return 'model' }
    return 'other'
}

function Get-ReleaseModelName([string]$modelId) {
    $suffix = $modelId.Substring($modelId.LastIndexOf('/') + 1).Replace('/', '-')
    return 'ppocrv5-' + $suffix + '.model.onnx'
}

$generated = [System.Collections.Generic.List[object]]::new()
foreach ($manifestFile in $manifests) {
    $manifest = Get-Content -Raw -LiteralPath $manifestFile.FullName | ConvertFrom-Json
    if ([string]$manifest.extensions.'deploysharp.release-tag' -ne $Tag) { throw "Manifest tag mismatch: $($manifest.modelId)" }
    $artifact = @($manifest.artifacts)[0]
    foreach ($file in @($artifact.files) | Where-Object { $_.role -in @('labels', 'license') }) {
        if ($file.role -eq 'labels' -and ([string]$file.sha256).ToLowerInvariant() -ne $dictionarySha256 -or $file.role -eq 'labels' -and [long]$file.size -ne $dictionarySize) { throw "Dictionary metadata mismatch: $($manifest.modelId)" }
        if ($file.role -eq 'license' -and ([string]$file.sha256).ToLowerInvariant() -ne $licenseSha256 -or $file.role -eq 'license' -and [long]$file.size -ne $licenseSize) { throw "License metadata mismatch: $($manifest.modelId)" }
    }
    $prefix = ([string]$manifest.modelId).Replace('/', '-')
    $manifestAssetId = $prefix + '-modelpack'
    $assets = [System.Collections.Generic.List[object]]::new()
    $manifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestFile.FullName).Hash.ToLowerInvariant()
    $assets.Add([ordered]@{
        assetId = $manifestAssetId
        kind = 'manifest'
        releaseTag = $Tag
        downloadUrl = $releaseBase + $manifestFile.Name
        relativePath = $manifestFile.Name
        size = [long]$manifestFile.Length
        sha256 = $manifestSha256
        mediaType = 'application/json'
        licenseExpression = 'Apache-2.0'
    })

    foreach ($file in @($artifact.files)) {
        $role = [string]$file.role
        $assetName = if ($role -eq 'model') { Get-ReleaseModelName ([string]$manifest.modelId) } elseif ($role -eq 'labels') { $dictionaryAssetName } elseif ($role -eq 'license') { $licenseAssetName } else { [IO.Path]::GetFileName([string]$file.relativePath) }
        $assetId = if ($role -eq 'model') { $prefix + '-model' } elseif ($role -eq 'labels') { $prefix + '-dictionary' } elseif ($role -eq 'license') { $prefix + '-license' } else { $prefix + '-other' }
        $assets.Add([ordered]@{
            assetId = $assetId
            kind = Get-AssetKind $role
            releaseTag = $Tag
            downloadUrl = $releaseBase + $assetName
            relativePath = [string]$file.relativePath
            size = [long]$file.size
            sha256 = ([string]$file.sha256).ToLowerInvariant()
            mediaType = [string]$file.mediaType
            licenseExpression = 'Apache-2.0'
        })
    }

    $required = @($assets | Where-Object { $_.kind -in @('model', 'other') -and $_.assetId -ne ($prefix + '-modelpack') } | ForEach-Object { [string]$_.assetId })
    $generated.Add([ordered]@{
        modelId = [string]$manifest.modelId
        name = [string]$manifest.name
        family = [string]$manifest.family
        task = [string]$manifest.task
        modelVersion = [string]$manifest.modelVersion
        status = 'preview'
        description = 'Public alpha-preview PP-OCRv5 ONNX bundle with immutable GitHub Release assets and SHA-256 checksums.'
        source = $manifest.source
        release = [ordered]@{ owner = 'guojin-yan'; repository = 'DeploySharp'; tag = $Tag; commit = $ReleaseCommit }
        artifacts = @([ordered]@{
            artifactId = [string]$artifact.artifactId
            format = [string]$artifact.format
            compatibleBackends = @($artifact.compatibleBackends)
            precision = [string]$artifact.precision
            quantization = [string]$artifact.quantization
            portable = [bool]$artifact.portable
            manifestAssetId = $manifestAssetId
            bundleRole = if ([string]$manifest.family -eq 'paddle-ocr-det') { 'text-detector' } elseif ([string]$manifest.family -eq 'paddle-ocr-rec') { 'text-recognizer' } else { 'text-orientation-classifier' }
            bundleVersion = [string]$manifest.modelVersion
            capabilities = @([string]$manifest.task)
            requiredAssetIds = $required
            assets = $assets
            conversion = [ordered]@{
                exporter = [string]$manifest.exporter.name
                exporterVersion = [string]$manifest.exporter.version
                sourceRevision = [string]$manifest.exporter.sourceRevision
                notes = 'Source, conversion, and runtime validation details are recorded in the released ModelPack.'
            }
        })
        testInputs = @()
        documentationPath = 'articles/visual-ocr-anomaly-rmbg.md'
    })
}

$knownIds = @{}
foreach ($entry in $generated) { $knownIds[[string]$entry.modelId] = $true }
$retained = @($catalog.entries | Where-Object { -not $knownIds.ContainsKey([string]$_.modelId) })
$document = [ordered]@{
    schemaVersion = [string]$catalog.schemaVersion
    generatedAt = '2026-08-18T00:00:00Z'
    catalogRevision = $Tag
    sourceRepository = [string]$catalog.sourceRepository
    entries = @($retained) + @($generated)
}
$content = ($document | ConvertTo-Json -Depth 40) + [Environment]::NewLine

if ($Check) {
    if ((Get-Content -Raw -LiteralPath $catalogFile) -ne $content) { throw "Generated PaddleOCR catalog is stale: $catalogFile" }
    Write-Output "DEPLOYSHARP_PADDLE_OCR_CATALOG_OK tag=$Tag models=$($generated.Count) entries=$($document.entries.Count)"
} else {
    [IO.File]::WriteAllText($catalogFile, $content, [Text.UTF8Encoding]::new($false))
    Write-Output "DEPLOYSHARP_PADDLE_OCR_CATALOG_WRITTEN tag=$Tag models=$($generated.Count) entries=$($document.entries.Count)"
}
