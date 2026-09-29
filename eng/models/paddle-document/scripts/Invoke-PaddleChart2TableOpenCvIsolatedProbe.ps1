[CmdletBinding()]
param(
    [string]$Project = 'tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj',
    [string]$ExportRoot = 'E:\Model\PaddleDocument\chart-export',
    [string]$TextRoot = '',
    [string]$OutputPath = 'artifacts\paddle-document\chart2table-opencv-isolated-20260929.json',
    [string]$Configuration = 'Debug',
    [string]$TargetFramework = 'net10.0'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$projectPath = if ([IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $repoRoot $Project }
$outputFull = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$outputDirectory = Split-Path -Parent $outputFull
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$textRootEffective = if ([string]::IsNullOrWhiteSpace($TextRoot)) { Join-Path $ExportRoot 'text-onnx-verify-20260923' } else { $TextRoot }
$graphs = @('vision','embedding','prefill','decode')
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($graph in $graphs) {
    $logPath = Join-Path $outputDirectory ('chart2table-opencv-' + $graph + '.log')
    $env:DEPLOYSHARP_CHART2TABLE_OPENCV_PROBE = '1'
    $env:DEPLOYSHARP_CHART2TABLE_OPENCV_GRAPH = $graph
    $env:DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT = $ExportRoot
    $env:DEPLOYSHARP_CHART2TABLE_TEXT_ROOT = $textRootEffective
    Write-Host "CHART2TABLE_OPENCV_ISOLATED graph=$graph"
    & dotnet test $projectPath -f $TargetFramework --no-restore --configuration $Configuration `
        --filter 'FullyQualifiedName~PaddleChart2TableOpenCvIsolatedIntegrationTests' `
        --logger 'console;verbosity=minimal' *> $logPath
    $exitCode = $LASTEXITCODE
    $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
    $status = if ($exitCode -eq 0) { 'pass' } elseif ($log -match '0xC0000005|Fatal error|test host process crashed') { 'native-crash' } else { 'unsupported' }
    $rows.Add([ordered]@{ graph = $graph; status = $status; exitCode = $exitCode; log = $logPath; summary = (($log -split "`r?`n") | Where-Object { $_ -match 'PADDLE_CHART2TABLE_OPENCV|OpenCV|import|Exception|Failed|Fatal' } | Select-Object -Last 12) })
}
$result = [ordered]@{
    schemaVersion = 'deploysharp-paddle-chart2table-opencv-isolated-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    nativeCrashIsolation = $true
    exportRoot = $ExportRoot
    textRoot = $textRootEffective
    boundary = 'This probe classifies per-graph OpenCV DNN importer/session loading. Full autoregressive generation is attempted only after all four graphs load safely.'
    graphs = $rows
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputFull -Encoding UTF8
$result | ConvertTo-Json -Depth 12
if (@($rows | Where-Object status -eq 'pass').Count -eq 0) { exit 2 }
