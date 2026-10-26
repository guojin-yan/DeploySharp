[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..\..'))
$root = Join-Path $repo ('artifacts/natural-wide-comparison-contracts/' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Force -Path $root
$script = Join-Path $PSScriptRoot '../Compare-PaddleOcrNaturalWideBackends.ps1'
$referencePath = Join-Path $root 'reference.json'; $candidatePath = Join-Path $root 'candidate.json'
function Report([string]$Backend, [int]$LineCount = 13) {
    $rows = @(foreach ($model in @('small', 'medium')) { foreach ($mode in @('Clamp', 'SlidingWindow')) { foreach ($i in 0..($LineCount - 1)) {
        [ordered]@{ Backend = $Backend; Model = $model; Mode = $mode; Key = "image/$i"; SourceIndex = $i; QualityStratum = 'legible-height-at-least-2px';
            NaturalWidth = 3500; ExpectedTextSha256 = 'gt'; TextSha256 = 'prediction'; EmittedClassSha256 = 'classes';
            Raw = @{ CharacterEditDistance = 1; WordEditDistance = 1 }; Lowercase = @{ CharacterEditDistance = 1; WordEditDistance = 1 };
            Windows = @(@{ Index = 0; Start = 0; End = 1; naturalWidth = 3500; targetWidth = 3200; tensorWidth = 3200;
                RemovedPrefixTokens = 0; SeamUncertain = $false; OverlapEditDistance = 0; textSha256 = 'window'; emittedClassSha256 = 'window-classes' });
            RemovedPrefixTokens = 0; UncertainSeams = 0 }
    } } })
    [ordered]@{ sourceRevision = 'synthetic-contract-only'; testAssemblySha256 = 'test'; visualAssemblySha256 = 'production';
        environment = @{ framework = 'contract' }; dataset = @{ revision = 'fixed'; split = 'validation'; lines = @(0..($LineCount - 1) | ForEach-Object { @{ ImageId = "image-$_"; imageSha256 = "image-$_" } }) };
        models = @(@{ model = 'small'; sha = 'small' }, @{ model = 'medium'; sha = 'medium' });
        protocol = @{ path = 'GT'; maximumWidthPixels = 3200; targetHeight = 48; overlapRatio = .2; batchSize = 1; sessions = 1;
            metrics = 'ordinal'; qualityStrata = 'fixed'; measurements = 'untimed' };
        runtimes = @(@{ backend = $Backend; nativeVersion = 'synthetic' }); rows = $rows; engines = @(); gpuObservation = $null }
}
function Save($Value, [string]$Path) { $Value | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Invoke-Comparison([string]$Backend = 'opencv-dnn') {
    & $script -ReferenceReportPath $referencePath -CandidateReportPath $candidatePath -CandidateBackend $Backend `
        -OutputJsonPath (Join-Path $root 'summary.json') -OutputMarkdownPath (Join-Path $root 'summary.md') | Out-Null
}
function Reject([string]$Reason) {
    $caught = $false
    try { Invoke-Comparison } catch { $caught = $true }
    if (!$caught) { throw "Failed to reject $Reason" }
}
Save (Report 'onnxruntime') $referencePath
Save (Report 'opencv-dnn') $candidatePath
Invoke-Comparison
$result = Get-Content (Join-Path $root 'summary.json') -Raw | ConvertFrom-Json
if ($result.status -cne 'decision-parity' -or $result.validation.rowsCompared -ne 52) { throw 'Exact synthetic reference failed' }
$changed = Report 'opencv-dnn'; $changed.rows[0].TextSha256 = 'different'; Save $changed $candidatePath
Invoke-Comparison
$result = Get-Content (Join-Path $root 'summary.json') -Raw | ConvertFrom-Json
if ($result.status -cne 'observed-differences' -or $result.validation.textMatched -ne 51) { throw 'Prediction mismatch was hidden' }
$changed = Report 'opencv-dnn'; $changed.dataset.revision = 'other'; Save $changed $candidatePath; Reject 'different dataset'
$changed = Report 'opencv-dnn'; $changed.visualAssemblySha256 = 'other'; Save $changed $candidatePath; Reject 'different production DLL'
$changed = Report 'opencv-dnn'; $changed.rows += $changed.rows[0]; Save $changed $candidatePath; Reject 'duplicate row'
$changed = Report 'opencv-dnn'; $changed.rows[0].Windows[0].Start = .01; Save $changed $candidatePath; Reject 'different crop/window geometry'
$reference = Report 'onnxruntime' 17; $reference.dataset.split = 'test'; Save $reference $referencePath
$candidate = Report 'opencv-dnn' 17; $candidate.dataset.split = 'test'; Save $candidate $candidatePath
Invoke-Comparison
$result = Get-Content (Join-Path $root 'summary.json') -Raw | ConvertFrom-Json
if ($result.status -cne 'decision-parity' -or $result.validation.rowsCompared -ne 68) { throw 'Variable-size independent split failed' }
$caught = $false
try { Invoke-Comparison 'onnxruntime' } catch { $caught = $true }
if (!$caught) { throw 'A single-backend self-comparison must not count as backend parity' }
Write-Output 'Natural wide-line backend comparison contracts: 8 passed (synthetic fixtures, no inference claim).'
