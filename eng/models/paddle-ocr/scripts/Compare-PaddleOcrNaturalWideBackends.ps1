[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReferenceReportPath,
    [Parameter(Mandatory)][string]$CandidateReportPath,
    [ValidateSet('onnxruntime', 'openvino', 'opencv-dnn', 'tensorrt')][string]$ReferenceBackend = 'onnxruntime',
    [Parameter(Mandatory)][ValidateSet('onnxruntime', 'openvino', 'opencv-dnn', 'tensorrt')][string]$CandidateBackend,
    [Parameter(Mandatory)][string]$OutputJsonPath,
    [Parameter(Mandatory)][string]$OutputMarkdownPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($ReferenceBackend -ceq $CandidateBackend) { throw 'A backend comparison requires two distinct backends.' }
function Assert-Same($Expected, $Actual, [string]$Context) {
    if (($Expected | ConvertTo-Json -Depth 45 -Compress) -cne ($Actual | ConvertTo-Json -Depth 45 -Compress)) { throw "Incomparable reports: $Context" }
}
function Same($Left, $Right) {
    ($Left | ConvertTo-Json -Depth 45 -Compress) -ceq ($Right | ConvertTo-Json -Depth 45 -Compress)
}
function Index-Backend($Report, [string]$Backend, [int]$ExpectedRows) {
    $index = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    if (@($Report.runtimes | Where-Object backend -CEQ $Backend).Count -ne 1) { throw "Expected exactly one runtime entry for $Backend" }
    foreach ($row in @($Report.rows | Where-Object Backend -CEQ $Backend)) { $index.Add("$($row.Model)/$($row.Mode)/$($row.Key)", $row) }
    if ($index.Count -ne $ExpectedRows) { throw "Expected $ExpectedRows unique model/mode/line rows for $Backend" }
    return ,$index
}
function Evidence([string]$Path, $Report, [string]$Backend) {
    [ordered]@{ fileName = [IO.Path]::GetFileName($Path); sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant();
        sourceRevision = $Report.sourceRevision; testAssemblySha256 = $Report.testAssemblySha256;
        visualAssemblySha256 = $Report.visualAssemblySha256; runtime = @($Report.runtimes | Where-Object backend -CEQ $Backend)[0] }
}
function Decisions($Windows) {
    @($Windows | ForEach-Object { [ordered]@{ index = $_.Index; removedPrefixTokens = $_.RemovedPrefixTokens;
        seamUncertain = $_.SeamUncertain; overlapEditDistance = $_.OverlapEditDistance } })
}
$reference = Get-Content -LiteralPath $ReferenceReportPath -Raw | ConvertFrom-Json -Depth 60
$candidate = Get-Content -LiteralPath $CandidateReportPath -Raw | ConvertFrom-Json -Depth 60
foreach ($field in @('dataset', 'models', 'visualAssemblySha256', 'environment')) { Assert-Same $reference.$field $candidate.$field $field }
$sourceLineCount = @($reference.dataset.lines).Count
if ($sourceLineCount -lt 1) { throw 'A backend comparison requires labelled source lines.' }
$expectedRows = $sourceLineCount * 4
foreach ($field in @('path', 'maximumWidthPixels', 'targetHeight', 'overlapRatio', 'batchSize', 'sessions', 'metrics', 'qualityStrata', 'measurements')) {
    Assert-Same $reference.protocol.$field $candidate.protocol.$field "protocol/$field"
}
Assert-Same 3200 $candidate.protocol.maximumWidthPixels 'required width'
Assert-Same 48 $candidate.protocol.targetHeight 'required height'
$left = Index-Backend $reference $ReferenceBackend $expectedRows
$right = Index-Backend $candidate $CandidateBackend $expectedRows
$rawWindowTextMatched = 0; $rawWindowClassesMatched = 0; $windowsCompared = 0
$rows = @(foreach ($key in @($left.Keys | Sort-Object -CaseSensitive)) {
    if (!$right.ContainsKey($key)) { throw "Missing candidate key: $key" }
    $a = $left[$key]; $b = $right[$key]
    foreach ($field in @('SourceIndex', 'QualityStratum', 'NaturalWidth', 'ExpectedTextSha256')) { Assert-Same $a.$field $b.$field "$key/$field" }
    $aw = @($a.Windows); $bw = @($b.Windows)
    Assert-Same $aw.Count $bw.Count "$key/window count"
    for ($i = 0; $i -lt $aw.Count; $i++) {
        foreach ($field in @('Index', 'Start', 'End', 'naturalWidth', 'targetWidth', 'tensorWidth')) { Assert-Same $aw[$i].$field $bw[$i].$field "$key/window $i/$field" }
        if ($aw[$i].textSha256 -ceq $bw[$i].textSha256) { $rawWindowTextMatched++ }
        if ($aw[$i].emittedClassSha256 -ceq $bw[$i].emittedClassSha256) { $rawWindowClassesMatched++ }
        $windowsCompared++
    }
    [ordered]@{ key = $key; stratum = $a.QualityStratum; expectedTextSha256 = $a.ExpectedTextSha256;
        referenceTextSha256 = $a.TextSha256; candidateTextSha256 = $b.TextSha256; textMatched = $a.TextSha256 -ceq $b.TextSha256;
        emittedClassesMatched = $a.EmittedClassSha256 -ceq $b.EmittedClassSha256;
        metricsMatched = (Same $a.Raw $b.Raw) -and (Same $a.Lowercase $b.Lowercase);
        seamDecisionsMatched = (Same (Decisions $aw) (Decisions $bw)) -and $a.RemovedPrefixTokens -eq $b.RemovedPrefixTokens -and $a.UncertainSeams -eq $b.UncertainSeams;
        windows = $aw.Count; referenceCharacterEdits = $a.Raw.CharacterEditDistance; candidateCharacterEdits = $b.Raw.CharacterEditDistance;
        referenceWordEdits = $a.Raw.WordEditDistance; candidateWordEdits = $b.Raw.WordEditDistance }
})
$validation = [ordered]@{ sourceModelDictionaryAndGeometryMatched = $true; sameVisualAssembly = $true; rowsCompared = $rows.Count;
    textMatched = @($rows | Where-Object textMatched -EQ $true).Count;
    emittedClassesMatched = @($rows | Where-Object emittedClassesMatched -EQ $true).Count;
    metricsMatched = @($rows | Where-Object metricsMatched -EQ $true).Count;
    seamDecisionsMatched = @($rows | Where-Object seamDecisionsMatched -EQ $true).Count;
    windowsCompared = $windowsCompared; rawWindowTextMatched = $rawWindowTextMatched; rawWindowClassesMatched = $rawWindowClassesMatched }
$matched = $validation.textMatched -eq $expectedRows -and $validation.emittedClassesMatched -eq $expectedRows -and $validation.metricsMatched -eq $expectedRows -and $validation.seamDecisionsMatched -eq $expectedRows `
    -and $rawWindowTextMatched -eq $windowsCompared -and $rawWindowClassesMatched -eq $windowsCompared
$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-natural-wide-backend-comparison-v1'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('o');
    status = $(if ($matched) { 'decision-parity' } else { 'observed-differences' });
    referenceBackend = $ReferenceBackend; candidateBackend = $CandidateBackend;
    referenceEvidence = Evidence $ReferenceReportPath $reference $ReferenceBackend;
    candidateEvidence = Evidence $CandidateReportPath $candidate $CandidateBackend;
    dataset = $candidate.dataset; models = $candidate.models; protocol = $candidate.protocol; validation = $validation;
    candidateEngines = @($candidate.engines); gpuObservation = $candidate.gpuObservation; rows = $rows;
    boundary = "Same $sourceLineCount HierText $($candidate.dataset.split) lines / $(@($candidate.dataset.lines.ImageId | Sort-Object -Unique).Count) source images. Same DLL, model/dictionary/source identities and preprocessing/window geometry required. Comparison covers text hashes, emitted classes, CER/WER and seam decisions, not native tensors/confidences bitwise parity, page accuracy, performance, or other exact model/backend combinations. DET/CLS bypassed; CPU crop/CTC/merge also used with TensorRT. A2 stays open."
}
$jsonFull = [IO.Path]::GetFullPath($OutputJsonPath); $mdFull = [IO.Path]::GetFullPath($OutputMarkdownPath)
foreach ($path in @($jsonFull, $mdFull)) {
    if ($path -ieq [IO.Path]::GetFullPath($ReferenceReportPath) -or $path -ieq [IO.Path]::GetFullPath($CandidateReportPath)) { throw 'An output must not overwrite an input report.' }
    $null = New-Item -ItemType Directory -Force -Path (Split-Path $path)
}
$summary | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath $jsonFull -Encoding utf8
$md = [Text.StringBuilder]::new()
[void]$md.AppendLine('# Natural wide-line backend comparison: ' + $ReferenceBackend + ' / ' + $CandidateBackend + ' (2026-10-23)')
[void]$md.AppendLine()
[void]$md.AppendLine("Status: **$($summary.status)**. Both native runs use the same production Visual DLL and pinned source images, labels, model and dictionary hashes. Each backend executes Small/Medium x Clamp/SlidingWindow x $sourceLineCount labelled quadrilaterals ($expectedRows calls). DET/CLS are bypassed, height is 48, maximum width is 3,200, overlap is 0.2 and batch/Session count is one. This comparison explicitly joins two real native reports; one-backend self-comparisons are not counted as backend pairs.")
[void]$md.AppendLine()
[void]$md.AppendLine('| Compared contract | Matched / compared |')
[void]$md.AppendLine('| --- | ---: |')
foreach ($field in @('textMatched', 'emittedClassesMatched', 'metricsMatched', 'seamDecisionsMatched')) {
    [void]$md.AppendLine("| $field | $($validation[$field]) / $expectedRows |")
}
[void]$md.AppendLine("| Raw window text | $rawWindowTextMatched / $windowsCompared |")
[void]$md.AppendLine("| Raw window emitted classes | $rawWindowClassesMatched / $windowsCompared |")
[void]$md.AppendLine()
[void]$md.AppendLine('Native runtimes: ' + $summary.referenceEvidence.runtime.nativeVersion + '; ' + $summary.candidateEvidence.runtime.nativeVersion + '. The [machine report](' + [IO.Path]::GetFileName($jsonFull) + ') includes per-row hashes/metrics, exact input report SHA values, DLL identities and any freshly built engine identities. No raw annotation text or image is copied into this report.')
[void]$md.AppendLine()
if ($CandidateBackend -ceq 'tensorrt') {
    [void]$md.AppendLine('TensorRT engines are built through the library with runtime-default precision, optimization level 0 and 256 MiB workspace; dynamic input profile [1,3,48,minimumWidth] / [1,3,48,320] / [1,3,48,3200]. CPU crop/CTC/merge is shared with the reference. GPU snapshots capture two instants only; concurrent workloads and uncontrolled clocks prevent performance interpretation.')
    [void]$md.AppendLine()
}
[void]$md.AppendLine($summary.boundary)
[void]$md.AppendLine()
[void]$md.AppendLine('Reproduce with `Compare-PaddleOcrNaturalWideBackends.ps1 -ReferenceReportPath <real-reference.json> -CandidateReportPath <real-candidate.json> -ReferenceBackend ' + $ReferenceBackend + ' -CandidateBackend ' + $CandidateBackend + ' -OutputJsonPath <new-summary.json> -OutputMarkdownPath <new-summary.md>`. Identity, protocol, duplicate-key or geometry mismatch rejects comparison. Prediction differences are reported as differences rather than hidden or converted to a pass.')
Set-Content -LiteralPath $mdFull -Value $md.ToString().TrimEnd() -Encoding utf8
Write-Output "NATURAL_WIDE_BACKEND_STATUS=$($summary.status)"
Write-Output "NATURAL_WIDE_BACKEND_JSON=$jsonFull"
