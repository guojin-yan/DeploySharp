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
$v1Types = @{
    yolov5 = 'YOLOv5Det'; yolov6 = 'YOLOv6Det'; yolov7 = 'YOLOv7Det'; yolov8 = 'YOLOv8Det'; yolov9 = 'YOLOv9Det'
    yolov10 = 'YOLOv10Det'; yolo11 = 'YOLOv11Det'; yolo12 = 'YOLOv12Det'; yolov13 = 'YOLOv13Det'; yolo26 = 'YOLOv26Det'
}

if ($support.models.Count -ne 10) { throw "Expected 10 YOLO detection rows, found $($support.models.Count)." }
if (-not $guide.Contains([string]$support.validationImage.preparedTensorSha256)) {
    throw "YOLO guide is missing prepared tensor evidence '$($support.validationImage.preparedTensorSha256)'."
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
    $v1Type = $v1Types[[string]$model.family]
    if ([string]::IsNullOrWhiteSpace($v1Type) -or -not $migration.Contains('`' + $v1Type + '`')) {
        throw "V1 migration matrix is missing '$($model.family)'."
    }
}
