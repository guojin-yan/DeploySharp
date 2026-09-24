[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$ReportPath,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$rows = [System.Collections.Generic.List[object]]::new()
$environment = [System.Collections.Generic.List[object]]::new()

foreach ($path in $ReportPath) {
    $fullPath = (Resolve-Path -LiteralPath $path).Path
    $csvRows = @(Import-Csv -LiteralPath $fullPath)
    foreach ($row in $csvRows) {
        $rows.Add([pscustomobject]@{
            Report = $fullPath
            Model = "$($row.version)-$($row.variant)"
            Backend = $row.backend
            Device = $row.device
            Status = $row.status
            Batch = $row.selected_batch_size
            Channels = $row.selected_inference_channels
            Regions = $row.regions
            MeanMs = $row.total_ms
            P50Ms = $row.total_p50_ms
            P95Ms = $row.total_p95_ms
            PreprocessMs = $row.preprocess_ms
            DetectionMs = $row.detection_ms
            RecognitionMs = $row.recognition_ms
            Image = $row.image_path
            ResultSha = $row.result_text_sha256
            ContractSha = $row.result_contract_sha256
        })
    }

    $metadataPath = "$fullPath.environment.json"
    if (Test-Path -LiteralPath $metadataPath) {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
        $environment.Add([pscustomobject]@{
            Report = $fullPath
            Machine = $metadata.machine
            OS = $metadata.os
            Framework = $metadata.framework
            Architecture = $metadata.architecture
            ProcessorCount = $metadata.processorCount
            SourceRevision = $metadata.sourceRevision
            ImageSha256 = $metadata.imageSha256
            Warmup = $metadata.protocol.warmup
            Iterations = $metadata.protocol.iterations
            Batch = $metadata.protocol.batchSize
            Channels = $metadata.protocol.stageConcurrency
            TensorRtApi = $metadata.protocol.tensorRtApiVersion
        })
    }
}

$fullOutput = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
$builder = [Text.StringBuilder]::new()
[void]$builder.AppendLine('# PaddleOCR benchmark matrix')
[void]$builder.AppendLine()
[void]$builder.AppendLine('Generated from complete-pipeline CSV reports. P50/P95 are copied from the raw reports; no mean-to-percentile conversion is performed.')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Model | Backend | Device | Status | Batch | Channels | Regions | Mean ms | P50 ms | P95 ms | Preprocess ms | Detection ms | Recognition ms | Input | Result SHA-256 |')
[void]$builder.AppendLine('| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |')
foreach ($row in $rows | Sort-Object Model, Backend, Report) {
    $input = ($row.Image -replace '\|', '\\|')
    [void]$builder.AppendLine("| $($row.Model) | $($row.Backend) | $($row.Device) | $($row.Status) | $($row.Batch) | $($row.Channels) | $($row.Regions) | $($row.MeanMs) | $($row.P50Ms) | $($row.P95Ms) | $($row.PreprocessMs) | $($row.DetectionMs) | $($row.RecognitionMs) | $input | ``" + $row.ResultSha + "`` |")
}
[void]$builder.AppendLine()
[void]$builder.AppendLine('## Environment records')
[void]$builder.AppendLine()
[void]$builder.AppendLine('| Report | Machine | OS | Framework | Architecture | CPU count | Source revision | Input SHA-256 | Warmup | Iterations | Batch | Stage channels | TensorRT API |')
[void]$builder.AppendLine('| --- | --- | --- | --- | --- | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: |')
foreach ($item in $environment | Sort-Object Report) {
    [void]$builder.AppendLine("| $($item.Report) | $($item.Machine) | $($item.OS) | $($item.Framework) | $($item.Architecture) | $($item.ProcessorCount) | $($item.SourceRevision) | ``" + $item.ImageSha256 + "`` | $($item.Warmup) | $($item.Iterations) | $($item.Batch) | $($item.Channels) | $($item.TensorRtApi) |")
}
Set-Content -LiteralPath $fullOutput -Value $builder.ToString() -Encoding utf8
Write-Host "PADDLEOCR_MATRIX_REPORT=$fullOutput"
