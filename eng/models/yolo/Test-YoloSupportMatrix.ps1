[CmdletBinding()]
param(
    [string]$SupportFile,
    [string]$GuideFile,
    [string]$MigrationFile
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SupportFile)) { $SupportFile = Join-Path $PSScriptRoot 'yolo-detection-support.json' }
if ([string]::IsNullOrWhiteSpace($GuideFile)) { $GuideFile = Join-Path $PSScriptRoot '..\..\..\docs\articles\visual-yolo-detection.md' }
if ([string]::IsNullOrWhiteSpace($MigrationFile)) { $MigrationFile = Join-Path $PSScriptRoot '..\..\..\docs\articles\v1-model-migration.md' }

& (Join-Path $PSScriptRoot 'Write-YoloModelPackCandidates.ps1') -SupportFile $SupportFile -Check

$support = Get-Content -Raw -LiteralPath $SupportFile | ConvertFrom-Json
$guide = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $GuideFile))
$migration = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $MigrationFile))
if ($support.models.Count -ne 10) { throw "Expected 10 YOLO detection rows, found $($support.models.Count)." }
if (-not $guide.Contains([string]$support.validationImage.preparedTensorSha256)) {
    throw "YOLO guide is missing prepared tensor evidence '$($support.validationImage.preparedTensorSha256)'."
}
if (-not $migration.Contains('visual-yolo-detection.md')) {
    throw 'V1 migration guide must link to the V2 YOLO detection guide.'
}
foreach ($model in $support.models) {
    foreach ($required in @($model.sha256)) {
        if (-not $guide.Contains([string]$required)) { throw "YOLO guide is missing '$required'." }
    }
    if ($null -ne $model.openVinoIr) {
        foreach ($required in @($model.openVinoIr.xmlSha256, $model.openVinoIr.binSha256)) {
            if (-not $guide.Contains([string]$required)) { throw "YOLO guide is missing IR evidence '$required'." }
        }
    }
}
