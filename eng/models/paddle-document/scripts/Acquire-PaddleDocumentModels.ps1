[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\PaddleDocument',
    [string[]]$ModelId,
    [switch]$All,
    [switch]$SkipConversion,
    [string]$Python = 'python',
    [string]$Paddle2OnnxModule = 'paddle2onnx',
    [int]$Opset = 17,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $scriptRoot 'paddle-document-models.json'
$catalog = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$selected = @($catalog.models | Where-Object { $All -or $ModelId -contains $_.id })
if ($selected.Count -eq 0) {
    throw 'Specify -All or one or more -ModelId values. Use the ids in paddle-document-models.json.'
}

$sourceRoot = Join-Path $ModelRoot 'source'
$onnxRoot = Join-Path $ModelRoot 'onnx'
$recordRoot = Join-Path $ModelRoot 'records'
foreach ($path in @($sourceRoot, $onnxRoot, $recordRoot)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
$records = [System.Collections.Generic.List[object]]::new()
$tar = Get-Command tar -ErrorAction SilentlyContinue

foreach ($entry in $selected) {
    $entrySource = Join-Path $sourceRoot $entry.id
    $archivePath = Join-Path $sourceRoot ($entry.id + '.tar')
    $onnxPath = Join-Path $onnxRoot ($entry.id + '.onnx')
    $url = $catalog.sourceBase + $entry.archive
    $record = [ordered]@{
        schemaVersion = 'deploysharp-paddle-document-acquisition-v1'
        id = $entry.id
        module = $entry.module
        sourceUrl = $url
        archive = $null
        onnx = $null
        status = 'not-started'
        error = $null
    }
    try {
        if (-not (Test-Path -LiteralPath $archivePath) -or $Force) {
            Write-Host "Downloading $($entry.id) from $url"
            Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archivePath
        }
        $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $record.archive = [ordered]@{ path = $archivePath; size = (Get-Item -LiteralPath $archivePath).Length; sha256 = $archiveHash }
        if (-not (Test-Path -LiteralPath $entrySource) -or $Force) {
            if (-not $tar) { throw 'Windows tar.exe is required to extract official Paddle archives.' }
            New-Item -ItemType Directory -Force -Path $entrySource | Out-Null
            & $tar.Source -xf $archivePath -C $entrySource
            if ($LASTEXITCODE -ne 0) { throw "tar extraction failed with exit code $LASTEXITCODE." }
        }
        $modelFile = Get-ChildItem -LiteralPath $entrySource -Recurse -File -Filter 'inference.json' | Select-Object -First 1
        if (-not $modelFile) { $modelFile = Get-ChildItem -LiteralPath $entrySource -Recurse -File -Filter 'inference.pdmodel' | Select-Object -First 1 }
        $paramsFile = Get-ChildItem -LiteralPath $entrySource -Recurse -File -Filter 'inference.pdiparams' | Select-Object -First 1
        if (-not $modelFile -or -not $paramsFile) { throw 'Official archive does not contain inference model and parameter files.' }
        if ($SkipConversion) {
            $record.status = 'downloaded'
        } else {
            $pythonCommand = Get-Command $Python -ErrorAction SilentlyContinue
            if (-not $pythonCommand) { throw "Python was not found at '$Python'. Install PaddlePaddle and paddle2onnx, then rerun." }
            $pythonFullPath = (Resolve-Path -LiteralPath $pythonCommand.Source).Path
            $pythonRoot = Split-Path -Parent $pythonFullPath
            $pythonLibs = Join-Path $pythonRoot 'Lib\site-packages\paddle\libs'
            $pythonBase = Join-Path $pythonRoot 'Lib\site-packages\paddle\base'
            $pythonScripts = Join-Path $pythonRoot 'Scripts'
            $pathEntries = @($pythonScripts, $pythonLibs, $pythonBase) | Where-Object { Test-Path -LiteralPath $_ }
            if ($pathEntries.Count -gt 0) { $env:PATH = (($pathEntries -join ';') + ';' + $env:PATH) }
            $modelName = $modelFile.Name
            $paramsName = $paramsFile.Name
            # paddle2onnx 2.x exposes its CLI through paddle2onnx.command rather than
            # a package __main__. Older installations may still provide a module entry.
            $moduleEntry = if ($Paddle2OnnxModule -eq 'paddle2onnx') { 'paddle2onnx.command' } else { $Paddle2OnnxModule }
            & $pythonCommand.Source -m $moduleEntry --model_dir $modelFile.DirectoryName --model_filename $modelName --params_filename $paramsName --save_file $onnxPath --opset_version $Opset --enable_onnx_checker True
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $onnxPath)) { throw "paddle2onnx conversion failed with exit code $LASTEXITCODE." }
            $onnxHash = (Get-FileHash -LiteralPath $onnxPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $record.onnx = [ordered]@{ path = $onnxPath; size = (Get-Item -LiteralPath $onnxPath).Length; sha256 = $onnxHash; opset = $Opset; exporter = $Paddle2OnnxModule }
            $record.status = 'onnx-converted'
        }
    } catch {
        $record.status = if ($record.archive) { 'conversion-blocked' } else { 'download-failed' }
        $record.error = $_.Exception.Message
        Write-Warning "$($entry.id): $($record.status): $($record.error)"
    }
    $recordPath = Join-Path $recordRoot ($entry.id + '.json')
    $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    $records.Add($record)
}

$summary = [ordered]@{
    schemaVersion = 'deploysharp-paddle-document-acquisition-summary-v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    modelRoot = $ModelRoot
    records = $records
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $ModelRoot 'acquisition-summary.json') -Encoding UTF8
$records | Select-Object id, module, status | Format-Table -AutoSize
if (@($records | Where-Object status -in @('download-failed', 'conversion-blocked')).Count -gt 0) { exit 2 }
