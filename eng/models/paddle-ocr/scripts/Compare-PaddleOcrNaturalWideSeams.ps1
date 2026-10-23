[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineReplayPath,
    [Parameter(Mandatory)][string]$CandidateReplayPath,
    [Parameter(Mandatory)][string]$NativeReportPath,
    [Parameter(Mandatory)][string]$BaselineNativeReportPath,
    [string]$OutputJsonPath = (Join-Path $PSScriptRoot '..\verification\paddleocr-natural-wide-seam-comparison-20261023.json'),
    [string]$OutputMarkdownPath = (Join-Path $PSScriptRoot '..\verification\paddleocr-natural-wide-seam-comparison-20261023.md')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-Report([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 60 }
function Assert-Same($Expected, $Actual, [string]$Context) {
    if (($Expected | ConvertTo-Json -Depth 50 -Compress) -cne ($Actual | ConvertTo-Json -Depth 50 -Compress)) { throw "Mismatch: $Context" }
}
function Evidence([string]$Path) {
    [ordered]@{ fileName = [IO.Path]::GetFileName($Path); sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
function Index-Rows($Rows) {
    $index = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($row in $Rows) { $index.Add("$($row.Backend)/$($row.Model)/$($row.Mode)/$($row.Key)", $row) }
    return ,$index
}
function Seam-Decisions($Windows) {
    @($Windows | ForEach-Object { [ordered]@{ Index = $_.Index; RemovedPrefixTokens = $_.RemovedPrefixTokens; SeamUncertain = $_.SeamUncertain; OverlapEditDistance = $_.OverlapEditDistance } })
}
function Percentile($Values, [double]$Fraction) {
    $ordered = @($Values | Sort-Object); $position = ($ordered.Count - 1) * $Fraction
    $lower = [int][Math]::Floor($position); $upper = [Math]::Min($lower + 1, $ordered.Count - 1)
    $ordered[$lower] + ($ordered[$upper] - $ordered[$lower]) * ($position - $lower)
}
$baseline = Read-Report $BaselineReplayPath
$candidate = Read-Report $CandidateReplayPath
$native = Read-Report $NativeReportPath
$beforeNative = Read-Report $BaselineNativeReportPath
$committedBaselinePath = Join-Path $PSScriptRoot '..\verification\paddleocr-hiertext-natural-wide-gt-20261023.json'
$committedBaseline = Read-Report $committedBaselinePath
Assert-Same 'baseline' $baseline.phase 'baseline replay phase'
Assert-Same 'candidate' $candidate.phase 'candidate replay phase'
Assert-Same $committedBaseline.detailedEvidence.sha256 (Evidence $BaselineNativeReportPath).sha256 'pinned original native report'
Assert-Same '9a8d065904da506c787f9a4d9285e374408ce443e6179a275d7ab4f7c02621f0' $baseline.visualAssemblySha256 'baseline code identity'
Assert-Same $beforeNative.visualAssemblySha256 $baseline.visualAssemblySha256 'baseline native/replay code'
Assert-Same $native.visualAssemblySha256 $candidate.visualAssemblySha256 'candidate native/replay code'
Assert-Same (Evidence $committedBaselinePath).sha256 $candidate.inputs.baselineReportSha256 'replay stratum metadata'
foreach ($replay in @($baseline, $candidate)) {
    Assert-Same $committedBaseline.detailedEvidence.localTraceEvidence.sha256 $replay.inputs.traceSha256 'same pinned raw CTC input'
    Assert-Same $beforeNative.dataset.candidateReportSha256 $replay.inputs.candidateSha256 'same geometry candidates'
    Assert-Same 52 $replay.inputs.cases 'replay case count'
    Assert-Same 44 $replay.inputs.mainQualityCases 'replay main-quality count'
    Assert-Same 52 @($replay.rows).Count 'unique replay row count'
    Assert-Same 5 $replay.protocol.warmupSweeps 'warmup count'
    Assert-Same 50 $replay.protocol.measuredSweeps 'measurement count'
    Assert-Same 50 @($replay.samples).Count 'measurement samples'
    $sampleTimes = @($replay.samples | ForEach-Object { [double]$_.milliseconds })
    foreach ($item in @(@('p50Ms', .5), @('p95Ms', .95))) {
        if ([Math]::Abs((Percentile $sampleTimes $item[1]) - $replay.cost.($item[0])) -gt 1e-9) { throw 'Invalid replay percentile' }
    }
}
foreach ($field in @('dataset', 'models', 'protocol', 'runtimes')) { Assert-Same $beforeNative.$field $native.$field "native $field" }
Assert-Same 104 @($native.rows).Count 'native call count'
$beforeIndex = Index-Rows $beforeNative.rows
$nativeIndex = Index-Rows $native.rows
$replayKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($row in $candidate.rows) {
    $key = "$($row.Backend)/$($row.Model)/SlidingWindow/$($row.Key)"
    if (!$replayKeys.Add($key)) { throw "Duplicate replay key: $key" }
    $original = $beforeIndex[$key]; $actual = $nativeIndex[$key]
    Assert-Same $original.TextSha256 $row.beforeTextSha256 "original text $key"
    Assert-Same $actual.TextSha256 $row.afterTextSha256 "native/replay text $key"
    Assert-Same $original.Raw $row.before "original metrics $key"
    Assert-Same $actual.Raw $row.after "native/replay metrics $key"
    Assert-Same (Seam-Decisions $actual.Windows) (Seam-Decisions $row.windows) "native/replay decisions $key"
}
foreach ($row in $baseline.rows) {
    $key = "$($row.Backend)/$($row.Model)/SlidingWindow/$($row.Key)"
    Assert-Same $beforeIndex[$key].TextSha256 $row.afterTextSha256 "baseline replay text $key"
    Assert-Same (Seam-Decisions $beforeIndex[$key].Windows) (Seam-Decisions $row.windows) "baseline replay decisions $key"
}
Assert-Same 52 @($baseline.rows | Group-Object { "$($_.Backend)/$($_.Model)/$($_.Key)" }).Count 'unique baseline replay keys'
Assert-Same 52 @($native.parity).Count 'native cross-backend pairs'
foreach ($field in @('textEqual', 'emittedClassEqual', 'windowsEqual')) {
    Assert-Same 52 @($native.parity | Where-Object $field -EQ $true).Count "native parity $field"
}
$clampCount = 0; $windowCount = 0
foreach ($key in $beforeIndex.Keys) {
    $before = $beforeIndex[$key]; $after = $nativeIndex[$key]
    if ($before.Mode -ceq 'Clamp') { Assert-Same $before $after "unchanged Clamp $key"; $clampCount++; continue }
    Assert-Same $before.ExpectedTextSha256 $after.ExpectedTextSha256 "reference identity $key"
    $oldWindows = @($before.Windows); $newWindows = @($after.Windows)
    Assert-Same $oldWindows.Count $newWindows.Count "window count $key"
    for ($i = 0; $i -lt $oldWindows.Count; $i++) {
        foreach ($field in @('Index', 'Start', 'End', 'naturalWidth', 'targetWidth', 'tensorWidth', 'textSha256', 'emittedClassSha256', 'emittedTokens')) {
            Assert-Same $oldWindows[$i].$field $newWindows[$i].$field "unchanged raw window $key/$i/$field"
        }
        $windowCount++
    }
}
$aggregates = @(foreach ($group in ($candidate.rows | Where-Object Stratum -CEQ 'legible-height-at-least-2px' | Group-Object { "$($_.Backend)/$($_.Model)" })) {
    $rows = @($group.Group)
    $characters = ($rows.before.ReferenceCharacters | Measure-Object -Sum).Sum
    $words = ($rows.before.ReferenceWords | Measure-Object -Sum).Sum
    $beforeCharacters = ($rows.before.CharacterEditDistance | Measure-Object -Sum).Sum
    $afterCharacters = ($rows.after.CharacterEditDistance | Measure-Object -Sum).Sum
    $beforeWords = ($rows.before.WordEditDistance | Measure-Object -Sum).Sum
    $afterWords = ($rows.after.WordEditDistance | Measure-Object -Sum).Sum
    [ordered]@{ backendModel = $group.Name; lines = $rows.Count; referenceCharacters = $characters; referenceWords = $words;
        beforeCharacterEdits = $beforeCharacters; afterCharacterEdits = $afterCharacters; beforeCer = $beforeCharacters / $characters; afterCer = $afterCharacters / $characters;
        beforeWordEdits = $beforeWords; afterWordEdits = $afterWords; beforeWer = $beforeWords / $words; afterWer = $afterWords / $words;
        cerBetterWorseEqual = @(@($rows | Where-Object { $_.after.CharacterEditDistance -lt $_.before.CharacterEditDistance }).Count,
            @($rows | Where-Object { $_.after.CharacterEditDistance -gt $_.before.CharacterEditDistance }).Count,
            @($rows | Where-Object { $_.after.CharacterEditDistance -eq $_.before.CharacterEditDistance }).Count) }
})
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-natural-wide-seam-comparison-v1'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    sourceRevision = $native.sourceRevision
    implementation = 'Exact matching and bounded substitutions retained; charge leading left whitespace to the edit budget; unique ordered exact right-prefix subsequence allows bounded left gaps and retains all left text. Ambiguous alignments are not accepted.'
    baselineVisualAssemblySha256 = $baseline.visualAssemblySha256; candidateVisualAssemblySha256 = $candidate.visualAssemblySha256
    evidence = [ordered]@{ baselineNative = Evidence $BaselineNativeReportPath; nativeRerun = Evidence $NativeReportPath;
        baselineReplay = Evidence $BaselineReplayPath; candidateReplay = Evidence $CandidateReplayPath; committedBaseline = Evidence $committedBaselinePath;
        rawInputTraceSha256 = $candidate.inputs.traceSha256; geometryCandidateSha256 = $candidate.inputs.candidateSha256 }
    validation = [ordered]@{ originalAndRerunRecognitionCalls = @(104, 104); unchangedClampRows = $clampCount; unchangedRawWindows = $windowCount;
        replayNativeTextMetricAndWindowDecisionsMatched = 52; nativeCrossBackendTextEmissionsAndWindowDecisionsMatched = @(
            @($native.parity | Where-Object textEqual -EQ $true).Count, @($native.parity | Where-Object emittedClassEqual -EQ $true).Count,
            @($native.parity | Where-Object windowsEqual -EQ $true).Count) }
    mainQuality = $aggregates
    mergeOnlyCost = [ordered]@{ protocol = $candidate.protocol; environment = $candidate.environment; baseline = $baseline.cost; candidate = $candidate.cost;
        baselineSamples = $baseline.samples; candidateSamples = $candidate.samples;
        boundary = '52 sequential line merges per sample, including diagnostics. Baseline then candidate; CPU clock not controlled, no randomized paired trials. Excludes native inference, preprocessing, CTC decode, fingerprinting, metrics and JSON. No speedup/full-pipeline latency claim.' }
    boundary = 'Same 13-line corpus as development diagnostics, not independent validation. Small/Medium Clamp remain better at 23/27 character edits versus candidate sliding 31/29. A2, other exact backends, natural repeat-number/single-character safety and broader quality acceptance stay open. Local reports/traces retain raw text; this summary does not.'
}
$jsonFull = [IO.Path]::GetFullPath($OutputJsonPath); $mdFull = [IO.Path]::GetFullPath($OutputMarkdownPath)
foreach ($path in @($jsonFull, $mdFull)) { $null = New-Item -ItemType Directory -Force -Path (Split-Path $path) }
$summary | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $jsonFull -Encoding utf8
$md = [Text.StringBuilder]::new()
[void]$md.AppendLine('# Natural wide-line seam repair: same-token replay and native follow-up (2026-10-23)')
[void]$md.AppendLine()
[void]$md.AppendLine('This follow-up preserves the [original native baseline](paddleocr-hiertext-natural-wide-gt-20261023.md). It changes seam merging only: exact matching/substitution fallback remain, leading left whitespace shares the edit budget, and an unambiguous exact right-prefix subsequence may skip bounded left tokens while retaining all left text. Right-only tokens are never skipped by this subsequence rule. Multiple alignments retain the prefix; non-exact fallback remains uncertain. Clamp and all public thresholds are unchanged. This is a heuristic using approximate CTC positions, not a proof of character correspondence.')
[void]$md.AppendLine()
[void]$md.AppendLine('Inputs: the same 13 labelled HierText lines / 9 cached images, with 11 main-quality lines (1,535 Unicode scalars / 207 whitespace words) and two separate diagnostics. Fixed 3,200px maximum, height 48, overlap 0.2, batch 1, single Session; GT quadrilateral recognition bypasses DET/CLS. ORT CPU 1.28.0 / OpenVINO CPU 2026.2.1 each reran Small/Medium Clamp and SlidingWindow, 104 calls total. Source SHA and assembly identities are in the [machine report](' + [IO.Path]::GetFileName($jsonFull) + ').')
[void]$md.AppendLine()
[void]$md.AppendLine('| CPU backend / v6 model | Character edits before → after | CER before → after | Word edits before → after | WER before → after | CER better / worse / equal lines |')
[void]$md.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: |')
foreach ($row in $aggregates) {
    [void]$md.AppendLine("| $($row.backendModel) | $($row.beforeCharacterEdits) → $($row.afterCharacterEdits) | $([Math]::Round(100 * $row.beforeCer, 3))% → $([Math]::Round(100 * $row.afterCer, 3))% | $($row.beforeWordEdits) → $($row.afterWordEdits) | $([Math]::Round(100 * $row.beforeWer, 3))% → $([Math]::Round(100 * $row.afterWer, 3))% | $($row.cerBetterWorseEqual -join ' / ') |")
}
[void]$md.AppendLine()
[void]$md.AppendLine("The pinned 52-case raw-token replay matches the native rerun's final text, metrics and seam decisions **52/52**. All **$clampCount/52** Clamp rows are unchanged. All **$windowCount** raw windows keep their geometry, text hash and emitted-class hash. Native cross-backend text, emitted-class and seam-decision matches are **$($summary.validation.nativeCrossBackendTextEmissionsAndWindowDecisionsMatched -join '/52, ')/52**. Thus these observed quality changes are attributable to merging, not new model/crop/CTC outputs; native confidences/tensors are not claimed bitwise equal.")
[void]$md.AppendLine()
[void]$md.AppendLine('| Merge-only phase | P50 / P95 per 52 lines | Managed allocation min / max per 52 lines |')
[void]$md.AppendLine('| --- | ---: | ---: |')
foreach ($phase in @('baseline', 'candidate')) { $cost = $summary.mergeOnlyCost.$phase
    [void]$md.AppendLine("| $phase | $([Math]::Round($cost.p50Ms, 3)) / $([Math]::Round($cost.p95Ms, 3)) ms | $($cost.minAllocatedBytes) / $($cost.maxAllocatedBytes) bytes |") }
[void]$md.AppendLine()
[void]$md.AppendLine('Each phase uses 5 warm-up sweeps + 50 measured sweeps of 52 preloaded lines. Allocations use `GC.GetAllocatedBytesForCurrentThread`; samples include result arrays/merge results, and exclude fingerprint/metric/JSON work. Phases ran baseline then candidate with no CPU clock control or randomized pairing. These are cost observations, **not a speedup claim**, native-backend comparison or complete OCR latency. The candidate uses two small bounded rolling arrays only on the fallback path; it introduces no image/tensor copying.')
[void]$md.AppendLine()
[void]$md.AppendLine('The development corpus improves, but Clamp still has fewer character edits (Small 23, Medium 27) than sliding (31, 29). This corpus was used to diagnose the bug and is not independent validation. A2 remains open for broader natural lines, repeated numbers/single-character seams and remaining exact backends. Six new controlled contracts cover left punctuation, whitespace plus substitution, right-only text, ambiguous numbers, budget/disable behavior and out-of-overlap geometry. The current Visual suite passes 531 tests (5 unrelated optional-asset tests skip); replay and native external tests each pass 1/1, and all 16 Visual target frameworks build with zero warnings/errors. The independent PowerShell metric helper also matches all 104 rerun pairs / 832 C# fields; its 11 fixed metric contracts pass.')
[void]$md.AppendLine()
[void]$md.AppendLine('Reproduce: run `OcrRecognitionWindowTraceReplayTests` with `DEPLOYSHARP_PADDLEOCR_SEAM_REPLAY_RUN=1` and the pinned local trace. The baseline phase must use the recorded old assembly, not candidate code with a different label. Run `PaddleOcrNaturalWideLineExternalIntegrationTests` for actual native reruns. Use `Compare-PaddleOcrNaturalWideSeams.ps1` with `-BaselineReplayPath`, `-CandidateReplayPath`, `-NativeReportPath` and `-BaselineNativeReportPath` to validate identities/results and regenerate this report. The [native follow-up](paddleocr-hiertext-natural-wide-seam-followup-20261023.md) records Clamp/sliding quality and diagnostics separately. Local raw reports/traces remain under ignored artifacts, referenced by filename and SHA; images/annotations are not redistributed.')
Set-Content -LiteralPath $mdFull -Value $md.ToString().TrimEnd() -Encoding utf8
Write-Output "SEAM_COMPARISON_JSON=$jsonFull"
Write-Output "SEAM_COMPARISON_MARKDOWN=$mdFull"
