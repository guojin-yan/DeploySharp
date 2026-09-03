[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ProjectPath = 'samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$project = Join-Path $repository $ProjectPath
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Benchmark project was not found: $project" }

function Invoke-Benchmark {
    param(
        [string[]]$Arguments,
        [int]$ExpectedExitCode
    )

    $output = @(& dotnet run --project $project -c Release --no-build --no-restore -- @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
    if ($exitCode -ne $ExpectedExitCode) {
        throw "Benchmark command exit code mismatch. Expected $ExpectedExitCode, got $exitCode. Output: $($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine)
}

Push-Location $repository
try {
    $help = Invoke-Benchmark @('--help') 0
    foreach ($token in @('Usage:', '--backend', '--warmup', '--iterations', '--output')) {
        if ($help -notmatch [regex]::Escape($token)) { throw "Benchmark help is missing '$token'." }
    }

    $unknownBackend = Invoke-Benchmark @('--backend', 'not-a-backend') 2
    if ($unknownBackend -notmatch 'DEPLOYSHARP_BENCHMARK_USAGE_ERROR' -or $unknownBackend -notmatch 'Unsupported backend') {
        throw 'Benchmark did not report the unknown backend as a usage error.'
    }

    $invalidWarmup = Invoke-Benchmark @('--warmup', '0') 2
    if ($invalidWarmup -notmatch 'DEPLOYSHARP_BENCHMARK_USAGE_ERROR' -or $invalidWarmup -notmatch 'positive integer') {
        throw 'Benchmark did not reject a non-positive warmup count.'
    }

    Write-Output 'DEPLOYSHARP_INFERENCE_BENCHMARK_CONTRACT_OK checks=3 native=not-required'
}
finally {
    Pop-Location
}
