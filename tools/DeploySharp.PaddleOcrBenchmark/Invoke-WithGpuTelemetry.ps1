[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Executable,
    [string[]]$ArgumentList = @(),
    [string]$OutputPath = 'artifacts\local-model-benchmarks\gpu-telemetry.csv',
    [ValidateRange(100, 60000)]
    [int]$IntervalMilliseconds = 250,
    [ValidateRange(0, 100)]
    [int]$ActiveUtilizationThreshold = 10
)

$ErrorActionPreference = 'Stop'
$nvidiaSmi = (Get-Command nvidia-smi.exe -ErrorAction Stop).Source
$outputFullPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$rawPath = "$outputFullPath.raw"

$fields = @(
    'timestamp',
    'name',
    'pstate',
    'utilization.gpu',
    'clocks.current.graphics',
    'clocks.current.sm',
    'clocks.current.memory',
    'power.draw',
    'temperature.gpu',
    'clocks_event_reasons.sw_power_cap',
    'clocks_event_reasons.sw_thermal_slowdown',
    'clocks_event_reasons.hw_slowdown'
)
$query = '--query-gpu=' + ($fields -join ',')
$monitor = Start-Process -FilePath $nvidiaSmi -ArgumentList @(
    $query,
    '--format=csv,noheader,nounits',
    "--loop-ms=$IntervalMilliseconds"
) -PassThru -NoNewWindow -RedirectStandardOutput $rawPath

$exitCode = 0
try {
    & $Executable @ArgumentList
    $exitCode = $LASTEXITCODE
}
finally {
    if (-not $monitor.HasExited) {
        Stop-Process -Id $monitor.Id
    }
    $monitor.WaitForExit()
}

$headers = @(
    'timestamp',
    'gpu_name',
    'pstate',
    'gpu_utilization_percent',
    'graphics_clock_mhz',
    'sm_clock_mhz',
    'memory_clock_mhz',
    'power_watts',
    'temperature_celsius',
    'software_power_cap',
    'software_thermal_slowdown',
    'hardware_slowdown'
)
$samples = @(Get-Content -LiteralPath $rawPath | ConvertFrom-Csv -Header $headers)
$samples | Export-Csv -LiteralPath $outputFullPath -NoTypeInformation -Encoding utf8
Remove-Item -LiteralPath $rawPath

$activeSamples = @($samples | Where-Object {
    [double]$utilization = 0
    [double]::TryParse($_.gpu_utilization_percent, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$utilization) -and
        $utilization -ge $ActiveUtilizationThreshold
})

if ($activeSamples.Count -gt 0) {
    $utilizations = @($activeSamples | ForEach-Object { [double]::Parse($_.gpu_utilization_percent, [Globalization.CultureInfo]::InvariantCulture) })
    $clocks = @($activeSamples | ForEach-Object { [double]::Parse($_.graphics_clock_mhz, [Globalization.CultureInfo]::InvariantCulture) })
    $powers = @($activeSamples | ForEach-Object { [double]::Parse($_.power_watts, [Globalization.CultureInfo]::InvariantCulture) })
    $temperatures = @($activeSamples | ForEach-Object { [double]::Parse($_.temperature_celsius, [Globalization.CultureInfo]::InvariantCulture) })
    $powerLimited = @($activeSamples | Where-Object { $_.software_power_cap -eq 'Active' })
    $thermalLimited = @($activeSamples | Where-Object { $_.software_thermal_slowdown -eq 'Active' })
    $hardwareSlowdown = @($activeSamples | Where-Object { $_.hardware_slowdown -eq 'Active' })
    $pstates = (($activeSamples.pstate | Sort-Object -Unique) -join ',')
    Write-Host ("GPU_TELEMETRY samples={0} active_samples={1} utilization_avg_percent={2:N1} utilization_max_percent={3:N0} pstates={4} graphics_clock_min_mhz={5:N0} graphics_clock_avg_mhz={6:N0} graphics_clock_max_mhz={7:N0} power_avg_w={8:N1} temperature_max_c={9:N0} power_limited_samples={10} thermal_limited_samples={11} hardware_slowdown_samples={12} output={13}" -f
        $samples.Count,
        $activeSamples.Count,
        ($utilizations | Measure-Object -Average).Average,
        ($utilizations | Measure-Object -Maximum).Maximum,
        $pstates,
        ($clocks | Measure-Object -Minimum).Minimum,
        ($clocks | Measure-Object -Average).Average,
        ($clocks | Measure-Object -Maximum).Maximum,
        ($powers | Measure-Object -Average).Average,
        ($temperatures | Measure-Object -Maximum).Maximum,
        $powerLimited.Count,
        $thermalLimited.Count,
        $hardwareSlowdown.Count,
        $outputFullPath)
}
else {
    Write-Warning "No GPU sample reached $ActiveUtilizationThreshold% utilization. The workload may be too short or CPU-bound."
}

if ($exitCode -ne 0) {
    throw "The measured command exited with code $exitCode. Telemetry was retained at '$outputFullPath'."
}
