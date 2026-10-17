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
    [ordered]@{
        id = $Id
        path = $RelativePath.Replace('\', '/')
        scope = $Scope
        modelIds = @($ModelIds)
        backends = @($Backends)
        warmupCount = $warmupCount
        measurementCount = $measurementCount
        sourceRevision = if ($null -ne $document.sourceRevision) { [string]$document.sourceRevision } else { $null }
    }
}

# These are deliberately explicit, small evidence bundles.  They are not inferred
# from filenames: each entry names the exact models/backends that the report covers.
# Keeping this list in the matrix audit prevents a single 5/50 report from being
# misread as a full PP-Structure performance matrix.
$formalPerformanceEvidence = @(
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-official-classification-benchmark-20261017.json' -Id 'official-classification' -Scope 'Official document-orientation and table-classification examples' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-table/pp-lcnet-x1-0-table-cls') -Backends @('onnxruntime-cpu', 'openvino-cpu', 'opencv-dnn-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-multipage-benchmark-20261014.json' -Id 'multipage-orientation-layout' -Scope 'Two-page orientation to layout task chain' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-doc/pp-doclayout-l') -Backends @('onnxruntime-cpu', 'openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-multipage-benchmark-20261014.json' -Id 'multipage-orientation-layout-tensorrt' -Scope 'Two-page orientation to layout task chain with TensorRT CUDA' -ModelIds @('paddle-doc/pp-lcnet-x1-0-doc-ori', 'paddle-doc/pp-doclayout-l') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.json' -Id 'table-task-chain-ort' -Scope 'Table classification, cell detection and original SLANeXt HTML task chain' -ModelIds @('paddle-table/pp-lcnet-x1-0-table-cls', 'paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/slanext-wired') -Backends @('onnxruntime-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.json' -Id 'table-task-chain-openvino' -Scope 'Table classification, cell detection and OpenVINO-compatible SLANeXt HTML task chain' -ModelIds @('paddle-table/pp-lcnet-x1-0-table-cls', 'paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/slanext-wired-openvino-compat') -Backends @('openvino-cpu')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/paddle-document-tensorrt-table-cell-matrix-20260930.json' -Id 'table-cell-tensorrt' -Scope 'Wired and wireless table-cell detector parity' -ModelIds @('paddle-table/rt-detr-l-wired-cell-det', 'paddle-table/rt-detr-l-wireless-cell-det') -Backends @('tensorrt-cuda')),
    (Read-FormalPerformanceEvidence -RepositoryRoot $repositoryRoot -RelativePath 'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017.json' -Id 'chart2table-fixed-sample' -Scope 'Fixed ChartQA Chart2Table single-sample generation' -ModelIds @('paddle-chart/pp-chart2table') -Backends @('onnxruntime-cpu', 'openvino-cpu', 'tensorrt-cuda'))
)

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
            errors = @()
        }
        boundary = 'These are the explicitly mapped formal evidence bundles currently present. They are partial coverage, not a claim that every one of the 31 PP-Structure artifacts has a unified 5/50 benchmark on every backend.'
    }
    boundary = [ordered]@{
        statement = 'This index records exact artifact/backend execution evidence only. It does not establish dataset accuracy, CER/WER, formal P50/P95 ranking, engine portability, long-duration stability, or support for unlisted artifacts.'
        qualityGates = @('A2 natural continuous >=3200-character ground truth', 'A3 natural rotation/inversion labels', 'B1b/B2/B3 natural degradation and enhancement cost', 'formula semantic quality', 'ChartQA split accuracy')
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
[void]$builder.AppendLine('# PP-Structure backend status — current index (2026-10-17)')
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
[void]$builder.AppendLine('Formal coverage is still incomplete for the remaining layout variants, seal/UVDoc/SLANeXt/FormulaNet families, OpenCV DNN where the matrix says unsupported, second-device reproducibility, locked-clock comparison, and long-duration throughput. Those rows remain governed by the exact backend matrix above.')

[void]$builder.AppendLine()
[void]$builder.AppendLine('## Boundaries still open')
[void]$builder.AppendLine()
[void]$builder.AppendLine('- Quality: A2 natural continuous ≥3,200-character ground truth, A3 natural rotation/inversion labels, B1b/B2/B3 natural degradation/enhancement safety, formula semantic quality, and ChartQA split accuracy.')
[void]$builder.AppendLine('- Performance: one unified formal 5-warm-up/50-measured protocol for the full PP-Structure set, a second-device matrix, and long-duration/throughput stability.')
[void]$builder.AppendLine('- `pass` means only that the exact artifact/backend/device/runtime contract executed with the evidence linked by the matrix; it does not promote neighboring exports, runtimes or model families.')

Set-Content -LiteralPath $markdownFullPath -Value $builder.ToString().TrimEnd() -Encoding utf8

if ($missingEvidence.Count -gt 0) {
    throw "Evidence audit failed with $($missingEvidence.Count) missing reference(s)."
}

Write-Host "PADDLE_DOCUMENT_MATRIX_JSON=$jsonFullPath"
Write-Host "PADDLE_DOCUMENT_MATRIX_MARKDOWN=$markdownFullPath"
Write-Host "PADDLE_DOCUMENT_MATRIX_STATUS=pass"
