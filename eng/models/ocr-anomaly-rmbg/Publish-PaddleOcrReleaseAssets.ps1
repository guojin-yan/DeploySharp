[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\ocr\ppocrv5',
    [string]$ServerClassificationModel = 'E:\Model\ocr\ppocrv5-1\PP-OCRv5_server_cls_onnx.onnx',
    [string]$OutputRoot = 'artifacts',
    [string]$Tag = 'models-20260903.visual.1',
    [string]$Repository = 'guojin-yan/DeploySharp',
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$evidencePath = Join-Path $PSScriptRoot 'paddleocr-release-admission.json'
$manifestRoot = Join-Path $PSScriptRoot 'manifests'
$releaseManifestRoot = Join-Path $PSScriptRoot 'releases'
$stageRoot = Join-Path $repositoryRoot (Join-Path $OutputRoot 'model-release-ppocrv5-20260818')
$evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
$releaseGeneratedAt = '2026-08-18T00:00:00Z'

$models = @(
    [ordered]@{ key = 'mobile-det'; sourceManifest = 'ppocrv5-mobile-det.modelpack.json'; sourceName = 'PP-OCRv5_mobile_det_onnx.onnx'; assetName = 'ppocrv5-mobile-det.model.onnx' },
    [ordered]@{ key = 'server-det'; sourceManifest = 'ppocrv5-server-det.modelpack.json'; sourceName = 'PP-OCRv5_server_det_onnx.onnx'; assetName = 'ppocrv5-server-det.model.onnx' },
    [ordered]@{ key = 'mobile-rec'; sourceManifest = 'ppocrv5-mobile-rec.modelpack.json'; sourceName = 'PP-OCRv5_mobile_rec_onnx.onnx'; assetName = 'ppocrv5-mobile-rec.model.onnx' },
    [ordered]@{ key = 'server-rec'; sourceManifest = 'ppocrv5-server-rec.modelpack.json'; sourceName = 'PP-OCRv5_server_rec_onnx.onnx'; assetName = 'ppocrv5-server-rec.model.onnx' },
    [ordered]@{ key = 'mobile-cls'; sourceManifest = 'ppocrv5-mobile-cls.modelpack.json'; sourceName = 'PP-OCRv5_mobile_cls_onnx.onnx'; assetName = 'ppocrv5-mobile-cls.model.onnx' },
    [ordered]@{ key = 'server-cls'; sourceManifest = 'ppocrv5-server-cls.modelpack.json'; sourceName = 'PP-OCRv5_server_cls_onnx.onnx'; sourcePath = $ServerClassificationModel; assetName = 'ppocrv5-server-cls.model.onnx' }
)

function Get-EvidenceArtifact {
    param([string]$Key)
    $item = @($evidence.artifacts | Where-Object { [string]$_.modelId -like "*/$Key/external" })
    if ($item.Count -ne 1) { throw "Expected one evidence artifact for $Key; found $($item.Count)." }
    return $item[0]
}

function Get-SourceResult {
    param([string]$Key)
    $item = @($evidence.exportReproduction.results | Where-Object { [string]$_.modelId -like "*/$Key/external" })
    if ($item.Count -ne 1) { throw "Expected one export result for $Key; found $($item.Count)." }
    return $item[0]
}

function Get-FileRecord {
    param([string]$Path, [string]$ExpectedHash, [long]$ExpectedSize)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing source file: $Path" }
    $item = Get-Item -LiteralPath $Path
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($item.Length -ne $ExpectedSize -or $hash -ne $ExpectedHash.ToLowerInvariant()) {
        throw "Source integrity mismatch: $Path expected=$ExpectedSize/$ExpectedHash actual=$($item.Length)/$hash"
    }
    return $item
}

function Copy-CheckedFile {
    param([string]$SourcePath, [string]$DestinationPath, [string]$ExpectedHash, [long]$ExpectedSize)
    [IO.Directory]::CreateDirectory((Split-Path -Parent $DestinationPath)) | Out-Null
    Get-FileRecord $SourcePath $ExpectedHash $ExpectedSize | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    Get-FileRecord $DestinationPath $ExpectedHash $ExpectedSize | Out-Null
}

function New-ReleaseManifest {
    param([object]$Model, [object]$External, [object]$EvidenceArtifact, [string]$LicenseRelativePath)
    $source = [ordered]@{
        sourceUrl = [string]$External.source.sourceUrl
        projectUrl = [string]$External.source.projectUrl
        revision = [string]$External.source.revision
        author = [string]$External.source.author
        licenseExpression = 'Apache-2.0'
        licenseFile = $LicenseRelativePath
        redistributionAllowed = $true
    }
    $artifactFiles = [System.Collections.Generic.List[object]]::new()
    $artifactFiles.Add([ordered]@{
        relativePath = 'bundle/model.onnx'
        sha256 = [string]$EvidenceArtifact.sha256
        size = [long]$EvidenceArtifact.size
        mediaType = 'application/onnx'
        role = 'model'
    })
    if ([string]$Model.key -in @('mobile-rec', 'server-rec')) {
        $dictionary = $evidence.sharedArtifacts.dictionary
        $artifactFiles.Add([ordered]@{
            relativePath = 'bundle/ppocrv5_dict.txt'
            sha256 = [string]$dictionary.sha256
            size = [long]$dictionary.size
            mediaType = 'text/plain'
            role = 'labels'
        })
    }
    $artifactFiles.Add([ordered]@{
        relativePath = $LicenseRelativePath
        sha256 = '3840c5c0c61c294264d2dd77b8777be6ddd90121ef4e0e64abcd22edea581d6e'
        size = 11376
        mediaType = 'text/plain'
        role = 'license'
    })
    $artifact = [ordered]@{
        artifactId = 'onnx.fp32'
        format = 'onnx'
        locationKind = 'directory'
        entrypoint = 'bundle'
        compatibleBackends = @('onnxruntime', 'openvino')
        files = @($artifactFiles)
        precision = 'fp32'
        quantization = 'none'
        opset = [int]$External.artifacts[0].opset
        portable = $true
        extensions = [ordered]@{
            'deploysharp.validation-status' = [string]$External.artifacts[0].extensions.'deploysharp.validation-status'
            'deploysharp.artifact-provenance' = 'byte-identical-local-export'
            'deploysharp.source-inference-archive-sha256' = [string]$External.artifacts[0].extensions.'deploysharp.source-inference-archive-sha256'
            'deploysharp.export-reproducibility' = [string]$External.artifacts[0].extensions.'deploysharp.export-reproducibility'
            'deploysharp.release-admission' = 'alpha-preview-redistributable-source-recorded'
        }
    }
    $modelId = ([string]$External.modelId).Replace('/external', '')
    return [ordered]@{
        schemaVersion = '2.0'
        modelId = $modelId
        name = ([string]$External.name -replace ' external candidate$', '')
        family = [string]$External.family
        task = [string]$External.task
        modelVersion = ([string]$External.modelVersion -replace '-local-export$', '-release')
        exporter = $External.exporter
        source = $source
        generatedAt = $releaseGeneratedAt
        profileId = ([string]$External.profileId -replace '/external', '')
        inputs = @($External.inputs)
        outputs = @($External.outputs)
        artifacts = @($artifact)
        extensions = [ordered]@{
            'deploysharp.publication-status' = 'alpha-preview'
            'deploysharp.downloadable' = 'true'
            'deploysharp.release-tag' = $Tag
            'deploysharp.release-repository' = $Repository
        }
    }
}

if (-not $Check) {
    [IO.Directory]::CreateDirectory($stageRoot) | Out-Null
    [IO.Directory]::CreateDirectory($releaseManifestRoot) | Out-Null
}

$licensePath = Join-Path $stageRoot 'paddleocr.LICENSE.txt'
$licenseHash = '3840c5c0c61c294264d2dd77b8777be6ddd90121ef4e0e64abcd22edea581d6e'
$licenseSize = 11376
if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    if ($Check) { throw "Missing staged license: $licensePath" }
    Invoke-WebRequest -NoProxy -UseBasicParsing -Uri 'https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/2661c7c0ef5c613e8f93c6e93b2e052399f0f854/LICENSE' -OutFile $licensePath
}
Get-FileRecord $licensePath $licenseHash $licenseSize | Out-Null

$dictionary = $evidence.sharedArtifacts.dictionary
$dictionarySource = Join-Path $ModelRoot ([string]$dictionary.localFileName)
$dictionaryTarget = Join-Path $stageRoot ([string]$dictionary.localFileName)
Copy-CheckedFile $dictionarySource $dictionaryTarget ([string]$dictionary.sha256) ([long]$dictionary.size)

$assetRecords = [System.Collections.Generic.List[object]]::new()
$assetRecords.Add([ordered]@{ name = 'paddleocr.LICENSE.txt'; size = $licenseSize; sha256 = $licenseHash })
$assetRecords.Add([ordered]@{ name = [string]$dictionary.localFileName; size = [long]$dictionary.size; sha256 = [string]$dictionary.sha256 })

foreach ($model in $models) {
    $evidenceArtifact = Get-EvidenceArtifact $model.key
    $sourceResult = Get-SourceResult $model.key
    if ([string]$sourceResult.generatedOnnxSha256 -ne [string]$evidenceArtifact.sha256) { throw "Evidence hash mismatch for $($model.key)." }
    $sourceManifestPath = Join-Path $manifestRoot $model.sourceManifest
    $external = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json
    $sourcePath = if ($model.Contains('sourcePath')) { [string]$model.sourcePath } else { Join-Path $ModelRoot $model.sourceName }
    $targetPath = Join-Path $stageRoot $model.assetName
    Copy-CheckedFile $sourcePath $targetPath ([string]$evidenceArtifact.sha256) ([long]$evidenceArtifact.size)
    $assetRecords.Add([ordered]@{ name = $model.assetName; size = [long]$evidenceArtifact.size; sha256 = [string]$evidenceArtifact.sha256 })

    $releaseManifest = New-ReleaseManifest $model $external $evidenceArtifact 'bundle/source/licenses/paddleocr.LICENSE.txt'
    $releaseManifestPath = Join-Path $releaseManifestRoot ($model.key + '.modelpack.json')
    $releaseJson = ($releaseManifest | ConvertTo-Json -Depth 30) + [Environment]::NewLine
    if ($Check) {
        if (-not (Test-Path -LiteralPath $releaseManifestPath -PathType Leaf) -or (Get-Content -Raw -LiteralPath $releaseManifestPath) -ne $releaseJson) {
            throw "Release manifest is stale: $releaseManifestPath"
        }
    } else {
        [IO.File]::WriteAllText($releaseManifestPath, $releaseJson, [Text.UTF8Encoding]::new($false))
    }
    $manifestItem = Get-Item -LiteralPath $releaseManifestPath
    $manifestTarget = Join-Path $stageRoot $manifestItem.Name
    if ($Check) {
        if (-not (Test-Path -LiteralPath $manifestTarget -PathType Leaf)) { throw "Missing staged release manifest: $manifestTarget" }
    } else {
        Copy-Item -LiteralPath $releaseManifestPath -Destination $manifestTarget -Force
    }
    $stagedManifest = Get-Item -LiteralPath $manifestTarget
    $stagedManifestHash = (Get-FileHash -LiteralPath $manifestTarget -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($stagedManifest.Length -ne $manifestItem.Length -or $stagedManifestHash -ne (Get-FileHash -LiteralPath $releaseManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "Staged release manifest integrity mismatch: $manifestTarget" }
    $assetRecords.Add([ordered]@{ name = $manifestItem.Name; size = [long]$stagedManifest.Length; sha256 = $stagedManifestHash })
}

$checksumPath = Join-Path $stageRoot 'SHA256SUMS'
$checksumLines = @($assetRecords | Sort-Object name | ForEach-Object { $_.sha256 + '  ' + $_.name })
$checksumContent = (($checksumLines -join "`n") + "`n")
if ($Check) {
    if ((Get-Content -Raw -LiteralPath $checksumPath) -ne $checksumContent) { throw "SHA256SUMS is stale: $checksumPath" }
} else {
    [IO.File]::WriteAllText($checksumPath, $checksumContent, [Text.UTF8Encoding]::new($false))
}
$checksumItem = Get-Item -LiteralPath $checksumPath
$assetRecords.Add([ordered]@{ name = $checksumItem.Name; size = [long]$checksumItem.Length; sha256 = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256).Hash.ToLowerInvariant() })

$assetPlan = [ordered]@{ schemaVersion = '1.0'; collection = 'ppocrv5'; tag = $Tag; repository = $Repository; assets = @($assetRecords) }
$assetPlanPath = Join-Path $stageRoot 'release-assets.json'
$assetPlanJson = ($assetPlan | ConvertTo-Json -Depth 10) + [Environment]::NewLine
if ($Check) {
    if ((Get-Content -Raw -LiteralPath $assetPlanPath) -ne $assetPlanJson) { throw "Release asset plan is stale: $assetPlanPath" }
} else {
    [IO.File]::WriteAllText($assetPlanPath, $assetPlanJson, [Text.UTF8Encoding]::new($false))
}

Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_ASSETS_OK tag=$Tag models=$($models.Count) dictionary=true manifests=$($models.Count) assets=$($assetRecords.Count) stage=$stageRoot"
