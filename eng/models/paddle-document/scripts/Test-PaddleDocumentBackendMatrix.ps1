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
