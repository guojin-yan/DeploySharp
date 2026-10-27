<#
.SYNOPSIS
Synchronizes model-case backend tables from the reviewed verification matrix.
.DESCRIPTION
The matrix is maintained from exact-artifact runtime evidence. This script never
overwrites it or infers support from catalog compatibility metadata. The legacy
script name and OutputPath alias are retained for existing callers.
#>
[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\..\src\DeploySharp.ModelFactory\catalog\deploysharp-official-catalog.json'),
    [Alias('OutputPath')]
    [string]$MatrixPath = (Join-Path $PSScriptRoot '..\..\docs\model-backend-verification-matrix.md'),
    [string]$CasesPath = (Join-Path $PSScriptRoot '..\..\samples\06-models\cases'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
$matrix = Get-Content -LiteralPath $MatrixPath -Raw
$matrixSection = [regex]::Match($matrix, '(?ms)^## 当前工件矩阵\r?\n(?<body>.*?)(?=^## |\z)')
if (-not $matrixSection.Success) { throw 'The matrix has no current-artifact section.' }

$verified = [System.Collections.Generic.Dictionary[string, string[]]]::new([StringComparer]::Ordinal)
foreach ($line in $matrixSection.Groups['body'].Value -split '\r?\n') {
    if (-not $line.StartsWith('| `', [StringComparison]::Ordinal)) { continue }
    $cells = @($line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() })
    if ($cells.Count -ne 6) { throw "Expected a model and five backend cells: $line" }
    $identity = [regex]::Match($cells[0], '^`(?<model>[^`]+)`(?:\s+\(`(?<artifact>[^`]+)`\))?')
    if (-not $identity.Success) { throw "Invalid matrix identity: $($cells[0])" }
    $key = $identity.Groups['model'].Value
    if ($identity.Groups['artifact'].Success) { $key += '|' + $identity.Groups['artifact'].Value }
    $statuses = [string[]]$cells[1..5]
    foreach ($status in $statuses) {
        if ($status -notin @('✓', '△', '✗', '—')) { throw "Unknown backend status '$status' for $key." }
    }
    if ($verified.ContainsKey($key)) { throw "Duplicate matrix identity: $key" }
    $verified.Add($key, $statuses)
}

# Validate all identities before writing any case. A missing row must not silently
# turn into unverified status or reuse another artifact's successful result.
$updates = [System.Collections.Generic.List[object]]::new()
$artifactCount = 0
foreach ($entry in $catalog.entries) {
    $readmePath = Join-Path $CasesPath (($entry.modelId -replace '/', '--') + '\README.md')
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) { throw "Missing model case: $readmePath" }
    $readme = Get-Content -LiteralPath $readmePath -Raw
    $newline = "`n" # Markdown uses LF in .gitattributes.
    $section = [System.Collections.Generic.List[string]]::new()
    $section.Add('## Backend verification')
    $section.Add('')
    $section.Add('Statuses are synchronized from the reviewed model/backend matrix for the exact official-catalog artifact. They summarize retained runtime evidence; regenerating this table does not rerun inference.')
    $section.Add('')
    $section.Add('| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LlamaSharp |')
    $section.Add('| --- | :---: | :---: | :---: | :---: | :---: |')
    foreach ($artifact in $entry.artifacts) {
        $key = [string]$entry.modelId
        if (@($entry.artifacts).Count -gt 1) { $key += '|' + $artifact.artifactId }
        if (-not $verified.ContainsKey($key)) { throw "Missing exact-artifact matrix row: $key" }
        $section.Add(('| {0} | {1} |' -f $artifact.artifactId, ($verified[$key] -join ' | ')))
        $artifactCount++
    }
    $section.Add('')
    $section.Add('`✓` means real inference and result decoding passed; `△` means partial contract or component evidence; `✗` means the tested combination cannot complete; `—` means untested or not applicable. A pass does not imply dataset accuracy, all input shapes, or all devices.')
    $section.Add('')
    if ($entry.modelId -like 'paddleocr/ppocrv5/*') {
        $section.Add('This case uses the exact published `models-visual.1` ModelPack. The separate `PaddleOcrReleaseClient` uses `models-paddleocr`; its v5 Server artifacts have different SHA-256 values. Keep each download path''s manifest and model together; see the [PaddleOCR guide](../../../../docs/articles/visual-ocr.md).')
        $section.Add('')
    }
    if ($entry.modelId -eq 'pp-yoloe/plus-crn-l') {
        $section.Add('TensorRT note: DeploySharp repairs the PaddleDetection opset-11 export''s missing `Squeeze` axes in memory (`Gather(axis=1)` -> `axes=[1]`) before parsing, without changing the exact catalog artifact on disk.')
        $section.Add('')
    }
    elseif ($entry.modelId -eq 'bria/rmbg-2.0') {
        $section.Add('TensorRT note: `onnx.fp32` passed engine build and CUDA inference. `onnx.dynamic-int8` remains unsupported because its graph contains `DynamicQuantizeLinear` and `ConvInteger`; use the fp32 variant for TensorRT.')
        $section.Add('')
    }
    $section.Add('See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the evidence scope, failure reasons, and links to reproduction instructions.')
    $existing = [regex]::Match($readme, '(?ms)^## Backend verification\r?\n.*?(?=^## |\z)')
    $prefix = if ($existing.Success) { $readme.Substring(0, $existing.Index) } else { $readme }
    $suffix = if ($existing.Success) { $readme.Substring($existing.Index + $existing.Length) } else { '' }
    $updated = $prefix.TrimEnd() + $newline + $newline + ($section -join $newline) + $newline
    if ($suffix.Length -gt 0) { $updated += $newline + $suffix.TrimStart("`r", "`n") }
    $updated = $updated.Replace("`r`n", "`n")
    if ($readme -cne $updated) { $updates.Add([pscustomobject]@{ Path = $readmePath; Content = $updated }) }
}

if ($Check -and $updates.Count -gt 0) {
    throw ("Backend tables are stale in {0} cases. Run this script without -Check to synchronize them from the matrix." -f $updates.Count)
}
if (-not $Check) {
    foreach ($update in $updates) {
        [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($update.Path), $update.Content, [System.Text.UTF8Encoding]::new($false))
    }
}
Write-Output ("Verified {0} artifacts in {1} cases; updated {2} case tables. Matrix was read only." -f $artifactCount, @($catalog.entries).Count, $updates.Count)
