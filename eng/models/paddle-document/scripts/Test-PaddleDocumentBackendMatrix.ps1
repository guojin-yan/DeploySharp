[CmdletBinding()]
param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot '..\verification\paddle-document-backend-matrix-20260929.json'),
    [string]$OutputJsonPath = (Join-Path $PSScriptRoot '..\verification\paddle-document-backend-matrix-current-20261017.json'),
    [string]$OutputMarkdownPath = (Join-Path $PSScriptRoot '..\verification\paddle-document-backend-matrix-current-20261017.md')
)

$ErrorActionPreference = 'Stop'

function Resolve-RepositoryRoot {
    $candidate = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    if (-not (Test-Path -LiteralPath (Join-Path $candidate '.git'))) {
        throw "Unable to resolve the DeploySharp repository root from $PSScriptRoot."
    }
    return $candidate
}

function Get-StatusCounts {
    param(
        [Parameter(Mandatory)]
        [object[]]$Rows,
        [Parameter(Mandatory)]
        [string]$Backend
    )

    $counts = [ordered]@{ pass = 0; unsupported = 0; unverified = 0 }
    foreach ($row in $Rows) {
        $status = [string]$row.$Backend
        if (-not $counts.Contains($status)) {
            throw "Unexpected status '$status' for backend '$Backend' and model '$($row.modelId)'."
        }
        $counts[$status]++
    }
    return [pscustomobject]$counts
}

function Get-MeasuredSampleCounts {
    param([object]$Node)

    if ($null -eq $Node) { return }
    if ($Node -is [array]) {
        foreach ($item in $Node) { Get-MeasuredSampleCounts -Node $item }
        return
    }
    if ($Node -isnot [pscustomobject]) { return }
    foreach ($property in $Node.PSObject.Properties) {
        if ($property.Name -eq 'samplesMs') {
            $samples = @($property.Value)
            if ($samples.Count -eq 0 -or @($samples | Where-Object {
                $_ -isnot [double] -and $_ -isnot [int] -and $_ -isnot [long] -and $_ -isnot [decimal]
            }).Count -gt 0) {
                throw 'A samplesMs field is empty or contains non-numeric values.'
            }
            $samples.Count
        } else {
            Get-MeasuredSampleCounts -Node $property.Value
        }
    }
}

$repositoryRoot = Resolve-RepositoryRoot
$matrixFullPath = (Resolve-Path -LiteralPath $MatrixPath).Path
$matrix = Get-Content -LiteralPath $matrixFullPath -Raw | ConvertFrom-Json

if ([string]$matrix.schemaVersion -ne 'deploysharp-paddle-document-backend-matrix-v1') {
    throw "Unsupported matrix schema: $($matrix.schemaVersion)"
}

$assets = @($matrix.assets)
if ($assets.Count -eq 0) {
    throw 'The backend matrix contains no assets.'
}

$backendNames = @('onnxruntimeCpu', 'openvinoCpu', 'opencvDnnCpu', 'tensorRtCuda')
$statusCounts = [ordered]@{}
foreach ($backend in $backendNames) {
    $statusCounts[$backend] = Get-StatusCounts -Rows $assets -Backend $backend
}

$missingEvidence = [System.Collections.Generic.List[string]]::new()
$evidencePaths = [System.Collections.Generic.List[string]]::new()
foreach ($property in $matrix.evidence.psobject.Properties) {
    $reference = [string]$property.Value
    if ([string]::IsNullOrWhiteSpace($reference)) {
        continue
    }

    $relativePath = ($reference -split '#', 2)[0]
    if ([string]::IsNullOrWhiteSpace($relativePath) -or $relativePath -match '^[a-z]+://') {
        continue
    }

    $evidencePaths.Add($relativePath)
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath))) {
        $missingEvidence.Add("$($property.Name): $reference")
    }
}

$dynamicRows = @($matrix.dynamicBatchEvidence)
$dynamicCounts = [ordered]@{}
foreach ($backend in $backendNames) {
    # Dynamic-Batch rows intentionally preserve scoped values such as
    # pass-separate-report and not-measured-in-this-dynamic-batch-run.  The
    # current index groups those values into the same three top-level buckets
    # without discarding the raw values in the authoritative matrix.
    $normalizedDynamicRows = @($dynamicRows | ForEach-Object {
        $raw = [string]$_.$backend
        $normalized = if ($raw -eq 'pass' -or $raw -like 'pass-*') {
            'pass'
        } elseif ($raw -eq 'unsupported' -or $raw -like 'unsupported-*') {
            'unsupported'
        } elseif ($raw -eq 'unverified' -or $raw -like 'not-measured-*' -or $raw -like 'unverified-*') {
            'unverified'
        } else {
            throw "Unexpected dynamic-Batch status '$raw' for backend '$backend' and model '$($_.modelId)'."
        }
        [pscustomobject]@{ modelId = $_.modelId; $backend = $normalized }
    })
    $dynamicCounts[$backend] = Get-StatusCounts -Rows $normalizedDynamicRows -Backend $backend
}

$generatedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$relativeMatrixPath = [IO.Path]::GetRelativePath($repositoryRoot, $matrixFullPath).Replace('\', '/')
$sourceRevision = (& git -C $repositoryRoot log -1 --format=%H -- $relativeMatrixPath).Trim()
$unsupportedByBackend = [ordered]@{}
foreach ($backend in $backendNames) {
    $unsupportedByBackend[$backend] = @($assets | Where-Object { [string]$_.$backend -eq 'unsupported' } | ForEach-Object { [string]$_.modelId })
}

function Read-FormalPerformanceEvidence {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory)]
        [string]$RelativePath,
        [Parameter(Mandatory)]
        [string]$Id,
        [Parameter(Mandatory)]
        [string]$Scope,
        [Parameter(Mandatory)]
        [string[]]$ModelIds,
        [Parameter(Mandatory)]
        [string[]]$Backends
    )

    $fullPath = Join-Path $RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Formal performance evidence '$Id' is missing: $RelativePath"
    }

    $document = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    $protocolCandidates = @()
    if ($null -ne $document.protocol) {
        $protocolCandidates += $document.protocol
    }
    if ($null -ne $document.execution -and $null -ne $document.execution.benchmark) {
        $protocolCandidates += $document.execution.benchmark
    }
    if ($null -ne $document.timing) {
        $protocolCandidates += $document.timing
    }
    $protocol = $protocolCandidates | Where-Object {
        $warmup = if ($null -ne $_.warmupCount) { [int]$_.warmupCount } elseif ($null -ne $_.warmup) { [int]$_.warmup } else { 0 }
        $measurements = if ($null -ne $_.measurementCount) { [int]$_.measurementCount } elseif ($null -ne $_.iterations) { [int]$_.iterations } else { 0 }
        $warmup -ge 5 -and $measurements -ge 50
    } | Select-Object -First 1
    if ($null -eq $protocol) {
        throw "Evidence '$RelativePath' does not declare a 5-warm-up/50-measured protocol."
    }

    $warmupCount = if ($null -ne $protocol.warmupCount) { [int]$protocol.warmupCount } else { [int]$protocol.warmup }
    $measurementCount = if ($null -ne $protocol.measurementCount) { [int]$protocol.measurementCount } else { [int]$protocol.iterations }
    $sampleCounts = @(Get-MeasuredSampleCounts -Node $document)
    foreach ($count in $sampleCounts) {
        if ($count -ne $measurementCount) {
            throw "Evidence '$RelativePath' has $count samplesMs values, but declares $measurementCount measurements."
        }
    }
    $linkedRawReportCount = 0
    foreach ($backend in @($document.backends)) {
        if ($null -eq $backend -or [string]::IsNullOrWhiteSpace([string]$backend.rawEvidence)) { continue }
        $rawName = [string]$backend.rawEvidence
        if ($rawName -ne [IO.Path]::GetFileName($rawName) -or [string]::IsNullOrWhiteSpace([string]$backend.rawEvidenceSha256)) {
            throw "Evidence '$RelativePath' has an invalid SHA-linked raw report reference."
        }
        $rawPath = Join-Path (Split-Path -Parent $fullPath) $rawName
        if (-not (Test-Path -LiteralPath $rawPath -PathType Leaf)) {
            throw "Evidence '$RelativePath' references missing raw report '$rawName'."
        }
        $rawRelativePath = [IO.Path]::GetRelativePath($RepositoryRoot, $rawPath).Replace('\', '/')
        if ($rawRelativePath.StartsWith('../', [StringComparison]::Ordinal) -or
            -not (& git -C $RepositoryRoot ls-files --error-unmatch -- $rawRelativePath 2>$null)) {
            throw "Raw report '$rawName' is not tracked within the repository."
        }
        $rawSha = (Get-FileHash -LiteralPath $rawPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($rawSha -ne [string]$backend.rawEvidenceSha256) {
            throw "Raw report '$rawName' does not match its registered SHA-256."
        }
        $rawDocument = Get-Content -LiteralPath $rawPath -Raw | ConvertFrom-Json
        $rawCounts = @(Get-MeasuredSampleCounts -Node $rawDocument)
        if ($rawCounts.Count -eq 0 -or @($rawCounts | Where-Object { $_ -ne $measurementCount }).Count -gt 0) {
            throw "Raw report '$rawName' has no complete samplesMs arrays for $measurementCount measurements."
        }
        $linkedRawReportCount++
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$document.trackedRawDirectory)) {
        $rawFolderName = [string]$document.trackedRawDirectory
        if ($rawFolderName -ne [IO.Path]::GetFileName($rawFolderName)) {
            throw "Evidence '$RelativePath' has an invalid tracked raw directory."
        }
        $rawFolder = Join-Path (Split-Path -Parent $fullPath) $rawFolderName
        $rows = @($document.rows)
        if ($rows.Count -eq 0) { throw "Evidence '$RelativePath' has no rows for its tracked raw reports." }
        foreach ($row in $rows) {
            $rawName = [string]$row.rawReport
            if ([string]::IsNullOrWhiteSpace($rawName) -or $rawName -ne [IO.Path]::GetFileName($rawName) -or
                [string]::IsNullOrWhiteSpace([string]$row.rawReportSha256)) {
                throw "Evidence '$RelativePath' has an invalid tracked raw report reference."
            }
            $rawPath = Join-Path $rawFolder $rawName
            if (-not (Test-Path -LiteralPath $rawPath -PathType Leaf)) {
                throw "Evidence '$RelativePath' references missing raw report '$rawName'."
            }
            $rawRelativePath = [IO.Path]::GetRelativePath($RepositoryRoot, $rawPath).Replace('\', '/')
            if ($rawRelativePath.StartsWith('../', [StringComparison]::Ordinal) -or
                -not (& git -C $RepositoryRoot ls-files --error-unmatch -- $rawRelativePath 2>$null)) {
                throw "Raw report '$rawName' is not tracked within the repository."
            }
            $rawSha = (Get-FileHash -LiteralPath $rawPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($rawSha -ne [string]$row.rawReportSha256) {
                throw "Raw report '$rawName' does not match its registered SHA-256."
            }
            $rawDocument = Get-Content -LiteralPath $rawPath -Raw | ConvertFrom-Json
            $samples = @($rawDocument.samples)
            if ($samples.Count -ne $measurementCount -or [string]$rawDocument.model -ne [string]$row.model -or
                [string]$rawDocument.backend -ne [string]$row.backend -or
                [string]$rawDocument.modelSha256 -ne [string]$row.modelSha256 -or
                [string]$rawDocument.imageSha256 -ne [string]$row.imageSha256 -or
                [int]$rawDocument.warmup -ne $warmupCount -or [int]$rawDocument.iterations -ne $measurementCount) {
                throw "Raw report '$rawName' does not match its summary identity or protocol."
            }
            foreach ($metric in @('preprocessing', 'inference', 'postprocessing', 'total')) {
                $sampleProperty = "${metric}Ms"
                $values = @($samples | ForEach-Object {
                    $value = $_.$sampleProperty
                    if ($null -eq $value -or -not [double]::IsFinite([double]$value)) {
                        throw "Raw report '$rawName' has an invalid $sampleProperty sample."
                    }
                    [double]$value
                } | Sort-Object)
                $p50 = $values[[int][Math]::Ceiling($measurementCount * 0.5) - 1]
                $p95 = $values[[int][Math]::Ceiling($measurementCount * 0.95) - 1]
                if ([Math]::Abs($p50 - [double]$row.$metric.p50Ms) -gt 0.000001 -or
                    [Math]::Abs($p95 - [double]$row.$metric.p95Ms) -gt 0.000001) {
                    throw "Raw report '$rawName' does not reproduce its $metric P50/P95."
                }
            }
            $linkedRawReportCount++
        }
    }
    [ordered]@{
        id = $Id
        path = $RelativePath.Replace('\', '/')
        scope = $Scope
        modelIds = @($ModelIds)
        backends = @($Backends)
        warmupCount = $warmupCount
        measurementCount = $measurementCount
        inRepositorySampleArrayCount = $sampleCounts.Count
        linkedRawReportCount = $linkedRawReportCount
        sourceRevision = if ($null -ne $document.sourceRevision) { [string]$document.sourceRevision } else { $null }
    }
}

# These are deliberately explicit, small evidence bundles.  They are not inferred
# from filenames: each entry names the exact models/backends that the report covers.
# Keeping this list in the matrix audit prevents a single 5/50 report from being
# misread as a full PP-Structure performance matrix.
$formalPerformanceEvidence = @(
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-official-classification-benchmark-20261017.json' -Id 'official-classification' -Scope 'Official document-orientation and table-classification examples' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-table/pp-lcnet-x1-0-table-cls') -Backends @('onnxruntime-cpu', 'openvino-cpu', 'opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-pp-doclayout-l-benchmark-20261017.json' -Id 'pp-doclayout-l-opencv-dnn' -Scope 'PP-DocLayout-L single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-l') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-pp-docblocklayout-benchmark-20261017.json' -Id 'pp-docblocklayout-opencv-dnn' -Scope 'PP-DocBlockLayout single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-docblocklayout') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-pp-doclayout-plus-l-benchmark-20261017.json' -Id 'pp-doclayout-plus-l-opencv-dnn' -Scope 'PP-DocLayout-Plus-L single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-plus-l') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-rt-detr-h-layout-3cls-benchmark-20261017.json' -Id 'rt-detr-h-layout-3cls-opencv-dnn' -Scope 'RT-DETR-H 3-class layout single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-3cls') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-rt-detr-h-layout-17cls-benchmark-20261017.json' -Id 'rt-detr-h-layout-17cls-opencv-dnn' -Scope 'RT-DETR-H 17-class layout single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-17cls') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-rt-detr-l-wired-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wired-cell-opencv-dnn' -Scope 'RT-DETR-L wired-cell detector single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-table/rt-detr-l-wired-cell-det') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-rt-detr-l-wireless-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wireless-cell-opencv-dnn' -Scope 'RT-DETR-L wireless-cell detector single-image OpenCV DNN CPU execution with bounded ORT parity' -ModelIds @('paddle-table/rt-detr-l-wireless-cell-det') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-orientation-20261004.json' -Id 'document-orientation-tensorrt10-single-image' -Scope 'PP-LCNet document orientation single-image TensorRT 10.11/API 10 execution and bounded CPU score comparison' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-table-classification-20261004.json' -Id 'table-classification-tensorrt10-single-image' -Scope 'PP-LCNet table classification single-image TensorRT 10.11/API 10 execution and bounded CPU score comparison' -ModelIds @('paddle-table/pp-lcnet-x1-0-table-cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-multipage-benchmark-20261014.json' -Id 'multipage-orientation-layout' -Scope 'Two-page orientation to layout task chain' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-doc/pp-doclayout-l') -Backends @('onnxruntime-cpu', 'openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-multipage-benchmark-20261014.json' -Id 'multipage-orientation-layout-tensorrt' -Scope 'Two-page orientation to layout task chain with TensorRT CUDA' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-doc/pp-doclayout-l') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-tensorrt11-20261011.json' -Id 'static-layout-tensorrt11' -Scope 'Five PP-DocLayout and PP-DocBlockLayout static Batch=1 models with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-l', 'paddle-doc/pp-doclayout-plus-l', 'paddle-doc/pp-doclayout-m', 'paddle-doc/pp-doclayout-s', 'paddle-doc/pp-docblocklayout') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-paddle-doc-pp-docblocklayout-tensorrt11-dynamic-batch-20261011.json' -Id 'pp-docblocklayout-tensorrt11-dynamic-batch2' -Scope 'PP-DocBlockLayout dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-doc/pp-docblocklayout') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-paddle-doc-pp-doclayout-l-tensorrt11-dynamic-batch-20261011.json' -Id 'pp-doclayout-l-tensorrt11-dynamic-batch2' -Scope 'PP-DocLayout-L dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-l') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-paddle-doc-pp-doclayout-plus-l-tensorrt11-dynamic-batch-20261011.json' -Id 'pp-doclayout-plus-l-tensorrt11-dynamic-batch2' -Scope 'PP-DocLayout-Plus-L dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-plus-l') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-paddle-doc-rt-detr-h-layout-17cls-tensorrt11-dynamic-batch-20261011.json' -Id 'rtdetr-h-17cls-tensorrt11-dynamic-batch2' -Scope 'RT-DETR-H 17-class layout dynamic Batch=2 with bounded ORT parity on TensorRT 11' -ModelIds @('paddle-doc/rt-detr-h-layout-17cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-paddle-doc-rt-detr-h-layout-3cls-tensorrt11-dynamic-batch-20261011.json' -Id 'rtdetr-h-3cls-tensorrt11-dynamic-batch2' -Scope 'RT-DETR-H 3-class layout dynamic Batch=2 with bounded ORT parity on TensorRT 11' -ModelIds @('paddle-doc/rt-detr-h-layout-3cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-picodet-static-tensorrt11-20261012.json' -Id 'picodet-static-layout-tensorrt11' -Scope 'Six static PicoDet layout models with bounded ORT parity' -ModelIds @('paddle-doc/picodet-layout-1x', 'paddle-doc/picodet-layout-1x-table', 'paddle-doc/picodet-s-layout-3cls', 'paddle-doc/picodet-l-layout-3cls', 'paddle-doc/picodet-s-layout-17cls', 'paddle-doc/picodet-l-layout-17cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-rt-detr-h-3cls-tensorrt10.11-dynamic-batch-20261017.json' -Id 'rtdetr-h-layout-3cls-tensorrt-dynamic-batch' -Scope 'RT-DETR-H 3-class layout dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-3cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-layout-rt-detr-h-17cls-tensorrt10.11-dynamic-batch-20261017.json' -Id 'rtdetr-h-layout-17cls-tensorrt-dynamic-batch' -Scope 'RT-DETR-H 17-class layout dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-17cls') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.json' -Id 'table-task-chain-ort' -Scope 'Table classification, cell detection and original SLANeXt HTML task chain' -ModelIds @('paddle-table/pp-lcnet-x1-0-table-cls', 'paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/slanext-wired') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.json' -Id 'table-task-chain-openvino' -Scope 'Table classification, cell detection and OpenVINO-compatible SLANeXt HTML task chain' -ModelIds @('paddle-table/pp-lcnet-x1-0-table-cls', 'paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/slanext-wired-openvino-compat') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-table-cell-matrix-20260930.json' -Id 'table-cell-tensorrt' -Scope 'Wired and wireless table-cell detector parity' -ModelIds @('paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/rt-detr-l-wireless-cell-det') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-seal-paddle-seal-ppocrv4-mobile-dynamic-batch-tensorrt11-20261017.json' -Id 'seal-mobile-tensorrt11-dynamic-batch2' -Scope 'PP-OCRv4 mobile seal detector dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-seal/ppocrv4-mobile') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-seal-paddle-seal-ppocrv4-server-dynamic-batch-tensorrt11-20261017.json' -Id 'seal-server-tensorrt11-dynamic-batch2' -Scope 'PP-OCRv4 server seal detector dynamic Batch=2 with bounded ORT parity' -ModelIds @('paddle-seal/ppocrv4-server') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-ppocrv4-mobile-seal-det-benchmark-20261018.json' -Id 'seal-mobile-opencv-dnn' -Scope 'PP-OCRv4 mobile seal detector single-image OpenCV DNN CPU execution with shape/decoder contract reference' -ModelIds @('paddle-seal/ppocrv4-mobile') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-opencv-ppocrv4-server-seal-det-benchmark-20261018.json' -Id 'seal-server-opencv-dnn' -Scope 'PP-OCRv4 server seal detector single-image OpenCV DNN CPU execution with shape/decoder contract reference' -ModelIds @('paddle-seal/ppocrv4-server') -Backends @('opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-pp-doclayout-l-benchmark-20261018.json' -Id 'pp-doclayout-l-openvino-dnn' -Scope 'PP-DocLayout-L single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-l') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-pp-docblocklayout-benchmark-20261018.json' -Id 'pp-docblocklayout-openvino-dnn' -Scope 'PP-DocBlockLayout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-docblocklayout') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-pp-doclayout-plus-l-benchmark-20261018.json' -Id 'pp-doclayout-plus-l-openvino-dnn' -Scope 'PP-DocLayout-Plus-L single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-plus-l') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-rt-detr-h-layout-3cls-benchmark-20261018.json' -Id 'rt-detr-h-layout-3cls-openvino-dnn' -Scope 'RT-DETR-H 3-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-3cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-rt-detr-h-layout-17cls-benchmark-20261018.json' -Id 'rt-detr-h-layout-17cls-openvino-dnn' -Scope 'RT-DETR-H 17-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/rt-detr-h-layout-17cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-rt-detr-l-wired-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wired-cell-openvino-dnn' -Scope 'RT-DETR-L wired-cell detector single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-table/rt-detr-l-wired-cell-det') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-rt-detr-l-wireless-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wireless-cell-openvino-dnn' -Scope 'RT-DETR-L wireless-cell detector single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-table/rt-detr-l-wireless-cell-det') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-ppocrv4-mobile-seal-det-benchmark-20261018.json' -Id 'seal-mobile-openvino-dnn' -Scope 'PP-OCRv4 mobile seal detector single-image OpenVINO CPU execution with shape/decoder contract reference' -ModelIds @('paddle-seal/ppocrv4-mobile') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-ppocrv4-server-seal-det-benchmark-20261018.json' -Id 'seal-server-openvino-dnn' -Scope 'PP-OCRv4 server seal detector single-image OpenVINO CPU execution with shape/decoder contract reference' -ModelIds @('paddle-seal/ppocrv4-server') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-pp-doclayout-m-benchmark-20261018.json' -Id 'pp-doclayout-m-openvino' -Scope 'PP-DocLayout-M single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-m') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-pp-doclayout-s-benchmark-20261018.json' -Id 'pp-doclayout-s-openvino' -Scope 'PP-DocLayout-S single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/pp-doclayout-s') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-layout-1x-benchmark-20261018.json' -Id 'picodet-layout-1x-openvino' -Scope 'PicoDet layout-1x single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-layout-1x') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-layout-1x-table-benchmark-20261018.json' -Id 'picodet-layout-1x-table-openvino' -Scope 'PicoDet layout-1x-table single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-layout-1x-table') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-s-layout-3cls-benchmark-20261018.json' -Id 'picodet-s-layout-3cls-openvino' -Scope 'PicoDet-S 3-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-s-layout-3cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-l-layout-3cls-benchmark-20261018.json' -Id 'picodet-l-layout-3cls-openvino' -Scope 'PicoDet-L 3-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-l-layout-3cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-s-layout-17cls-benchmark-20261018.json' -Id 'picodet-s-layout-17cls-openvino' -Scope 'PicoDet-S 17-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-s-layout-17cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-picodet-l-layout-17cls-benchmark-20261018.json' -Id 'picodet-l-layout-17cls-openvino' -Scope 'PicoDet-L 17-class layout single-image OpenVINO CPU execution with bounded ORT parity' -ModelIds @('paddle-doc/picodet-l-layout-17cls') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-uvdoc-benchmark-20261018.json' -Id 'uvdoc-openvino' -Scope 'UVDoc single-image OpenVINO CPU unwarping execution, decoder contract and bounded raw-output parity with ORT' -ModelIds @('paddle-doc/uvdoc') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-tensorrt-benchmark-20261018.json' -Id 'uvdoc-tensorrt-dynamic-batch2' -Scope 'UVDoc TensorRT dynamic Batch=2 repeated inference and Decoder latency with bounded ORT raw-output parity' -ModelIds @('paddle-doc/uvdoc') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-openvino-slanext-wireless-benchmark-20261018.json' -Id 'slanext-wireless-openvino-compat' -Scope 'Wireless SLANeXt derived OpenVINO compatibility graph with table decoder and exact source-graph ORT markup parity' -ModelIds @('paddle-table/slanext-wireless-openvino-compat') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-pp-doclayout-m-benchmark-20261018.json' -Id 'pp-doclayout-m-ort-cpu' -Scope 'PP-DocLayout-M single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/pp-doclayout-m') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-pp-doclayout-s-benchmark-20261018.json' -Id 'pp-doclayout-s-ort-cpu' -Scope 'PP-DocLayout-S single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/pp-doclayout-s') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-layout-1x-benchmark-20261018.json' -Id 'picodet-layout-1x-ort-cpu' -Scope 'PicoDet layout-1x single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-layout-1x') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-layout-1x-table-benchmark-20261018.json' -Id 'picodet-layout-1x-table-ort-cpu' -Scope 'PicoDet layout-1x-table single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-layout-1x-table') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-s-layout-3cls-benchmark-20261018.json' -Id 'picodet-s-layout-3cls-ort-cpu' -Scope 'PicoDet-S 3-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-s-layout-3cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-l-layout-3cls-benchmark-20261018.json' -Id 'picodet-l-layout-3cls-ort-cpu' -Scope 'PicoDet-L 3-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-l-layout-3cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-s-layout-17cls-benchmark-20261018.json' -Id 'picodet-s-layout-17cls-ort-cpu' -Scope 'PicoDet-S 17-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-s-layout-17cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-picodet-l-layout-17cls-benchmark-20261018.json' -Id 'picodet-l-layout-17cls-ort-cpu' -Scope 'PicoDet-L 17-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/picodet-l-layout-17cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-pp-doclayout-l-benchmark-20261018.json' -Id 'pp-doclayout-l-ort-region-cpu' -Scope 'PP-DocLayout-L single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/pp-doclayout-l') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-pp-docblocklayout-benchmark-20261018.json' -Id 'pp-docblocklayout-ort-region-cpu' -Scope 'PP-DocBlockLayout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/pp-docblocklayout') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-pp-doclayout-plus-l-benchmark-20261018.json' -Id 'pp-doclayout-plus-l-ort-region-cpu' -Scope 'PP-DocLayout-Plus-L single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/pp-doclayout-plus-l') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-rt-detr-h-layout-3cls-benchmark-20261018.json' -Id 'rt-detr-h-layout-3cls-ort-region-cpu' -Scope 'RT-DETR-H 3-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/rt-detr-h-layout-3cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-rt-detr-h-layout-17cls-benchmark-20261018.json' -Id 'rt-detr-h-layout-17cls-ort-region-cpu' -Scope 'RT-DETR-H 17-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-doc/rt-detr-h-layout-17cls') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-rt-detr-l-wired-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wired-cell-ort-region-cpu' -Scope 'RT-DETR-L wired-cell detector single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-table/rt-detr-l-wired-cell-det') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-rt-detr-l-wireless-cell-det-benchmark-20261018.json' -Id 'rt-detr-l-wireless-cell-ort-region-cpu' -Scope 'RT-DETR-L wireless-cell detector single-image ONNX Runtime CPU execution with bounded OpenVINO parity' -ModelIds @('paddle-table/rt-detr-l-wireless-cell-det') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-ppocrv4-mobile-seal-det-benchmark-20261018.json' -Id 'seal-mobile-ort-cpu' -Scope 'PP-OCRv4 mobile seal detector single-image ONNX Runtime CPU execution with shape and decoder region-count contract' -ModelIds @('paddle-seal/ppocrv4-mobile') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-ppocrv4-server-seal-det-benchmark-20261018.json' -Id 'seal-server-ort-cpu' -Scope 'PP-OCRv4 server seal detector single-image ONNX Runtime CPU execution with shape and decoder region-count contract' -ModelIds @('paddle-seal/ppocrv4-server') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-uvdoc-benchmark-20261018.json' -Id 'uvdoc-ort-cpu' -Scope 'UVDoc single-image ONNX Runtime CPU unwarping execution, decoder contract and bounded raw-output parity with OpenVINO' -ModelIds @('paddle-doc/uvdoc') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-slanext-wireless-benchmark-20261018.json' -Id 'slanext-wireless-ort-cpu' -Scope 'Official wireless SLANeXt single-image ONNX Runtime CPU execution with table decoder and derived OpenVINO compatibility-graph markup parity' -ModelIds @('paddle-table/slanext-wireless') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-ort-formula-benchmark-20261018.json' -Id 'formula-six-models-ort-cpu' -Scope 'Six official Paddle formula exports with stable token/EOS/LaTeX contracts on ONNX Runtime CPU' -ModelIds @('paddle-formula/pp-formulanet-plus-s', 'paddle-formula/pp-formulanet-plus-m', 'paddle-formula/pp-formulanet-plus-l', 'paddle-formula/pp-formulanet-s', 'paddle-formula/pp-formulanet-l', 'paddle-formula/unimernet') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017.json' -Id 'chart2table-fixed-sample' -Scope 'Fixed ChartQA Chart2Table single-sample generation' -ModelIds @('paddle-chart/pp-chart2table') -Backends @('onnxruntime-cpu', 'openvino-cpu', 'tensorrt-cuda'))
)
$sampledBundles = @($formalPerformanceEvidence | Where-Object { $_.inRepositorySampleArrayCount -gt 0 })
$summaryOnlyBundles = @($formalPerformanceEvidence | Where-Object { $_.inRepositorySampleArrayCount -eq 0 })
$linkedRawBundles = @($summaryOnlyBundles | Where-Object { $_.linkedRawReportCount -gt 0 })
$unlinkedSummaryBundles = @($summaryOnlyBundles | Where-Object { $_.linkedRawReportCount -eq 0 })
$linkedRawReportTotal = 0
foreach ($bundle in $linkedRawBundles) { $linkedRawReportTotal += [int]$bundle.linkedRawReportCount }

$performancePairRows = [System.Collections.Generic.List[object]]::new()
foreach ($bundle in $formalPerformanceEvidence) {
    foreach ($modelId in $bundle.modelIds) {
        foreach ($backend in $bundle.backends) {
            $performancePairRows.Add([pscustomobject]@{ modelId = $modelId; backend = $backend; evidenceId = $bundle.id })
        }
    }
}
$performancePairSummary = @($performancePairRows | Group-Object backend | ForEach-Object {
    [ordered]@{
        backend = $_.Name
        evidencePairCount = $_.Count
        distinctModelCount = @($_.Group | ForEach-Object { $_.modelId } | Sort-Object -Unique).Count
        distinctModelBackendPairCount = @($_.Group | ForEach-Object { "$($_.modelId)|$($_.backend)" } | Sort-Object -Unique).Count
        models = @($_.Group | ForEach-Object { $_.modelId } | Sort-Object -Unique)
    }
})
$distinctPerformancePairs = @($performancePairRows | Group-Object { "$($_.modelId)|$($_.backend)" } | ForEach-Object {
    $first = $_.Group | Select-Object -First 1
    [ordered]@{
        modelId = [string]$first.modelId
        backend = [string]$first.backend
        evidenceIds = @($_.Group | ForEach-Object { $_.evidenceId } | Sort-Object -Unique)
    }
})
$performanceBackendProperties = @{
    'onnxruntime-cpu' = 'onnxruntimeCpu'
    'openvino-cpu' = 'openvinoCpu'
    'opencv-dnn-cpu' = 'opencvDnnCpu'
    'tensorrt-cuda' = 'tensorRtCuda'
}
$performanceCoverageErrors = [System.Collections.Generic.List[string]]::new()
foreach ($pair in $distinctPerformancePairs) {
    if (-not $performanceBackendProperties.ContainsKey($pair.backend)) {
        $performanceCoverageErrors.Add("Unknown performance backend '$($pair.backend)' for '$($pair.modelId)'.")
        continue
    }
    $asset = $assets | Where-Object { [string]$_.modelId -eq $pair.modelId } | Select-Object -First 1
    if ($null -eq $asset) {
        $performanceCoverageErrors.Add("Performance evidence model '$($pair.modelId)' is not present in the authoritative matrix.")
        continue
    }
    $statusProperty = $performanceBackendProperties[$pair.backend]
    if ([string]$asset.$statusProperty -ne 'pass') {
        $performanceCoverageErrors.Add("Performance evidence '$($pair.modelId)' / '$($pair.backend)' is registered as '$($asset.$statusProperty)' in the authoritative matrix, not 'pass'.")
    }
}
$performancePairKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($pair in $distinctPerformancePairs) {
    [void]$performancePairKeys.Add("$($pair.modelId)|$($pair.backend)")
}
$excludedDerivedPairs = [System.Collections.Generic.List[object]]::new()
$requiredPassingPairCount = 0
foreach ($asset in $assets) {
    foreach ($entry in $performanceBackendProperties.GetEnumerator()) {
        if ([string]$asset.($entry.Value) -ne 'pass') { continue }
        $key = "$($asset.modelId)|$($entry.Key)"
        if ([string]$asset.artifactKind -eq 'derived-compat-onnx' -and -not $performancePairKeys.Contains($key)) {
            $excludedDerivedPairs.Add([ordered]@{ modelId = [string]$asset.modelId; backend = [string]$entry.Key })
            continue
        }
        $requiredPassingPairCount++
        if (-not $performancePairKeys.Contains($key)) {
            $performanceCoverageErrors.Add("Passing release artifact '$($asset.modelId)' / '$($entry.Key)' has no formal performance evidence.")
        }
    }
}
if ($performanceCoverageErrors.Count -gt 0) {
    throw "Formal performance coverage audit failed: $($performanceCoverageErrors -join ' | ')"
}

$result = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-backend-matrix-summary-v1'
    generatedUtc = $generatedUtc
    sourceRevision = $sourceRevision
    sourceMatrix = [ordered]@{
        path = $relativeMatrixPath
        generatedUtc = [string]$matrix.generatedUtc
        schemaVersion = [string]$matrix.schemaVersion
    }
    deviceScope = $matrix.deviceScope
    runtimes = $matrix.runtimes
    artifactCount = $assets.Count
    statusSemantics = $matrix.statusSemantics
    statusCounts = $statusCounts
    unsupportedByBackend = $unsupportedByBackend
    dynamicBatch = [ordered]@{
        artifactCount = $dynamicRows.Count
        statusCounts = $dynamicCounts
    }
    evidenceAudit = [ordered]@{
        referenceCount = $evidencePaths.Count
        missingCount = $missingEvidence.Count
        missing = @($missingEvidence)
    }
    formalPerformanceEvidence = [ordered]@{
        protocol = '5 warm-ups + 50 measured calls (or a stricter declared count)'
        bundleCount = $formalPerformanceEvidence.Count
        bundles = @($formalPerformanceEvidence)
        distinctModelBackendPairs = $distinctPerformancePairs
        byBackend = $performancePairSummary
        coverageAudit = [ordered]@{
            status = 'pass'
            validatedPairCount = $distinctPerformancePairs.Count
            requiredPassingPairCount = $requiredPassingPairCount
            excludedDerivedPairs = @($excludedDerivedPairs)
            errors = @()
        }
        sampleAvailability = [ordered]@{
            inRepositoryBundleCount = $sampledBundles.Count
            summaryOnlyBundleCount = $summaryOnlyBundles.Count
            summaryOnlyEvidenceIds = @($summaryOnlyBundles | ForEach-Object { $_.id })
            shaLinkedRawBundleCount = $linkedRawBundles.Count
            shaLinkedRawReportCount = $linkedRawReportTotal
            unlinkedSummaryBundleCount = $unlinkedSummaryBundles.Count
            unlinkedSummaryEvidenceIds = @($unlinkedSummaryBundles | ForEach-Object { $_.id })
            boundary = 'Embedded numeric samplesMs arrays are checked against declared counts. SHA-linked tracked raw reports are checked for hash and sample count; official classification reports also reproduce summary P50/P95. Other summary-only bundles may have separate local raw reports; their P50/P95 cannot be recomputed from the registered JSON alone.'
        }
        boundary = 'Every currently passing primary release artifact/backend pair has a mapped formal evidence bundle. Two ORT-pass derived OpenVINO compatibility graphs lack standalone formal timing. The reports use different inputs and devices/profiles, so coverage does not establish a unified comparable performance matrix.'
    }
    boundary = [ordered]@{
        statement = 'This index records exact artifact/backend execution evidence only. It does not establish dataset accuracy, CER/WER, formal P50/P95 ranking, engine portability, long-duration stability, or support for unlisted artifacts.'
        qualityGates = @('A2 annotated natural lines with normalized recognition width >3200 pixels and seam quality', 'A3 natural rotation/inversion labels', 'B1b/B2/B3 natural degradation and enhancement cost', 'formula semantic quality', 'ChartQA split accuracy')
        performanceGates = @('unified formal 5 warm-up + 50 measured PP-Structure protocol', 'second-device matrix', 'long-duration soak and cross-page throughput')
    }
    assets = @($assets | ForEach-Object {
        [ordered]@{
            modelId = [string]$_.modelId
            artifactKind = [string]$_.artifactKind
            onnxruntimeCpu = [string]$_.onnxruntimeCpu
            openvinoCpu = [string]$_.openvinoCpu
            opencvDnnCpu = [string]$_.opencvDnnCpu
            tensorRtCuda = [string]$_.tensorRtCuda
        }
    })
}

$jsonFullPath = [IO.Path]::GetFullPath($OutputJsonPath)
$markdownFullPath = [IO.Path]::GetFullPath($OutputMarkdownPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $jsonFullPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFullPath) | Out-Null
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonFullPath -Encoding utf8

$builder = [Text.StringBuilder]::new()
[void]$builder.AppendLine('# PP-Structure backend status — current index')
[void]$builder.AppendLine()
$markdownDirectory = Split-Path -Parent $markdownFullPath
$relativeMatrixLink = [IO.Path]::GetRelativePath($markdownDirectory, (Join-Path $repositoryRoot $relativeMatrixPath)).Replace('\', '/')
[void]$builder.AppendLine("Generated $generatedUtc from source revision $sourceRevision. The authoritative matrix is [$relativeMatrixPath]($relativeMatrixLink), whose artifact rows are intentionally preserved as the source of truth.")
[void]$builder.AppendLine()
[void]$builder.AppendLine("This index covers **$($assets.Count) exact artifact/backend rows** on the named Windows host. It is an execution/contract index, not a claim that every PP-Structure task is production-ready.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Backend | pass | unsupported | unverified |')
[void]$builder.AppendLine('| --- | ---: | ---: | ---: |')
foreach ($backend in $backendNames) {
    $counts = $statusCounts[$backend]
    [void]$builder.AppendLine("| $backend | $($counts.pass) | $($counts.unsupported) | $($counts.unverified) |")
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Device and runtime scope')
[void]$builder.AppendLine()
[void]$builder.AppendLine("- Device: **$($matrix.deviceScope.label)**; OS build $($matrix.deviceScope.osBuild); GPU $($matrix.deviceScope.gpu); driver $($matrix.deviceScope.driver).")
[void]$builder.AppendLine("- ORT CPU: $($matrix.runtimes.onnxruntimeCpu); OpenVINO CPU: $($matrix.runtimes.openvinoCpu); OpenCV DNN CPU: $($matrix.runtimes.opencvDnnCpu); TensorRT CUDA: $($matrix.runtimes.tensorRtCuda).")
[void]$builder.AppendLine('- GPU rows are valid only for the named TensorRT/CUDA installation and the exact rebuilt engines. CPU rows are independent of the GPU.')

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Exact unsupported rows')
[void]$builder.AppendLine()
foreach ($backend in $backendNames) {
    $ids = @($unsupportedByBackend[$backend])
    if ($ids.Count -eq 0) {
        [void]$builder.AppendLine("- **$backend**: none.")
    } else {
        [void]$builder.AppendLine("- **$backend**: $($ids -join ', ').")
    }
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Dynamic Batch snapshot')
[void]$builder.AppendLine()
[void]$builder.AppendLine("The matrix carries $($dynamicRows.Count) exact dynamic-Batch artifact rows. These are narrower binding/row-isolation contracts and must not be read as a complete PP-Structure throughput or quality benchmark.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Backend | pass | unsupported | unverified |')
[void]$builder.AppendLine('| --- | ---: | ---: | ---: |')
foreach ($backend in $backendNames) {
    $counts = $dynamicCounts[$backend]
    [void]$builder.AppendLine("| $backend | $($counts.pass) | $($counts.unsupported) | $($counts.unverified) |")
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Evidence audit')
[void]$builder.AppendLine()
if ($missingEvidence.Count -eq 0) {
    [void]$builder.AppendLine("All $($evidencePaths.Count) matrix-level evidence references resolve locally. The audit does not validate external URLs or the semantic correctness of a report.")
} else {
    [void]$builder.AppendLine("**Missing references: $($missingEvidence.Count)**")
    foreach ($item in $missingEvidence) { [void]$builder.AppendLine("- $item") }
}

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Formal performance evidence coverage')
[void]$builder.AppendLine()
[void]$builder.AppendLine('The following exact evidence bundles declare at least 5 warm-ups and 50 measured calls. This is a coverage index, not a ranking and not a substitute for a full 31-artifact × backend matrix.')
[void]$builder.AppendLine("Of $($formalPerformanceEvidence.Count) registered JSON bundles, $($sampledBundles.Count) embed numeric ``samplesMs`` arrays with the declared measurement count. Another $($linkedRawBundles.Count) summary bundles link $linkedRawReportTotal tracked raw reports by SHA-256 and sample count; official classification P50/P95 are recomputed from those reports. The remaining $($unlinkedSummaryBundles.Count) have no tracked raw samples linked by this index; separate local raw reports may still exist, so they must not be called lost or rerun by default.")
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Evidence bundle | Scope | Backends | Models | Protocol |')
[void]$builder.AppendLine('| --- | --- | --- | --- | --- |')
foreach ($bundle in $formalPerformanceEvidence) {
    $models = (@($bundle.modelIds) -join '<br>')
    $backends = (@($bundle.backends) -join ', ')
    [void]$builder.AppendLine("| $($bundle.id) | $($bundle.scope) | $backends | $models | $($bundle.warmupCount) warm-up + $($bundle.measurementCount) measured |" )
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Backend | Evidence rows | Distinct model/backend pairs | Distinct models |')
[void]$builder.AppendLine('| --- | ---: | ---: | ---: |')
foreach ($summary in $performancePairSummary) {
    [void]$builder.AppendLine("| $($summary.backend) | $($summary.evidencePairCount) | $($summary.distinctModelBackendPairCount) | $($summary.distinctModelCount) |" )
}
[void]$builder.AppendLine()
[void]$builder.AppendLine("All $requiredPassingPairCount currently passing primary release artifact/backend pairs have mapped formal evidence; $($excludedDerivedPairs.Count) ORT-pass derived OpenVINO compatibility-graph pairs lack standalone formal timing. The bundles use different inputs and device profiles, so this is coverage, not a unified comparable performance matrix. These records do not change backend pass/unsupported state or establish dataset accuracy, second-device reproducibility, locked-clock performance, or long-duration throughput.")

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Boundaries still open')
[void]$builder.AppendLine()
[void]$builder.AppendLine('- Quality: A2 annotated natural lines with normalized recognition width >3,200 pixels and seam quality, A3 natural rotation/inversion labels, B1b/B2/B3 natural degradation/enhancement safety, formula semantic quality, and ChartQA split accuracy.')
[void]$builder.AppendLine('- Performance: one unified formal 5-warm-up/50-measured protocol for the full PP-Structure set, a second-device matrix, and long-duration/throughput stability.')
[void]$builder.AppendLine('- `pass` means only that the exact artifact/backend/device/runtime contract executed with the evidence linked by the matrix; it does not promote neighboring exports, runtimes or model families.')

Set-Content -LiteralPath $markdownFullPath -Value $builder.ToString().TrimEnd() -Encoding utf8

if ($missingEvidence.Count -gt 0) {
    throw "Evidence audit failed with $($missingEvidence.Count) missing reference(s)."
}

Write-Host "PADDLE_DOCUMENT_MATRIX_JSON=$jsonFullPath"
Write-Host "PADDLE_DOCUMENT_MATRIX_MARKDOWN=$markdownFullPath"
Write-Host "PADDLE_DOCUMENT_MATRIX_STATUS=pass"
