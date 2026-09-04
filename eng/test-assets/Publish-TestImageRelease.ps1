[CmdletBinding()]
param(
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$Tag = 'test-assets.1',
    [string]$OutputRoot = 'artifacts\test-assets',
    [string]$ImageRoot = 'E:\Data\image',
    [string]$OcrRoot = 'E:\Data\ocr',
    [long]$ReleaseId = 0,
    [int]$MaximumAttempts = 5,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($Tag -ne 'test-assets.1') { throw 'The test-image release tag is stable by design; append assets to test-assets.1.' }
& (Join-Path $PSScriptRoot 'Stage-TestImageAssets.ps1') -ImageRoot $ImageRoot -OcrRoot $OcrRoot -OutputRoot $OutputRoot
if (-not $?) { throw 'Test image staging failed.' }
$stageDirectory = Join-Path $repositoryRoot (Join-Path $OutputRoot $Tag)
$assetPlan = Get-Content -Raw -LiteralPath (Join-Path $stageDirectory 'test-image-assets.json') | ConvertFrom-Json
if ([string]$assetPlan.repository -ne $Repository -or [string]$assetPlan.tag -ne $Tag) { throw 'Staged test-image release identity does not match the requested release.' }

function New-ReleaseBody([object]$Plan) {
    $tasksByFile = @{}
    foreach ($property in $Plan.defaults.PSObject.Properties) {
        $fileName = [string]$property.Value
        if (-not $tasksByFile.ContainsKey($fileName)) { $tasksByFile[$fileName] = [System.Collections.Generic.List[string]]::new() }
        $tasksByFile[$fileName].Add([string]$property.Name)
    }
    $lines = [System.Collections.Generic.List[string]]::new()
    [void]$lines.Add('Stable default test images for DeploySharp visual and PaddleOCR examples.')
    [void]$lines.Add('')
    [void]$lines.Add('The asset names are independent of source-machine paths. The repository keeps the mapping and SHA-256 values in eng/test-assets/test-image-catalog.json; future images should be appended to this release and recorded here.')
    [void]$lines.Add('')
    [void]$lines.Add('Current defaults:')
    foreach ($fileName in ($tasksByFile.Keys | Sort-Object)) {
        [void]$lines.Add(('- {0}: {1}' -f $fileName, (($tasksByFile[$fileName] | Sort-Object) -join ', ')))
    }
    return ($lines -join [Environment]::NewLine)
}
$releaseBody = New-ReleaseBody $assetPlan

$credentialInput = "protocol=https`nhost=github.com`n`n"
$credential = $credentialInput | git credential fill
$credentialParts = @{}
foreach ($line in $credential) {
    $parts = $line -split '=', 2
    if ($parts.Count -eq 2) { $credentialParts[$parts[0]] = $parts[1] }
}
if ([string]::IsNullOrWhiteSpace($credentialParts['password'])) { throw 'GitHub credentials are unavailable.' }
$headers = @{
    Authorization = 'Bearer ' + $credentialParts['password']
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'DeploySharp-test-image-publisher'
}
$apiBase = 'https://api.github.com/repos/' + $Repository
$uploadBase = 'https://uploads.github.com/repos/' + $Repository

function Get-Release {
    $releases = @()
    foreach ($releaseItem in (Invoke-RestMethod -NoProxy -Headers $headers -Uri ($apiBase + '/releases?per_page=100'))) { $releases += $releaseItem }
    $matches = @($releases | Where-Object { [string]$_.tag_name -eq $Tag })
    if ($matches.Count -gt 1) { throw "More than one GitHub release uses tag '$Tag'; remove the duplicate draft before retrying." }
    if ($matches.Count -eq 0) { return $null }
    return $matches[0]
}
function Get-Assets([long]$ReleaseId) {
    foreach ($assetItem in (Invoke-RestMethod -NoProxy -Headers $headers -Uri ($apiBase + '/releases/' + $ReleaseId + '/assets?per_page=100'))) { Write-Output $assetItem }
}
function Wait-ForAsset([long]$ReleaseId, [object]$Expected) {
    for ($attempt = 1; $attempt -le 100; $attempt++) {
        $asset = Get-Assets $ReleaseId | Where-Object { $_.name -eq [string]$Expected.name } | Select-Object -First 1
        if ($null -ne $asset -and [string]$asset.state -eq 'uploaded' -and [long]$asset.size -eq [long]$Expected.sizeBytes) {
            if ([string]::IsNullOrWhiteSpace([string]$asset.digest)) { return $asset }
            if ([string]$asset.digest -ne ('sha256:' + [string]$Expected.sha256)) { throw "Remote digest mismatch for '$($Expected.name)': $($asset.digest)" }
            return $asset
        }
        Start-Sleep -Seconds 3
    }
    throw "GitHub did not finalize test image asset '$($Expected.name)'."
}
function Remove-Starter([long]$ReleaseId, [object]$Asset) {
    if ($Asset.state -ne 'starter') { throw "Conflicting remote asset exists: $($Asset.name)" }
    Invoke-WebRequest -NoProxy -Method Delete -Headers $headers -Uri ($apiBase + '/releases/assets/' + $Asset.id) | Out-Null
}
function Remove-MetadataAsset([object]$Asset) {
    if ([string]$Asset.name -notin @('README.md', 'test-image-catalog.json', 'SHA256SUMS')) { throw "Refusing to replace non-metadata asset: $($Asset.name)" }
    Invoke-WebRequest -NoProxy -Method Delete -Headers $headers -Uri ($apiBase + '/releases/assets/' + $Asset.id) | Out-Null
}

$release = if ($ReleaseId -gt 0) { Invoke-RestMethod -NoProxy -Headers $headers -Uri ($apiBase + '/releases/' + $ReleaseId) } else { Get-Release }
if ($null -eq $release) {
    if (-not $Publish) {
        Write-Output "DEPLOYSHARP_TEST_IMAGE_RELEASE_READY tag=$Tag assets=$(@($assetPlan.assets).Count) stage=$stageDirectory"
        return
    }
    $targetCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $request = [ordered]@{ tag_name = $Tag; target_commitish = $targetCommit; name = 'DeploySharp Test Images (alpha preview)'; body = $releaseBody; draft = $true; prerelease = $true }
    $release = Invoke-RestMethod -NoProxy -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json) -Uri ($apiBase + '/releases')
    Write-Output "DEPLOYSHARP_TEST_IMAGE_RELEASE_DRAFT_CREATED tag=$Tag id=$($release.id)"
}
if (-not $Publish) {
    $state = if ([bool]$release.draft) { 'DRAFT_READY' } else { 'READY' }
    Write-Output "DEPLOYSHARP_TEST_IMAGE_RELEASE_$state tag=$Tag id=$($release.id) assets=$(@($assetPlan.assets).Count)"
    return
}
$releaseWasDraft = [bool]$release.draft
if (-not [bool]$release.prerelease) { throw 'Test image assets must be maintained in a prerelease.' }
if ([string]$release.body -ne $releaseBody) {
    $release = Invoke-RestMethod -NoProxy -Method Patch -Headers $headers -ContentType 'application/json' -Body (@{ body = $releaseBody } | ConvertTo-Json) -Uri ($apiBase + '/releases/' + $release.id)
    Write-Output "[$Tag] release notes updated"
}

$uploaded = 0
foreach ($expected in @($assetPlan.assets)) {
    $sourcePath = Join-Path $stageDirectory ([string]$expected.name)
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Staged asset is missing: $sourcePath" }
    $existing = Get-Assets $release.id | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
    if ($null -ne $existing) {
        if ($existing.state -eq 'uploaded' -and [long]$existing.size -eq [long]$expected.sizeBytes -and $existing.digest -eq ('sha256:' + [string]$expected.sha256)) { continue }
        if ([string]$expected.name -in @('README.md', 'test-image-catalog.json', 'SHA256SUMS')) { Remove-MetadataAsset $existing }
        else { Remove-Starter $release.id $existing }
    }
    $uploadUri = $uploadBase + '/releases/' + $release.id + '/assets?name=' + [Uri]::EscapeDataString([string]$expected.name)
    $done = $false
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        $curlArgs = @('--fail-with-body', '--silent', '--show-error', '--http1.1', '--request', 'POST', '--header', ('Authorization: Bearer ' + $credentialParts['password']), '--header', 'Accept: application/vnd.github+json', '--header', 'X-GitHub-Api-Version: 2022-11-28', '--header', 'Content-Type: application/octet-stream', '--data-binary', ('@' + $sourcePath), '--output', 'NUL', $uploadUri)
        $curlOutput = & curl.exe @curlArgs 2>&1
        if ($LASTEXITCODE -eq 0) { $done = $true; break }
        $remote = Get-Assets $release.id | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
        if ($null -ne $remote -and $remote.state -eq 'uploaded' -and [long]$remote.size -eq [long]$expected.sizeBytes -and $remote.digest -eq ('sha256:' + [string]$expected.sha256)) { $done = $true; break }
        if ($attempt -eq $MaximumAttempts) { throw "Upload failed for $($expected.name): $($curlOutput -join [Environment]::NewLine)" }
        Start-Sleep -Seconds (3 * $attempt)
    }
    if (-not $done) { throw "Upload did not complete for $($expected.name)." }
    [void](Wait-ForAsset $release.id $expected)
    $uploaded++
    Write-Output "[$Tag] uploaded $uploaded/$(@($assetPlan.assets).Count) $($expected.name)"
}

$remoteAssets = Get-Assets $release.id
foreach ($expected in @($assetPlan.assets)) {
    $asset = $remoteAssets | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
    if ($null -eq $asset -or $asset.state -ne 'uploaded' -or [long]$asset.size -ne [long]$expected.sizeBytes -or $asset.digest -ne ('sha256:' + [string]$expected.sha256)) { throw "Test image release asset verification failed: $($expected.name)" }
}
if ($releaseWasDraft) {
    $published = Invoke-RestMethod -NoProxy -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"draft":false}' -Uri ($apiBase + '/releases/' + $release.id)
    if ($published.draft -or -not $published.prerelease) { throw 'GitHub did not publish the test image prerelease.' }
    Write-Output "DEPLOYSHARP_TEST_IMAGE_RELEASE_PUBLISHED tag=$Tag id=$($published.id) assets=$($remoteAssets.Count) url=$($published.html_url)"
}
else {
    Write-Output "DEPLOYSHARP_TEST_IMAGE_RELEASE_UPDATED tag=$Tag id=$($release.id) assets=$($remoteAssets.Count) url=$($release.html_url)"
}
