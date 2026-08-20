[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$ComparisonPackageDirectory,
    [string]$RepositoryRoot,
    [string]$Configuration = 'Release',
    [string]$CandidateGatePath = (Join-Path $PSScriptRoot 'Test-ReleaseCandidatePackages.ps1'),
    [string]$NormalizerPath = (Join-Path $PSScriptRoot 'Normalize-NuGetPackage.ps1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$primary = (Resolve-Path -LiteralPath $PackageDirectory).Path
$comparison = [IO.Path]::GetFullPath($ComparisonPackageDirectory)
$gate = (Resolve-Path -LiteralPath $CandidateGatePath).Path
$normalizer = (Resolve-Path -LiteralPath $NormalizerPath).Path
if (Test-Path -LiteralPath $comparison) { throw "Comparison package directory already exists: $comparison" }

$baselinePath = Join-Path $PSScriptRoot 'release-candidate-packages.json'
$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
foreach ($definition in @($baseline.packages)) {
    $project = Join-Path $repository ([string]$definition.projectPath).Replace('/', [IO.Path]::DirectorySeparatorChar)
    & dotnet pack $project -c $Configuration --no-restore -o $comparison
    if ($LASTEXITCODE -ne 0) { throw "Comparison pack failed: $($definition.packageId)." }
}

& $normalizer -PackageDirectory $primary
& $normalizer -PackageDirectory $comparison
$gateOutput = & $gate -PackageDirectory $primary -ComparisonPackageDirectory $comparison 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "Release candidate reproducibility gate failed: $gateOutput" }
$expected = @($baseline.packages).Count
if ($gateOutput -notmatch ("raw-identical=$expected/$expected")) {
    throw "Raw NuGet reproducibility was not established. Output: $gateOutput"
}
$symbolMatches = 0
foreach ($definition in @($baseline.packages)) {
    $fileName = "$($definition.packageId).$($baseline.packageVersion).snupkg"
    $primaryHash = (Get-FileHash -LiteralPath (Join-Path $primary $fileName) -Algorithm SHA256).Hash
    $comparisonHash = (Get-FileHash -LiteralPath (Join-Path $comparison $fileName) -Algorithm SHA256).Hash
    if (-not [string]::Equals($primaryHash, $comparisonHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Raw symbol package reproducibility was not established: $fileName."
    }
    $symbolMatches++
}
Write-Output "DEPLOYSHARP_RELEASE_REPRODUCIBILITY_OK packages=$expected raw-nupkg=$expected/$expected raw-snupkg=$expected/$expected normalizer=Normalize-NuGetPackage.ps1"
