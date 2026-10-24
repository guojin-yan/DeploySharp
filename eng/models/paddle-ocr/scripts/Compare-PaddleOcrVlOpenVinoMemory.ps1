[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BeforePath,
    [Parameter(Mandatory)][string]$AfterPath,
    [Parameter(Mandatory)][string]$OutputJsonPath,
    [Parameter(Mandatory)][string]$OutputMarkdownPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-Report([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 60 }
function Assert-Same($Expected, $Actual, [string]$Label) {
    if (($Expected | ConvertTo-Json -Depth 50 -Compress) -cne ($Actual | ConvertTo-Json -Depth 50 -Compress)) { throw "Mismatch: $Label" }
}
function Evidence([string]$Path) {
    [ordered]@{ fileName = [IO.Path]::GetFileName($Path); sha256 = (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant() }
}
function Modules($Report) {
    @($Report.nativeRuntime.modules | ForEach-Object {
        [ordered]@{ name = ($_.path -split '/')[-1]; sha256 = $_.sha256 }
    } | Sort-Object { $_.name })
}
function GiB([long]$Value) { ($Value / 1GB).ToString('F3', [Globalization.CultureInfo]::InvariantCulture) }
$before = Read-Report $BeforePath
$after = Read-Report $AfterPath
$pinned = Read-Report (Join-Path $PSScriptRoot '../verification/paddleocr-vl-openvino-linux-20261024.json')
Assert-Same 'retained-source-model' $before.runLabel 'before label'
Assert-Same 'released-source-model' $after.runLabel 'after label'
Assert-Same $before.testSourceSha256 $after.testSourceSha256 'same harness source'
Assert-Same $before.sourceCommit $after.sourceCommit 'same development baseline'
Assert-Same $pinned.environment $before.environment 'pinned execution configuration'
Assert-Same $pinned.resourcePolicy $before.resourcePolicy 'pinned resource settings'
Assert-Same $before.environment $after.environment 'same execution environment/configuration'
Assert-Same $before.resourcePolicy $after.resourcePolicy 'same resource settings'
Assert-Same $before.execution $after.execution 'same async protocol'
Assert-Same $before.nativeRuntime.buildNumber $after.nativeRuntime.buildNumber 'same native runtime'
Assert-Same (Modules $pinned) (Modules $before) 'pinned native modules'
Assert-Same (Modules $before) (Modules $after) 'same native modules'
Assert-Same @($pinned.assemblies.name | Sort-Object) @($before.assemblies.name | Sort-Object) 'before assembly set'
Assert-Same @($before.assemblies.name | Sort-Object) @($after.assemblies.name | Sort-Object) 'after assembly set'
$changedAssemblies = @()
foreach ($assembly in $before.assemblies) {
    $matches = @($after.assemblies | Where-Object name -CEQ $assembly.name)
    Assert-Same 1 $matches.Count 'unique assembly identity'
    if ($assembly.sha256 -cne $matches[0].sha256) { $changedAssemblies += $assembly.name }
}
Assert-Same @('JYPPX.DeploySharp.Backend.OpenVINO', 'DeploySharp.Backend.OpenVINO.Tests') $changedAssemblies 'only backend/test assemblies changed'
$rows = @()
$totalTokens = 0
foreach ($report in @($before, $after)) {
    Assert-Same 'six-task-openvino-managed-rgb-generation-exact' $report.status 'complete generation'
    foreach ($field in @('checkpointRevision', 'referenceSha256', 'processorFixtureManifestSha256')) { Assert-Same $pinned.$field $report.$field $field }
    Assert-Same 4 @($report.graphs).Count 'four graphs'
    Assert-Same 4 @($report.graphs.role | Sort-Object -Unique).Count 'unique graph roles'
    Assert-Same 6 @($report.cases).Count 'six tasks'
    Assert-Same 6 @($report.cases.id | Sort-Object -Unique).Count 'unique tasks'
    foreach ($graph in $report.graphs) {
        $reference = @($pinned.graphs | Where-Object role -CEQ $graph.role)
        Assert-Same 1 $reference.Count 'unique graph role'
        foreach ($field in @('sha256', 'inputs', 'outputs')) { Assert-Same $reference[0].$field $graph.$field ($graph.role + '/' + $field) }
    }
    foreach ($case in $report.cases) {
        $reference = @($pinned.cases | Where-Object id -CEQ $case.id)
        Assert-Same 1 $reference.Count 'unique task ID'
        foreach ($field in @('task', 'inputSha256', 'patchSha256', 'patchFloatCount', 'grid', 'promptTokens', 'generatedTokenIds', 'text', 'finishReason')) {
            Assert-Same $reference[0].$field $case.$field ($case.id + '/' + $field)
        }
        Assert-Same 'generated-eos' $case.status 'EOS status'
        Assert-Same $true $case.tokenExact 'exact token decisions'
        Assert-Same $true $case.textExact 'exact text'
        Assert-Same 1 $case.visionCalls 'single vision encoding'
        foreach ($field in @('decoderCalls', 'embeddingCalls', 'headCalls')) { Assert-Same @($case.generatedTokenIds).Count $case.$field $field }
        Assert-Same @($case.generatedTokenIds).Count @($case.decoderTrace).Count 'trace length'
        for ($step = 0; $step -lt $case.decoderTrace.Count; $step++) {
            $trace = $case.decoderTrace[$step]
            Assert-Same $(if ($step -eq 0) { $case.promptTokens } else { 1 }) $trace.QueryLength 'query length'
            Assert-Same $(if ($step -eq 0) { 0 } else { $case.promptTokens + $step - 1 }) $trace.PastLength 'past length'
            Assert-Same ($case.promptTokens + $step) $trace.PresentLength 'present length'
        }
    }
}
foreach ($case in $before.cases) {
    $candidate = @($after.cases | Where-Object id -CEQ $case.id)[0]
    $totalTokens += @($case.generatedTokenIds).Count
    $rows += [ordered]@{ id = $case.id; tokens = @($case.generatedTokenIds).Count;
        beforeWorkingSetBytes = $case.processWorkingSetBytes; afterWorkingSetBytes = $candidate.processWorkingSetBytes;
        workingSetDifferenceBytes = $candidate.processWorkingSetBytes - $case.processWorkingSetBytes }
}
Assert-Same 765 $totalTokens 'total tokens'
$beforePeak = [long]($before.cases | Measure-Object processPeakWorkingSetBytes -Maximum).Maximum
$afterPeak = [long]($after.cases | Measure-Object processPeakWorkingSetBytes -Maximum).Maximum
$result = [ordered]@{
    schemaVersion = 'deploysharp-paddleocr-vl-openvino-memory-comparison-v1'; generatedUtc = [DateTimeOffset]::UtcNow.ToString('O');
    status = 'six-task-output-and-kv-identical-memory-observations-recorded'; before = Evidence $BeforePath; after = Evidence $AfterPath;
    pinnedReference = Evidence (Join-Path $PSScriptRoot '../verification/paddleocr-vl-openvino-linux-20261024.json');
    scriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();
    developmentBaseline = $before.sourceCommit; harnessSourceSha256 = $before.testSourceSha256;
    checkpointRevision = $before.checkpointRevision; tasks = 6; tokensPerRun = $totalTokens; changedAssemblies = $changedAssemblies;
    beforeMemoryAfterGraphLoads = $before.memoryAfterGraphLoads; afterMemoryAfterGraphLoads = $after.memoryAfterGraphLoads;
    beforePeakWorkingSetBytes = $beforePeak; afterPeakWorkingSetBytes = $afterPeak; peakDifferenceBytes = $afterPeak - $beforePeak;
    environment = $before.environment; resourcePolicy = $before.resourcePolicy; rows = $rows;
    boundary = 'Two sequential single-process runs, before then after; same six fixed RGB inputs/graphs/runtime/harness/configuration. No random interleaving, clock telemetry, labelled quality, 5+50 performance, leak proof or long-running stability. Native and total-process memory are not bounded by the managed heap limit.'
}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# PaddleOCR-VL OpenVINO 源 Model 生命周期：自动对照')
$lines.Add('')
$lines.Add('两次运行的六任务、全部 765 Token/文本、图和 KV 长度均匹配固定已验参考；下列仅为两次顺序运行的进程内存观察。')
$lines.Add('')
$lines.Add('改动将 OpenVINO 原始 Model 的生命周期缩短到编译及元数据复制完成为止；CompiledModel、Core 和推理请求仍由 Session 管理。未改图、精度、生成参数、托管堆或分配器设置。')
$lines.Add('')
$lines.Add("原始记录：[before]($($result.before.fileName))、[after]($($result.after.fileName))。")
$lines.Add('')
$lines.Add('Ubuntu 22.04.5 x64 / i7-1165G7 / 16 GB、无 Swap / .NET 10.0.9 / OpenVINO 2026.3.1；CPU Latency、4 线程、1 stream、1 request、每图一个 Session。两次均使用 MALLOC_ARENA_MAX=2、DOTNET_GCHeapHardLimit=0x40000000；该 1 GiB 只限制托管堆。')
$lines.Add('')
$lines.Add('| 位置 | 保留源 Model GiB | 提前释放源 Model GiB | 差值 GiB |')
$lines.Add('| --- | ---: | ---: | ---: |')
$lines.Add("| 四图加载完成 RSS | $(GiB $before.memoryAfterGraphLoads.workingSetBytes) | $(GiB $after.memoryAfterGraphLoads.workingSetBytes) | $(GiB ($after.memoryAfterGraphLoads.workingSetBytes - $before.memoryAfterGraphLoads.workingSetBytes)) |")
foreach ($row in $rows) { $lines.Add("| $($row.id) 后 RSS | $(GiB $row.beforeWorkingSetBytes) | $(GiB $row.afterWorkingSetBytes) | $(GiB $row.workingSetDifferenceBytes) |") }
$lines.Add("| 进程峰值 RSS | $(GiB $beforePeak) | $(GiB $afterPeak) | $(GiB ($afterPeak - $beforePeak)) |")
$lines.Add('')
$lines.Add('先 before 后 after，各运行一次；没有随机配对、连续时钟/负载遥测或长期 soak。不得解释为泄漏定位、最优内存上界、吞吐/延迟排名、5+50 性能或标注质量。托管堆预算不限制原生和进程总内存。')
$lines.Add('')
$lines.Add("两份报告的 sourceCommit 都是开发基线 $($before.sourceCommit)，不是干净 checkout：before 仅增加同一测试采样，after 再增加上述后端改动。构建还包含未提交的既有其它测试修改；实际运行身份以报告中的五个程序集 SHA 和十三个 native SHA 为准。")
$lines.Add('')
$lines.Add('完整模型、输入、KV 和环境定义见[原始六任务验证](paddleocr-vl-openvino-linux-20261024.md)。本对照不撤销原始默认异步配置的 OOM 记录；Windows、页面联合质量、其它后端、正式性能及稳定性仍需单独验证。')
$lines.Add('')
$lines.Add('复算：')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add("pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlOpenVinoMemory.ps1 ``")
$lines.Add("  -BeforePath eng/models/paddle-ocr/verification/$($result.before.fileName) ``")
$lines.Add("  -AfterPath eng/models/paddle-ocr/verification/$($result.after.fileName) ``")
$lines.Add('  -OutputJsonPath artifacts/checks/paddleocr-vl-openvino-memory.json `')
$lines.Add('  -OutputMarkdownPath artifacts/checks/paddleocr-vl-openvino-memory.md')
$lines.Add('```')
foreach ($path in @($OutputJsonPath, $OutputMarkdownPath)) {
    $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($path)); [IO.Directory]::CreateDirectory($directory) | Out-Null
}
$utf8 = [Text.UTF8Encoding]::new($false)
$json = ($result | ConvertTo-Json -Depth 60).Replace("`r`n", "`n") + "`n"
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputJsonPath), $json, $utf8)
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputMarkdownPath), ($lines -join "`n") + "`n", $utf8)
Write-Host "Verified 6 tasks / $totalTokens tokens per run; peak bytes before=$beforePeak after=$afterPeak"
