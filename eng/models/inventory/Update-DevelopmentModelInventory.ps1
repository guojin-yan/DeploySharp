[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$WarehouseRoot = 'E:\DeploySharp-Models',
    [string]$OutputPath,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $PSScriptRoot 'development-model-inventory.json' }

function Get-StageNumber([string]$modelId, [string]$manifestPath) {
    if ($manifestPath -match '\\yolo\\') { return 16 }
    if ($modelId.StartsWith('rt-detr/', [System.StringComparison]::OrdinalIgnoreCase)) { return 21 }
    if ($manifestPath -match '\\detr\\') { return 18 }
    if ($modelId -match '/(legacy|mobile|server)-cls/') { return 20 }
    if ($manifestPath -match '\\ocr-anomaly-rmbg\\') { return 19 }
    if ($manifestPath -match '\\sam\\') { return 22 }
    if ($manifestPath -match '\\open-vocabulary\\') { return 23 }
    if ($manifestPath -match '\\vision-language\\') { return 24 }
    if ($manifestPath -match '\\generative-vision-language\\') { return 25 }
    if ($manifestPath -match '\\native-multimodal\\') { return 26 }
    if ($manifestPath -match '\\document-understanding\\') { return 27 }
    if ($manifestPath -match '\\audio-speech\\') { return 28 }
    if ($modelId -eq 'llm/gguf/external-blocker') { return 30 }
    if ($modelId -eq 'llm/qwen2.5-0.5b-instruct-q4-k-m/external') { return 31 }
    if ($manifestPath -match '\\llm\\') { return 29 }
    throw "No development stage mapping exists for $modelId ($manifestPath)."
}

function Get-LocalStorage([string]$modelId) {
    switch -Regex ($modelId) {
        '^sam/v1/' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\sam-v1-vit-b" }) }
        '^sam2/' { return @([ordered]@{ kind = 'canonical-partial'; path = "$WarehouseRoot\sam2.1-hiera-tiny" }, [ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\sam' }) }
        '^sam3/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\sam\SAM3' }) }
        '^vision-language/clip-' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\clip-vit-base-patch32" }) }
        '^vision-language/siglip-base' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\siglip-base-patch16-224" }) }
        '^vision-language/siglip2-' { return @([ordered]@{ kind = 'canonical-partial'; path = "$WarehouseRoot\siglip2-base-patch16-224" }) }
        '^grounded-sam/' { return @([ordered]@{ kind = 'canonical-composition-evidence'; path = "$WarehouseRoot\grounded-sam-yoloworldv2-person-bus" }, [ordered]@{ kind = 'canonical-text-encoder-checkpoint'; path = "$WarehouseRoot\openai-clip-vit-b-32-stage23" }, [ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\yolo' }, [ordered]@{ kind = 'canonical-component'; path = "$WarehouseRoot\sam-v1-vit-b" }) }
        '^yolo-world/|^yoloe/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\yolo' }) }
        '^grounding-dino/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model' }) }
        '^yolo/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\yolo' }) }
        '^rt-detr/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\RT-DETR' }) }
        '^rf-detr/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\rf-detr' }) }
        '^deim/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\DEIMv2' }) }
        '^pp-yoloe/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\ppyoloe' }) }
        '^paddleocr/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\ocr' }) }
        '^anomalib/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\anomalib' }) }
        '^bria/' { return @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\RMBG' }) }
        '^generative-vision-language/blip-caption-base' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\blip-caption-base" }) }
        '^generative-vision-language/blip-vqa-base' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\blip-vqa-base" }) }
        '^generative-vision-language/blip2-caption-opt2\.7b' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\blip2-caption-opt2.7b" }) }
        '^generative-vision-language/instructblip-flant5xl' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\instructblip-flant5xl" }) }
        '^native-multimodal/llava-onevision-qwen2-0\.5b' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\llava-onevision-qwen2-0.5b-ov-hf" }) }
        '^native-multimodal/qwen2\.5-vl-3b-instruct' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\qwen2.5-vl-3b-instruct" }) }
        '^native-multimodal/phi-3\.5-vision-instruct' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\phi-3.5-vision-instruct" }) }
        '^document-understanding/donut-base-finetuned-cord-v2' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\donut-base-finetuned-cord-v2" }) }
        '^document-understanding/layoutlmv3-base' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\layoutlmv3-base" }) }
        '^document-understanding/pix2struct-docvqa-base' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\pix2struct-docvqa-base" }) }
        '^audio/wav2vec2-base-960h' { return @([ordered]@{ kind = 'canonical'; path = "$WarehouseRoot\wav2vec2-base-960h" }) }
        '^audio/whisper-tiny\.en' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\whisper-tiny.en" }) }
        '^audio/hubert-base-ls960' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\hubert-base-ls960" }) }
        '^audio/pyannote-speaker-diarization-3\.1' { return @([ordered]@{ kind = 'canonical-source-contract'; path = "$WarehouseRoot\pyannote-speaker-diarization-3.1" }) }
        '^llm/gguf/external-blocker' { return @([ordered]@{ kind = 'caller-owned-environment'; path = 'DEPLOYSHARP_LLAMA_MODEL' }, [ordered]@{ kind = 'warehouse-audit-root'; path = $WarehouseRoot }) }
        '^llm/qwen2\.5-0\.5b-instruct-q4-k-m/' { return @([ordered]@{ kind = 'authorized-local-model'; path = "$WarehouseRoot\qwen2.5-0.5b-instruct-q4_k_m" }, [ordered]@{ kind = 'runtime-evidence'; path = "$WarehouseRoot\qwen2.5-0.5b-instruct-q4_k_m\evidence" }) }
        default { return @() }
    }
}

function Get-AcquisitionArticle([string]$modelId, [string]$manifestPath) {
    if ($manifestPath -match '\\yolo\\') { return 'docs/articles/model-acquisition-yolo.md' }
    if ($manifestPath -match '\\detr\\') { return 'docs/articles/model-acquisition-detr-rtdetr.md' }
    if ($manifestPath -match '\\ocr-anomaly-rmbg\\') { return 'docs/articles/model-acquisition-ocr-anomaly-rmbg.md' }
    if ($manifestPath -match '\\sam\\' -or $manifestPath -match '\\open-vocabulary\\') { return 'docs/articles/model-acquisition-sam-grounded-sam.md' }
    if ($manifestPath -match '\\vision-language\\') { return 'docs/articles/model-acquisition-clip-siglip.md' }
    if ($manifestPath -match '\\generative-vision-language\\') { return 'docs/articles/model-acquisition-blip-family.md' }
    if ($manifestPath -match '\\native-multimodal\\') { return 'docs/articles/model-acquisition-native-multimodal.md' }
    if ($manifestPath -match '\\document-understanding\\') { return 'docs/articles/model-acquisition-document-understanding.md' }
    if ($manifestPath -match '\\audio-speech\\') { return 'docs/articles/model-acquisition-audio-speech.md' }
    if ($manifestPath -match '\\llm\\') { return 'docs/articles/model-acquisition-llm-gguf.md' }
    return 'docs/articles/development-model-inventory.md'
}

function New-FixtureRow([int]$stage, [string]$id, [string]$family, [string]$task, [string[]]$artifacts, [string]$evidence) {
    return [ordered]@{
        inventoryId = $id
        stage = $stage
        family = $family
        task = $task
        kind = 'contract-fixture'
        manifestPath = $null
        source = [ordered]@{ projectUrl = $null; revision = $null; licenseExpression = 'Apache-2.0'; redistributionAllowed = $false }
        artifacts = @($artifacts | ForEach-Object { [ordered]@{ relativePath = $_; size = $null; sha256 = $null } })
        localStorage = @([ordered]@{ kind = 'generated-on-demand'; path = 'eng\test-models' })
        modelFactory = [ordered]@{ state = 'fixture-only'; uploaded = $false; downloadable = $false; blocker = 'Synthetic contract fixtures are not official algorithm models or release assets.' }
        evidence = $evidence
        acquisitionArticle = 'docs/articles/development-model-inventory.md'
    }
}

$rows = [System.Collections.Generic.List[object]]::new()
$rows.Add([ordered]@{
    inventoryId = 'llama/gguf/external-gated'
    stage = 2
    family = 'llama.cpp-compatible-gguf'
    task = 'language-generation-and-embedding'
    kind = 'environment-gated-external-model'
    manifestPath = $null
    source = [ordered]@{ projectUrl = 'https://github.com/SciSharp/LLamaSharp'; revision = '0.27.0'; licenseExpression = 'model-specific'; redistributionAllowed = $false }
    artifacts = @()
    localStorage = @([ordered]@{ kind = 'caller-owned-environment'; path = 'DEPLOYSHARP_LLAMA_MODEL' })
    modelFactory = [ordered]@{ state = 'not-acquired'; uploaded = $false; downloadable = $false; blocker = 'No exact GGUF checkpoint, SHA256, or redistribution grant was supplied during Stage 2.' }
    evidence = 'Managed contract and gated native path only; the real-model test was skipped when the environment variable was absent.'
    acquisitionArticle = 'docs/articles/development-model-inventory.md'
})
$rows.Add((New-FixtureRow 5 'fixtures/visual-fake-classification-detection' 'visual-fake' 'classification,detection' @('in-memory fake tensors') 'Fake backend contract evidence only.'))
$rows.Add((New-FixtureRow 6 'fixtures/onnxruntime-core-suite' 'onnx-contract-fixtures' 'classification,detection,tensor-lifecycle' @('classification.onnx','detection.onnx','five dynamic/type/multi-io fixtures') 'Seven reproducible ONNX graphs; real ORT CPU contract evidence.'))
$rows.Add((New-FixtureRow 7 'fixtures/openvino-core-suite' 'openvino-contract-fixtures' 'classification,tensor-lifecycle' @('classification.xml','classification.bin') 'Real OpenVINO CPU IR contract evidence.'))
$rows.Add((New-FixtureRow 8 'fixtures/opencv-classification-detection' 'opencv-contract-fixtures' 'classification,detection' @('classification.onnx','detection.onnx','rgb/gray/alpha PNG fixtures') 'Reuses Stage 6 graphs through the real OpenCV adapter.'))
$rows.Add((New-FixtureRow 9 'fixtures/semantic-segmentation' 'semantic-segmentation-fixtures' 'semantic-segmentation' @('segmentation-logits.onnx','segmentation-probabilities.onnx','segmentation-label-map.onnx','segmentation.xml','segmentation.bin') 'Real ORT/OpenVINO/OpenCV contract evidence; not AlgorithmVerified.'))
$rows.Add((New-FixtureRow 10 'fixtures/pose' 'pose-fixtures' 'pose-estimation' @('direct-pose.onnx','heatmap-pose.onnx','direct-pose.xml','direct-pose.bin') 'Real ORT/OpenVINO/OpenCV contract evidence; exact hashes remain in the Stage 10 diary.'))
$rows.Add((New-FixtureRow 11 'fixtures/instance-segmentation' 'instance-segmentation-fixtures' 'instance-segmentation' @('direct-instance-segmentation.onnx','prototype-instance-segmentation.onnx','direct-instance-segmentation.xml','direct-instance-segmentation.bin') 'Real ORT/OpenVINO/OpenCV contract evidence; not an official algorithm export.'))
$rows.Add((New-FixtureRow 12 'fixtures/oriented-detection' 'oriented-detection-fixtures' 'oriented-object-detection' @('direct-obb.onnx','corner-obb.onnx','direct-obb.xml','direct-obb.bin') 'Real ORT/OpenVINO/OpenCV contract evidence; not an official algorithm export.'))
$rows.Add((New-FixtureRow 13 'fixtures/ocr-detection-recognition' 'ocr-fixtures' 'ocr-detection-and-recognition' @('text-detection.onnx','text-recognition-ctc.onnx','text-detection.xml','text-detection.bin','text-recognition-ctc.xml','text-recognition-ctc.bin','charset.txt') 'Real two-stage ORT/OpenVINO/OpenCV contract evidence.'))
$rows.Add((New-FixtureRow 14 'fixtures/anomaly' 'anomaly-fixtures' 'anomaly-detection-and-segmentation' @('anomaly.onnx','anomaly.xml','anomaly.bin') 'Real ORT/OpenVINO/OpenCV contract evidence.'))
$rows.Add((New-FixtureRow 15 'fixtures/ocr-orientation' 'ocr-orientation-fixtures' 'ocr-orientation-classification' @('ocr-orientation.onnx','ocr-orientation.xml','ocr-orientation.bin') 'Real four-direction ORT/OpenVINO/OpenCV contract evidence.'))
$rows.Add([ordered]@{
    inventoryId = 'ultralytics/yolo-multitask/local-matrix'
    stage = 17
    family = 'yolo-multitask'
    task = 'classification,detection,segmentation,pose,obb'
    kind = 'legacy-read-only-local-matrix'
    manifestPath = $null
    source = [ordered]@{ projectUrl = 'https://github.com/ultralytics/ultralytics'; revision = 'stage17-audit'; licenseExpression = 'artifact-specific'; redistributionAllowed = $false }
    artifacts = @()
    localStorage = @([ordered]@{ kind = 'legacy-read-only'; path = 'E:\Model\yolo' })
    modelFactory = [ordered]@{ state = 'external-unpublished'; uploaded = $false; downloadable = $false; blocker = 'The local multitask rows lack a complete independent checkpoint/export/license redistribution chain.' }
    evidence = 'Stage 17 ORT/OpenVINO/OpenCV local execution matrix; exact files remain caller-owned.'
    acquisitionArticle = 'docs/articles/model-acquisition-yolo.md'
})

$manifestRoot = Join-Path $RepositoryRoot 'eng\models'
foreach ($file in Get-ChildItem -LiteralPath $manifestRoot -Recurse -Filter '*.modelpack.json' | Sort-Object FullName) {
    $manifest = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    $relativeManifest = $file.FullName.Substring($RepositoryRoot.Length + 1).Replace('\', '/')
    $artifactFiles = @()
    foreach ($artifact in $manifest.artifacts) {
        foreach ($artifactFile in $artifact.files) {
            $artifactFiles += [ordered]@{
                artifactId = $artifact.artifactId
                format = $artifact.format
                relativePath = $artifactFile.relativePath
                role = $artifactFile.role
                size = $artifactFile.size
                sha256 = $artifactFile.sha256
            }
        }
    }
    $redistributionAllowed = [bool]$manifest.source.redistributionAllowed
    $isExactGgufBlocker = $manifest.modelId -eq 'llm/gguf/external-blocker'
    $blocker = if ($isExactGgufBlocker) {
        [string]$manifest.artifacts[0].extensions.'deploysharp.blocker'
    } elseif ($redistributionAllowed) {
        'No immutable DeploySharp model-asset URI has been authorized or published.'
    } else {
        'The audited manifest explicitly sets redistributionAllowed:false; publication requires a separate artifact-license grant and immutable asset review.'
    }
    $rows.Add([ordered]@{
        inventoryId = $manifest.modelId
        stage = Get-StageNumber $manifest.modelId $file.FullName
        family = $manifest.family
        task = $manifest.task
        kind = if ($manifest.modelId -match 'blocker') { 'external-blocker' } else { 'audited-external-model' }
        manifestPath = $relativeManifest
        source = [ordered]@{
            sourceUrl = $manifest.source.sourceUrl
            projectUrl = $manifest.source.projectUrl
            revision = $manifest.source.revision
            licenseExpression = $manifest.source.licenseExpression
            redistributionAllowed = $redistributionAllowed
        }
        artifacts = $artifactFiles
        localStorage = @(Get-LocalStorage $manifest.modelId)
        modelFactory = [ordered]@{
            state = if ($isExactGgufBlocker) { 'external-blocked' } else { 'external-metadata-ready' }
            uploaded = $false
            downloadable = $false
            blocker = $blocker
        }
        evidence = if ($isExactGgufBlocker) { 'Stage 30 found no exact GGUF; the ModelPack retains hash-protected blocker audit evidence only.' } else { 'The ModelPack manifest is the exact size/SHA/provenance/backend-evidence source of truth.' }
        acquisitionArticle = Get-AcquisitionArticle $manifest.modelId $file.FullName
    })
}

$orderedRows = @($rows | Sort-Object @{ Expression = { [int]$_['stage'] } }, @{ Expression = { [string]$_['inventoryId'] } })
$document = [ordered]@{
    schemaVersion = '1.0'
    generatedAt = '2026-08-11T00:00:00Z'
    warehouseRootDefault = $WarehouseRoot
    scope = 'All model, checkpoint, converted graph, blocker, and contract-fixture families evidenced during DeploySharp V2 Stages 1-35. Stage 1 had no model execution; Stage 31 admitted one authorized local-only Qwen GGUF with real LLamaSharp CPU evidence; Stages 32-35 revalidated its immutable model, sidecar, evidence, and package boundary without adding an inventory row.'
    policy = [ordered]@{
        userAssetRootsRemainReadOnly = @('E:\Model','E:\Data')
        officialCatalogAdmission = 'empty'
        releaseAssetsWritten = $false
        actionsInvoked = $false
        uploadRule = 'Only artifacts with an independently audited redistribution grant and an immutable authorized URI may become downloadable ModelFactory assets.'
        requestedDestination = 'DeploySharp ModelFactory immutable release assets and content-addressed cache'
        publicationState = 'metadata-indexed-binaries-blocked-by-redistribution-and-no-release-authority'
    }
    counts = [ordered]@{
        entries = $orderedRows.Count
        structuredManifests = @($orderedRows | Where-Object { $_.manifestPath }).Count
        contractFixtures = @($orderedRows | Where-Object { $_.kind -eq 'contract-fixture' }).Count
        downloadable = @($orderedRows | Where-Object { $_.modelFactory.downloadable }).Count
        uploaded = @($orderedRows | Where-Object { $_.modelFactory.uploaded }).Count
    }
    entries = $orderedRows
}

$json = ($document | ConvertTo-Json -Depth 12) + [Environment]::NewLine
if ($Check) {
    if (-not (Test-Path -LiteralPath $OutputPath)) { throw "Inventory output is missing: $OutputPath" }
    $current = Get-Content -LiteralPath $OutputPath -Raw
    if ($current -cne $json) { throw 'Development model inventory is stale. Run Update-DevelopmentModelInventory.ps1.' }
    Write-Output "DEPLOYSHARP_DEVELOPMENT_MODEL_INVENTORY_OK entries=$($orderedRows.Count)"
    return
}

$parent = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $parent -Force | Out-Null
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output "DEPLOYSHARP_DEVELOPMENT_MODEL_INVENTORY_WRITTEN entries=$($orderedRows.Count) path=$OutputPath"
