[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ProjectPath = 'tools/DeploySharp.ModelFactory.Cli/DeploySharp.ModelFactory.Cli.csproj'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$project = Join-Path $repository $ProjectPath
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "CLI project was not found: $project" }

function Invoke-CliJson {
    param([string[]]$Arguments)

    $output = @(& dotnet run --project $project -c Release --no-build --no-restore -- @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    if ($LASTEXITCODE -ne 0) {
        throw "ModelFactory CLI failed ($LASTEXITCODE): $($output -join [Environment]::NewLine)"
    }

    $text = $output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($text)) { throw 'ModelFactory CLI returned empty output.' }
    try { return $text | ConvertFrom-Json }
    catch { throw "ModelFactory CLI returned invalid JSON: $text" }
}

Push-Location $repository
try {
    $doctor = Invoke-CliJson @('doctor', '--json')
    if ($doctor.status -ne 'ok' -or [string]::IsNullOrWhiteSpace([string]$doctor.catalogRevision) -or $doctor.catalogEntries -le 0) {
        throw 'doctor JSON did not report a healthy catalog.'
    }

    $catalog = Invoke-CliJson @('list', '--preview', '--json')
    if ($catalog.entries.Count -le 0 -or [string]::IsNullOrWhiteSpace([string]$catalog.catalogRevision)) {
        throw 'list JSON did not return catalog entries.'
    }
    foreach ($entry in $catalog.entries) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.modelId) -or [string]::IsNullOrWhiteSpace([string]$entry.artifact)) {
            throw 'list JSON contains an incomplete artifact row.'
        }
    }

    $details = Invoke-CliJson @('show', '--model-id', 'bria/rmbg-2.0', '--preview', '--json')
    if ($details.modelId -ne 'bria/rmbg-2.0' -or $details.artifacts.Count -le 0) {
        throw 'show JSON did not return the requested model.'
    }
    foreach ($artifact in $details.artifacts) {
        if ($artifact.assets.Count -le 0) { throw "Artifact $($artifact.artifactId) has no assets." }
    }

    $previewOutput = @(& dotnet run --project $project -c Release --no-build --no-restore -- show --model-id bria/rmbg-2.0 2>&1)
    if ($LASTEXITCODE -eq 0) { throw 'show unexpectedly allowed a Preview model without --preview.' }

    # The non-zero code above is the expected policy rejection. Clear it so
    # the validator itself returns success to CI and local release scripts.
    $global:LASTEXITCODE = 0

    Write-Output ('DEPLOYSHARP_MODELFACTORY_CLI_OK artifactRows={0} artifacts={1} checks=4' -f $catalog.entries.Count, $details.artifacts.Count)
}
finally {
    Pop-Location
}
