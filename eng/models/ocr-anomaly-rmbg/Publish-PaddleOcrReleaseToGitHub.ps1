[CmdletBinding()]
param(
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$Tag = 'models-20260818.ppocrv5.1',
    [string]$StageRoot = 'artifacts\model-release-ppocrv5-20260818',
    [int]$MaximumAttempts = 5,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$stageDirectory = Join-Path $repositoryRoot $StageRoot
$assetPlanPath = Join-Path $stageDirectory 'release-assets.json'
$null = & (Join-Path $PSScriptRoot 'Publish-PaddleOcrReleaseAssets.ps1') -OutputRoot (Split-Path -Parent $StageRoot) -Tag $Tag -Repository $Repository -Check
if (-not $?) { throw 'Local PP-OCR release asset validation failed.' }
$assetPlan = Get-Content -Raw -LiteralPath $assetPlanPath | ConvertFrom-Json
if ([string]$assetPlan.tag -ne $Tag -or [string]$assetPlan.repository -ne $Repository) { throw 'Staged release plan identity does not match the requested GitHub release.' }

$credentialInput = "protocol=https`nhost=github.com`n`n"
$credential = $credentialInput | git credential fill
$credentialParts = @{}
foreach ($line in $credential) {
    $pair = $line -split '=', 2
    if ($pair.Count -eq 2) { $credentialParts[$pair[0]] = $pair[1] }
}
if ([string]::IsNullOrWhiteSpace($credentialParts['password'])) { throw 'GitHub credentials are unavailable.' }

$headers = @{
    Authorization = 'Bearer ' + $credentialParts['password']
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'DeploySharp-paddleocr-release-publisher'
}
$apiBase = 'https://api.github.com/repos/' + $Repository
$uploadBase = 'https://uploads.github.com/repos/' + $Repository

function Get-ReleaseByTag {
    $response = Invoke-WebRequest -NoProxy -UseBasicParsing -Headers $headers -Uri ($apiBase + '/releases?per_page=100')
    $allReleases = @($response.Content | ConvertFrom-Json)
    $found = @($allReleases | Where-Object { [string]$_.tag_name -eq [string]$Tag })
    if ($found.Count -gt 1) { throw "More than one GitHub release uses tag '$Tag'." }
    if ($found.Count -eq 0) { return $null }
    return ,$found[0]
}

function Get-ReleaseAssets {
    param([long]$ReleaseId)
    $response = Invoke-WebRequest -NoProxy -UseBasicParsing -Headers $headers -Uri ($apiBase + '/releases/' + $ReleaseId + '/assets?per_page=100')
    return @($response.Content | ConvertFrom-Json)
}

function Wait-ForUploadedAsset {
    param([long]$ReleaseId, [object]$Expected)
    for ($attempt = 1; $attempt -le 40; $attempt++) {
        $asset = Get-ReleaseAssets $ReleaseId | Where-Object { $_.name -eq [string]$Expected.name } | Select-Object -First 1
        if ($null -ne $asset -and $asset.state -eq 'uploaded' -and $asset.size -eq [long]$Expected.size -and $asset.digest -eq ('sha256:' + [string]$Expected.sha256)) { return $asset }
        Start-Sleep -Seconds 3
    }
    throw "GitHub did not finalize the expected asset: $($Expected.name)"
}

function Remove-StarterAsset {
    param([long]$ReleaseId, [object]$Asset)
    if ($Asset.state -ne 'starter') { throw "Only a starter asset may be removed: $($Asset.name)" }
    $response = Invoke-WebRequest -NoProxy -Method Delete -Headers $headers -Uri ($apiBase + '/releases/assets/' + $Asset.id)
    if ($response.StatusCode -ne 204) { throw "GitHub did not remove starter asset: $($Asset.name)" }
}

$release = Get-ReleaseByTag
if ($null -eq $release) {
    if (-not $Publish) {
        Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_READY tag=$Tag assets=$(@($assetPlan.assets).Count) stage=$stageDirectory"
        return
    }
    $targetCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $targetCommit -notmatch '^[0-9a-f]{40}$') { throw 'Unable to resolve current HEAD for the release tag.' }
    $request = [ordered]@{
        tag_name = $Tag
        target_commitish = $targetCommit
        name = 'PP-OCRv5 ONNX models'
        body = 'PP-OCRv5 ONNX detector, recognizer, classifier, dictionary, license, ModelPack manifests, and SHA-256 checksums.'
        draft = $true
        prerelease = $true
    }
    $release = Invoke-RestMethod -NoProxy -Method Post -Headers $headers -ContentType 'application/json' -Body ($request | ConvertTo-Json) -Uri ($apiBase + '/releases')
    Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_DRAFT_CREATED tag=$Tag id=$($release.id)"
}

if (-not $Publish) {
    Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_DRAFT_READY tag=$Tag id=$($release.id) assets=$(@($assetPlan.assets).Count)"
    return
}
if (-not [bool]$release.draft) { throw "Release tag is already published; choose a new immutable tag instead of uploading again: $Tag" }
$releaseTag = [string]$release.tag_name
$releaseIsDraft = [bool]$release.draft
$releaseIsPrerelease = [bool]$release.prerelease
if ($releaseTag -ne $Tag -or $releaseIsDraft -ne $true -or $releaseIsPrerelease -ne $true) { throw "Release must be a draft prerelease while assets upload: $Tag" }

$uploaded = 0
foreach ($expected in @($assetPlan.assets)) {
    $sourcePath = Join-Path $stageDirectory ([string]$expected.name)
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Staged asset is missing: $sourcePath" }
    $existing = Get-ReleaseAssets $release.id | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
    if ($null -ne $existing) {
        if ($existing.state -eq 'uploaded' -and $existing.size -eq [long]$expected.size -and $existing.digest -eq ('sha256:' + [string]$expected.sha256)) {
            Write-Output "[$Tag] verified-existing $($expected.name)"
            continue
        }
        if ($existing.state -eq 'starter') { Remove-StarterAsset $release.id $existing }
        else { throw "Existing remote asset conflicts with staged content: $($expected.name)" }
    }

    $uploadUri = $uploadBase + '/releases/' + $release.id + '/assets?name=' + [Uri]::EscapeDataString([string]$expected.name)
    $completed = $false
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
        $arguments = @(
            '--fail-with-body', '--silent', '--show-error', '--http1.1', '--request', 'POST',
            '--header', ('Authorization: Bearer ' + $credentialParts['password']),
            '--header', 'Accept: application/vnd.github+json',
            '--header', 'X-GitHub-Api-Version: 2022-11-28',
            '--header', 'Content-Type: application/octet-stream',
            '--data-binary', ('@' + $sourcePath), '--output', 'NUL', $uploadUri
        )
        $curlOutput = & curl.exe @arguments 2>&1
        if ($LASTEXITCODE -eq 0) {
            $completed = $true
            break
        }
        $remote = Get-ReleaseAssets $release.id | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
        if ($null -ne $remote -and $remote.state -eq 'uploaded' -and $remote.size -eq [long]$expected.size -and $remote.digest -eq ('sha256:' + [string]$expected.sha256)) {
            $completed = $true
            break
        }
        if ($null -ne $remote -and $remote.state -eq 'starter') { Remove-StarterAsset $release.id $remote }
        if ($attempt -eq $MaximumAttempts) { throw "Upload failed for $($expected.name): $($curlOutput -join [Environment]::NewLine)" }
        Start-Sleep -Seconds (3 * $attempt)
    }
    if (-not $completed) { throw "Upload did not complete: $($expected.name)" }
    [void](Wait-ForUploadedAsset $release.id $expected)
    $uploaded++
    Write-Output "[$Tag] uploaded $uploaded/$(@($assetPlan.assets).Count) $($expected.name)"
}

$remoteAssets = Get-ReleaseAssets $release.id
foreach ($expected in @($assetPlan.assets)) {
    $asset = $remoteAssets | Where-Object { $_.name -eq [string]$expected.name } | Select-Object -First 1
    if ($null -eq $asset -or $asset.state -ne 'uploaded' -or $asset.size -ne [long]$expected.size -or $asset.digest -ne ('sha256:' + [string]$expected.sha256)) {
        throw "Release asset verification failed: $($expected.name)"
    }
}

$published = Invoke-RestMethod -NoProxy -Method Patch -Headers $headers -ContentType 'application/json' -Body '{"draft":false}' -Uri ($apiBase + '/releases/' + $release.id)
if ($published.draft -or -not $published.prerelease -or [string]$published.tag_name -ne $Tag) { throw "GitHub did not publish the expected prerelease: $Tag" }
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_PUBLISHED tag=$Tag id=$($published.id) assets=$($remoteAssets.Count) url=$($published.html_url)"
