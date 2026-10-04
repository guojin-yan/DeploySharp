param(
    [string]$BenchmarkRoot = 'F:\OCRBenchmarkTesting',
    [string]$ValidationRoot = 'E:\Model\PaddleDocument\validation',
    [string]$OutputPath = 'eng/models/paddle-document/verification/formula-data-availability-audit-20261005.json',
    [string]$MarkdownPath = 'eng/models/paddle-document/verification/formula-data-availability-audit-20261005.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$maximumHashBytes = 64MB
$maximumTextBytes = 16MB
$formulaPattern = '(?i)(formula|latex|equation|im2latex|unimernet|formulanet)'
$labelPattern = '(?i)"(?:latex|formula|equation|ground[_-]?truth|target|label|text)"\s*:'

function Resolve-RepoPath([string]$PathValue) {
    $candidate = if ([IO.Path]::IsPathRooted($PathValue)) { $PathValue } else { Join-Path $repoRoot $PathValue }
    return [IO.Path]::GetFullPath($candidate)
}

function Get-Sha256([string]$PathValue) {
    return (Get-FileHash -LiteralPath $PathValue -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePath([string]$RootPath, [string]$FilePath) {
    return [IO.Path]::GetRelativePath($RootPath, $FilePath).Replace('\', '/')
}

function Get-TextProbe([string]$PathValue) {
    $length = (Get-Item -LiteralPath $PathValue).Length
    $bytesToRead = [Math]::Min([long]$length, [long]$maximumTextBytes)
    if ($bytesToRead -le 0) { return '' }
    $buffer = [byte[]]::new([int]$bytesToRead)
    $stream = [IO.File]::OpenRead($PathValue)
    try {
        $read = 0
        while ($read -lt $buffer.Length) {
            $count = $stream.Read($buffer, $read, $buffer.Length - $read)
            if ($count -eq 0) { break }
            $read += $count
        }
    }
    finally {
        $stream.Dispose()
    }
    return [Text.Encoding]::UTF8.GetString($buffer, 0, $read)
}

function Get-RecordCount([string]$PathValue, [string]$Probe) {
    $extension = [IO.Path]::GetExtension($PathValue).ToLowerInvariant()
    if ($extension -eq '.jsonl') {
        return @($Probe -split "`r?`n" | Where-Object { ![string]::IsNullOrWhiteSpace($_) }).Count
    }
    if ($extension -ne '.json') { return 0 }
    try {
        $parsed = $Probe | ConvertFrom-Json -Depth 32
        if ($parsed -is [Array]) { return $parsed.Count }
        foreach ($name in @('records', 'items', 'data', 'annotations', 'instances', 'samples')) {
            $property = $parsed.PSObject.Properties[$name]
            if ($null -ne $property -and $property.Value -is [Array]) { return $property.Value.Count }
        }
    }
    catch {
        return 0
    }
    return 1
}

function Get-CandidateRows([string]$RootPath, [string]$SourceId) {
    $rows = [System.Collections.Generic.List[object]]::new()
    $exists = Test-Path -LiteralPath $RootPath -PathType Container
    if (!$exists) { return $rows }
    foreach ($file in @(Get-ChildItem -LiteralPath $RootPath -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -match $formulaPattern -or $_.DirectoryName -match $formulaPattern
    })) {
        $extension = $file.Extension.ToLowerInvariant()
        $kind = if ($extension -in @('.png', '.jpg', '.jpeg', '.bmp', '.webp', '.tif', '.tiff')) { 'image' }
            elseif ($extension -in @('.json', '.jsonl', '.csv', '.tsv', '.txt')) { 'text-or-manifest' }
            elseif ($extension -in @('.onnx', '.pdmodel', '.pdiparams', '.bin', '.safetensors')) { 'model-or-weights' }
            elseif ($extension -eq '.f32') { 'preprocessed-tensor' }
            else { 'other' }
        $sha256 = $null
        $hashSkippedReason = $null
        if ($file.Length -le $maximumHashBytes) {
            $sha256 = Get-Sha256 $file.FullName
        }
        else {
            $hashSkippedReason = 'file exceeds 64 MiB audit hash limit'
        }
        $probe = ''
        $labelSignal = $false
        $recordCount = 0
        if ($kind -eq 'text-or-manifest' -and $file.Length -le $maximumTextBytes) {
            $probe = Get-TextProbe $file.FullName
            $labelSignal = [regex]::IsMatch($probe, $labelPattern)
            $recordCount = Get-RecordCount $file.FullName $probe
        }
        $notes = [System.Collections.Generic.List[string]]::new()
        if ($file.Name -match '(?i)general_formula_rec_001') { $notes.Add('official single-image formula sample name') }
        if ($kind -eq 'text-or-manifest' -and $file.Length -gt $maximumTextBytes) { $notes.Add('text probe skipped above 16 MiB') }
        if ($kind -eq 'model-or-weights') { $notes.Add('model/weight asset is not evaluation truth') }
        if ($kind -eq 'preprocessed-tensor') { $notes.Add('preprocessed input tensor is not evaluation truth') }
        $rows.Add([ordered]@{
            source = $SourceId
            relativePath = Get-RelativePath $RootPath $file.FullName
            extension = $extension
            kind = $kind
            sizeBytes = $file.Length
            sha256 = $sha256
            hashSkippedReason = $hashSkippedReason
            labelSignal = $labelSignal
            recordCount = $recordCount
            notes = @($notes)
        })
    }
    return $rows
}

$roots = @(
    [ordered]@{ id = 'OCRBenchmarkTesting'; path = [IO.Path]::GetFullPath($BenchmarkRoot); exists = (Test-Path -LiteralPath $BenchmarkRoot -PathType Container) },
    [ordered]@{ id = 'PaddleDocumentValidation'; path = [IO.Path]::GetFullPath($ValidationRoot); exists = (Test-Path -LiteralPath $ValidationRoot -PathType Container) }
)
$candidates = [System.Collections.Generic.List[object]]::new()
foreach ($root in $roots) {
    foreach ($row in @(Get-CandidateRows $root.path $root.id)) { $candidates.Add($row) }
}
$formulaImages = @($candidates | Where-Object { $_['kind'] -eq 'image' })
$labelCandidates = @($candidates | Where-Object { $_['kind'] -eq 'text-or-manifest' -and $_['labelSignal'] })
$multiEquationCandidates = @($labelCandidates | Where-Object { [int]$_['recordCount'] -gt 1 })
$officialSingleSamples = @($candidates | Where-Object { @($_['notes']) -contains 'official single-image formula sample name' })

$output = [ordered]@{
    schemaVersion = 'deploysharp-formula-evaluation-data-audit-v1'
    generatedUtc = [DateTimeOffset]::UtcNow
    roots = $roots
    candidateCount = $candidates.Count
    formulaImageCandidateCount = $formulaImages.Count
    labelManifestCandidateCount = $labelCandidates.Count
    multiEquationLabeledCandidateCount = $multiEquationCandidates.Count
    officialSingleImageCandidateCount = $officialSingleSamples.Count
    admission = [ordered]@{
        status = if ($multiEquationCandidates.Count -gt 0) { 'review-required' } else { 'blocked' }
        redistributableMultiEquationTruthSet = $false
        eligibleSampleCount = 0
        reason = if ($multiEquationCandidates.Count -gt 0) { 'Candidate label files require manual license, schema and alignment review before any CER/WER is admitted.' } else { 'No formula-named multi-equation label manifest was found in the audited roots; current evidence remains a single official sample and controlled variants.' }
    }
    candidates = $candidates
    boundary = 'This audit inventories local candidate files only. It does not download data, grant a license, infer labels from model outputs, or calculate formula accuracy.'
}

$outputFull = Resolve-RepoPath $OutputPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputFull) | Out-Null
$output | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $outputFull -Encoding utf8

if (-not [string]::IsNullOrWhiteSpace($MarkdownPath)) {
    $markdownFull = Resolve-RepoPath $MarkdownPath
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('# Formula evaluation data availability audit')
    $lines.Add('')
    $lines.Add(('Generated `{0}`. This is an inventory of local candidate files; it is not an accuracy report.' -f $output.generatedUtc))
    $lines.Add('')
    $lines.Add('| Measure | Count |')
    $lines.Add('| --- | ---: |')
    $lines.Add("| Formula image candidates | $($output.formulaImageCandidateCount) |")
    $lines.Add("| Label/manifest candidates with label-like fields | $($output.labelManifestCandidateCount) |")
    $lines.Add("| Multi-equation label candidates | $($output.multiEquationLabeledCandidateCount) |")
    $lines.Add("| Official single-image sample candidates | $($output.officialSingleImageCandidateCount) |")
    $lines.Add('')
    $lines.Add(('Admission: **{0}**. {1}' -f $output.admission.status, $output.admission.reason))
    $lines.Add('')
    $lines.Add('| Source | Exists | Candidate files | Path |')
    $lines.Add('| --- | :---: | ---: | --- |')
    foreach ($root in $roots) {
        $count = @($candidates | Where-Object { $_['source'] -eq $root.id }).Count
        $lines.Add(('| `{0}` | {1} | {2} | `{3}` |' -f $root.id, ([bool]$root.exists), $count, $root.path))
    }
    if ($candidates.Count -gt 0) {
        $lines.Add('')
        $lines.Add('| Source | Kind | Relative path | Size | Label signal | Records | SHA-256 |')
        $lines.Add('| --- | --- | --- | ---: | :---: | ---: | --- |')
        foreach ($row in ($candidates | Sort-Object source, relativePath)) {
            $sha = if ($null -eq $row['sha256']) { 'not hashed' } else { $row['sha256'] }
            $lines.Add(('| `{0}` | {1} | `{2}` | {3} | {4} | {5} | `{6}` |' -f $row['source'], $row['kind'], $row['relativePath'], $row['sizeBytes'], ([bool]$row['labelSignal']), $row['recordCount'], $sha))
        }
    }
    $lines.Add('')
    $lines.Add('A candidate file is not admitted automatically: license, schema, image-to-label alignment and multi-equation coverage still require manual review. Rerun this script after adding a legally usable formula dataset; only then create a separate normalized LaTeX/CER report.')
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $markdownFull) | Out-Null
    $lines -join [Environment]::NewLine | Set-Content -LiteralPath $markdownFull -Encoding utf8
}

Write-Output ('Audited {0} formula-named candidates; images={1}; labelCandidates={2}; multiEquationCandidates={3}; output={4}' -f $candidates.Count, $formulaImages.Count, $labelCandidates.Count, $multiEquationCandidates.Count, $outputFull)
