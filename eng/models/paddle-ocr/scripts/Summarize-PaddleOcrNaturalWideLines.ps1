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
if (@($data.rows).Count -ne 104 -or @($data.parity).Count -ne 52 -or @($data.paired).Count -ne 52) { throw 'Expected the full two-model/two-mode/two-backend/13-line evidence.' }
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
    protocol = $data.protocol
    executionSummary = [ordered]@{ sourceImages = @($data.dataset.lines.ImageId | Sort-Object -Unique).Count; sourceLines = 13; mainQualityLines = 11;
        officialIllegibleDiagnostics = 1; onePixelHeightDiagnostics = 1; modelCount = 2; backendCount = 2; modeCount = 2; recognitionCalls = 104; executionContractsPassed = 104 }
    crossBackend = [ordered]@{ pairCount = 52; textMatched = @($data.parity | Where-Object textEqual -EQ $true).Count;
        emittedClassesMatched = @($data.parity | Where-Object emittedClassEqual -EQ $true).Count; windowDecisionsMatched = @($data.parity | Where-Object windowsEqual -EQ $true).Count;
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
[void]$md.AppendLine('The original A2 requirement concerns **normalized recognition width >3,200 pixels**, not 3,200 characters. This experiment uses original labelled quadrilaterals directly with `RecognizeOnlyAsync`, bypassing DET/CLS and detector-association misses. All source JPEGs were already cached; none was downloaded again or added to the repository.')
[void]$md.AppendLine()
[void]$md.AppendLine('Pinned HierText validation revision: `70b6620b2b112597d8219e11eee9773a1403827c`; annotation SHA-256: `ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a`. The fixed geometry audit contains 13 lines in 9 images. Main quality uses 11 officially legible lines with source bbox height >=2px (1,535 reference Unicode scalars / 207 whitespace words). One officially illegible line and one officially legible but 1px-high line are retained as separate diagnostic strata. No prediction-based exclusion is applied. Image/model/dictionary identities are recorded in the [JSON summary](' + [IO.Path]::GetFileName($jsonFull) + ').')
[void]$md.AppendLine()
[void]$md.AppendLine('Protocol: target height 48; maximum width **3,200**; Clamp versus SlidingWindow with overlap 0.2 and maximum 32 windows; batch 1; one independently created Session per stage; one untimed execution per line/mode/backend. ORT CPU 1.28.0 and OpenVINO CPU 2026.2.1. The 32x32 prepared DET tensor is unused: recognition crop sampling always reads the original image. This is neither a latency benchmark nor a claim of statistical significance.')
[void]$md.AppendLine()
[void]$md.AppendLine('| CPU backend | v6 recognizer | Mode | Main lines | Character edits / reference | CER | Word edits / reference | WER | Windows | Uncertain seams |')
[void]$md.AppendLine('| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($quality in ($data.quality | Where-Object key -CLike '*/legible-height-at-least-2px')) {
    $keyParts = $quality.key -split '/'
    [void]$md.AppendLine("| $($keyParts[0]) | $($keyParts[1]) | $($keyParts[2]) | $($quality.count) | $($quality.raw.characterEdits) / $($quality.raw.referenceCharacters) | $([Math]::Round(100 * $quality.raw.cer, 3))% | $($quality.raw.wordEdits) / $($quality.raw.referenceWords) | $([Math]::Round(100 * $quality.raw.wer, 3))% | $($quality.windows) | $($quality.uncertainSeams) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine('CER uses ordinal Unicode scalars and WER uses whitespace-token edit distance, aggregated as total edits / total reference units. Explicit invariant-lowercase results are also recorded and happen to equal the raw metrics on this selection; spaces and punctuation are not removed. These corpus/micro rates differ from the historical detector-association report''s macro means and should not be compared as if they used the same input or protocol.')
[void]$md.AppendLine()
[void]$md.AppendLine("All **104/104** calls pass width/source/CTC contracts: original source region and coordinates survive, each raw window is <=3,200 pixels, coverage is ordered and overlapping from 0 to 1, and retained emitted tokens reconstruct the final text. Cross-backend text, emitted-class and window-decision matches are **$($summary.crossBackend.textMatched)/52**, **$($summary.crossBackend.emittedClassesMatched)/52**, **$($summary.crossBackend.windowDecisionsMatched)/52**; raw output tensors/confidences were not compared for bitwise equality.")
[void]$md.AppendLine()
[void]$md.AppendLine('| Backend / model | Sliding vs Clamp: CER better / worse / equal lines | Sliding minus Clamp: character edits | Sliding minus Clamp: word edits |')
[void]$md.AppendLine('| --- | ---: | ---: | ---: |')
foreach ($paired in $pairedSummary) {
    [void]$md.AppendLine("| $($paired.backendModel) | $($paired.cerBetterWorseEqual -join ' / ') | $($paired.totalCharacterEditsDelta) | $($paired.totalWordEditsDelta) |")
}
[void]$md.AppendLine()
[void]$md.AppendLine('Positive deltas mean more errors with SlidingWindow. The conservative merger keeps unmatched prefixes rather than silently deleting text; execution parity alone does not prove accurate seams. Clamp remains the default. This bounded selection does not justify changing overlap thresholds or enabling automatic correction.')
[void]$md.AppendLine()
[void]$md.AppendLine('A2 remains open for repeat-number/single-character seam quality, remaining exact backend/model combinations and broader independent natural-width evidence. The 13-line geometry selection is not official HierText scoring, detector/page accuracy or a release accuracy gate. Source-model training overlap is unknown. Local optional traces provide seam diagnostics; raw text, labels and images are excluded from committed reports.')
[void]$md.AppendLine()
[void]$md.AppendLine('Reproduce with `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_RUN=1`, the optional dataset/model/candidate/output variables documented by `PaddleOcrNaturalWideLineExternalIntegrationTests`, and the test filter `FullyQualifiedName~PaddleOcrNaturalWideLineExternalIntegrationTests`. Set `DEPLOYSHARP_PADDLEOCR_NATURAL_WIDE_TRACE=1` only for local raw seam traces. Summarize its report with `eng/models/paddle-ocr/scripts/Summarize-PaddleOcrNaturalWideLines.ps1 -ReportPath <local-test-json>`. Full per-line text hashes, counts and aggregate recomputation inputs are retained in the committed JSON; detailed local report and trace SHA-256 values identify the ignored artifacts.')
Set-Content -LiteralPath $markdownFull -Value $md.ToString().TrimEnd() -Encoding utf8
Write-Output "NATURAL_WIDE_SUMMARY_JSON=$jsonFull"
Write-Output "NATURAL_WIDE_SUMMARY_MARKDOWN=$markdownFull"
