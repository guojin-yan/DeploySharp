[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^2\.0\.0(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string]$PackageVersion,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$BaselinePath = (Join-Path $PSScriptRoot 'release-candidate-packages.json'),
    [switch]$PublicationAuthorized,
    [string]$Approver,
    [string]$ApprovalReference
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PublicationAuthorized -and ([string]::IsNullOrWhiteSpace($Approver) -or [string]::IsNullOrWhiteSpace($ApprovalReference))) {
    throw 'Publication requires an explicit approver and approval reference; validation alone does not authorize publication.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Release configuration output already exists: $output" }
$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
$baseline.packageVersion = $PackageVersion
$preview = $PackageVersion.Contains('-')
$authorization = [ordered]@{
    schemaVersion = '1.0'
    packageVersion = $PackageVersion
    publication = [ordered]@{
        status = if ($PublicationAuthorized) { 'authorized' } else { 'not-authorized' }
        channel = if ($preview) { 'nuget.org' } else { 'nuget.org+github-release+github-packages' }
        approver = if ($PublicationAuthorized) { $Approver } else { $null }
        approvedAt = if ($PublicationAuthorized) { [DateTimeOffset]::UtcNow.ToString('o') } else { $null }
        evidenceReference = if ($PublicationAuthorized) { $ApprovalReference } else { $null }
    }
    packageSigning = [ordered]@{
        status = if ($baseline.packageSigningPolicy -eq 'required') { 'not-configured' } else { 'not-required' }
        certificateSha256Fingerprint = $null
        timestampServer = $null
        scope = 'open-source-release'
    }
    rawPackageReproducibility = [ordered]@{
        status = 'established-before-signing'
        normalizer = 'eng/pack/Normalize-NuGetPackage.ps1'
        verification = 'eng/pack/Invoke-ReleasePackageReproducibility.ps1'
        scope = 'unsigned .nupkg and .snupkg containers before optional signing'
    }
}
New-Item -ItemType Directory -Path $output | Out-Null
foreach ($item in @(
    @{ Name = 'release-candidate-packages.json'; Value = $baseline },
    @{ Name = 'release-authorization.json'; Value = $authorization }
)) {
    $json = (($item.Value | ConvertTo-Json -Depth 100) + "`n").Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $output $item.Name), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output "DEPLOYSHARP_RELEASE_CONFIGURATION_OK version=$PackageVersion channel=$($authorization.publication.channel) publication=$($authorization.publication.status) output=$output"
