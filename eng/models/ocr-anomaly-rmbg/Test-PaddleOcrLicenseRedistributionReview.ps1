[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\ocr\ppocrv5',
    [string]$EvidencePath = '',
    [string]$ReferenceRoot = '',
    [string]$SourceEvidenceRoot = '',
    [switch]$VerifyRemoteSources,
    [switch]$RequireRedistributionApproval
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($EvidencePath)) { $EvidencePath = Join-Path $PSScriptRoot 'paddleocr-license-redistribution-review.json' }
if ([string]::IsNullOrWhiteSpace($ReferenceRoot)) { $ReferenceRoot = Join-Path $repositoryRoot 'artifacts\paddleocr-reference' }
$siblingRoot = Join-Path (Split-Path -Parent $ModelRoot) 'ppocrv5-1'
if ([string]::IsNullOrWhiteSpace($SourceEvidenceRoot)) { $SourceEvidenceRoot = Join-Path $siblingRoot ([string][char]0x6E90 + [string][char]0x6587 + [string][char]0x4EF6) }

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) { throw "License review evidence does not exist: $EvidencePath" }
$evidence = Get-Content -Raw -LiteralPath $EvidencePath | ConvertFrom-Json
if ($evidence.schemaVersion -ne '1.0') { throw 'Unsupported PaddleOCR license review evidence schema.' }
if ($evidence.upstreamCode.pinnedRevision -ne '2661c7c0ef5c613e8f93c6e93b2e052399f0f854') { throw 'PaddleOCR license evidence is pinned to an unexpected source revision.' }
if ($evidence.decision.redistributionApproved) { throw 'License review evidence cannot approve redistribution without an attributable release decision.' }

function Assert-FileEvidence {
    param([string]$Path, [long]$ExpectedSize, [string]$ExpectedSha256, [string]$Description)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing ${Description}: $Path" }
    $item = Get-Item -LiteralPath $Path
    if ([long]$item.Length -ne $ExpectedSize) { throw "Size mismatch for $Description." }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedSha256) { throw "SHA256 mismatch for $Description. Expected $ExpectedSha256, got $actual." }
}

if ([int]$evidence.upstreamCode.licenseObservation.httpStatus -ne 200 -or $evidence.upstreamCode.licenseObservation.sha256 -ne '3840c5c0c61c294264d2dd77b8777be6ddd90121ef4e0e64abcd22edea581d6e') { throw 'Pinned PaddleOCR LICENSE observation drifted.' }
if ([int]$evidence.upstreamCode.readmeLicenseObservation.httpStatus -ne 200 -or -not $evidence.upstreamCode.readmeLicenseObservation.licenseStatementObserved) { throw 'Pinned PaddleOCR README license observation is incomplete.' }
if (@($evidence.officialWebsite.pages).Count -lt 3 -or @($evidence.officialWebsite.pages | Where-Object { $_.httpStatus -ne 200 }).Count -ne 0 -or @($evidence.officialWebsite.pages | Where-Object { $_.modelLicenseStatementObserved }).Count -ne 0) { throw 'Official PaddleOCR website license observations are incomplete or unexpectedly permissive.' }
if ([int]$evidence.dictionary.sourceObservation.httpStatus -ne 200 -or -not $evidence.dictionary.sourceObservation.exactLocalMatch) { throw 'The dictionary source identity observation is incomplete.' }

$modelRoots = @($ModelRoot, $siblingRoot) | Select-Object -Unique
function Find-LocalFile {
    param([string]$FileName)
    foreach ($root in $modelRoots) {
        $candidate = Join-Path $root $FileName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}

$dictionaryPath = Find-LocalFile $evidence.dictionary.localFileName
if ($null -eq $dictionaryPath) { throw "The reviewed dictionary is missing: $($evidence.dictionary.localFileName)" }
Assert-FileEvidence -Path $dictionaryPath -ExpectedSize ([long]$evidence.dictionary.localSize) -ExpectedSha256 $evidence.dictionary.localSha256 -Description 'PP-OCRv5 dictionary'

if (-not (Get-Command tar -ErrorAction SilentlyContinue)) { throw 'The license review requires tar to inspect official model archive entries.' }
$archiveCount = 0
$licenseEntryCount = 0
$noticeEntryCount = 0
foreach ($archive in @($evidence.modelArchives)) {
    $uri = [Uri]$archive.sourceUrl
    $officialFileName = [IO.Path]::GetFileName($uri.AbsolutePath)
    $candidates = @(
        (Join-Path $ReferenceRoot $officialFileName),
        (Join-Path $ReferenceRoot $archive.evidenceId),
        (Join-Path $SourceEvidenceRoot $officialFileName)
    ) | Select-Object -Unique
    $archivePath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ($null -eq $archivePath) { throw "Missing official model archive for $($archive.modelId)." }
    Assert-FileEvidence -Path $archivePath -ExpectedSize ([long]$archive.size) -ExpectedSha256 $archive.sha256 -Description $archive.modelId
    $entries = @(tar -tf $archivePath)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect archive entries for $($archive.modelId)." }
    $licenseEntries = @($entries | Where-Object { $_ -match '(?i)(^|/)(license|copying)(\.|$)' })
    $noticeEntries = @($entries | Where-Object { $_ -match '(?i)(^|/)notice(\.|$)' })
    if ($licenseEntries.Count -ne [int]$archive.archiveLicenseEntries) { throw "License entry count drifted for $($archive.modelId)." }
    if ($noticeEntries.Count -ne [int]$archive.archiveNoticeEntries) { throw "NOTICE entry count drifted for $($archive.modelId)." }
    $licenseEntryCount += $licenseEntries.Count
    $noticeEntryCount += $noticeEntries.Count
    $archiveCount++
}

if ($VerifyRemoteSources) {
    $remoteSources = @($evidence.upstreamCode.licenseSourceUrl, $evidence.upstreamCode.readmeLicenseSourceUrl, $evidence.dictionary.sourceUrl) + @($evidence.officialWebsite.pages | ForEach-Object { $_.url }) + @($evidence.modelArchives | ForEach-Object { $_.sourceUrl })
    foreach ($url in $remoteSources) {
        $response = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 30
        if ([int]$response.StatusCode -ne 200) { throw "Remote evidence source did not return HTTP 200: $url" }
    }
}

$status = 'DEPLOYSHARP_PADDLE_OCR_LICENSE_REVIEW_BLOCKED'
Write-Output "$status archives=$archiveCount/$(@($evidence.modelArchives).Count);archiveLicenseEntries=$licenseEntryCount;archiveNoticeEntries=$noticeEntryCount;codeLicense=Apache-2.0-observed;dictionaryExactSource=true;noticeBundle=not-recorded;redistributionApproved=false"
if ($RequireRedistributionApproval) { throw 'PaddleOCR redistribution is not approved: model permission, dictionary terms and NOTICE bundle remain open.' }
