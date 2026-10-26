$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../Confirm-PaddleOcrVlReleaseGeneration.ps1'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$reportPath = Join-Path $repoRoot 'eng/models/paddle-ocr/verification/paddleocr-vl-release-six-task-offline-linux-20261026.json'

$valid = & $scriptPath -ReportPath $reportPath
if ($valid -notmatch 'validated=6') { throw 'Valid report was not accepted.' }

$temporary = New-TemporaryFile
try {
    $altered = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $altered.cases[0].tokenIdsCsvSha256 = '0' * 64
    $altered | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $temporary.FullName -Encoding utf8
    $rejected = $false
    try { & $scriptPath -ReportPath $temporary.FullName | Out-Null }
    catch { $rejected = $_.Exception.Message -match 'Reference mismatch: ocr-demo-1' }
    if (-not $rejected) { throw 'Altered Token ID hash was not rejected.' }
}
finally {
    Remove-Item -LiteralPath $temporary.FullName -Force
}

Write-Output 'valid=pass; altered-token-hash=rejected'
