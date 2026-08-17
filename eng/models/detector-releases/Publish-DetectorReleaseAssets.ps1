[CmdletBinding()]
param(
    [string]$Repository = 'guojin-yan/DeploySharp',
    [string]$StageRoot = 'artifacts',
    [Parameter(Mandatory = $true)]
    [long]$YoloReleaseId,
    [Parameter(Mandatory = $true)]
    [long]$DetrReleaseId,
    [int]$MaximumAttempts = 5
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
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
    'User-Agent' = 'DeploySharp-release-publisher'
}
$apiBase = 'https://api.github.com/repos/' + $Repository
$uploadBase = 'https://uploads.github.com/repos/' + $Repository

function Get-ReleaseAssets {
    param([long]$ReleaseId)
    return @(Invoke-RestMethod -NoProxy -Headers $headers -Uri ($apiBase + '/releases/' + $ReleaseId + '/assets?per_page=100'))
}

function Wait-ForUploadedAsset {
    param([long]$ReleaseId, [object]$Expected)
    for ($attempt = 1; $attempt -le 40; $attempt++) {
        $asset = Get-ReleaseAssets $ReleaseId | Where-Object { $_.name -eq $Expected.name } | Select-Object -First 1
        if ($null -ne $asset -and $asset.state -eq 'uploaded' -and $asset.size -eq [long]$Expected.size -and $asset.digest -eq ('sha256:' + $Expected.sha256)) { return $asset }
        Start-Sleep -Seconds 3
    }
    throw "GitHub did not finalize the expected asset: $($Expected.name)"
}

foreach ($collection in @('yolo', 'detr')) {
    $stageDirectory = Join-Path $repositoryRoot (Join-Path $StageRoot ('model-release-' + $collection + '-20260817'))
    $assetPlan = Get-Content -Raw -LiteralPath (Join-Path $stageDirectory 'release-assets.json') | ConvertFrom-Json
    $releaseId = if ($collection -eq 'yolo') { $YoloReleaseId } else { $DetrReleaseId }
    $release = Invoke-RestMethod -NoProxy -Headers $headers -Uri ($apiBase + '/releases/' + $releaseId)
    if ($release.tag_name -ne $assetPlan.tag) { throw "Draft release tag does not match staged collection: $($assetPlan.tag)" }
    if (-not $release.draft -or -not $release.prerelease) { throw "Release must remain a draft prerelease until all uploads verify: $($assetPlan.tag)" }
    $uploaded = 0
    foreach ($expected in $assetPlan.assets) {
        $existing = Get-ReleaseAssets $release.id | Where-Object { $_.name -eq $expected.name } | Select-Object -First 1
        if ($null -ne $existing) {
            if ($existing.state -eq 'uploaded' -and $existing.size -eq [long]$expected.size -and $existing.digest -eq ('sha256:' + $expected.sha256)) {
                Write-Host "[$($assetPlan.tag)] verified existing $($expected.name)"
                continue
            }
            throw "Existing remote asset conflicts with expected content: $($expected.name)"
        }

        $sourcePath = Join-Path $stageDirectory $expected.name
        $uploadUri = $uploadBase + '/releases/' + $release.id + '/assets?name=' + [Uri]::EscapeDataString([string]$expected.name)
        $completed = $false
        for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
            try {
                $curlArguments = @(
                    '--fail-with-body', '--silent', '--show-error', '--http1.1', '--request', 'POST',
                    '--header', ('Authorization: Bearer ' + $credentialParts['password']),
                    '--header', 'Accept: application/vnd.github+json',
                    '--header', 'X-GitHub-Api-Version: 2022-11-28',
                    '--header', 'Content-Type: application/octet-stream',
                    '--data-binary', ('@' + $sourcePath), '--output', 'NUL', $uploadUri
                )
                $curlOutput = & curl.exe @curlArguments 2>&1
                if ($LASTEXITCODE -ne 0) { throw ($curlOutput -join [Environment]::NewLine) }
                $completed = $true
                break
            } catch {
                $remoteAfterFailure = Get-ReleaseAssets $release.id | Where-Object { $_.name -eq $expected.name } | Select-Object -First 1
                if ($null -ne $remoteAfterFailure -and $remoteAfterFailure.state -eq 'uploaded' -and $remoteAfterFailure.size -eq [long]$expected.size -and $remoteAfterFailure.digest -eq ('sha256:' + $expected.sha256)) {
                    Write-Warning "Remote upload completed despite a client transport error: $($expected.name)"
                    $completed = $true
                    break
                }
                if ($attempt -eq $MaximumAttempts) { throw }
                Write-Warning "Upload retry $attempt/$MaximumAttempts for $($expected.name): $($_.Exception.Message)"
                Start-Sleep -Seconds (3 * $attempt)
            }
        }
        if (-not $completed) { throw "Upload did not complete: $($expected.name)" }
        [void](Wait-ForUploadedAsset $release.id $expected)
        $uploaded++
        Write-Host "[$($assetPlan.tag)] uploaded $uploaded/$($assetPlan.assets.Count) $($expected.name)"
    }

    $finalAssets = Get-ReleaseAssets $release.id
    foreach ($expected in $assetPlan.assets) {
        $asset = $finalAssets | Where-Object { $_.name -eq $expected.name } | Select-Object -First 1
        if ($null -eq $asset -or $asset.state -ne 'uploaded' -or $asset.size -ne [long]$expected.size -or $asset.digest -ne ('sha256:' + $expected.sha256)) {
            throw "Release asset verification failed: $($assetPlan.tag) / $($expected.name)"
        }
    }
    Write-Host "[$($assetPlan.tag)] all $($assetPlan.assets.Count) release assets verified."
}
