param(
    [string]$Destination = 'E:\Model\PaddleDocument\validation\chartqa-chart2table-extended-20261002',
    [string]$Revision = '044eabfc306abfe9340c5741f0093aefc5973d06',
    [ValidateSet('val', 'test', 'train')]
    [string]$Split = 'val',
    [int]$CountPerGroup = 2,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
if ($CountPerGroup -lt 1 -or $CountPerGroup -gt 16) { throw 'CountPerGroup must be between 1 and 16.' }

$repo = 'https://github.com/vis-nlp/ChartQA'
$rawRoot = "https://raw.githubusercontent.com/vis-nlp/ChartQA/$Revision/ChartQA%20Dataset/$Split"
$apiUri = "https://api.github.com/repos/vis-nlp/ChartQA/contents/ChartQA%20Dataset/$Split/png?ref=$Revision"
$headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'DeploySharp-ChartQA-Validation' }
$files = @((Invoke-WebRequest -UseBasicParsing -Headers $headers -Uri $apiUri).Content | ConvertFrom-Json)
$names = @($files | Where-Object { $_.type -eq 'file' -and $_.name -like '*.png' } | ForEach-Object name | Sort-Object)
if ($names.Count -eq 0) { throw "No ChartQA image files found for $Split at $Revision." }

# Keep the smoke set deterministic while covering the repository's common filename groups.
$groups = @(
    @($names | Where-Object { $_ -like 'OECD*' }),
    @($names | Where-Object { $_ -like 'two_col_*' }),
    @($names | Where-Object { $_ -notlike 'OECD*' -and $_ -notlike 'two_col_*' })
)
$selected = [System.Collections.Generic.List[string]]::new()
foreach ($group in $groups) {
    foreach ($name in @($group | Select-Object -First $CountPerGroup)) {
        if (!$selected.Contains($name)) { $selected.Add($name) }
    }
}
if ($selected.Count -lt 3) { throw 'The selected ChartQA groups did not produce enough samples.' }

$imageRoot = Join-Path $Destination 'images'
$tableRoot = Join-Path $Destination 'tables'
New-Item -ItemType Directory -Force -Path $imageRoot, $tableRoot | Out-Null

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Download-Asset([string]$Uri, [string]$Path) {
    if ((Test-Path -LiteralPath $Path) -and !$Force) { return }
    $temporary = "$Path.download"
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    Invoke-WebRequest -UseBasicParsing -Uri $Uri -OutFile $temporary
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Convert-CsvToPipe([string]$Path) {
    $records = @(Import-Csv -LiteralPath $Path)
    if ($records.Count -eq 0) { return '' }
    $headers = @($records[0].PSObject.Properties.Name)
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add((@($headers | ForEach-Object { ([string]$_).Replace('|', '\|') }) -join ' | '))
    foreach ($record in $records) {
        $cells = foreach ($header in $headers) {
            $value = [string]$record.$header
            $value.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
        }
        $lines.Add(($cells -join ' | '))
    }
    return ($lines -join "`n")
}

$samples = [System.Collections.Generic.List[object]]::new()
foreach ($name in $selected) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($name)
    $tableName = "$stem.csv"
    $imagePath = Join-Path $imageRoot $name
    $tablePath = Join-Path $tableRoot $tableName
    $imageUri = "$rawRoot/png/$name"
    $tableUri = "$rawRoot/tables/$tableName"
    Download-Asset $imageUri $imagePath
    Download-Asset $tableUri $tablePath
    if (!(Test-Path -LiteralPath $imagePath) -or !(Test-Path -LiteralPath $tablePath)) { throw "Missing downloaded ChartQA asset for $name." }
    $samples.Add([ordered]@{
        file = $name
        tableFile = $tableName
        expectedText = Convert-CsvToPipe $tablePath
        imageSha256 = Get-Sha256 $imagePath
        tableSha256 = Get-Sha256 $tablePath
        imageUri = $imageUri
        tableUri = $tableUri
    })
}

$manifest = [ordered]@{
    schemaVersion = 1
    dataset = 'ChartQA'
    split = $Split
    sourceRepository = $repo
    sourceRevision = $Revision
    generatedUtc = [DateTimeOffset]::UtcNow
    sampleCount = $samples.Count
    samples = $samples
    boundary = 'Bounded external quality selection; table structure/row/cell metrics are not split-level ChartQA accuracy.'
}
$manifestPath = Join-Path $Destination 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Output "ChartQA manifest: $manifestPath"
Write-Output "Samples: $($samples.Count); revision: $Revision"
