[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model\ocr\ppocrv5',
    [string]$EvidencePath = '',
    [string]$ReferenceRoot = '',
    [string]$SourceEvidenceRoot = '',
    [switch]$RequireReleaseEligible
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path $PSScriptRoot 'paddleocr-release-admission.json'
}
if ([string]::IsNullOrWhiteSpace($ReferenceRoot)) {
    $ReferenceRoot = Join-Path $repositoryRoot 'artifacts\paddleocr-reference'
}
if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "PaddleOCR release-admission evidence does not exist: $EvidencePath"
}

$evidence = Get-Content -Raw -LiteralPath $EvidencePath | ConvertFrom-Json
if ($evidence.schemaVersion -ne '1.0') { throw 'Unsupported PaddleOCR release-admission evidence schema.' }
if ($evidence.upstreamCode.pinnedRevision -ne '2661c7c0ef5c613e8f93c6e93b2e052399f0f854') { throw 'The pinned PaddleOCR source revision drifted.' }
if ($evidence.releaseAdmission.state -ne 'blocked-external-only' -or $evidence.releaseAdmission.redistributionAllowed) { throw 'This evidence must remain external-only until a reviewed release admission changes it.' }
$licenseReviewPath = Join-Path $repositoryRoot ([string]$evidence.releaseAdmission.licenseReviewEvidence).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $licenseReviewPath -PathType Leaf)) { throw "License review evidence is missing: $licenseReviewPath" }
$licenseReview = Get-Content -Raw -LiteralPath $licenseReviewPath | ConvertFrom-Json
if ($licenseReview.decision.redistributionApproved -or $licenseReview.decision.state -ne 'blocked-external-only' -or @($licenseReview.modelArchives).Count -ne 6) { throw 'License review evidence must keep all six model archives blocked and unapproved.' }
if ($evidence.exportReproduction.status -ne 'byte-identical-all-six' -or $evidence.exportReproduction.invocationId -ne 'paddle2onnx-export-defaults-v1') { throw 'The locked PaddleOCR export-reproduction contract drifted.' }
$expectedToolchain = @{
    'paddlepaddle' = @{ Version = '3.0.0.dev20250613'; Sha256 = '37012c64c278f4761a1dfa3c711b0d8507b1981a640d5763879a550584c32319' }
    'paddle2onnx' = @{ Version = '2.0.2rc3'; Sha256 = 'a76c241ea8102991b97061cad55b3e524ba01225a4c1f2031472b782df9f2562' }
    'onnx' = @{ Version = '1.17.0'; Sha256 = '659b8232d627a5460d74fd3c96947ae83db6d03f035ac633e20cd69cfa029227' }
    'protobuf' = @{ Version = '5.29.3'; Sha256 = 'a4fa6f80816a9a0678429e84973f2f98cbc218cca434abe8db2ad0bffc98503a' }
    'polygraphy' = @{ Version = '0.49.24'; Sha256 = '1e5964a24af34d21b1f2f1817536b54625ac8c7fd7464d567d0a4fbae9cff8cc' }
    'onnx-graphsurgeon' = @{ Version = '0.5.8'; Sha256 = '6f611ea29a8e4740fbab1aae52bf4c40b8b9918f8459058d20b99acc79fce121' }
    'onnxruntime' = @{ Version = '1.22.0'; Sha256 = 'c0d534a43d1264d1273c2d4f00a5a588fa98d21117a3345b7104fa0bbcaadb9a' }
    'numpy' = @{ Version = '2.5.2'; Sha256 = '28ac63476ec7651484215ee7fa15a1f78b57c14621f01e392afe17b9a1390ce4' }
}
$observedToolchain = @{}
foreach ($package in @($evidence.exportReproduction.toolchainLock)) {
    if ($observedToolchain.ContainsKey($package.package)) { throw "Duplicate export dependency '$($package.package)'." }
    $observedToolchain[$package.package] = $package
}
foreach ($packageName in $expectedToolchain.Keys) {
    if (-not $observedToolchain.ContainsKey($packageName)) { throw "The export dependency lock is missing '$packageName'." }
    $expected = $expectedToolchain[$packageName]
    $actual = $observedToolchain[$packageName]
    if ($actual.version -ne $expected.Version -or $actual.sha256 -ne $expected.Sha256) { throw "The export dependency lock drifted for '$packageName'." }
}
$modelRoots = [System.Collections.Generic.List[string]]::new()
$modelRoots.Add($ModelRoot)
$siblingRoot = Join-Path (Split-Path -Parent $ModelRoot) 'ppocrv5-1'
if ((Test-Path -LiteralPath $siblingRoot -PathType Container) -and -not [string]::Equals($siblingRoot, $ModelRoot, [System.StringComparison]::OrdinalIgnoreCase)) { $modelRoots.Add($siblingRoot) }
if ([string]::IsNullOrWhiteSpace($SourceEvidenceRoot)) {
    $SourceEvidenceRoot = Join-Path $siblingRoot ([string][char]0x6E90 + [string][char]0x6587 + [string][char]0x4EF6)
}

function Assert-Sha256 {
    param([string]$Path, [string]$Expected, [string]$Description)
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    if ($actual -ne $Expected) { throw "SHA256 mismatch for $Description. Expected $Expected, got $actual." }
}

function Get-ManifestModelFile {
    param([object]$Manifest)
    $files = @($Manifest.artifacts[0].files | Where-Object { $_.role -eq 'model' })
    if ($files.Count -ne 1) { throw "Manifest '$($Manifest.modelId)' must contain exactly one model file." }
    return $files[0]
}

function Test-ObservedFile {
    param([string]$Path, [long]$ExpectedSize, [string]$ExpectedSha256, [string]$Description)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    if ([long](Get-Item -LiteralPath $Path).Length -ne $ExpectedSize) { throw "Size mismatch for $Description at $Path." }
    Assert-Sha256 -Path $Path -Expected $ExpectedSha256 -Description $Description
    return $true
}

function Read-ProtobufVarint {
    param([System.IO.BinaryReader]$Reader)
    [UInt64]$value = 0
    for ([int]$shift = 0; $shift -lt 64; $shift += 7) {
        [byte]$current = $Reader.ReadByte()
        $value = $value -bor (([UInt64]($current -band 0x7f)) -shl $shift)
        if (($current -band 0x80) -eq 0) { return $value }
    }
    throw 'Invalid protobuf varint.'
}

function Skip-ProtobufField {
    param([System.IO.BinaryReader]$Reader, [int]$WireType)
    switch ($WireType) {
        0 { [void](Read-ProtobufVarint -Reader $Reader) }
        1 { [void]$Reader.BaseStream.Seek(8, [System.IO.SeekOrigin]::Current) }
        2 {
            [long]$length = [long](Read-ProtobufVarint -Reader $Reader)
            [void]$Reader.BaseStream.Seek($length, [System.IO.SeekOrigin]::Current)
        }
        5 { [void]$Reader.BaseStream.Seek(4, [System.IO.SeekOrigin]::Current) }
        default { throw "Unsupported protobuf wire type $WireType while reading ONNX metadata." }
    }
}

function Get-OnnxDefaultOpset {
    param([string]$Path)
    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        while ($stream.Position -lt $stream.Length) {
            [UInt64]$tag = Read-ProtobufVarint -Reader $reader
            [int]$fieldNumber = [int]($tag -shr 3)
            [int]$wireType = [int]($tag -band 7)
            if ($fieldNumber -ne 8 -or $wireType -ne 2) {
                Skip-ProtobufField -Reader $reader -WireType $wireType
                continue
            }

            [long]$messageLength = [long](Read-ProtobufVarint -Reader $reader)
            [long]$messageEnd = $stream.Position + $messageLength
            [string]$domain = ''
            [Nullable[Int64]]$version = $null
            while ($stream.Position -lt $messageEnd) {
                [UInt64]$opsetTag = Read-ProtobufVarint -Reader $reader
                [int]$opsetField = [int]($opsetTag -shr 3)
                [int]$opsetWire = [int]($opsetTag -band 7)
                if ($opsetField -eq 1 -and $opsetWire -eq 2) {
                    [int]$domainLength = [int](Read-ProtobufVarint -Reader $reader)
                    $domain = [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($domainLength))
                }
                elseif ($opsetField -eq 2 -and $opsetWire -eq 0) {
                    $version = [Int64](Read-ProtobufVarint -Reader $reader)
                }
                else {
                    Skip-ProtobufField -Reader $reader -WireType $opsetWire
                }
            }
            if ([string]::IsNullOrEmpty($domain) -and $null -ne $version) { return [int]$version }
        }
        throw "The ONNX default-domain opset is missing: $Path"
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

$archiveById = @{}
$officialArchiveCount = 0
foreach ($archive in @($evidence.officialInferenceArchives)) {
    $sourceUri = [System.Uri]$archive.sourceUrl
    if ($sourceUri.Scheme -ne 'https' -or $sourceUri.Host -ne 'paddle-model-ecology.bj.bcebos.com') { throw "Unexpected official archive URL for $($archive.evidenceId)." }
    if ($archive.scope -ne 'official-paddle-inference-source-and-semantic-reference') { throw "Official archive '$($archive.evidenceId)' has an invalid evidence scope." }
    if ($archiveById.ContainsKey($archive.evidenceId)) { throw "Duplicate official archive evidence '$($archive.evidenceId)'." }
    $archiveById[$archive.evidenceId] = $archive
    $officialFileName = [System.IO.Path]::GetFileName($sourceUri.AbsolutePath)
    $archiveCandidates = @(
        (Join-Path $ReferenceRoot $archive.localFileName),
        (Join-Path $ReferenceRoot $officialFileName),
        (Join-Path $SourceEvidenceRoot $archive.localFileName),
        (Join-Path $SourceEvidenceRoot $officialFileName)
    ) | Select-Object -Unique
    foreach ($archivePath in $archiveCandidates) {
        if (Test-ObservedFile -Path $archivePath -ExpectedSize ([long]$archive.size) -ExpectedSha256 $archive.sha256 -Description $archive.evidenceId) {
            $officialArchiveCount++
            break
        }
    }
}

$reproductionByModel = @{}
foreach ($result in @($evidence.exportReproduction.results)) {
    if ($reproductionByModel.ContainsKey($result.modelId)) { throw "Duplicate export reproduction for $($result.modelId)." }
    if (-not $result.exactCandidateMatch) { throw "Export reproduction is not byte-identical for $($result.modelId)." }
    if (-not $archiveById.ContainsKey($result.sourceArchiveEvidenceId)) { throw "Export reproduction references an unknown source archive for $($result.modelId)." }
    $reproductionByModel[$result.modelId] = $result
}

$inferenceMetadataCount = 0
$observedMetadataByModel = @{}
foreach ($metadata in @($evidence.localInferenceMetadata)) {
    if ($metadata.scope -ne 'local-observation-not-export-provenance') { throw "Inference metadata '$($metadata.modelId)' has an invalid evidence scope." }
    if ($observedMetadataByModel.ContainsKey($metadata.modelId)) { throw "Duplicate inference metadata for $($metadata.modelId)." }
    $observedMetadataByModel[$metadata.modelId] = $metadata
    $relativePath = $metadata.relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $metadataPath = Join-Path $SourceEvidenceRoot $relativePath
    if (Test-ObservedFile -Path $metadataPath -ExpectedSize ([long]$metadata.size) -ExpectedSha256 $metadata.sha256 -Description "$($metadata.modelId) inference metadata") { $inferenceMetadataCount++ }
}

$manifestRoot = Join-Path $PSScriptRoot 'manifests'
$available = 0
$verifiedOnnxOpsets = 0
$verifiedSourceInputs = 0
$verifiedExactReproductions = 0
$missing = [System.Collections.Generic.List[string]]::new()
foreach ($record in @($evidence.artifacts)) {
    $manifestPath = Join-Path $manifestRoot $record.manifestFile
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Manifest is missing for $($record.modelId): $manifestPath" }
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $manifestFile = Get-ManifestModelFile $manifest
    if ($manifest.modelId -ne $record.modelId -or [int]$manifest.artifacts[0].opset -ne [int]$record.opset -or $manifestFile.sha256 -ne $record.sha256 -or [long]$manifestFile.size -ne [long]$record.size) {
        throw "Manifest identity does not match release-admission evidence for $($record.modelId)."
    }
    if ($manifest.source.redistributionAllowed) { throw "Manifest '$($record.modelId)' unexpectedly allows redistribution." }
    if ($record.redistributionApproved) { throw "Evidence '$($record.modelId)' cannot claim redistribution approval while admission is external-only." }
    if (-not $observedMetadataByModel.ContainsKey($record.modelId)) { throw "Inference metadata observation is missing for $($record.modelId)." }
    if (-not $reproductionByModel.ContainsKey($record.modelId)) { throw "Export reproduction evidence is missing for $($record.modelId)." }
    $reproduction = $reproductionByModel[$record.modelId]
    if ($record.checkpointSource -ne $reproduction.sourceArchiveEvidenceId -or $record.exportCommand -ne $evidence.exportReproduction.invocationId) { throw "Source or export binding drifted for $($record.modelId)." }
    $sourceArchive = $archiveById[$record.checkpointSource]
    if ([long]$reproduction.generatedOnnxSize -ne [long]$record.size -or $reproduction.generatedOnnxSha256 -ne $record.sha256) { throw "Reproduced ONNX identity drifted for $($record.modelId)." }
    if ($manifest.exporter.name -ne 'paddle2onnx' -or $manifest.exporter.version -ne '2.0.2rc3' -or $manifest.source.sourceUrl -ne $sourceArchive.sourceUrl -or $manifest.source.revision -ne "sha256:$($sourceArchive.sha256)") { throw "Manifest provenance does not match release-admission evidence for $($record.modelId)." }
    $extensions = $manifest.artifacts[0].extensions
    if ($extensions.'deploysharp.source-inference-archive-sha256' -ne $sourceArchive.sha256 -or $extensions.'deploysharp.export-reproducibility' -ne 'byte-identical;paddle2onnx-2.0.2rc3;paddlepaddle-3.0.0.dev20250613;default-options') { throw "Manifest export-reproduction extensions drifted for $($record.modelId)." }

    $metadata = $observedMetadataByModel[$record.modelId]
    $sourceDirectory = Split-Path -Parent ($metadata.relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $sourceJson = Join-Path (Join-Path $SourceEvidenceRoot $sourceDirectory) 'inference.json'
    $sourceParams = Join-Path (Join-Path $SourceEvidenceRoot $sourceDirectory) 'inference.pdiparams'
    if (Test-ObservedFile -Path $sourceJson -ExpectedSize ([long]$reproduction.inferenceJsonSize) -ExpectedSha256 $reproduction.inferenceJsonSha256 -Description "$($record.modelId) inference.json") { $verifiedSourceInputs++ }
    if (Test-ObservedFile -Path $sourceParams -ExpectedSize ([long]$reproduction.inferenceParamsSize) -ExpectedSha256 $reproduction.inferenceParamsSha256 -Description "$($record.modelId) inference.pdiparams") { $verifiedSourceInputs++ }
    $verifiedExactReproductions++

    $localPath = $null
    foreach ($root in $modelRoots) {
        $candidate = Join-Path $root $record.localFileName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $localPath = $candidate; break }
    }
    if ($null -eq $localPath) {
        $missing.Add($record.localFileName)
        continue
    }
    if ([long](Get-Item -LiteralPath $localPath).Length -ne [long]$record.size) { throw "Size mismatch for $localPath." }
    Assert-Sha256 -Path $localPath -Expected $record.sha256 -Description $record.modelId
    $actualOpset = Get-OnnxDefaultOpset -Path $localPath
    if ($actualOpset -ne [int]$record.opset) { throw "ONNX opset mismatch for $($record.modelId). Expected $($record.opset), got $actualOpset." }
    $verifiedOnnxOpsets++
    $available++
}

$dictionary = $evidence.sharedArtifacts.dictionary
$dictionaryPath = $null
foreach ($root in $modelRoots) {
    $candidate = Join-Path $root $dictionary.localFileName
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { $dictionaryPath = $candidate; break }
}
if ($null -ne $dictionaryPath) {
    if ([long](Get-Item -LiteralPath $dictionaryPath).Length -ne [long]$dictionary.size) { throw "Size mismatch for $dictionaryPath." }
    Assert-Sha256 -Path $dictionaryPath -Expected $dictionary.sha256 -Description 'PP-OCRv5 dictionary'
}
else {
    $missing.Add($dictionary.localFileName)
}

$openBlockers = @($evidence.blockers | Where-Object { $_.status -eq 'open' })
if ($openBlockers.Count -ne 2 -or $openBlockers.id -notcontains 'license-and-redistribution' -or $openBlockers.id -notcontains 'immutable-release-binding') { throw 'The release-admission evidence must retain exactly the license/redistribution and immutable-release blockers.' }
$missingSummary = if ($missing.Count -eq 0) { 'none' } else { [string]::Join(',', $missing) }
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_ADMISSION_BLOCKED verifiedLocalArtifacts=$available/$(@($evidence.artifacts).Count);verifiedOnnxOpsets=$verifiedOnnxOpsets/$(@($evidence.artifacts).Count);verifiedOfficialArchives=$officialArchiveCount/$(@($evidence.officialInferenceArchives).Count);verifiedInferenceMetadata=$inferenceMetadataCount/$(@($evidence.localInferenceMetadata).Count);verifiedSourceInputs=$verifiedSourceInputs/$(@($evidence.artifacts).Count * 2);verifiedExactReproductions=$verifiedExactReproductions/$(@($evidence.artifacts).Count);missingLocalArtifacts=$missingSummary;openBlockers=$($openBlockers.Count);redistributionAllowed=false"

if ($RequireReleaseEligible) {
    throw 'PaddleOCR is not release eligible: license/redistribution approval and immutable release binding remain open.'
}
