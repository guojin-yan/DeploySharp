[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$OutputJsonPath = (Join-Path $PSScriptRoot '..\verification\paddleocr-hiertext-natural-wide-gt-20261023.json'),
    [string]$OutputMarkdownPath = (Join-Path $PSScriptRoot '..\verification\paddleocr-hiertext-natural-wide-gt-20261023.md'),
    [string]$Title = 'HierText natural wide-line GT recognition — PP-OCRv6 Small / Medium (2026-10-23)'
)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $ReportPath).Path
$data = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json -Depth 60
$engines = @($data.engines | Where-Object { $null -ne $_ })
$backendCount = @($data.runtimes).Count
$sourceLineCount = @($data.dataset.lines).Count
$rowsPerBackend = 4 * $sourceLineCount
$callCount = $rowsPerBackend * $backendCount
$mainLineCount = @($data.dataset.lines | Where-Object qualityStratum -CEQ 'legible-height-at-least-2px').Count
$illegibleCount = @($data.dataset.lines | Where-Object qualityStratum -CEQ 'official-illegible').Count
$shortLineCount = @($data.dataset.lines | Where-Object qualityStratum -CEQ 'one-pixel-height-diagnostic').Count
if ($backendCount -lt 1 -or $sourceLineCount -lt 1 -or @($data.rows).Count -ne $callCount -or @($data.parity).Count -ne $rowsPerBackend -or @($data.paired).Count -ne 2 * $sourceLineCount * $backendCount -or
    @($data.parity | Where-Object rows -NE $backendCount).Count -gt 0) { throw 'Expected two models and two modes for every labelled line and configured backend, with complete comparison groups.' }
if (@($data.rows | Group-Object { "$($_.Backend)/$($_.Model)/$($_.Mode)/$($_.Key)" }).Count -ne $callCount) { throw 'Duplicate or missing backend/model/mode/line keys.' }
if ($data.protocol.maximumWidthPixels -ne 3200 -or $data.protocol.targetHeight -ne 48 -or $data.protocol.batchSize -ne 1) { throw 'Unexpected width or batch protocol.' }
$pairedSummary = @(foreach ($group in ($data.paired | Where-Object stratum -CEQ 'legible-height-at-least-2px' | Group-Object { ($_.key -split '/')[0..1] -join '/' })) {
    [pscustomobject]@{
        backendModel = $group.Name
        lines = @($group.Group).Count
        cerBetterWorseEqual = @(@($group.Group | Where-Object rawCerEditsDelta -LT 0).Count, @($group.Group | Where-Object rawCerEditsDelta -GT 0).Count, @($group.Group | Where-Object rawCerEditsDelta -EQ 0).Count)
        totalCharacterEditsDelta = ($group.Group.rawCerEditsDelta | Measure-Object -Sum).Sum
        totalWordEditsDelta = ($group.Group.rawWerEditsDelta | Measure-Object -Sum).Sum
    }
})
$compactRows = @($data.rows | ForEach-Object {
    [pscustomobject]@{ backend = $_.Backend; model = $_.Model; mode = $_.Mode; key = $_.Key; stratum = $_.QualityStratum; naturalWidthPixels = $_.NaturalWidth;
        expectedTextSha256 = $_.ExpectedTextSha256; recognizedTextSha256 = $_.TextSha256; emittedClassSha256 = $_.EmittedClassSha256;
        raw = $_.Raw; lowercase = $_.Lowercase; windows = @($_.Windows).Count; removedPrefixTokens = $_.RemovedPrefixTokens; uncertainSeams = $_.UncertainSeams }
})
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-natural-wide-gt-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    measurementGeneratedUtc = $data.generatedUtc
    sourceRevision = $data.sourceRevision
    testAssemblySha256 = $data.testAssemblySha256
    visualAssemblySha256 = $data.visualAssemblySha256
    environment = $data.environment
    dataset = $data.dataset
    models = $data.models
    runtimes = $data.runtimes
    engines = $engines
    gpuObservation = $data.gpuObservation
    protocol = $data.protocol
    executionSummary = [ordered]@{ sourceImages = @($data.dataset.lines.ImageId | Sort-Object -Unique).Count; sourceLines = $sourceLineCount; mainQualityLines = $mainLineCount;
        officialIllegibleDiagnostics = $illegibleCount; onePixelHeightDiagnostics = $shortLineCount; modelCount = 2; backendCount = $backendCount; modeCount = 2; recognitionCalls = $callCount; executionContractsPassed = $callCount }
    crossBackend = [ordered]@{ compared = $backendCount -gt 1; pairCount = $(if ($backendCount -gt 1) { $rowsPerBackend } else { 0 });
        textMatched = $(if ($backendCount -gt 1) { @($data.parity | Where-Object textEqual -EQ $true).Count } else { $null });
        emittedClassesMatched = $(if ($backendCount -gt 1) { @($data.parity | Where-Object emittedClassEqual -EQ $true).Count } else { $null });
        windowDecisionsMatched = $(if ($backendCount -gt 1) { @($data.parity | Where-Object windowsEqual -EQ $true).Count } else { $null });
        boundary = 'Text, emitted classes and window decisions only; native output tensors and confidence values are not claimed bitwise equal.' }
    quality = $data.quality
    pairedMainQuality = $pairedSummary
    rows = $compactRows
    detailedEvidence = [ordered]@{ fileName = [IO.Path]::GetFileName($resolved); sha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant(); localTraceEvidence = $data.localTraceEvidence;
        location = 'Local ignored artifacts; exact report/trace filename and SHA are recorded here. Source images, labels and raw text/token traces are not distributed in this summary.' }
    result = 'Execution/source/window contracts pass. Paired quality deltas are recorded per backend/model; positive edit deltas mean worse SlidingWindow quality. No default-policy or seam-threshold change.'
    boundary = $data.boundary
}
$jsonFull = [IO.Path]::GetFullPath($OutputJsonPath)
$markdownFull = [IO.Path]::GetFullPath($OutputMarkdownPath)
$null = New-Item -ItemType Directory -Force -Path (Split-Path $jsonFull)
$null = New-Item -ItemType Directory -Force -Path (Split-Path $markdownFull)
$summary | ConvertTo-Json -Depth 35 | Set-Content -LiteralPath $jsonFull -Encoding utf8
$md = [Text.StringBuilder]::new()
[void]$md.AppendLine('# ' + $Title)
[void]$md.AppendLine()
[void]$md.AppendLine('The original A2 requirement concerns **normalized recognition width >3,200 pixels**, not 3,200 characters. This experiment uses original labelled quadrilaterals directly with `RecognizeOnlyAsync`, bypassing DET/CLS and detector-association misses. Source JPEGs remain outside the repository.')
[void]$md.AppendLine()
[void]$md.AppendLine("Pinned HierText $($data.dataset.split) revision: ``$($data.dataset.revision)``; annotation SHA-256: ``$($data.dataset.annotationSha256)``. The fixed geometry audit contains $sourceLineCount lines in $($summary.executionSummary.sourceImages) images. Main quality uses $mainLineCount officially legible lines with source bbox height >=2px. $illegibleCount officially illegible and $shortLineCount sub-2px-height lines are retained as separate diagnostic strata. No prediction-based exclusion is applied. Image/model/dictionary identities are recorded in the [JSON summary]($([IO.Path]::GetFileName($jsonFull))).")
[void]$md.AppendLine()
$runtimeDescription = ($data.runtimes | ForEach-Object { "$($_.backend) $($_.nativeVersion)" }) -join '; '
[void]$md.AppendLine('Protocol: target height 48; maximum width **3,200**; Clamp versus SlidingWindow with overlap 0.2 and maximum 32 windows; batch 1; one independently created Session per stage; one untimed execution per line/mode/backend. Runtime(s): ' + $runtimeDescription + '. The 32x32 prepared DET tensor is unused: recognition crop sampling always reads the original image. This is neither a latency benchmark nor a claim of statistical significance.')
if ($engines.Count -gt 0) { [void]$md.AppendLine('TensorRT uses freshly built library engines, runtime-default graph precision, optimization level 0 and 256 MiB workspace; dynamic profiles are [1,3,48,minimumWidth] / [1,3,48,320] / [1,3,48,3200]. Engine/source/build-input SHA values and bytes are retained in JSON. This path executes CPU crop/CTC/merge and TensorRT REC; it is not a device-resident OCR pipeline. GPU snapshots only describe load at two instants, not controlled clocks or a benchmark.') }
[void]$md.AppendLine()
[void]$md.AppendLine('| Recognition backend | v6 recognizer | Mode | Main lines | Character edits / reference | CER | Word edits / reference | WER | Windows | Uncertain seams |')
[void]$md.AppendLine('| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($quality in ($data.quality | Where-Object key -CLike '*/legible-height-at-least-2px')) {
    $keyParts = $quality.key -split '/'
    [void]$md.AppendLine("| $($keyParts[0]) | $($keyParts[1]) | $($keyParts[2]) | $($quality.count) | $($quality.raw.characterEdits) / $($quality.raw.referenceCharacters) | $([Math]::Round(100 * $quality.raw.cer, 3))% | $($quality.raw.wordEdits) / $($quality.raw.referenceWords) | $([Math]::Round(100 * $quality.raw.wer, 3))% | $($quality.windows) | $($quality.uncertainSeams) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine('CER uses ordinal Unicode scalars and WER uses whitespace-token edit distance, aggregated as total edits / total reference units. Explicit invariant-lowercase results are recorded separately in JSON; they need not equal the raw metrics. Spaces and punctuation are not removed. These corpus/micro rates differ from historical detector-association macro means and should not be compared as if they used the same input or protocol.')
[void]$md.AppendLine()
[void]$md.AppendLine("All **$callCount/$callCount** calls pass width/source/CTC contracts: original source region and coordinates survive, each raw window is <=3,200 pixels, coverage is ordered and overlapping from 0 to 1, and retained emitted tokens reconstruct the final text.")
if ($backendCount -gt 1) { [void]$md.AppendLine("Across all configured backends, text, emitted-class and window-decision matches are **$($summary.crossBackend.textMatched)/$rowsPerBackend**, **$($summary.crossBackend.emittedClassesMatched)/$rowsPerBackend**, **$($summary.crossBackend.windowDecisionsMatched)/$rowsPerBackend**; raw output tensors/confidences were not compared for bitwise equality.") }
else { [void]$md.AppendLine('Only one backend was run in this report: cross-backend parity is **not assessed**. Single-row groups do not count as matched backend pairs; use a separately validated reference comparison.') }
[void]$md.AppendLine()
[void]$md.AppendLine('| Backend / model | Sliding vs Clamp: CER better / worse / equal lines | Sliding minus Clamp: character edits | Sliding minus Clamp: word edits |')
[void]$md.AppendLine('| --- | ---: | ---: | ---: |')
foreach ($paired in $pairedSummary) {
    [void]$md.AppendLine("| $($paired.backendModel) | $($paired.cerBetterWorseEqual -join ' / ') | $($paired.totalCharacterEditsDelta) | $($paired.totalWordEditsDelta) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine('Positive deltas mean more errors with SlidingWindow. The conservative merger keeps unmatched prefixes rather than silently deleting text; execution parity alone does not prove accurate seams. Clamp remains the default. This bounded selection does not justify changing overlap thresholds or enabling automatic correction.')
[void]$md.AppendLine()
[void]$md.AppendLine("A2 remains open for repeat-number/single-character seam quality, remaining exact backend/model combinations and broader natural-width evidence. This $sourceLineCount-line geometry selection is not official HierText scoring, detector/page accuracy or a release accuracy gate. Source-model training overlap is unknown. Local optional traces provide seam diagnostics; raw text, labels and images are excluded from committed reports.")
[void]$md.AppendLine()
[void]$md.AppendLine('Reproduce with `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1`, optional `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_BACKENDS=onnxruntime,openvino,opencv-dnn,tensorrt` (default: the two CPU reference backends), the dataset/model/candidate/output variables documented by `PaddleOcrNaturalWideLineExternalIntegrationTests`, and test filter `FullyQualifiedName~PaddleOcrNaturalWideLineExternalIntegrationTests`. TensorRT additionally needs the matching JYPPX native bridge, TensorRT/CUDA/cuDNN roots and `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRT_API=11` (or 10); engines are built into a unique local artifacts directory. The required but unused detector Session stays on ORT CPU for OpenCV/TensorRT REC-only runs; no DET/CLS inference is performed. Set `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1` only for local raw seam traces. Summarize with `eng/models/paddle-ocr/scripts/Summarize-PaddleOcrNaturalWideLines.ps1 -ReportPath <local-test-json>` and explicit output filenames to preserve earlier reports. Text hashes/counts and recomputation inputs stay in committed JSON; detailed local report/trace SHA values identify ignored artifacts.')
Set-Content -LiteralPath $markdownFull -Value $md.ToString().TrimEnd() -Encoding utf8
Write-Output "NATURAL_WIDE_SUMMARY_JSON=$jsonFull"
Write-Output "NATURAL_WIDE_SUMMARY_MARKDOWN=$markdownFull"
