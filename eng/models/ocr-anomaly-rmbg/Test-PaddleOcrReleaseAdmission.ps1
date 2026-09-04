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
if ($evidence.releaseAdmission.state -ne 'preview-algorithm-admission-blocked') { throw 'The PaddleOCR algorithm-admission state drifted.' }
if (-not [bool]$evidence.releaseAdmission.catalogRedistributionDeclared -or [bool]$evidence.releaseAdmission.algorithmAdmissionRedistributionApproved) {
    throw 'The catalog publication fact and AlgorithmVerified redistribution decision must remain explicitly separated.'
}
if ($evidence.releaseAdmission.immutableReleaseAsset -ne 'closed-public-prerelease' -or $evidence.releaseAdmission.releaseBoundGoldenAudit -ne 'local-ort-openvino-parity-and-independent-official-predictor-complete') {
    throw 'The immutable Release or release-bound golden boundary drifted.'
}
$licenseReviewPath = Join-Path $repositoryRoot ([string]$evidence.releaseAdmission.licenseReviewEvidence).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $licenseReviewPath -PathType Leaf)) { throw "License review evidence is missing: $licenseReviewPath" }
$licenseReview = Get-Content -Raw -LiteralPath $licenseReviewPath | ConvertFrom-Json
if ($licenseReview.decision.redistributionApproved -or $licenseReview.decision.state -ne 'blocked-external-only' -or @($licenseReview.modelArchives).Count -ne 6) { throw 'License review evidence must keep all six model archives blocked and unapproved.' }
$officialOnnx = @{}
$expectedOfficialOnnx = @{
    'paddleocr/ppocrv5/mobile-det/external' = @{ ArchiveSize = 4843520; ArchiveSha256 = '781056046c9ed77a15c94681605db6a0f62317c2e9cce6931c71da2478d4bc30'; OnnxSize = 4826518; OnnxSha256 = 'a431985659dc921974177a95adcfbb90fd9e51989a5e04d70d0b75f597b6e61d' }
    'paddleocr/ppocrv5/mobile-rec/external' = @{ ArchiveSize = 16701440; ArchiveSha256 = 'f7e792bc836f36e7ef895ad47c426d75b0b75b1650caa6d63fe9418441ffba8c'; OnnxSize = 16534782; OnnxSha256 = 'da72dc72ca4dc220df0dfde68c1dedc31c58d3e76a25871122e5056227d50092' }
    'paddleocr/ppocrv5/mobile-cls/external' = @{ ArchiveSize = 1044480; ArchiveSha256 = 'e29f1bffb2cec4db1ef8da9b2369d033d0a16d0a1a8f033b518d6063e6b9a1af'; OnnxSize = 1019454; OnnxSha256 = '94a6a0a0425f2b5f08b5df72086f2d72abe40f1d22f6d12d2cd83674f11f2ff3' }
    'paddleocr/ppocrv5/server-det/external' = @{ ArchiveSize = 88135680; ArchiveSha256 = 'cd28389ed2c11dfe02d6a9847ec95ca6153a51bf38bc1c4e2521c1f548188f58'; OnnxSize = 88116791; OnnxSha256 = '10803475a591f7dc623e24670fb5752ec94d39a1f8cf069aac1b6f0ce19cfc85' }
    'paddleocr/ppocrv5/server-rec/external' = @{ ArchiveSize = 84674560; ArchiveSha256 = '67af91d7ab16288116d578c8055d1d4d114c8380d8059ad3c044cd52cae206f1'; OnnxSize = 84503027; OnnxSha256 = 'd9dc333c9c7b042c6dffb8e33d72b6f65c9c1d463d0a3c2f78174fea55e94752' }
    'paddleocr/ppocrv5/server-cls/external' = @{ ArchiveSize = 6799360; ArchiveSha256 = '274241c75b18f4c1787915383f6a2b73a76f2b56e5023d581af1ce856ba98e1e'; OnnxSize = 6777816; OnnxSha256 = '38aa97cd4be591e0ad304e659f07ba30d946f27a63315433f6659c69c8778345' }
}
foreach ($archive in @($evidence.officialOnnxInferenceArchives)) {
    if ($officialOnnx.ContainsKey([string]$archive.modelId)) { throw "Duplicate official ONNX source identity '$($archive.modelId)'." }
    if (-not $expectedOfficialOnnx.ContainsKey([string]$archive.modelId)) { throw "Unexpected official ONNX source identity '$($archive.modelId)'." }
    $expected = $expectedOfficialOnnx[[string]$archive.modelId]
    $uri = [Uri]$archive.sourceUrl
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'paddle-model-ecology.bj.bcebos.com' -or -not $uri.AbsolutePath.EndsWith('_onnx_infer.tar', [StringComparison]::Ordinal)) { throw "Official ONNX source URL is not an immutable Paddle archive for $($archive.modelId)." }
    if ([long]$archive.archiveSize -ne $expected.ArchiveSize -or [string]$archive.archiveSha256 -ne $expected.ArchiveSha256 -or [long]$archive.onnxSize -ne $expected.OnnxSize -or [string]$archive.onnxSha256 -ne $expected.OnnxSha256 -or -not ([string]$archive.archiveEntry).EndsWith('/inference.onnx', [StringComparison]::Ordinal)) { throw "Official ONNX source identity drifted for $($archive.modelId)." }
    if ([string]$archive.scope -ne 'official-paddle-onnx-inference-archive;not-the-current-DeploySharp-ModelPack') { throw "Official ONNX source scope drifted for $($archive.modelId)." }
    $officialOnnx[[string]$archive.modelId] = $archive
}
if ($officialOnnx.Count -ne $expectedOfficialOnnx.Count) { throw "Expected $($expectedOfficialOnnx.Count) official ONNX source contracts, found $($officialOnnx.Count)." }
if ($evidence.sharedArtifacts.dictionary.sourceRevision -ne $evidence.upstreamCode.pinnedRevision -or -not ([string]$evidence.sharedArtifacts.dictionary.sourceUrl).EndsWith('/ppocrv5_dict.txt', [StringComparison]::Ordinal) -or $evidence.sharedArtifacts.dictionary.sourceStatus -ne 'official-pinned-repository-file') { throw 'Official dictionary source binding is incomplete.' }
$officialPredictorEvidencePath = Join-Path $repositoryRoot ([string]$evidence.releaseAdmission.officialPredictorGoldenEvidence).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $officialPredictorEvidencePath -PathType Leaf)) { throw "Official Paddle Predictor golden evidence is missing: $officialPredictorEvidencePath" }
$officialPredictor = Get-Content -Raw -LiteralPath $officialPredictorEvidencePath | ConvertFrom-Json
if ($officialPredictor.schemaVersion -ne '1.0' -or $officialPredictor.purpose -ne 'independent official Paddle Predictor golden for PP-OCRv5 mobile-cls') { throw 'Official Paddle Predictor golden schema drifted.' }
if ($officialPredictor.runtime.engine -ne 'paddle.inference.Predictor' -or $officialPredictor.runtime.paddleVersion -ne '3.4.0.dev20260129' -or $officialPredictor.runtime.paddleCommit -ne '5d0f669bd5911b75da3ff9b7e8a8d39f3c91de31' -or $officialPredictor.runtime.device -ne 'cpu' -or -not [bool]$officialPredictor.runtime.irOptimization) { throw 'Official Paddle Predictor runtime identity drifted.' }
if ([long]$officialPredictor.model.inferenceJson.size -ne 104123 -or $officialPredictor.model.inferenceJson.sha256 -ne '8b3f80ca25f4765594640ddb3cb26f507f00e7e5d11bfef4c2915a6b9dff5d3c' -or [long]$officialPredictor.model.inferenceParams.size -ne 985712 -or $officialPredictor.model.inferenceParams.sha256 -ne '08ee1ce4bcdb30dd4e784862334d1df49d80a600294d75daae4ca4c70bc860e9') { throw 'Official Paddle Predictor source-model identity drifted.' }
if ([long]$officialPredictor.input.image.size -ne 3996 -or $officialPredictor.input.image.sha256 -ne '872200f57a1408e7aab2856d5f2c687b3a937805e0c4ff74bd7de21df1f742b9' -or $officialPredictor.input.sha256 -ne '7cda055c7450b2e6f52d5993a827dbd1c202ae8044d3fdbb132453b602c2d340') { throw 'Official Paddle Predictor input identity drifted.' }
if ($officialPredictor.output.name -ne 'fetch_name_0' -or $officialPredictor.output.sha256 -ne 'd2820ebee4744ef48a7897cd888c659f5f733ae2b618b638577ad30902181e5d' -or [int]$officialPredictor.output.classIndex -ne 1 -or $officialPredictor.output.label -ne '180_degree' -or [Math]::Abs([double]$officialPredictor.output.confidence - 0.9986026883125305) -gt 0.00001) { throw 'Official Paddle Predictor output golden drifted.' }
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

function Assert-Sequence {
    param([object[]]$Actual, [object[]]$Expected, [string]$Description)
    if ([string]::Join(',', $Actual) -ne [string]::Join(',', $Expected)) { throw "$Description drifted." }
}

$algorithmCandidate = $evidence.algorithmCandidate
if ($algorithmCandidate.catalogModelId -ne 'paddleocr/ppocrv5/mobile-cls' -or $algorithmCandidate.externalEvidenceModelId -ne 'paddleocr/ppocrv5/mobile-cls/external' -or $algorithmCandidate.catalogStatus -ne 'preview') {
    throw 'The selected PaddleOCR algorithm candidate identity drifted.'
}
$release = $algorithmCandidate.release
if ($release.repository -ne 'guojin-yan/DeploySharp' -or $release.tag -ne 'models-20260903.visual.1' -or $release.commit -ne '3c868b0bf7234ebb8af30034716cb37519cb53e0') {
    throw 'The selected PaddleOCR Release identity drifted.'
}

$releaseManifestPath = Join-Path $repositoryRoot ([string]$release.manifestFile).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $releaseManifestPath -PathType Leaf)) { throw "The release-bound ModelPack is missing: $releaseManifestPath" }
if ([long](Get-Item -LiteralPath $releaseManifestPath).Length -ne [long]$release.manifestSize) { throw 'The release-bound ModelPack size drifted.' }
Assert-Sha256 -Path $releaseManifestPath -Expected $release.manifestSha256 -Description 'release-bound mobile-cls ModelPack'
$releaseManifest = Get-Content -Raw -LiteralPath $releaseManifestPath | ConvertFrom-Json
if ($releaseManifest.modelId -ne $algorithmCandidate.catalogModelId -or $releaseManifest.profileId -ne $algorithmCandidate.profileId -or $releaseManifest.extensions.'deploysharp.release-tag' -ne $release.tag) {
    throw 'The release-bound ModelPack identity drifted.'
}
if (-not [bool]$releaseManifest.source.redistributionAllowed) { throw 'The published ModelPack redistribution declaration drifted.' }
Assert-Sequence -Actual @($releaseManifest.inputs[0].shape) -Expected @($algorithmCandidate.contract.inputShape) -Description 'mobile-cls input shape'
Assert-Sequence -Actual @($releaseManifest.outputs[0].shape) -Expected @($algorithmCandidate.contract.outputShape) -Description 'mobile-cls output shape'
if ($releaseManifest.inputs[0].name -ne $algorithmCandidate.contract.inputName -or $releaseManifest.inputs[0].elementType -ne $algorithmCandidate.contract.inputElementType -or $releaseManifest.outputs[0].name -ne $algorithmCandidate.contract.outputName -or $releaseManifest.outputs[0].elementType -ne $algorithmCandidate.contract.outputElementType) {
    throw 'The mobile-cls named tensor contract drifted.'
}
$releaseModelFile = @($releaseManifest.artifacts[0].files | Where-Object role -eq 'model')
if ($releaseModelFile.Count -ne 1 -or $releaseModelFile[0].sha256 -ne $release.modelSha256 -or [long]$releaseModelFile[0].size -ne [long]$release.modelSize) {
    throw 'The release-bound mobile-cls model identity drifted.'
}

$externalManifestPath = Join-Path $PSScriptRoot 'manifests\ppocrv5-mobile-cls.modelpack.json'
$externalManifest = Get-Content -Raw -LiteralPath $externalManifestPath | ConvertFrom-Json
$externalExtensions = $externalManifest.artifacts[0].extensions
if ($externalExtensions.'deploysharp.preprocessing-version' -ne $algorithmCandidate.contract.preprocessingVersion -or $externalExtensions.'deploysharp.prepared-tensor-sha256' -ne $algorithmCandidate.golden.preparedTensorSha256 -or $externalExtensions.'deploysharp.label-order' -ne ([string]::Join(',', @($algorithmCandidate.contract.labelOrder))) -or [double]$externalExtensions.'deploysharp.reject-threshold' -ne [double]$algorithmCandidate.contract.rejectionThreshold) {
    throw 'The mobile-cls preprocessing, label order, threshold, or prepared tensor binding drifted.'
}
if ($externalExtensions.'deploysharp.release-admission' -ne 'blocked-license-redistribution' -or -not ([string]$externalExtensions.'deploysharp.official-golden').Contains('independent-Paddle-Predictor-output-recorded', [System.StringComparison]::Ordinal) -or -not ([string]$externalExtensions.'deploysharp.official-golden').Contains('official-predictor-output-sha256-d2820ebe', [System.StringComparison]::Ordinal)) {
    throw 'The mobile-cls manifest admission or golden scope drifted.'
}
if ($algorithmCandidate.golden.scope -ne 'pinned-official-image-and-preprocessing-semantics-with-local-export-and-independent-Paddle-Predictor-output' -or $algorithmCandidate.golden.officialPredictorOutputStatus -ne 'recorded' -or $algorithmCandidate.golden.officialPredictorGoldenEvidence -ne 'eng/models/ocr-anomaly-rmbg/paddleocr-official-predictor-golden.json' -or $algorithmCandidate.golden.officialPredictorOutputSha256 -ne $officialPredictor.output.sha256) {
    throw 'The independent official-Predictor evidence boundary drifted.'
}
if ($algorithmCandidate.golden.sourceUrl -ne 'https://paddle-model-ecology.bj.bcebos.com/paddlex/imgs/demo_image/textline_rot180_demo.jpg' -or [long]$algorithmCandidate.golden.imageSize -ne 3996 -or $algorithmCandidate.golden.imageSha256 -ne '872200f57a1408e7aab2856d5f2c687b3a937805e0c4ff74bd7de21df1f742b9' -or $algorithmCandidate.golden.referenceOutputSha256 -ne '7b2495af2f5a8bcc459041a65440f7a3900c43e022601aa9e49e912b96ea0dd5') {
    throw 'The fixed mobile-cls input or reference output identity drifted.'
}
if ([int]$algorithmCandidate.golden.classIndex -ne 1 -or $algorithmCandidate.golden.label -ne '180_degree' -or [double]$algorithmCandidate.golden.confidence -ne 0.9986026883125305 -or [double]$algorithmCandidate.golden.confidenceTolerance -ne 0.00001) {
    throw 'The mobile-cls semantic golden or tolerance drifted.'
}
Assert-Sequence -Actual @($algorithmCandidate.golden.backends) -Expected @('onnxruntime-cpu', 'openvino-cpu') -Description 'mobile-cls golden backend matrix'

$catalogPath = Join-Path $repositoryRoot 'src\DeploySharp.ModelFactory\catalog\deploysharp-official-catalog.json'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json
$catalogCandidate = @($catalog.entries | Where-Object modelId -eq $algorithmCandidate.catalogModelId)
if ($catalogCandidate.Count -ne 1 -or $catalogCandidate[0].status -ne 'preview' -or -not [bool]$catalogCandidate[0].source.redistributionAllowed -or $catalogCandidate[0].release.tag -ne $release.tag -or $catalogCandidate[0].release.commit -ne $release.commit) {
    throw 'The mobile-cls catalog admission boundary drifted.'
}
$catalogAssets = @($catalogCandidate[0].artifacts[0].assets)
$catalogManifestAsset = @($catalogAssets | Where-Object assetId -eq 'paddleocr-ppocrv5-mobile-cls-modelpack')
$catalogModelAsset = @($catalogAssets | Where-Object assetId -eq 'paddleocr-ppocrv5-mobile-cls-model')
if ($catalogManifestAsset.Count -ne 1 -or $catalogManifestAsset[0].relativePath -ne $release.manifestAssetName -or [long]$catalogManifestAsset[0].size -ne [long]$release.manifestSize -or $catalogManifestAsset[0].sha256 -ne $release.manifestSha256) {
    throw 'The catalog-to-ModelPack Release binding drifted.'
}
if ($catalogModelAsset.Count -ne 1 -or [long]$catalogModelAsset[0].size -ne [long]$release.modelSize -or $catalogModelAsset[0].sha256 -ne $release.modelSha256 -or -not ([string]$catalogModelAsset[0].downloadUrl).EndsWith('/' + [string]$release.modelAssetName, [System.StringComparison]::Ordinal)) {
    throw 'The catalog-to-model Release binding drifted.'
}

$publishedAuditRoot = Join-Path $repositoryRoot ('artifacts\model-release-public-audit-' + [string]$release.tag)
$publishedChecksumPath = Join-Path $publishedAuditRoot ([string]$release.checksumAssetName)
if (Test-Path -LiteralPath $publishedChecksumPath -PathType Leaf) {
    if ([long](Get-Item -LiteralPath $publishedChecksumPath).Length -ne [long]$release.checksumSize) { throw 'The cached public SHA256SUMS size drifted.' }
    Assert-Sha256 -Path $publishedChecksumPath -Expected $release.checksumSha256 -Description 'cached public SHA256SUMS'
    $checksumText = Get-Content -Raw -LiteralPath $publishedChecksumPath
    if ($checksumText -notmatch ([regex]::Escape([string]$release.manifestSha256) + '\s+' + [regex]::Escape([string]$release.manifestAssetName)) -or $checksumText -notmatch ([regex]::Escape([string]$release.modelSha256) + '\s+' + [regex]::Escape([string]$release.modelAssetName))) {
        throw 'The cached public SHA256SUMS does not bind the mobile-cls assets.'
    }
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
if ($openBlockers.Count -ne 1 -or $openBlockers.id -notcontains 'license-and-redistribution') { throw 'The release-admission evidence must retain the attributable legal redistribution blocker.' }
$missingSummary = if ($missing.Count -eq 0) { 'none' } else { [string]::Join(',', $missing) }
Write-Output "DEPLOYSHARP_PADDLE_OCR_RELEASE_ADMISSION_BLOCKED candidate=$($algorithmCandidate.catalogModelId);catalogStatus=preview;immutableReleaseBinding=closed;releaseBoundLocalGolden=closed;verifiedLocalArtifacts=$available/$(@($evidence.artifacts).Count);verifiedOnnxOpsets=$verifiedOnnxOpsets/$(@($evidence.artifacts).Count);verifiedOfficialArchives=$officialArchiveCount/$(@($evidence.officialInferenceArchives).Count);verifiedOfficialOnnxSources=$($officialOnnx.Count)/$($expectedOfficialOnnx.Count);verifiedInferenceMetadata=$inferenceMetadataCount/$(@($evidence.localInferenceMetadata).Count);verifiedSourceInputs=$verifiedSourceInputs/$(@($evidence.artifacts).Count * 2);verifiedExactReproductions=$verifiedExactReproductions/$(@($evidence.artifacts).Count);missingLocalArtifacts=$missingSummary;openBlockers=$($openBlockers.Count);algorithmAdmissionRedistributionApproved=false;officialPredictorOutputStatus=recorded"

if ($RequireReleaseEligible) {
    throw 'PaddleOCR mobile-cls is not AlgorithmVerified eligible: attributable legal redistribution approval remains open.'
}
