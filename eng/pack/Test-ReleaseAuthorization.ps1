[CmdletBinding()]
param(
    [string]$AuthorizationPath = (Join-Path $PSScriptRoot 'release-authorization.json'),
    [switch]$RequireReleaseEligible
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$path = (Resolve-Path -LiteralPath $AuthorizationPath).Path
$authorization = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
if ($authorization.schemaVersion -ne '1.0' -or $authorization.packageVersion -ne '2.0.0-alpha.1') {
    throw 'Release authorization schema or package version is invalid.'
}
if ($authorization.publication.channel -ne 'nuget.org') { throw 'Release authorization channel is invalid.' }
if ($authorization.packageSigning.status -eq 'not-required-alpha-preview' -and $authorization.packageSigning.scope -ne '2.0.0-alpha.1 personal open-source non-commercial preview only') {
    throw 'The alpha-preview signing exception scope is invalid.'
}
if ($authorization.rawPackageReproducibility.status -ne 'established-before-signing' -or
    $authorization.rawPackageReproducibility.normalizer -ne 'eng/pack/Normalize-NuGetPackage.ps1' -or
    $authorization.rawPackageReproducibility.verification -ne 'eng/pack/Invoke-ReleasePackageReproducibility.ps1') {
    throw 'Release reproducibility policy is invalid.'
}

$blockers = [Collections.Generic.List[string]]::new()
switch ([string]$authorization.publication.status) {
    'authorized' {
        if ([string]::IsNullOrWhiteSpace([string]$authorization.publication.approver) -or
            [string]::IsNullOrWhiteSpace([string]$authorization.publication.approvedAt) -or
            [string]::IsNullOrWhiteSpace([string]$authorization.publication.evidenceReference)) {
            throw 'Authorized publication requires an attributable approver, timestamp, and evidence reference.'
        }
        try { [void][DateTimeOffset]::Parse([string]$authorization.publication.approvedAt, [Globalization.CultureInfo]::InvariantCulture) }
        catch { throw 'Publication approval timestamp is invalid.' }
    }
    'not-authorized' { $blockers.Add('publication-authority-not-granted') }
    default { throw "Unsupported publication authorization state: $($authorization.publication.status)." }
}

switch ([string]$authorization.packageSigning.status) {
    'configured' {
        if ([string]$authorization.packageSigning.certificateSha256Fingerprint -notmatch '^[0-9a-fA-F]{64}$' -or
            [string]::IsNullOrWhiteSpace([string]$authorization.packageSigning.timestampServer)) {
            throw 'Configured package signing requires a certificate SHA256 fingerprint and timestamp server.'
        }
    }
    'not-configured' { $blockers.Add('package-signing-credential-not-configured') }
    'not-required-alpha-preview' { }
    default { throw "Unsupported package-signing state: $($authorization.packageSigning.status)." }
}

$blocked = $blockers.Count -ne 0
$blockerText = if ($blocked) { $blockers -join ',' } else { 'none' }
Write-Output "DEPLOYSHARP_RELEASE_AUTHORIZATION_GATE_OK channel=$($authorization.publication.channel) publication=$($authorization.publication.status) signing=$($authorization.packageSigning.status) raw-reproducibility=$($authorization.rawPackageReproducibility.status) release-eligible=$((-not $blocked).ToString().ToLowerInvariant()) blockers=$blockerText"
if ($RequireReleaseEligible -and $blocked) { throw "Release authorization is blocked: $blockerText." }
