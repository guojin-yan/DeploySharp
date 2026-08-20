[CmdletBinding()]
param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot 'platform-support.json'),
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\', '/')
$matrix = Get-Content -LiteralPath (Resolve-Path -LiteralPath $MatrixPath).Path -Raw | ConvertFrom-Json

function Assert-ExactSet {
    param([object[]]$Actual, [object[]]$Expected, [string]$Description)
    $actualValues = @($Actual | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $expectedValues = @($Expected | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    if ($actualValues.Count -ne $expectedValues.Count -or (Compare-Object $expectedValues $actualValues)) {
        throw "$Description mismatch. Expected '$($expectedValues -join ',')'; found '$($actualValues -join ',')'."
    }
}

$expectedLevels = @('BuildOnly', 'ManagedTested', 'NativeSmoke', 'GoldenVerified', 'ReleaseSupported')
$expectedPlatforms = @('windows-x64-cpu', 'linux-x64-cpu', 'linux-arm64-cpu', 'macos-cpu', 'windows-arm64-cpu', 'windows-x64-gpu', 'windows-npu')
$allowedStatuses = @('tested', 'untested', 'planned', 'unsupported')
if ([string]$matrix.schemaVersion -ne '1.0' -or [string]$matrix.releaseVersion -ne '2.0.0-alpha.1') { throw 'Platform support matrix schema or release version is invalid.' }
Assert-ExactSet @($matrix.supportLevels) $expectedLevels 'Support levels'
Assert-ExactSet @($matrix.platforms.platformId) $expectedPlatforms 'Platform IDs'
if (@($matrix.platforms.platformId | Sort-Object -Unique).Count -ne @($matrix.platforms).Count) { throw 'Platform IDs must be unique.' }

[xml]$centralDocument = Get-Content -LiteralPath (Join-Path $repository 'Directory.Packages.props') -Raw
$centralVersions = @{}
foreach ($node in @($centralDocument.SelectNodes("//*[local-name()='PackageVersion']"))) { $centralVersions[[string]$node.Include] = [string]$node.Version }
$candidate = Get-Content -LiteralPath (Join-Path $repository 'eng\pack\release-candidate-packages.json') -Raw | ConvertFrom-Json
$candidateIds = @($candidate.packages.packageId)
$testedPlatform = @($matrix.platforms | Where-Object { $_.platformId -eq 'windows-x64-cpu' })
if ($testedPlatform.Count -ne 1 -or [string]$testedPlatform[0].status -ne 'tested') { throw 'Windows x64 CPU must be the only current tested platform record.' }
Assert-ExactSet @($testedPlatform[0].claims.packageId) $candidateIds 'Windows x64 CPU package claims'

$claimCount = 0
$levelCounts = @{}
foreach ($level in $expectedLevels) { $levelCounts[$level] = 0 }
foreach ($platform in @($matrix.platforms)) {
    if ($allowedStatuses -notcontains [string]$platform.status) { throw "Invalid platform status: $($platform.platformId)/$($platform.status)." }
    $claims = @($platform.claims)
    $blockers = @($platform.blockers)
    if ([string]$platform.status -eq 'tested') {
        if ($claims.Count -eq 0) { throw "Tested platform has no claims: $($platform.platformId)." }
    }
    elseif ($claims.Count -ne 0 -or $blockers.Count -eq 0) {
        throw "Untested/planned/unsupported platform must have zero positive claims and at least one blocker: $($platform.platformId)."
    }

    if (@($claims.packageId | Sort-Object -Unique).Count -ne $claims.Count) { throw "Duplicate package claim: $($platform.platformId)." }
    foreach ($claim in $claims) {
        $claimCount++
        $level = [string]$claim.level
        if ($expectedLevels -notcontains $level) { throw "Invalid support level: $($platform.platformId)/$($claim.packageId)/$level." }
        if ($level -eq 'ReleaseSupported') { throw 'Alpha platform matrix cannot contain ReleaseSupported claims.' }
        $levelCounts[$level]++
        if ($candidateIds -notcontains [string]$claim.packageId) { throw "Unknown release package claim: $($claim.packageId)." }
        if ([string]::IsNullOrWhiteSpace([string]$claim.scope)) { throw "Platform claim scope is missing: $($claim.packageId)." }
        $evidence = @($claim.evidence)
        if ($evidence.Count -eq 0) { throw "Platform claim evidence is missing: $($claim.packageId)." }
        foreach ($relativePath in $evidence) {
            $path = Join-Path $repository ([string]$relativePath).Replace('/', '\')
            if (-not (Test-Path -LiteralPath $path)) { throw "Platform evidence path is missing: $relativePath." }
        }

        $runtimeDependencies = @($claim.runtimeDependencies)
        if ($level -in @('NativeSmoke', 'GoldenVerified') -and $runtimeDependencies.Count -eq 0) { throw "Native/golden claim lacks runtime identity: $($claim.packageId)." }
        foreach ($dependency in $runtimeDependencies) {
            if (-not $centralVersions.ContainsKey([string]$dependency.id) -or $centralVersions[[string]$dependency.id] -ne [string]$dependency.version) {
                throw "Platform runtime version drift: $($claim.packageId)/$($dependency.id)/$($dependency.version)."
            }
        }
        if ($level -eq 'GoldenVerified') {
            if ([string]$claim.goldenKind -notin @('contract-fixture', 'local-model', 'official-model')) { throw "Golden kind is invalid: $($claim.packageId)." }
            if ([string]$claim.modelSha256 -notmatch '^[0-9a-f]{64}$') { throw "Golden model SHA-256 is invalid: $($claim.packageId)." }
        }
    }
}

Write-Output "DEPLOYSHARP_PLATFORM_MATRIX_OK platforms=$(@($matrix.platforms).Count) claims=$claimCount build-only=$($levelCounts.BuildOnly) managed-tested=$($levelCounts.ManagedTested) native-smoke=$($levelCounts.NativeSmoke) golden-verified=$($levelCounts.GoldenVerified) release-supported=$($levelCounts.ReleaseSupported) unverified-platforms=$(@($matrix.platforms | Where-Object { $_.status -ne 'tested' }).Count)"
