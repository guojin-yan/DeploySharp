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
function Get-Levenshtein([string]$First,[string]$Second) {
    $a=@($First.ToCharArray());$b=@($Second.ToCharArray());$prev=0..$b.Count
    for($i=1;$i -le $a.Count;$i++){$cur=New-Object int[] ($b.Count+1);$cur[0]=$i;for($j=1;$j -le $b.Count;$j++){$cost=if($a[$i-1]-eq $b[$j-1]){0}else{1};$cur[$j]=[Math]::Min([Math]::Min($cur[$j-1]+1,$prev[$j]+1),$prev[$j-1]+$cost)};$prev=$cur};$prev[$b.Count]
}
function Get-Cer([string]$Expected,[string]$Actual){if($Expected.Length -eq 0){if($Actual.Length -eq 0){return 0.0}else{return 1.0}};return (Get-Levenshtein $Expected $Actual)/[double]$Expected.Length}
function Get-Wer([string]$Expected,[string]$Actual){$e=@($Expected.Trim()-split '\s+'|?{$_ -ne ''});$a=@($Actual.Trim()-split '\s+'|?{$_ -ne ''});if($e.Count -eq 0){if($a.Count -eq 0){return 0.0}else{return 1.0}};return (Get-Levenshtein ([string]::Join("`n",$e)) ([string]::Join("`n",$a)))/[double]$e.Count}
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
        $out.Add([ordered]@{backend=$Name;imageId=[string]$c.imageId;paragraphIndex=[int]$c.paragraphIndex;lineIndex=[int]$c.lineIndex;expectedCharacters=$expected.Length;expectedTextSha256=(Hash-Text $expected);projectedWidth=[int]$c.projectedWidth;matched=$matched;boundingBoxIoU=[Math]::Round($bestIou,6);predictionSourceIndex=if($matched){[int]$best.SourceIndex}else{$null};recognizedCharacters=$actual.Length;recognizedTextSha256=(Hash-Text $actual);characterErrorRate=if($matched){Get-Cer $expected $actual}else{$null};wordErrorRate=if($matched){Get-Wer $expected $actual}else{$null};naturalWidth=if($matched){[int]$best.RecognitionWidth.NaturalWidth}else{$null};windowCount=if($matched){[int]$best.RecognitionWidth.WindowCount}else{0};seamUncertainCount=@($windows|?{[bool]$_.SeamUncertain}).Count;windowSourceIndices=@($windows|%{[int]$_.Recognition.SourceRegionIndex});allocatedBytes=if($null -ne $rr){[int64]$rr.pipeline_process_allocated_bytes}else{$null};totalMs=if($null -ne $rr){[double]$rr.total_ms}else{$null}})
    };@($out)
}
$annFull=(Resolve-Path $AnnotationsPath).Path;$candidateFull=(Resolve-Path $CandidateReportPath).Path;$annotations=Read-GzipJson $annFull;$candidates=Get-Content -Raw $candidateFull|ConvertFrom-Json
$ort=Read-Backend 'onnxruntime-cpu' (Resolve-Path $OrtRunRoot).Path $annotations $candidates;$ov=Read-Backend 'openvino-cpu' (Resolve-Path $OpenVinoRunRoot).Path $annotations $candidates;$all=@($ort+$ov)
$groups=@($all|Group-Object {"$($_.imageId)|$($_.paragraphIndex)|$($_.lineIndex)"});$parity=@($groups|%{$r=@($_.Group);[ordered]@{key=$_.Name;textMatch=($r.Count -eq 2 -and $r[0].recognizedTextSha256 -eq $r[1].recognizedTextSha256);ortMatched=[bool]($r|? backend -eq 'onnxruntime-cpu'|select -First 1).matched;openVinoMatched=[bool]($r|? backend -eq 'openvino-cpu'|select -First 1).matched}})
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
$result=[ordered]@{schemaVersion='deploysharp-paddleocr-hiertext-natural-wide-a2-v6-small-v1';generatedUtc=[DateTimeOffset]::UtcNow.ToString('o');source=[ordered]@{annotationPath=$annFull;annotationSha256=(Get-FileHash $annFull -Algorithm SHA256).Hash.ToLowerInvariant();candidateReportPath=$candidateFull;candidateReportSha256=(Get-FileHash $candidateFull -Algorithm SHA256).Hash.ToLowerInvariant();sourceRevision=[string]$candidates.sourceRevision;annotationVersion=[string]$candidates.annotationVersion};model='paddleocr/ppocrv6/small';protocol=[ordered]@{overflowMode='SlidingWindow';overlapRatio=.2;maximumWindowsPerRegion=32;batchSize=16;inferenceChannels=2;boundingBoxIoUThreshold=$MinimumBoundingBoxIoU;warmup=1;iterations=1;pageRuns=9};imageCount=@($candidates.candidates|%{[string]$_.imageId}|sort -Unique).Count;candidateLineCount=@($candidates.candidates).Count;backends=$summary;crossBackend=[ordered]@{pairCount=$groups.Count;textMatchCount=@($parity|? textMatch).Count;textMismatchCount=@($parity|?{-not $_.textMatch}).Count;allMatchedPairCount=@($parity|?{$_.ortMatched -and $_.openVinoMatched}).Count};rows=$all;boundary='Natural HierText lines selected by projected width >= 3200 at target height 48. This is a real source-linked geometry/SlidingWindow execution slice, not a natural 3200-character line benchmark: the longest annotated candidate is 182 characters. Matching uses axis-aligned bounding-box IoU as a conservative association diagnostic, not the official polygon metric. CER/WER and cross-backend text agreement are reported for matched candidates only; managed allocation is not a process RSS peak. This does not close A2, dataset accuracy, or a formal 5+50 performance gate.'}
$jsonFull=[IO.Path]::GetFullPath($OutputJsonPath);$mdFull=[IO.Path]::GetFullPath($OutputMarkdownPath);$null=New-Item -ItemType Directory -Force (Split-Path $jsonFull);$result|ConvertTo-Json -Depth 15|Set-Content $jsonFull -Encoding utf8
$md=[Text.StringBuilder]::new();[void]$md.AppendLine('# HierText natural wide-line A2 execution slice — PP-OCRv6 Small (2026-10-17)');[void]$md.AppendLine();[void]$md.AppendLine('This report runs the nine source-linked HierText images whose annotated lines have projected recognition width ≥3,200 at target height 48. It exercises real SlidingWindow execution and source provenance; it does **not** close the natural continuous 3,200-character gate because the longest annotated candidate is 182 characters.');[void]$md.AppendLine();[void]$md.AppendLine("Protocol: SlidingWindow overlap $($result.protocol.overlapRatio), maximum windows $($result.protocol.maximumWindowsPerRegion), batch $($result.protocol.batchSize), stage channels $($result.protocol.inferenceChannels), association bbox IoU threshold $($result.protocol.boundingBoxIoUThreshold), 1 warm-up + 1 measured page run.");[void]$md.AppendLine();[void]$md.AppendLine('| Backend | Candidate lines | Matched | Match rate | Mean bbox IoU | Mean CER | Mean WER | Total windows | Max width | Max managed allocation | P50/P95 page ms |');[void]$md.AppendLine('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |');foreach($s in $summary){[void]$md.AppendLine("| $($s.backend) | $($s.candidateCount) | $($s.matchedCount) | $([Math]::Round($s.matchRate*100,2))% | $([Math]::Round([double]$s.meanBoundingBoxIoU,4)) | $([Math]::Round([double]$s.meanCharacterErrorRate*100,2))% | $([Math]::Round([double]$s.meanWordErrorRate*100,2))% | $($s.totalWindows) | $($s.maximumRecognitionWidth) | $($s.maximumManagedAllocationBytes) | $([Math]::Round([double]$s.p50TotalMs,2)) / $([Math]::Round([double]$s.p95TotalMs,2)) |")};[void]$md.AppendLine();[void]$md.AppendLine("Cross-backend candidate text matches: **$($result.crossBackend.textMatchCount)/$($result.crossBackend.pairCount)**; mismatches: **$($result.crossBackend.textMismatchCount)**. Both backends matched the same candidate geometry in $($result.crossBackend.allMatchedPairCount)/$($result.crossBackend.pairCount) pairs.");[void]$md.AppendLine();[void]$md.AppendLine('The report uses bounding-box IoU only for candidate association and records managed allocation rather than RSS. CER/WER are not official HierText scores because this selection is geometry-based and detector output is compared to individual line annotations. The natural continuous ≥3,200-character A2 gate, formal 5+50 performance, and broader quality thresholds remain open.');Set-Content $mdFull $md.ToString().TrimEnd() -Encoding utf8
Write-Host "HIERtext_A2_EXECUTION_JSON=$jsonFull";Write-Host "HIERtext_A2_EXECUTION_MARKDOWN=$mdFull";Write-Host 'HIERtext_A2_EXECUTION_STATUS=pass'
