param(
    [string]$OutputPath = 'eng/models/paddle-document/verification/document-link-audit-20261002.json'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$documents = @(
    'docs/articles/visual-ocr.md',
    'docs/articles/visual-paddle-structure.md',
    'eng/models/paddle-ocr/README.md',
    'eng/models/paddle-document/README.md',
    'eng/models/paddle-document/verification/paddle-document-backend-matrix-current-20261017.md',
    'eng/models/paddle-document/verification/paddle-document-ort-static-layout-benchmark-20261018.md',
    'eng/models/paddle-document/verification/paddle-document-ort-uvdoc-benchmark-20261018.md',
    'eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-tensorrt-benchmark-20261018.md',
    'eng/models/paddle-document/verification/paddle-document-ort-slanext-wireless-benchmark-20261018.md',
    'eng/models/paddle-document/verification/slanext-tensorrt10.11-builder-probe-20261018.md',
    'eng/models/paddle-document/verification/slanext-tensorrt11-rtx2060-builder-probe-20261018.md',
    'docs/articles/device-performance-benchmarks.md',
    'eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007.md',
    'eng/models/paddle-document/verification/formula-realformula-error-patterns-20261007.md',
    'eng/models/paddle-document/verification/formula-realformula-structural-quality-20261011.md',
    'eng/models/paddle-document/verification/formula-backend-quality-status-20261011.md',
    'eng/models/paddle-document/verification/formula-realformula-syntax-quality-20261012.md',
    'eng/models/paddle-document/verification/formula-realformula-render-quality-20261017.md',
    'eng/models/paddle-document/verification/formula-realformula-numeric-equivalence-20261018.md',
    'eng/models/paddle-document/verification/formula-quality-gate-20261017.md',
    'eng/models/paddle-document/verification/paddle-document-table-pipeline-ort-openvino-20261014.md',
    'eng/models/paddle-document/verification/paddle-document-official-classification-benchmark-20261017.md',
    'eng/models/paddle-document/verification/chart2table-fixed-benchmark-20261017.md',
    'eng/models/paddle-document/verification/formula-formulanet-l-cuda-lifecycle-20261014.md',
    'eng/models/paddle-document/verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md',
    'eng/models/paddle-document/verification/formula-formulanet-l-generation-limit-20261007.md',
    'docs/model-backend-verification-matrix.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-20261002.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-ort-20261012.md',
    'eng/models/paddle-document/verification/chart2table-backend-quality-status-20261011.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-ort-48-20261013.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-openvino-48-20261013.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-tensorrt-48-20261013.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-ort-openvino-48-alignment-20261013.md',
    'eng/models/paddle-document/verification/chart2table-extended-quality-ort-tensorrt-48-alignment-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-detectors-tensorrt-dynamic-batch-decoder-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-tensorrt-full-pipeline-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-tensorrt10.11-steady-formal-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-ort1.28-openvino-steady-formal-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-ort-cuda12-steady-formal-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-opencv-dnn-steady-formal-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-three-image-opencv-ort-20261013.md',
    'eng/models/paddle-ocr/verification/paddleocr-quality-status-20261014.md',
    'eng/models/paddle-ocr/verification/paddleocr-quality-status-20261018.md',
    'eng/models/paddle-ocr/verification/paddleocr-quality-status-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-sroie-natural-source-enhancement-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-funsd-natural-low-quality-enhancement-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-funsd-paired-enhancement-cost-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-gt-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-seam-followup-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-natural-wide-seam-comparison-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-cpu-reference-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-opencv-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-opencv-parity-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-hiertext-natural-wide-tensorrt-interrupted-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-vl-official-assets-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-vl-tokenizer-contract-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-vl-reference-summary-linux-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-seven-models-ort-openvino-component-parity-windows-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-seven-models-ort-openvino-component-parity-linux-rtx2060-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-core-seven-models-ort-openvino-three-image-linux-rtx2060-20261023.md',
    'eng/models/paddle-ocr/verification/paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-20261018.md',
    'eng/models/paddle-ocr/verification/paddleocr-v6-small-session-pool-prefetch-linux-rtx2060-benchmark-20261018.md',
    'eng/models/paddle-ocr/verification/paddleocr-longtext-coverage-a2-20261014.md',
    'eng/models/paddle-ocr/verification/hiertext-natural-wide-coverage-a2-20261017.md',
    'eng/models/paddle-ocr/verification/hiertext-natural-wide-a2-v6-small-20261017.md',
    'eng/models/paddle-ocr/verification/paddleocr-orientation-coverage-a3-20261014.md'
)
$records = [System.Collections.Generic.List[object]]::new()
foreach ($relativeDocument in $documents) {
    $documentPath = Join-Path $repoRoot $relativeDocument
    if (!(Test-Path -LiteralPath $documentPath -PathType Leaf)) { throw "Missing document: $relativeDocument" }
    $documentDirectory = Split-Path -Parent $documentPath
    $text = Get-Content -LiteralPath $documentPath -Raw
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) { $target = $target.Substring(1, $target.Length - 2) }
        if ([string]::IsNullOrWhiteSpace($target) -or $target.StartsWith('#') -or $target -match '^(?i:https?|mailto):') { continue }
        $target = $target.Split('#')[0].Split('?')[0]
        if ([string]::IsNullOrWhiteSpace($target)) { continue }
        $target = [Uri]::UnescapeDataString($target)
        $resolved = [IO.Path]::GetFullPath((Join-Path $documentDirectory $target))
        $exists = Test-Path -LiteralPath $resolved -PathType Leaf
        if (!$exists -and (Test-Path -LiteralPath $resolved -PathType Container)) {
            $exists = (Test-Path -LiteralPath (Join-Path $resolved 'README.md') -PathType Leaf) -or
                (Test-Path -LiteralPath (Join-Path $resolved 'index.md') -PathType Leaf)
        }
        $records.Add([ordered]@{
            document = $relativeDocument
            target = $target
            resolved = $resolved.Substring($repoRoot.Length).TrimStart('\', '/')
            exists = $exists
        })
    }
}

$broken = @($records | Where-Object { !$_.exists })
$output = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-doc-link-audit-v2'
    generatedUtc = [DateTimeOffset]::UtcNow
    documents = $documents
    localMarkdownLinksChecked = $records.Count
    brokenLocalMarkdownLinks = $broken.Count
    broken = $broken
    scope = 'Relative local Markdown links only; external URLs and generated runtime paths are not network-validated.'
}
$outputPathFull = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
$outputDirectory = Split-Path -Parent $outputPathFull
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$output | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPathFull -Encoding utf8
if ($broken.Count -gt 0) {
    $broken | ForEach-Object { Write-Error ("Broken link: " + $_.document + " -> " + $_.target) }
    exit 1
}
Write-Output ("Checked {0} local Markdown links; broken={1}; report={2}" -f $records.Count, $broken.Count, $outputPathFull)
