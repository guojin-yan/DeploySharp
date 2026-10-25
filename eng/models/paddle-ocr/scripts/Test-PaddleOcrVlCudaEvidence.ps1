[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OriginalReportPath,
    [Parameter(Mandatory)][string]$GroupedReportPath,
    [Parameter(Mandatory)][string]$ExportReportPath,
    [Parameter(Mandatory)][string]$FeatureReportPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ProfilesPath,
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-Json([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 80 }
function Assert-Same($Expected, $Actual, [string]$Label) {
    if (($Expected | ConvertTo-Json -Depth 60 -Compress) -cne ($Actual | ConvertTo-Json -Depth 60 -Compress)) { throw "Mismatch: $Label" }
}
function Evidence([string]$Path) {
    [ordered]@{ fileName = [IO.Path]::GetFileName($Path); sha256 = (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant() }
}
function One($Rows, [string]$Field, [string]$Value) {
    $matches = @($Rows | Where-Object { $_.$Field -ceq $Value })
    Assert-Same 1 $matches.Count "unique $Field=$Value"
    $matches[0]
}
function Modules($Report) {
    @($Report.nativeRuntime.modules | ForEach-Object {
        [ordered]@{ name = ($_.path -split '/')[-1]; sha256 = $_.sha256 }
    } | Sort-Object { $_.name })
}
$original = Read-Json $OriginalReportPath
$grouped = Read-Json $GroupedReportPath
$export = Read-Json $ExportReportPath
$features = Read-Json $FeatureReportPath
$pinned = Read-Json (Join-Path $PSScriptRoot '../verification/paddleocr-vl-openvino-source-released-linux-20261024.json')
Assert-Same 'failed' $original.status 'original all-task result'
Assert-Same 'six-task-cuda-managed-rgb-generation-exact' $grouped.status 'grouped all-task result'
Assert-Same 'exported-not-runtime-verified' $export.status 'preserved export-time report'
Assert-Same 'passed' $export.checker 'ONNX checker'
Assert-Same 'unchanged-float32' $export.precision 'graph precision'
Assert-Same 1 $export.headsPerGroup 'validated 6 GB head group size'
Assert-Same 'loop' $export.schedule 'sequential head execution'
Assert-Same 'explicit-carried-qkv' $export.loopInputBinding 'explicit outer attention inputs'
Assert-Same 16 $export.heads 'total heads'
Assert-Same 'float32 output alpha matching ORT MatMulScaleFusion' $export.scalePlacement 'preserved runtime scale arithmetic'
Assert-Same 27 @($export.blocks).Count 'all vision attention layers'
foreach ($block in $export.blocks) {
    Assert-Same 1 $block.headsPerGroup 'block group size'; Assert-Same 16 $block.groups 'groups per block'
    Assert-Same 'loop' $block.schedule 'sequential block execution'
    Assert-Same 0.1178511381149292 $block.float32ScoreAlpha 'original fused float32 alpha'
}
Assert-Same (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Optimize-PaddleOcrVlVisionHeads.py')).Hash.ToLowerInvariant() $export.scriptSha256 'optimizer source'
Assert-Same 'six-task-feature-parity-within-tolerance' $features.status 'real CPU feature comparison'
Assert-Same 'fresh subprocess per case and graph' $features.protocol.isolation 'feature isolation'
Assert-Same 0.001 $features.protocol.rtol 'unchanged feature relative tolerance'
Assert-Same 0.003 $features.protocol.atol 'unchanged feature absolute tolerance'
Assert-Same 6 @($features.cases).Count 'six real feature comparisons'
Assert-Same (Get-FileHash -LiteralPath $ExportReportPath).Hash.ToLowerInvariant() $features.derivativeReportSha256 'feature derivative identity'
Assert-Same (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Compare-PaddleOcrVlHeadGroupFeatures.py')).Hash.ToLowerInvariant() $features.scriptSha256 'feature comparer source'
foreach ($field in @('checkpointRevision', 'referenceSha256', 'processorFixtureManifestSha256')) {
    Assert-Same $pinned.$field $original.$field "original $field"
    Assert-Same $pinned.$field $grouped.$field "grouped $field"
}
Assert-Same $pinned.checkpointRevision $export.checkpointRevision 'export checkpoint'
Assert-Same $pinned.checkpointRevision $features.checkpointRevision 'feature checkpoint'
Assert-Same $pinned.referenceSha256 $features.referenceSha256 'feature inputs/reference'
Assert-Same $original.testSourceSha256 $grouped.testSourceSha256 'same CUDA harness'
Assert-Same (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot '../../../../tests/DeploySharp.Backend.OnnxRuntime.Tests/PaddleOcrVlCudaIntegrationTests.cs')).Hash.ToLowerInvariant() $grouped.testSourceSha256 'final test source'
Assert-Same $original.sourceCommit $grouped.sourceCommit 'same development baseline'
Assert-Same $original.assemblies $grouped.assemblies 'identical five assemblies'
Assert-Same 5 @($grouped.assemblies).Count 'assembly count'
Assert-Same '1.23.2' $grouped.nativeRuntime.version 'native ORT version'
Assert-Same '1.23.2' $original.nativeRuntime.version 'original native ORT version'
Assert-Same $original.environment $grouped.environment 'same execution configuration'
Assert-Same $original.execution $grouped.execution 'same execution protocol'
Assert-Same 'CUDAExecutionProvider' $grouped.environment.provider 'explicit CUDA selection'
Assert-Same '0' $grouped.environment.cudaProviderOptions.use_tf32 'TF32 disabled'
Assert-Same 'kSameAsRequested' $grouped.environment.cudaProviderOptions.arena_extend_strategy 'same-sized CUDA arena extension'
Assert-Same '1' $grouped.environment.cudaProviderOptions.use_ep_level_unified_stream 'EP unified stream'
Assert-Same 'gpu:0' $grouped.environment.visionRunConfigEntries.'memory.enable_memory_arena_shrinkage' 'opt-in vision arena shrink'
Assert-Same $original.resourcePolicy $grouped.resourcePolicy 'same process settings'
Assert-Same (Modules $original) (Modules $grouped) 'same loaded native binary content'
$sourceChanged = @()
foreach ($report in @($original, $grouped)) { Assert-Same 4 @($report.graphs).Count 'loaded graph count' }
foreach ($graphs in @($export.sourceGraphs, $export.graphs)) { Assert-Same 4 @($graphs).Count 'export graph count' }
foreach ($role in @('vision', 'embedding', 'decoder', 'head')) {
    $source = One $export.sourceGraphs role $role
    $target = One $export.graphs role $role
    $referenceGraph = One $pinned.graphs role $role
    $before = One $original.graphs role $role
    $after = One $grouped.graphs role $role
    Assert-Same $referenceGraph.sha256 $source.sha256 "pinned source $role"
    Assert-Same $source.sha256 $before.sha256 "original loaded $role"
    Assert-Same $target.sha256 $after.sha256 "derived loaded $role"
    foreach ($field in @('inputs', 'outputs')) { Assert-Same $before.$field $after.$field "$role public $field" }
    if ($source.sha256 -cne $target.sha256) { $sourceChanged += $role }
}
Assert-Same @('vision') $sourceChanged 'only vision changed'
$verifiedProfiles = 0
foreach ($report in @($original, $grouped)) {
    Assert-Same 6 @($report.cases).Count 'six distinct tasks attempted'
    Assert-Same 6 @($report.cases.id | Sort-Object -Unique).Count 'unique task IDs'
    Assert-Same 4 @($report.providerAttribution).Count 'four attribution profiles'
    foreach ($role in @('vision', 'embedding', 'decoder', 'head')) { $null = One $report.providerAttribution role $role }
    foreach ($profile in $report.providerAttribution) {
        if ($profile.kernelEventsByProvider.CUDAExecutionProvider -le 0) { throw 'No observed CUDA kernel events' }
        if (!$ProfilesPath) { continue }
        $path = Join-Path $ProfilesPath $profile.fileName
        Assert-Same (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() $profile.sha256 'raw attribution profile hash'
        $events = @(Read-Json $path | Where-Object {
            $_.PSObject.Properties['cat'] -and $_.cat -ceq 'Node' -and $_.PSObject.Properties['args'] -and $_.args.PSObject.Properties['provider']
        })
        $actualProviders = @($events.args.provider | Sort-Object -Unique)
        Assert-Same @($profile.kernelEventsByProvider.PSObject.Properties.Name | Sort-Object) $actualProviders 'observed provider set'
        foreach ($provider in $actualProviders) {
            $kernels = @($events | Where-Object { $_.args.provider -ceq $provider })
            Assert-Same $kernels.Count $profile.kernelEventsByProvider.$provider 'observed provider kernel count'
            Assert-Same @($kernels.args.op_name | Sort-Object -Unique) @($profile.operationsByProvider.$provider) 'observed operator set'
        }
        $verifiedProfiles++
    }
}
$tokens = 0
$patchValues = 0L
foreach ($case in $grouped.cases) {
    $reference = One $pinned.cases id $case.id
    foreach ($field in @('task', 'inputSha256', 'patchSha256', 'patchFloatCount', 'grid', 'generatedTokenIds', 'text', 'finishReason', 'promptTokens')) {
        Assert-Same $reference.$field $case.$field "$($case.id)/$field"
    }
    Assert-Same 'generated-eos' $case.status 'full EOS'
    Assert-Same $true $case.tokenExact 'exact tokens'
    Assert-Same $true $case.textExact 'exact text'
    Assert-Same 1 $case.visionCalls 'one image encoding'
    foreach ($field in @('embeddingCalls', 'decoderCalls', 'headCalls')) { Assert-Same @($case.generatedTokenIds).Count $case.$field $field }
    Assert-Same @($case.generatedTokenIds).Count @($case.decoderTrace).Count 'KV trace count'
    for ($step = 0; $step -lt $case.decoderTrace.Count; $step++) {
        $trace = $case.decoderTrace[$step]
        Assert-Same $(if ($step -eq 0) { $case.promptTokens } else { 1 }) $trace.QueryLength 'query length'
        Assert-Same $(if ($step -eq 0) { 0 } else { $case.promptTokens + $step - 1 }) $trace.PastLength 'past length'
        Assert-Same ($case.promptTokens + $step) $trace.PresentLength 'present length'
    }
    $feature = One $features.cases id $case.id
    Assert-Same $case.patchSha256 $feature.patchSha256 'feature pixel identity'
    Assert-Same $case.grid $feature.grid 'feature grid'
    Assert-Same 0 $feature.nonfiniteValues 'finite features'
    Assert-Same 0 $feature.outsideToleranceValues 'feature tolerance'
    $tokens += @($case.generatedTokenIds).Count
    $patchValues += $case.patchFloatCount
}
Assert-Same 765 $tokens 'total generated tokens'
Assert-Same 6818448 $patchValues 'total patch floats'
$failed = @($original.cases | Where-Object status -CEQ 'failed')
Assert-Same 1 $failed.Count 'one original failing task'
Assert-Same 5 @($original.cases | Where-Object status -CEQ 'generated-eos').Count 'five original successful tasks'
foreach ($case in $original.cases | Where-Object status -CEQ 'generated-eos') {
    $reference = One $pinned.cases id $case.id
    Assert-Same $reference.generatedTokenIds $case.generatedTokenIds 'original completed task tokens'
    Assert-Same $reference.text $case.text 'original completed task text'
}
Assert-Same 'spotting-demo-1' $failed[0].id 'original Spotting failure'
Assert-Same 1 $failed[0].visionCalls 'failure during image encoding'
Assert-Same 0 $failed[0].decoderCalls 'no decode on original failed task'
Assert-Same $true ($failed[0].error.Contains('1719926784')) 'original full attention allocation failure'
$verifiedAssemblies = 0
if ($AssemblyPath) {
    foreach ($assembly in $grouped.assemblies) {
        Assert-Same (Get-FileHash -LiteralPath (Join-Path $AssemblyPath ($assembly.name + '.dll'))).Hash.ToLowerInvariant() $assembly.sha256 'actual deployed assembly'
        $verifiedAssemblies++
    }
}
$result = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-cuda-evidence-validation-v1'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('O');
    status = 'six-task-cuda-generation-and-cpu-feature-comparison-verified';
    original = Evidence $OriginalReportPath; grouped = Evidence $GroupedReportPath; export = Evidence $ExportReportPath; features = Evidence $FeatureReportPath;
    scriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant(); developmentBaseline = $grouped.sourceCommit;
    cases = 6; tokens = $tokens; patchValues = $patchValues; verifiedRawProfiles = $verifiedProfiles; verifiedLocalAssemblies = $verifiedAssemblies;
    changedGraphRoles = $sourceChanged; originalFailingTask = $failed[0].id;
    headsPerGroup = $export.headsPerGroup;
    boundary = 'CUDA attribution is one profiled OCR preflight token, followed by fresh unprofiled production sessions for all six tasks. CPU shape/control operations and per-step CPU KV/logits remain. Feature comparison is between original/derived CPU graphs, not bit-exact or upstream/CUDA feature parity. Single-run diagnostics are not labelled quality, formal latency, VRAM peaks, clock locks or long-running stability.'
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($result | ConvertTo-Json -Depth 12).Replace("`r`n", "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Verified six CUDA tasks / $tokens tokens / $patchValues patch floats; raw profiles=$verifiedProfiles; local assemblies=$verifiedAssemblies"
