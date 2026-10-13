[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ReleaseCommit,
    [string]$CatalogPath = 'src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json',
    [string]$Tag = 'models-visual.1',
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
$officialPaddleOcrAssets = @{
    'mobile-det' = @{ Repository = 'PP-OCRv5_mobile_det_onnx'; Revision = 'e6f4fa85f00e168c862bc462aebca69eef9b3d3d'; Sha256 = 'a431985659dc921974177a95adcfbb90fd9e51989a5e04d70d0b75f597b6e61d'; Size = 4826518L }
    'mobile-rec' = @{ Repository = 'PP-OCRv5_mobile_rec_onnx'; Revision = 'ed152b8b495f84de93cda5709d768548a9127622'; Sha256 = 'da72dc72ca4dc220df0dfde68c1dedc31c58d3e76a25871122e5056227d50092'; Size = 16534782L }
    'mobile-cls' = @{ Repository = 'PP-LCNet_x0_25_textline_ori_onnx'; Revision = 'aea1f18d97338aaf84b463f90192f2820524a00a'; Sha256 = '94a6a0a0425f2b5f08b5df72086f2d72abe40f1d22f6d12d2cd83674f11f2ff3'; Size = 1019454L }
    'server-det' = @{ Repository = 'PP-OCRv5_server_det_onnx'; Revision = 'dcf248c4dcc064c6d030db241255f9e8fbc6733a'; Sha256 = '10803475a591f7dc623e24670fb5752ec94d39a1f8cf069aac1b6f0ce19cfc85'; Size = 88116791L }
    'server-rec' = @{ Repository = 'PP-OCRv5_server_rec_onnx'; Revision = 'b70df217f4fd99d14f970bad092cebe7d74cc4d1'; Sha256 = 'd9dc333c9c7b042c6dffb8e33d72b6f65c9c1d463d0a3c2f78174fea55e94752'; Size = 84503027L }
    'server-cls' = @{ Repository = 'PP-LCNet_x1_0_textline_ori_onnx'; Revision = '7fdcf3cf7061163eda7183b224aa334bd33068f7'; Sha256 = '38aa97cd4be591e0ad304e659f07ba30d946f27a63315433f6659c69c8778345'; Size = 6777816L }
}
$officialDictionaryUrl = 'https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/2661c7c0ef5c613e8f93c6e93b2e052399f0f854/ppocr/utils/dict/ppocrv5_dict.txt'
$officialLicenseUrl = 'https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/2661c7c0ef5c613e8f93c6e93b2e052399f0f854/LICENSE'

$catalog = Get-Content -Raw -LiteralPath $catalogFile | ConvertFrom-Json
$manifests = @(Get-ChildItem -LiteralPath $manifestDirectory -Filter '*.modelpack.json' -File |
    Where-Object { $_.Name -match '^(mobile|server)-(cls|det|rec)\.modelpack\.json$' } |
    Sort-Object Name)
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
    $key = ([string]$manifest.modelId).Split('/')[-1]
    if (-not $officialPaddleOcrAssets.ContainsKey($key)) { throw "Missing official ONNX source mapping for $key." }
    $official = $officialPaddleOcrAssets[$key]
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
        $assetUrl = if ($role -eq 'model') { "https://huggingface.co/PaddlePaddle/$($official.Repository)/resolve/$($official.Revision)/inference.onnx" } elseif ($role -eq 'labels') { $officialDictionaryUrl } elseif ($role -eq 'license') { $officialLicenseUrl } else { $releaseBase + $assetName }
        $assetSize = if ($role -eq 'model') { [long]$official.Size } elseif ($role -eq 'labels') { $dictionarySize } elseif ($role -eq 'license') { $licenseSize } else { [long]$file.size }
        $assetSha256 = if ($role -eq 'model') { [string]$official.Sha256 } elseif ($role -eq 'labels') { $dictionarySha256 } elseif ($role -eq 'license') { $licenseSha256 } else { ([string]$file.sha256).ToLowerInvariant() }
        $assets.Add([ordered]@{
            assetId = $assetId
            kind = Get-AssetKind $role
            releaseTag = $Tag
            downloadUrl = $assetUrl
            relativePath = [string]$file.relativePath
            size = $assetSize
            sha256 = $assetSha256
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
        description = 'Public alpha-preview PP-OCRv5 ONNX bundle using revision-pinned official PaddlePaddle Hugging Face models, PaddleOCR dictionary/license files, and SHA-256 checksums.'
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
        documentationPath = 'articles/visual-paddle-ocr3.md'
    })
}

$knownIds = @{}
foreach ($entry in $generated) { $knownIds[[string]$entry.modelId] = $true }
$retained = @($catalog.entries | Where-Object { -not $knownIds.ContainsKey([string]$_.modelId) })
$entries = @(@($retained) + @($generated) | Sort-Object { [string]$_.modelId })
$document = [ordered]@{
    schemaVersion = [string]$catalog.schemaVersion
    generatedAt = '2026-09-03T00:00:00Z'
    catalogRevision = $Tag
    sourceRepository = [string]$catalog.sourceRepository
    entries = $entries
}
$content = ($document | ConvertTo-Json -Depth 40) + [Environment]::NewLine

if ($Check) {
    if ((Get-Content -Raw -LiteralPath $catalogFile) -ne $content) { throw "Generated PaddleOCR catalog is stale: $catalogFile" }
    Write-Output "DEPLOYSHARP_PADDLE_OCR_CATALOG_OK tag=$Tag models=$($generated.Count) entries=$($document.entries.Count)"
} else {
    [IO.File]::WriteAllText($catalogFile, $content, [Text.UTF8Encoding]::new($false))
    Write-Output "DEPLOYSHARP_PADDLE_OCR_CATALOG_WRITTEN tag=$Tag models=$($generated.Count) entries=$($document.entries.Count)"
}
