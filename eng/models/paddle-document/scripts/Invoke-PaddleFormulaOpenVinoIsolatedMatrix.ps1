[CmdletBinding()]
param(
    [string]$Project = 'tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj',
    [string]$OutputPath = 'artifacts\paddle-document\formula-openvino-isolated-20260929.json',
    [string]$Configuration = 'Debug',
    [string]$TargetFramework = 'net10.0'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$projectPath = if ([IO.Path]::IsPathRooted($Project)) { $Project } else { Join-Path $repoRoot $Project }
$outputFull = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repoRoot $OutputPath }
$outputDirectory = Split-Path -Parent $outputFull
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$models = @(
    @{ id = 'pp-formulanet-plus-s'; source = 'PP-FormulaNet_plus-S_infer'; size = '384x384' },
    @{ id = 'pp-formulanet-plus-m'; source = 'PP-FormulaNet_plus-M_infer'; size = '384x384' },
    @{ id = 'pp-formulanet-plus-l'; source = 'PP-FormulaNet_plus-L_infer'; size = '768x768' },
    @{ id = 'pp-formulanet-s'; source = 'PP-FormulaNet-S_infer'; size = '384x384' },
    @{ id = 'pp-formulanet-l'; source = 'PP-FormulaNet-L_infer'; size = '768x768' },
    @{ id = 'unimernet'; source = 'UniMERNet_infer'; size = '672x192' }
)

$rows = [System.Collections.Generic.List[object]]::new()
foreach ($model in $models) {
    $logPath = Join-Path $outputDirectory ($model.id + '.log')
    $env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_OPENVINO = '1'
    $env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_MODEL = $model.id
    Write-Host "FORMULA_OPENVINO_ISOLATED model=$($model.id)"
    & dotnet test $projectPath -f $TargetFramework --no-restore --configuration $Configuration `
        --filter 'FullyQualifiedName~PaddleDocumentFormulaOpenVinoIsolatedIntegrationTests' `
        --logger 'console;verbosity=minimal' *> $logPath
    $exitCode = $LASTEXITCODE
    $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
    $status = if ($exitCode -eq 0 -and $log -match 'Passed') { 'pass' } elseif ($exitCode -eq -1073741819 -or $log -match '0xC0000005|Fatal error|test host process crashed') { 'native-crash' } else { 'managed-failure' }
    $rows.Add([ordered]@{
        model = $model.id
        sourceDirectory = $model.source
        modelPath = "E:\Model\PaddleDocument\onnx\$($model.id).onnx"
        modelSize = $model.size
        status = $status
        exitCode = $exitCode
        log = $logPath
        summary = (($log -split "`r?`n") | Where-Object { $_ -match 'PADDLE_DOCUMENT_FORMULA_OPENVINO_ISOLATED|OpenVINO|Fatal|Exception|Failed|Passed' } | Select-Object -Last 12)
    })
}

$result = [ordered]@{
    schemaVersion = 'deploysharp-paddle-formula-openvino-isolated-v1'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    project = $projectPath
    targetFramework = $TargetFramework
    configuration = $Configuration
    nativeCrashIsolation = $true
    boundary = 'Each model runs in a separate dotnet test process. native-crash is an exact runtime/importer blocker; managed-failure is a normal test failure.'
    models = $rows
}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputFull -Encoding UTF8
$result | ConvertTo-Json -Depth 12
if (@($rows | Where-Object status -eq 'pass').Count -eq 0) { exit 2 }
