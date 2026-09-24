[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\paddleocr',
    [string[]]$ModelId,
    [switch]$All,
    [switch]$SkipConversion,
    [string]$Python = 'E:\Model\PaddleDocument\paddle3\python.exe',
    [int]$OpsetOverride = 0,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $scriptRoot 'paddle-ocr-models.json'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$selected = @($catalog.models | Where-Object { $All -or $ModelId -contains $_.id })
if ($selected.Count -eq 0) { throw 'Specify -All or one or more -ModelId values from paddle-ocr-models.json.' }

$sourceRoot = Join-Path $ModelRoot 'source'
$extractRoot = Join-Path $ModelRoot 'source-extracted'
$recordRoot = Join-Path $ModelRoot 'records'
foreach ($path in @($sourceRoot, $extractRoot, $recordRoot)) { New-Item -ItemType Directory -Force -Path $path | Out-Null }
$tar = Get-Command tar -ErrorAction SilentlyContinue
$pythonCommand = Get-Command $Python -ErrorAction SilentlyContinue
if (-not $pythonCommand -and -not $SkipConversion) { throw "Python was not found at '$Python'." }
if ($pythonCommand) { $pythonFullPath = (Resolve-Path -LiteralPath $pythonCommand.Source).Path }

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Download-Checked([string]$Url, [string]$Path) {
    if ((Test-Path -LiteralPath $Path -PathType Leaf) -and -not $Force) { return }
    Write-Host "Downloading $Url"
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path
}
function Find-ModelFile([string]$Root) {
    $item = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter 'inference.json' | Select-Object -First 1
    if (-not $item) { $item = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter 'inference.pdmodel' | Select-Object -First 1 }
    return $item
}
function Find-ParamsFile([string]$Root) { Get-ChildItem -LiteralPath $Root -Recurse -File -Filter 'inference.pdiparams' | Select-Object -First 1 }

$records = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $selected) {
    $idSafe = ($entry.id -replace '/', '-')
    $archivePath = Join-Path $sourceRoot ($idSafe + '.tar')
    $extractPath = Join-Path $extractRoot $idSafe
    $onnxPath = Join-Path (Join-Path $ModelRoot ([string]$entry.directory)) ([string]$entry.onnx)
    $dictionaryPath = if ($null -eq $entry.dictionary) { $null } else { Join-Path $ModelRoot ([string]$entry.dictionary) }
    $sourceBase = [string]$catalog.sourceBase
    if ($entry.PSObject.Properties.Name -contains 'sourceBase' -and -not [string]::IsNullOrWhiteSpace([string]$entry.sourceBase)) { $sourceBase = [string]$entry.sourceBase }
    $sourceUrl = $sourceBase + [string]$entry.archive
    $record = [ordered]@{
        schemaVersion = 'deploysharp-paddle-ocr-acquisition-v1'; id = [string]$entry.id; version = [string]$entry.version; family = [string]$entry.family; name = [string]$entry.name
        sourceUrl = $sourceUrl
        archive = $null; onnx = $null; dictionary = $null; codeProfile = [string]$entry.codeProfile; input = [string]$entry.input; output = [string]$entry.output; status = 'not-started'; error = $null
    }
    try {
        Download-Checked $record.sourceUrl $archivePath
        $record.archive = [ordered]@{ path = $archivePath; size = (Get-Item -LiteralPath $archivePath).Length; sha256 = Get-Sha256 $archivePath }
        if (-not (Test-Path -LiteralPath $extractPath -PathType Container) -or $Force) {
            if (-not $tar) { throw 'Windows tar.exe is required to extract Paddle archives.' }
            if (Test-Path -LiteralPath $extractPath -PathType Container) { Remove-Item -LiteralPath $extractPath -Recurse -Force }
            New-Item -ItemType Directory -Force -Path $extractPath | Out-Null
            & $tar.Source -xf $archivePath -C $extractPath
            if ($LASTEXITCODE -ne 0) { throw "tar extraction failed with exit code $LASTEXITCODE." }
        }
        $modelFile = Find-ModelFile $extractPath
        $paramsFile = Find-ParamsFile $extractPath
        if (-not $modelFile -or -not $paramsFile) { throw 'Archive does not contain inference.json/inference.pdmodel and inference.pdiparams.' }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $onnxPath) | Out-Null
        if (-not (Test-Path -LiteralPath $onnxPath -PathType Leaf) -or $Force) {
            if ($SkipConversion) { throw "ONNX is missing and -SkipConversion was specified: $onnxPath" }
            $pythonRoot = Split-Path -Parent $pythonFullPath
            $pathEntries = @((Join-Path $pythonRoot 'Scripts'), (Join-Path $pythonRoot 'Lib\site-packages\paddle\libs'), (Join-Path $pythonRoot 'Lib\site-packages\paddle\base')) | Where-Object { Test-Path -LiteralPath $_ }
            if ($pathEntries.Count -gt 0) { $env:PATH = (($pathEntries -join ';') + ';' + $env:PATH) }
            $moduleEntry = 'paddle2onnx.command'
            $opset = if ($OpsetOverride -gt 0) { $OpsetOverride } else { [int]$entry.opset }
            & $pythonFullPath -m $moduleEntry --model_dir $modelFile.DirectoryName --model_filename $modelFile.Name --params_filename $paramsFile.Name --save_file $onnxPath --opset_version $opset --enable_onnx_checker True
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $onnxPath -PathType Leaf)) { throw "paddle2onnx conversion failed with exit code $LASTEXITCODE." }
            $status = 'onnx-converted'
        } else { $status = 'onnx-existing' }
        $onnxItem = Get-Item -LiteralPath $onnxPath
        $record.onnx = [ordered]@{ path = $onnxPath; size = $onnxItem.Length; sha256 = Get-Sha256 $onnxPath; opset = [int]$entry.opset }
        if ($dictionaryPath) {
            if (-not (Test-Path -LiteralPath $dictionaryPath -PathType Leaf)) { throw "Required dictionary is missing: $dictionaryPath" }
            $dictionaryItem = Get-Item -LiteralPath $dictionaryPath
            $record.dictionary = [ordered]@{ path = $dictionaryPath; size = $dictionaryItem.Length; sha256 = Get-Sha256 $dictionaryPath }
        }
        $record.status = $status
    } catch {
        $record.status = if ($record.archive) { if ($record.onnx) { 'dictionary-blocked' } else { 'conversion-blocked' } } else { 'download-failed' }
        $record.error = $_.Exception.Message
        Write-Warning "$($entry.id): $($record.status): $($record.error)"
    }
    $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $recordRoot ($idSafe + '.json')) -Encoding UTF8
    $records.Add($record)
}
$summary = [ordered]@{ schemaVersion = 'deploysharp-paddle-ocr-acquisition-summary-v1'; generatedAtUtc = [DateTime]::UtcNow.ToString('o'); modelRoot = $ModelRoot; records = $records }
$summary | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $ModelRoot 'paddle-ocr-acquisition-summary.json') -Encoding UTF8
$records | Select-Object id, version, family, status | Format-Table -AutoSize
if (@($records | Where-Object status -in @('download-failed','conversion-blocked','dictionary-blocked')).Count -gt 0) { exit 2 }
