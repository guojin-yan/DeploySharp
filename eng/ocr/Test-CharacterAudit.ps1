param([string]$BenchmarkAssembly)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $BenchmarkAssembly) { $BenchmarkAssembly = Join-Path $repo 'tools/DeploySharp.PaddleOcrBenchmark/bin/Release/net10.0/DeploySharp.PaddleOcrBenchmark.dll' }
$fixtures = Join-Path $repo ('artifacts/ocr-character-audit-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$dictionary = Join-Path $fixtures 'dict.txt'
$requirements = Join-Path $fixtures 'requirements.json'
$report = Join-Path $fixtures 'report.json'
[IO.File]::WriteAllText($dictionary, "A`nA`nAB`n😀`n", [Text.UTF8Encoding]::new($true))
$dictionaryHash = (Get-FileHash -LiteralPath $dictionary).Hash
function Assert-Exit([int]$expected, [string[]]$arguments) {
    $output = & dotnet $BenchmarkAssembly @arguments 2>&1
    if ($LASTEXITCODE -ne $expected) { throw "Expected exit $expected; got $LASTEXITCODE : $output" }
}
[IO.File]::WriteAllText($requirements, '{"basic":"A😀 "}', $utf8)
Assert-Exit 0 @('--audit-characters', $dictionary, $requirements, $report)
$value = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if (-not $value.IsCovered -or $value.Dictionaries[0].Dictionary.DuplicateTokens.Count -ne 1) { throw 'Covered/duplicate evidence mismatch.' }
Assert-Exit 4 @('--audit-characters', $dictionary, $requirements, $report, '--no-space')
[IO.File]::WriteAllText($requirements, '{"business":"ABＡ"}', $utf8)
Assert-Exit 4 @('--audit-characters', $dictionary, $requirements, $report)
$value = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$missing = $value.Dictionaries[0].Groups[0].Coverage.MissingCharacters
if ($missing.Count -ne 2 -or $missing[0].CodePoint -ne 'U+0042' -or -not $missing[0].AppearsInCompoundToken) { throw 'Compound-token evidence mismatch.' }
foreach ($invalid in @('{}', '{"empty":""}', '{"x":"A","x":"B"}', '{"x":"\uD800"}', '{"x":"\uDC00"}', '{"x":"\uD800A"}')) {
    [IO.File]::WriteAllText($requirements, $invalid, $utf8)
    Assert-Exit 2 @('--audit-characters', $dictionary, $requirements, $report)
}
[IO.File]::WriteAllText($requirements, '{"unicode":"\uD83D\uDE00"}', [Text.UTF8Encoding]::new($true))
Assert-Exit 0 @('--audit-characters', $dictionary, $requirements, $report)
[IO.File]::WriteAllText($requirements, ('{"large":"' + ('A' * 65537) + '"}'), $utf8)
Assert-Exit 2 @('--audit-characters', $dictionary, $requirements, $report)
[IO.File]::WriteAllText($requirements, '{"basic":"A"}', $utf8)
Assert-Exit 2 @('--audit-characters', $dictionary, $requirements, $dictionary)
if ((Get-FileHash -LiteralPath $dictionary).Hash -ne $dictionaryHash) { throw 'Dictionary was modified.' }
$badDictionary = Join-Path $fixtures 'bad.txt'
[IO.File]::WriteAllBytes($badDictionary, [byte[]]@(0xC3, 0x28))
Assert-Exit 2 @('--audit-characters', $badDictionary, $requirements, $report)

# A failing startup requirement must stop before attempting to load this invalid ONNX.
$modelRoot = Join-Path $fixtures 'models/PP-OCRv4'
New-Item -ItemType Directory -Path $modelRoot -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $modelRoot 'ppocrv4_rec.onnx'), [byte[]]@())
Copy-Item -LiteralPath $dictionary -Destination (Join-Path $modelRoot 'ppocrv4_keys.txt')
[IO.File]::WriteAllText($requirements, '{"missing":"B"}', $utf8)
$names = @('DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT', 'DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS', 'DEPLOYSHARP_PADDLEOCR_VERSIONS')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT = 'Require'
    $env:DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS = $requirements
    $env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4'
    $csv = Join-Path $fixtures 'startup.csv'
    Assert-Exit 4 @($modelRoot, $csv)
    if (Test-Path -LiteralPath $csv) { throw 'Inference must not start after audit rejection.' }
    if (-not (Test-Path -LiteralPath ($csv + '.characters.json'))) { throw 'Startup rejection must retain its report.' }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
Write-Output "CHARACTER_AUDIT_CLI_OK fixtures=$fixtures"
