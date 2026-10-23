[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AnnotationsPath,
    [Parameter(Mandatory=$true)][string]$CandidateReportPath,
    [Parameter(Mandatory=$true)][string]$OrtRunRoot,
    [Parameter(Mandatory=$true)][string]$OpenVinoRunRoot,
    [string]$OutputJsonPath = (Join-Path $PSScriptRoot '..\verification\hiertext-natural-wide-a2-v6-small-20261017.json'),
    [string]$OutputMarkdownPath = (Join-Path $PSScriptRoot '..\verification\hiertext-natural-wide-a2-v6-small-20261017.md'),
    [double]$MinimumBoundingBoxIoU = 0.30
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OcrTextAccuracy.ps1')
function Read-GzipJson([string]$Path) {
    $fs=[IO.File]::OpenRead($Path); try {$gz=[IO.Compression.GzipStream]::new($fs,[IO.Compression.CompressionMode]::Decompress); try {$sr=[IO.StreamReader]::new($gz); try {return ($sr.ReadToEnd()|ConvertFrom-Json)} finally {$sr.Dispose()}} finally {$gz.Dispose()}} finally {$fs.Dispose()}
}
function Hash-Text([string]$Value) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant() }
function Get-Bounds($Vertices) {
    $points=@(); foreach($p in @($Vertices)){if($null -ne $p.x -and $null -ne $p.y){$points += [pscustomobject]@{x=[double]$p.x;y=[double]$p.y}}}
    if($points.Count -eq 0){return $null}; $xs=@($points|%{$_.x});$ys=@($points|%{$_.y})
    [pscustomobject]@{left=($xs|Measure-Object -Minimum).Minimum;top=($ys|Measure-Object -Minimum).Minimum;right=($xs|Measure-Object -Maximum).Maximum;bottom=($ys|Measure-Object -Maximum).Maximum}
}
function Get-FlatBounds($Values) {
    $v=@($Values);$xs=@();$ys=@();for($i=0;$i+1 -lt $v.Count;$i+=2){$xs += [double]$v[$i];$ys += [double]$v[$i+1]};if($xs.Count -eq 0){return $null};[pscustomobject]@{left=($xs|Measure-Object -Minimum).Minimum;top=($ys|Measure-Object -Minimum).Minimum;right=($xs|Measure-Object -Maximum).Maximum;bottom=($ys|Measure-Object -Maximum).Maximum}
}
function Get-Iou($A,$B) {
    if($null -eq $A -or $null -eq $B){return 0.0};$left=[Math]::Max($A.left,$B.left);$top=[Math]::Max($A.top,$B.top);$right=[Math]::Min($A.right,$B.right);$bottom=[Math]::Min($A.bottom,$B.bottom)
    $inter=[Math]::Max(0.0,$right-$left)*[Math]::Max(0.0,$bottom-$top);$areaA=[Math]::Max(0.0,$A.right-$A.left)*[Math]::Max(0.0,$A.bottom-$A.top);$areaB=[Math]::Max(0.0,$B.right-$B.left)*[Math]::Max(0.0,$B.bottom-$B.top);$u=$areaA+$areaB-$inter
    if($u -le 0){return 0.0};return $inter/$u
}
function Read-RunRows([string]$Root){$rows=@{};foreach($csv in Get-ChildItem (Join-Path $Root 'csv') -Filter '*.csv' -File){$row=Import-Csv $csv.FullName|select -First 1;if($null -ne $row){$rows[$csv.BaseName]=$row}};$rows}
function Read-Backend([string]$Name,[string]$Root,$Annotations,$Candidates) {
    $run=Read-RunRows $Root;$out=[Collections.Generic.List[object]]::new()
    foreach($c in @($Candidates.candidates)) {
        $ann=$Annotations.annotations|?{[string]$_.image_id -eq [string]$c.imageId}|select -First 1;if($null -eq $ann){throw "Missing annotation: $($c.imageId)"}
        $line=$ann.paragraphs[[int]$c.paragraphIndex].lines[[int]$c.lineIndex]
        $rp=Get-ChildItem (Join-Path $Root 'raw' $c.imageId) -Filter '*.width.json' -File|select -First 1;if($null -eq $rp){throw "Missing width report: $($c.imageId)"};$j=Get-Content -Raw $rp.FullName|ConvertFrom-Json
        $gb=Get-FlatBounds $c.vertices;$best=$null;$bestIou=0.0
        foreach($r in @($j.Regions)){$iou=Get-Iou $gb (Get-Bounds @($r.Polygon|%{[pscustomobject]@{x=$_.X;y=$_.Y}}));if($iou -gt $bestIou){$bestIou=$iou;$best=$r}}
        $matched=$bestIou -ge $MinimumBoundingBoxIoU;$expected=[string]$line.text;$actual=if($matched){[string]$best.Text}else{''};$windows=if($matched){@($best.RecognitionWindows)}else{@()};$rr=if($run.ContainsKey($c.imageId)){$run[$c.imageId]}else{$null}
        $metrics=Get-OcrTextAccuracy -Reference $expected -Hypothesis $actual
        $bbox=Get-FlatBounds $c.vertices;$height=$bbox.bottom-$bbox.top
        $out.Add([ordered]@{backend=$Name;imageId=[string]$c.imageId;paragraphIndex=[int]$c.paragraphIndex;lineIndex=[int]$c.lineIndex;expectedCharacters=$metrics.referenceCharacters;expectedTextSha256=(Hash-Text $expected);projectedWidth=[int]$c.projectedWidth;matched=$matched;boundingBoxIoU=[Math]::Round($bestIou,6);predictionSourceIndex=if($matched){[int]$best.SourceIndex}else{$null};recognizedCharacters=$metrics.hypothesisCharacters;recognizedTextSha256=(Hash-Text $actual);characterErrorRate=if($matched){$metrics.characterErrorRate}else{$null};wordErrorRate=if($matched){$metrics.wordErrorRate}else{$null};characterEditDistance=if($matched){$metrics.characterEditDistance}else{$null};referenceWords=$metrics.referenceWords;wordEditDistance=if($matched){$metrics.wordEditDistance}else{$null};officialLegible=[bool]$line.legible;sourceBoundingBoxHeight=$height;qualityStratum=if(-not $line.legible){'official-illegible'}elseif($height -lt 2){'one-pixel-height-diagnostic'}else{'legible-height-at-least-2px'};widthReportSha256=(Get-FileHash -LiteralPath $rp.FullName -Algorithm SHA256).Hash.ToLowerInvariant();configuredMaximumWidthPixels=[int]$j.Crop.MaximumWidth;naturalWidth=if($matched){[int]$best.RecognitionWidth.NaturalWidth}else{$null};windowCount=if($matched){[int]$best.RecognitionWidth.WindowCount}else{0};seamUncertainCount=@($windows|?{[bool]$_.SeamUncertain}).Count;windowSourceIndices=@($windows|%{[int]$_.Recognition.SourceRegionIndex});allocatedBytes=if($null -ne $rr){[int64]$rr.pipeline_process_allocated_bytes}else{$null};totalMs=if($null -ne $rr){[double]$rr.total_ms}else{$null}})
    };@($out)
}
$annFull=(Resolve-Path $AnnotationsPath).Path;$candidateFull=(Resolve-Path $CandidateReportPath).Path;$annotations=Read-GzipJson $annFull;$candidates=Get-Content -Raw $candidateFull|ConvertFrom-Json
$ort=Read-Backend 'onnxruntime-cpu' (Resolve-Path $OrtRunRoot).Path $annotations $candidates;$ov=Read-Backend 'openvino-cpu' (Resolve-Path $OpenVinoRunRoot).Path $annotations $candidates;$all=@($ort+$ov)
$groups=@($all|Group-Object {"$($_.imageId)|$($_.paragraphIndex)|$($_.lineIndex)"});$parity=@($groups|%{$r=@($_.Group);[ordered]@{key=$_.Name;textMatch=($r.Count -eq 2 -and $r[0].matched -and $r[1].matched -and $r[0].recognizedTextSha256 -eq $r[1].recognizedTextSha256);ortMatched=[bool]($r|? backend -eq 'onnxruntime-cpu'|select -First 1).matched;openVinoMatched=[bool]($r|? backend -eq 'openvino-cpu'|select -First 1).matched}})
$summary=[Collections.Generic.List[object]]::new();foreach($backendName in @('onnxruntime-cpu','openvino-cpu')){
    $backendRows=@($all|Where-Object {$_.backend -eq $backendName});
    $matchedRows=@($backendRows|Where-Object {$_.matched -eq $true});
    $iouValues=@($matchedRows|ForEach-Object {[double]$_.boundingBoxIoU});
    $cerValues=@($matchedRows|ForEach-Object {[double]$_.characterErrorRate});
    $werValues=@($matchedRows|ForEach-Object {[double]$_.wordErrorRate});
    $windowValues=@($matchedRows|ForEach-Object {[int]$_.windowCount});
    $widthValues=@($matchedRows|ForEach-Object {[int]$_.naturalWidth});
    $allocationValues=@($backendRows|Where-Object {$null -ne $_.allocatedBytes}|ForEach-Object {[int64]$_.allocatedBytes});
    $iou=if($iouValues.Count -eq 0){$null}else{($iouValues|Measure-Object -Average).Average};
    $cer=if($cerValues.Count -eq 0){$null}else{($cerValues|Measure-Object -Average).Average};
    $wer=if($werValues.Count -eq 0){$null}else{($werValues|Measure-Object -Average).Average};
    $wins=if($windowValues.Count -eq 0){0}else{[int](($windowValues|Measure-Object -Sum).Sum)};
    $maxWins=if($windowValues.Count -eq 0){$null}else{($windowValues|Measure-Object -Maximum).Maximum};
    $maxWidth=if($widthValues.Count -eq 0){$null}else{($widthValues|Measure-Object -Maximum).Maximum};
    $maxAlloc=if($allocationValues.Count -eq 0){$null}else{($allocationValues|Measure-Object -Maximum).Maximum};
    $times=@($backendRows|Where-Object {$null -ne $_.totalMs}|ForEach-Object {[double]$_.totalMs}|Sort-Object);
    $p50=if($times.Count -eq 0){$null}else{$times[[Math]::Max(0,[int][Math]::Ceiling($times.Count/2.0)-1)]};
    $p95=if($times.Count -eq 0){$null}else{$times[[Math]::Max(0,[int][Math]::Ceiling($times.Count*.95)-1)]};
    $summary.Add([ordered]@{backend=$backendName;candidateCount=$backendRows.Count;matchedCount=$matchedRows.Count;unmatchedCount=$backendRows.Count-$matchedRows.Count;matchRate=if($backendRows.Count -eq 0){0}else{$matchedRows.Count/[double]$backendRows.Count};meanBoundingBoxIoU=$iou;meanCharacterErrorRate=$cer;meanWordErrorRate=$wer;totalWindows=$wins;maximumWindowsForCandidate=$maxWins;maximumRecognitionWidth=$maxWidth;maximumManagedAllocationBytes=$maxAlloc;p50TotalMs=$p50;p95TotalMs=$p95})
}
$widthLimits=@($all|ForEach-Object {$_.configuredMaximumWidthPixels}|Sort-Object -Unique)
if($widthLimits.Count -ne 1 -or $widthLimits[0] -le 0){throw 'Raw reports must declare the same positive maximum recognition width.'}
$metricCorrection=[ordered]@{
    date='2026-10-23'
    reason='v1 incorrectly divided character edit distance by reference word count to report WER. v2 uses ordinal Unicode-scalar CER and whitespace-token WER. Empty predictions from unmatched detections are no longer counted as cross-backend text matches.'
    previousMeanWordErrorRate=2.9119942557442555
    previousCrossBackendTextMatchCountIncludingFiveUnmatchedEmptyPairs=13
    measurementsRerun=$false
    gateCorrection='The original A2 requirement is normalized recognition width >3200 pixels, not a 3200-character line. This legacy execution used a 320-pixel window limit, not the library default 3200.'
}
$result=[ordered]@{schemaVersion='deploysharp-paddleocr-hiertext-natural-wide-a2-v6-small-v2';generatedUtc=[DateTimeOffset]::UtcNow.ToString('o');measurementDate='2026-10-17';metricCorrection=$metricCorrection;source=[ordered]@{annotationPath=$annFull;annotationSha256=(Get-FileHash $annFull -Algorithm SHA256).Hash.ToLowerInvariant();candidateReportPath=$candidateFull;candidateReportSha256=(Get-FileHash $candidateFull -Algorithm SHA256).Hash.ToLowerInvariant();sourceRevision=[string]$candidates.source.sourceRevision;annotationVersion=[string]$candidates.source.annotationVersion};model='paddleocr/ppocrv6/small';protocol=[ordered]@{overflowMode='SlidingWindow';overlapRatio=.2;maximumWindowsPerRegion=32;configuredMaximumWidthPixels=$widthLimits[0];batchSize=16;inferenceChannels=2;boundingBoxIoUThreshold=$MinimumBoundingBoxIoU;warmup=1;iterations=1;pageRuns=9;metrics='Ordinal Unicode-scalar CER and whitespace-token WER, no implicit normalization; macro mean of matched candidate rates, not corpus/micro rates.'};imageCount=@($candidates.candidates|%{[string]$_.imageId}|sort -Unique).Count;candidateLineCount=@($candidates.candidates).Count;backends=$summary;crossBackend=[ordered]@{pairCount=$groups.Count;textMatchCount=@($parity|? textMatch).Count;textMismatchCount=@($parity|?{$_.ortMatched -and $_.openVinoMatched -and -not $_.textMatch}).Count;allMatchedPairCount=@($parity|?{$_.ortMatched -and $_.openVinoMatched}).Count;unmatchedPairCount=@($parity|?{-not ($_.ortMatched -and $_.openVinoMatched)}).Count};rows=$all;boundary='Natural HierText lines selected by projected width >=3200 pixels at target height 48. A2 uses normalized pixel width, not character count. This legacy DET pipeline used a 320-pixel window cap and only associated 8/13 candidates. Matching uses axis-aligned bbox IoU, not the official polygon metric. Macro CER/WER includes all associated candidates without legibility filtering; row strata are explicit. Five unmatched pairs are excluded from text parity. Managed allocation is not RSS. This does not close A2, dataset accuracy or a formal 5+50 performance gate.'}
$jsonFull=[IO.Path]::GetFullPath($OutputJsonPath);$mdFull=[IO.Path]::GetFullPath($OutputMarkdownPath);$null=New-Item -ItemType Directory -Force (Split-Path $jsonFull);$result|ConvertTo-Json -Depth 15|Set-Content $jsonFull -Encoding utf8
$md=[Text.StringBuilder]::new()
[void]$md.AppendLine('# HierText natural wide-line A2 execution slice — PP-OCRv6 Small (2026-10-17)')
[void]$md.AppendLine()
[void]$md.AppendLine('Metric correction, 2026-10-23: the previous WER of 291.20% used character distance divided by word count and is invalid. The table below is recomputed from unchanged original predictions using actual word-token distance. No model execution or timing was repeated. The original A2 requirement is normalized recognition width **>3,200 pixels**, not 3,200 characters; previous character-count gate wording is superseded.')
[void]$md.AppendLine()
[void]$md.AppendLine("This report used nine source-linked images and 13 annotated geometry candidates. Actual maximum recognition window width was **$($result.protocol.configuredMaximumWidthPixels) pixels**, not the library default 3,200. Protocol: SlidingWindow overlap $($result.protocol.overlapRatio), maximum windows $($result.protocol.maximumWindowsPerRegion), batch $($result.protocol.batchSize), stage channels $($result.protocol.inferenceChannels), association bbox IoU threshold $($result.protocol.boundingBoxIoUThreshold), 1 warm-up + 1 measured page run.")
[void]$md.AppendLine()
[void]$md.AppendLine('| Backend | Candidate lines | Matched | Match rate | Mean bbox IoU | Mean CER | Corrected mean WER | Total windows | Max natural width | Max managed allocation | P50/P95 page ms |')
[void]$md.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($s in $summary){[void]$md.AppendLine("| $($s.backend) | $($s.candidateCount) | $($s.matchedCount) | $([Math]::Round($s.matchRate*100,2))% | $([Math]::Round([double]$s.meanBoundingBoxIoU,4)) | $([Math]::Round([double]$s.meanCharacterErrorRate*100,2))% | $([Math]::Round([double]$s.meanWordErrorRate*100,2))% | $($s.totalWindows) | $($s.maximumRecognitionWidth) | $($s.maximumManagedAllocationBytes) | $([Math]::Round([double]$s.p50TotalMs,2)) / $([Math]::Round([double]$s.p95TotalMs,2)) |")}
[void]$md.AppendLine()
[void]$md.AppendLine("Cross-backend text matches among associated candidates: **$($result.crossBackend.textMatchCount)/$($result.crossBackend.allMatchedPairCount)**; text mismatches: **$($result.crossBackend.textMismatchCount)**. **$($result.crossBackend.unmatchedPairCount)** candidate pairs are unmatched and excluded. The former 13/13 claim incorrectly included five pairs of empty placeholders.")
[void]$md.AppendLine()
[void]$md.AppendLine('CER/WER are macro means over associated candidates with ordinal, case-sensitive text and no implicit normalization. Official illegibility and one-pixel-height annotations remain in this historical association diagnostic and are explicitly identified in JSON; use the newer GT-quadrilateral report for stratified recognition quality. Bbox IoU is not the official polygon metric and managed allocation is not RSS. A2 repeat/single-character seam quality, additional model/backend evidence, formal 5+50 performance and general dataset accuracy remain open.')
Set-Content $mdFull $md.ToString().TrimEnd() -Encoding utf8
Write-Host "HIERtext_A2_EXECUTION_JSON=$jsonFull";Write-Host "HIERtext_A2_EXECUTION_MARKDOWN=$mdFull";Write-Host 'HIERtext_A2_EXECUTION_STATUS=pass'
