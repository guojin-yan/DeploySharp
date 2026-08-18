[CmdletBinding()]
param(
    [string]$SupportFile,
    [string]$OutputDirectory,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($SupportFile)) { $SupportFile = Join-Path $PSScriptRoot 'yolo-detection-support.json' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot 'manifests' }
$support = Get-Content -Raw -LiteralPath $SupportFile | ConvertFrom-Json
$expected = @{}

foreach ($model in $support.models) {
    $familyId = if ($model.family.StartsWith('yolov', [StringComparison]::Ordinal)) { 'v' + $model.family.Substring(5) } else { 'v' + $model.family.Substring(4) }
    $profileId = 'yolo.detect.' + $familyId + '.' + $model.outputKind + '.onnx.' + $model.modelId
    $onnxArtifact = [ordered]@{
        artifactId = 'onnx.fp32'
        format = 'onnx'
        locationKind = 'file'
        entrypoint = 'model.onnx'
        compatibleBackends = @('onnxruntime', 'openvino')
        files = @([ordered]@{
            relativePath = 'model.onnx'
            sha256 = $model.sha256
            size = [long]$model.size
            mediaType = 'application/onnx'
            role = 'model'
        })
        precision = 'fp32'
        quantization = 'none'
        opset = [int]$model.opset
        portable = $true
        extensions = [ordered]@{
            'deploysharp.validation-status' = 'local-backend-verified'
            'deploysharp.artifact-provenance' = 'unverified-local-file'
            'deploysharp.preprocessing-version' = $support.preprocessingVersion
            'deploysharp.postprocessing-version' = $support.postprocessingVersion
            'deploysharp.validation-image-sha256' = $support.validationImage.sha256
            'deploysharp.prepared-tensor-sha256' = $support.validationImage.preparedTensorSha256
            'deploysharp.release-admission' = 'blocked-pending-license-provenance-and-official-golden-review'
        }
    }
    $artifacts = @($onnxArtifact)
    if ($null -ne $model.openVinoIr) {
        $artifacts += [ordered]@{
            artifactId = 'openvino-ir.fp32'
            format = 'openvino-ir'
            locationKind = 'file'
            entrypoint = 'model.xml'
            compatibleBackends = @('openvino')
            files = @(
                [ordered]@{ relativePath = 'model.xml'; sha256 = $model.openVinoIr.xmlSha256; size = [long]$model.openVinoIr.xmlSize; mediaType = 'application/xml'; role = 'model' },
                [ordered]@{ relativePath = 'model.bin'; sha256 = $model.openVinoIr.binSha256; size = [long]$model.openVinoIr.binSize; mediaType = 'application/octet-stream'; role = 'weights' }
            )
            precision = 'fp32'
            quantization = 'none'
            portable = $true
            extensions = [ordered]@{
                'deploysharp.validation-status' = 'local-backend-verified'
                'deploysharp.artifact-provenance' = 'locally-converted-from-audited-onnx'
                'deploysharp.converter' = 'OpenVINO OVC ' + $model.openVinoIr.converterVersion
                'deploysharp.source-onnx-sha256' = $model.sha256
                'deploysharp.preprocessing-version' = $support.preprocessingVersion
                'deploysharp.postprocessing-version' = $support.postprocessingVersion
                'deploysharp.validation-image-sha256' = $support.validationImage.sha256
                'deploysharp.prepared-tensor-sha256' = $support.validationImage.preparedTensorSha256
                'deploysharp.release-admission' = 'blocked-pending-license-provenance-and-official-golden-review'
            }
        }
    }
    $manifest = [ordered]@{
        schemaVersion = '2.0'
        modelId = $model.modelId
        name = $model.name
        family = $model.family
        task = 'object-detection'
        modelVersion = $model.modelVersion
        exporter = [ordered]@{
            name = 'audited-upstream-onnx-export'
            version = $model.exporterVersion
            sourceRevision = $model.referenceCommit
        }
        source = [ordered]@{
            sourceUrl = $model.repository
            projectUrl = $model.repository
            revision = 'local-artifact-provenance-unverified'
            author = 'Upstream YOLO maintainers'
            licenseExpression = $model.licenseExpression
            redistributionAllowed = $false
        }
        generatedAt = $support.verifiedAt
        profileId = $profileId
        inputs = @([ordered]@{ name = 'images'; elementType = 'float32'; shape = @(1, 3, 640, 640) })
        outputs = @([ordered]@{ name = $model.outputName; elementType = 'float32'; shape = @($model.outputShape) })
        artifacts = $artifacts
    }

    $fileName = $model.family + '-detect-' + $model.modelVersion + '.modelpack.json'
    $json = ($manifest | ConvertTo-Json -Depth 20) + [Environment]::NewLine
    $expected[$fileName] = $json
}

if ($Check) {
    foreach ($item in $expected.GetEnumerator()) {
        $path = Join-Path $OutputDirectory $item.Key
        $matches = $false
        if (Test-Path -LiteralPath $path) {
            try {
                $actualCanonical = ([IO.File]::ReadAllText($path) | ConvertFrom-Json | ConvertTo-Json -Depth 100 -Compress)
                $expectedCanonical = ($item.Value | ConvertFrom-Json | ConvertTo-Json -Depth 100 -Compress)
                $matches = $actualCanonical -ceq $expectedCanonical
            }
            catch {
                $matches = $false
            }
        }
        if (-not $matches) {
            throw "YOLO ModelPack candidate is stale: $path"
        }
    }
    $unexpected = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.modelpack.json' -File | Where-Object { -not $expected.ContainsKey($_.Name) })
    if ($unexpected.Count -ne 0) { throw "Unexpected YOLO ModelPack candidate: $($unexpected[0].FullName)" }
    return
}

[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
foreach ($item in $expected.GetEnumerator()) {
    [IO.File]::WriteAllText((Join-Path $OutputDirectory $item.Key), $item.Value, [Text.UTF8Encoding]::new($false))
}
