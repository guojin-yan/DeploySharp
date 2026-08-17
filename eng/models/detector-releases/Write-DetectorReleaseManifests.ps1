[CmdletBinding()]
param(
    [string]$ModelRoot = 'E:\Model',
    [switch]$FetchLicenses,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$modelsRoot = Split-Path -Parent $PSScriptRoot
$yoloCandidates = Join-Path $modelsRoot 'yolo\manifests'
$yoloReleases = Join-Path $modelsRoot 'yolo\releases'
$detrCandidates = Join-Path $modelsRoot 'detr\manifests'
$detrReleases = Join-Path $modelsRoot 'detr\releases'
$licenseDirectory = Join-Path $PSScriptRoot 'licenses'
$planPath = Join-Path $PSScriptRoot 'detector-release-assets.json'

function New-LicenseDefinition {
    param([string]$Slug, [string]$Expression, [string]$ApiUrl)
    [ordered]@{ Slug = $Slug; Expression = $Expression; ApiUrl = $ApiUrl }
}

$licenses = @(
    (New-LicenseDefinition 'agpl-ultralytics' 'AGPL-3.0-only' 'https://api.github.com/repos/ultralytics/ultralytics/contents/LICENSE?ref=1367566337fb8056223a1aeb469360747f1b1bcd'),
    (New-LicenseDefinition 'gpl-yolo-family' 'GPL-3.0-only' 'https://api.github.com/repos/meituan/YOLOv6/contents/LICENSE?ref=e86a483f3f6bded25d45970b56831345a99744a4'),
    (New-LicenseDefinition 'agpl-yolov12-13' 'AGPL-3.0-only' 'https://api.github.com/repos/sunsmarterjie/yolov12/contents/LICENSE?ref=01a22c0603e0eaa6d9bd62120a391e744d92cea2'),
    (New-LicenseDefinition 'apache-paddledetection' 'Apache-2.0' 'https://api.github.com/repos/PaddlePaddle/PaddleDetection/contents/LICENSE?ref=b25522a0f4bde8c80603f3ba5e3472059972e3b5'),
    (New-LicenseDefinition 'apache-rf-detr' 'Apache-2.0' 'https://api.github.com/repos/roboflow/rf-detr/contents/LICENSE?ref=cc538cea510c24d6d7bc64332f0bf29875a5b2d6'),
    (New-LicenseDefinition 'apache-deimv2' 'Apache-2.0' 'https://api.github.com/repos/Intellindust-AI-Lab/DEIMv2/contents/LICENSE?ref=0fff8d4dcdc272e6cf2d84be31399db471357941')
)

function Get-LicenseInfo {
    param([string]$Slug)
    $license = $licenses | Where-Object Slug -eq $Slug | Select-Object -First 1
    if ($null -eq $license) { throw "Unknown license slug: $Slug" }
    $path = Join-Path $licenseDirectory ($license.Slug + '.txt')
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing license file: $path" }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{
        Slug = $license.Slug
        Expression = $license.Expression
        Url = $license.Url
        Path = $path
        RelativePath = 'source/licenses/' + $file.Name
        Size = [long]$file.Length
        Sha256 = $hash
    }
}

function Convert-FileRecord {
    param([object]$File)
    [ordered]@{
        relativePath = [string]$File.relativePath
        sha256 = ([string]$File.sha256).ToLowerInvariant()
        size = [long]$File.size
        mediaType = [string]$File.mediaType
        role = [string]$File.role
    }
}

function New-PublicArtifact {
    param([object]$Artifact, [object]$License)
    $files = @($Artifact.files | ForEach-Object { Convert-FileRecord $_ })
    foreach ($file in $files) { $file.relativePath = 'bundle/' + $file.relativePath }
    $files += [ordered]@{
        relativePath = 'bundle/source/licenses/' + [IO.Path]::GetFileName($License.RelativePath)
        sha256 = $License.Sha256
        size = $License.Size
        mediaType = 'text/plain'
        role = 'license'
    }
    $extensions = [ordered]@{}
    if ($null -ne $Artifact.extensions) {
        if ($Artifact.extensions -is [System.Collections.IDictionary]) {
            foreach ($key in $Artifact.extensions.Keys) { $extensions[[string]$key] = [string]$Artifact.extensions[$key] }
        } else {
            foreach ($property in $Artifact.extensions.PSObject.Properties) { $extensions[$property.Name] = [string]$property.Value }
        }
    }
    $extensions['deploysharp.release-admission'] = 'alpha-preview-redistributable-source-recorded'
    [ordered]@{
        artifactId = [string]$Artifact.artifactId
        format = [string]$Artifact.format
        locationKind = 'directory'
        entrypoint = 'bundle'
        compatibleBackends = @($Artifact.compatibleBackends)
        files = $files
        precision = [string]$Artifact.precision
        quantization = [string]$Artifact.quantization
        opset = if ($null -ne $Artifact.opset) { [int]$Artifact.opset } else { $null }
        portable = [bool]$Artifact.portable
        extensions = $extensions
    }
}

function New-SourceRecord {
    param([string]$Project, [string]$Revision, [string]$Author, [object]$License)
    [ordered]@{
        sourceUrl = $Project.TrimEnd('/') + '/tree/' + $Revision
        projectUrl = $Project
        revision = $Revision
        author = $Author
        licenseExpression = $License.Expression
        licenseFile = 'bundle/' + $License.RelativePath
        redistributionAllowed = $true
    }
}

function New-Manifest {
    param(
        [string]$ModelId, [string]$Name, [string]$Family, [string]$Task, [string]$ModelVersion,
        [string]$Project, [string]$Revision, [string]$Author, [object]$License,
        [string]$ExporterName, [string]$ExporterVersion, [object]$Inputs, [object]$Outputs,
        [object]$Artifact, [string]$ProfileId, [string]$GeneratedAt
    )
    [ordered]@{
        schemaVersion = '2.0'
        modelId = $ModelId
        name = $Name
        family = $Family
        task = $Task
        modelVersion = $ModelVersion
        exporter = [ordered]@{ name = $ExporterName; version = $ExporterVersion; sourceRevision = $Revision }
        source = New-SourceRecord $Project $Revision $Author $License
        generatedAt = $GeneratedAt
        profileId = $ProfileId
        inputs = @($Inputs)
        outputs = @($Outputs)
        artifacts = @((New-PublicArtifact $Artifact $License))
        extensions = [ordered]@{
            'deploysharp.publication-status' = 'alpha-preview'
            'deploysharp.downloadable' = 'true'
        }
    }
}

if ($FetchLicenses) {
    [IO.Directory]::CreateDirectory($licenseDirectory) | Out-Null
    foreach ($license in $licenses) {
        $path = Join-Path $licenseDirectory ($license.Slug + '.txt')
        $response = Invoke-RestMethod -NoProxy -Headers @{ 'User-Agent' = 'DeploySharp-model-manifest-generator' } -Uri $license.ApiUrl
        [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String(($response.content -replace '\s', '')))
    }
}

foreach ($license in $licenses) { [void](Get-LicenseInfo $license.Slug) }

$generatedAt = '2026-08-17T00:00:00Z'
$planEntries = [System.Collections.Generic.List[object]]::new()
$expected = [ordered]@{}

$detectionPaths = @{
    'yolo/v5/detect/n' = 'yolo\yolov5\yolov5n.onnx'
    'yolo/v6/detect/s' = 'yolo\yolov6s.onnx'
    'yolo/v7/detect/base' = 'yolo\yolov7.onnx'
    'yolo/v8/detect/n' = 'yolo\yolov8\yolov8n.onnx'
    'yolo/v9/detect/s' = 'yolo\yolov9s.onnx'
    'yolo/v10/detect/n' = 'yolo\yolov10\yolov10n.onnx'
    'yolo/v11/detect/n' = 'yolo\yolov11\yolo11n.onnx'
    'yolo/v12/detect/n' = 'yolo\yolov12\yolo12n.onnx'
    'yolo/v13/detect/n' = 'yolo\yolov13n.onnx'
    'yolo/v26/detect/n' = 'yolo\yolov26\yolo26n.onnx'
}

$support = Get-Content -Raw -LiteralPath (Join-Path $modelsRoot 'yolo\yolo-detection-support.json') | ConvertFrom-Json
foreach ($candidate in Get-ChildItem -LiteralPath $yoloCandidates -Filter '*.modelpack.json' -File) {
    $old = Get-Content -Raw -LiteralPath $candidate.FullName | ConvertFrom-Json
    $model = $support.models | Where-Object modelId -eq $old.modelId | Select-Object -First 1
    if ($null -eq $model) { throw "YOLO candidate has no support record: $($old.modelId)" }
    $licenseSlug = if ($model.family -in @('yolov6','yolov7','yolov9')) { 'gpl-yolo-family' } elseif ($model.family -in @('yolov12','yolov13')) { 'agpl-yolov12-13' } else { 'agpl-ultralytics' }
    $license = Get-LicenseInfo $licenseSlug
    $artifact = $old.artifacts[0]
    $ref = [string]$old.exporter.sourceRevision
    $project = [string]$model.repository
    $manifest = New-Manifest $old.modelId $old.name $old.family $old.task $old.modelVersion $project $ref 'Upstream YOLO maintainers' $license ([string]$old.exporter.name) ([string]$old.exporter.version) $old.inputs $old.outputs $artifact ([string]$old.profileId) $generatedAt
    $fileName = ([string]$old.modelId).Replace('/', '-') + '.modelpack.json'
    $json = ($manifest | ConvertTo-Json -Depth 30) + [Environment]::NewLine
    $expected[(Join-Path $yoloReleases $fileName)] = $json
    $planEntries.Add([ordered]@{ collection = 'yolo'; tag = 'models-20260817.yolo.1'; modelId = $old.modelId; manifestFile = 'yolo/releases/' + $fileName; localPath = $detectionPaths[$old.modelId]; licenseSlug = $licenseSlug; licenseExpression = $license.Expression })
}

$multi = @(
    [ordered]@{ Id='yolo/v8/classify/s'; Name='YOLOv8s Classification'; Family='yolov8'; Task='image-classification'; Version='s-8.1.6'; Path='yolo\yolov8\yolov8s-cls.onnx'; Hash='6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee'; Size=25460947; Ref='ef141af4b837e0a1c34ff187ac40ef36af56c135'; Exporter='8.1.6'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,224,224)); Contract='classifier output output0; 1000 ImageNet classes' }
    [ordered]@{ Id='yolo/v5/segment/s'; Name='YOLOv5s Segmentation'; Family='yolov5'; Task='instance-segmentation'; Version='s'; Path='yolo\yolov5\yolov5s-seg.onnx'; Hash='ab44adf19119521f4764966a48f76fbac9125d22f5db776589bf049b49267576'; Size=30897895; Ref='20d1d78a08277e365d57bfa3a2cce752772d9e59'; Exporter='local-pytorch2.1.2-export'; License='gpl-yolo-family'; Input=@('images','float32',@(1,3,640,640)); Contract='raw box plus mask-coefficient output; DeploySharp mask prototype decode' }
    [ordered]@{ Id='yolo/v8/segment/n'; Name='YOLOv8n Segmentation'; Family='yolov8'; Task='instance-segmentation'; Version='n-8.0.119'; Path='yolo\yolov8\yolov8n-seg.onnx'; Hash='986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2'; Size=13821992; Ref='ef141af4b837e0a1c34ff187ac40ef36af56c135'; Exporter='8.0.119'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='attribute-major detection and prototype mask outputs' }
    [ordered]@{ Id='yolo/v9/segment/c'; Name='YOLOv9c Segmentation'; Family='yolov9'; Task='instance-segmentation'; Version='c'; Path='yolo\yolov9-c-seg.onnx'; Hash='2cc4ea632009115d72f30841d7295d5ca064cc9697a2fb4efbea3ce41ac0a2a0'; Size=110001237; Ref='5b1ea9a8b3f0ffe4fe0e203ec6232d788bb3fcff'; Exporter='local-pytorch2.2.1-export'; License='gpl-yolo-family'; Input=@('images','float32',@(1,3,640,640)); Contract='attribute-major detection and prototype mask outputs' }
    [ordered]@{ Id='yolo/v11/segment/s'; Name='YOLO11s Segmentation'; Family='yolo11'; Task='instance-segmentation'; Version='s-8.3.24'; Path='yolo\yolov11\yolo11s-seg.onnx'; Hash='0707f946915fcdfdbc5438d1f45ca446e70d388805e422ac849996240880fe48'; Size=40657020; Ref='636685ace98527cd0113656fd024a82291fa3122'; Exporter='8.3.24'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='attribute-major detection and prototype mask outputs' }
    [ordered]@{ Id='yolo/v26/segment/s'; Name='YOLO26s Segmentation'; Family='yolo26'; Task='instance-segmentation'; Version='s-8.4.0'; Path='yolo\yolov26\yolo26s-seg.onnx'; Hash='79682f271d30833adfe97c97572cd85d348eb1636be8d5b13009ae48e51dbd6f'; Size=41912261; Ref='6f6158be448c73471c000cf41db5cd9169300ed9'; Exporter='8.4.0-end2end'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='end-to-end detections with prototype mask outputs' }
    [ordered]@{ Id='yolo/v8/pose/s'; Name='YOLOv8s Pose'; Family='yolov8'; Task='pose-estimation'; Version='s-8.1.6'; Path='yolo\yolov8\yolov8s-pose.onnx'; Hash='253504de521c91115afba4dcee4c77d23a7a0a87b8f8101b170d6cae4f9c302b'; Size=46787174; Ref='ef141af4b837e0a1c34ff187ac40ef36af56c135'; Exporter='8.1.6'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='attribute-major boxes plus 17-keypoint pose output' }
    [ordered]@{ Id='yolo/v11/pose/s'; Name='YOLO11s Pose'; Family='yolo11'; Task='pose-estimation'; Version='s-8.3.24'; Path='yolo\yolov11\yolo11s-pose.onnx'; Hash='5b8d5bce3dff5ac176ea922faf14705fa46fa3b0d3a4b7974b765c355806bae5'; Size=39942660; Ref='636685ace98527cd0113656fd024a82291fa3122'; Exporter='8.3.24'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='attribute-major boxes plus 17-keypoint pose output' }
    [ordered]@{ Id='yolo/v26/pose/s'; Name='YOLO26s Pose'; Family='yolo26'; Task='pose-estimation'; Version='s-8.4.0'; Path='yolo\yolov26\yolo26s-pose.onnx'; Hash='55c609d18dc635b54a91c8f038d29138a421a4f8e700f645c78779fe6080ddcc'; Size=41859220; Ref='6f6158be448c73471c000cf41db5cd9169300ed9'; Exporter='8.4.0-end2end'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,640,640)); Contract='end-to-end boxes plus 17-keypoint pose output' }
    [ordered]@{ Id='yolo/v8/obb/s'; Name='YOLOv8s Oriented Detection'; Family='yolov8'; Task='oriented-object-detection'; Version='s-8.1.6'; Path='yolo\yolov8\yolov8s-obb.onnx'; Hash='2bbf67f4cbab45e18779f9a0b602a71cd9f266cb8d34f8df5bd3e8ab4bdcb981'; Size=45980835; Ref='ef141af4b837e0a1c34ff187ac40ef36af56c135'; Exporter='8.1.6'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,1024,1024)); Contract='oriented boxes with angle; DeploySharp rotated decode' }
    [ordered]@{ Id='yolo/v11/obb/s'; Name='YOLO11s Oriented Detection'; Family='yolo11'; Task='oriented-object-detection'; Version='s-8.3.24'; Path='yolo\yolov11\yolo11s-obb.onnx'; Hash='50ae0e11b742007fcd297408382be94a25c884093d63dce00ead62f37ea2cad0'; Size=39172187; Ref='636685ace98527cd0113656fd024a82291fa3122'; Exporter='8.3.24'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,1024,1024)); Contract='oriented boxes with angle; DeploySharp rotated decode' }
    [ordered]@{ Id='yolo/v26/obb/s'; Name='YOLO26s Oriented Detection'; Family='yolo26'; Task='oriented-object-detection'; Version='s-8.4.0'; Path='yolo\yolov26\yolo26s-obb.onnx'; Hash='bbc7c924dcac9e94888ef706f7aa5648cbc38f5fbd4c8a360401ebee7be955df'; Size=39438174; Ref='6f6158be448c73471c000cf41db5cd9169300ed9'; Exporter='8.4.0-end2end'; License='agpl-ultralytics'; Input=@('images','float32',@(1,3,1024,1024)); Contract='end-to-end oriented boxes with angle' }
)

$multiProjects = @{
    'yolo/v5/segment/s' = 'https://github.com/ultralytics/yolov5'
    'yolo/v9/segment/c' = 'https://github.com/WongKinYiu/yolov9'
}
$multiOpsets = @{
    'yolo/v8/classify/s' = 17
    'yolo/v5/segment/s' = 17
    'yolo/v8/segment/n' = 12
    'yolo/v9/segment/c' = 12
    'yolo/v11/segment/s' = 19
    'yolo/v26/segment/s' = 19
    'yolo/v8/pose/s' = 17
    'yolo/v11/pose/s' = 19
    'yolo/v26/pose/s' = 19
    'yolo/v8/obb/s' = 17
    'yolo/v11/obb/s' = 19
    'yolo/v26/obb/s' = 19
}
foreach ($item in $multi) {
    $item.Project = if ($multiProjects.ContainsKey($item.Id)) { $multiProjects[$item.Id] } else { 'https://github.com/ultralytics/ultralytics' }
    $item.Opset = $multiOpsets[$item.Id]
    $license = Get-LicenseInfo $item.License
    $filePath = Join-Path $ModelRoot $item.Path
    if (-not (Test-Path -LiteralPath $filePath)) { throw "Missing YOLO release model: $filePath" }
    $actual = Get-Item -LiteralPath $filePath
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual.Length -ne [long]$item.Size -or $actualHash -ne $item.Hash) { throw "YOLO release model integrity mismatch: $filePath" }
    $artifact = [ordered]@{
        artifactId = 'onnx.fp32'
        format = 'onnx'
        locationKind = 'directory'
        entrypoint = 'model.onnx'
        compatibleBackends = @('onnxruntime','openvino')
        files = @([ordered]@{ relativePath='model.onnx'; sha256=$item.Hash; size=[long]$item.Size; mediaType='application/onnx'; role='model' })
        precision = 'fp32'
        quantization = 'none'
        opset = [int]$item.Opset
        portable = $true
        extensions = [ordered]@{
            'deploysharp.validation-status' = 'local-ort-openvino-real-image-matrix-verified'
            'deploysharp.preprocessing-version' = 'ultralytics-letterbox-or-task-specific-v1'
            'deploysharp.postprocessing-contract' = $item.Contract
            'deploysharp.release-admission' = 'alpha-preview-redistributable-source-recorded'
        }
    }
    $inputs = @([ordered]@{ name=$item.Input[0]; elementType=$item.Input[1]; shape=$item.Input[2] })
    $manifest = New-Manifest $item.Id $item.Name $item.Family $item.Task $item.Version $item.Project $item.Ref 'Upstream YOLO maintainers' $license 'Upstream YOLO ONNX export' $item.Exporter $inputs @() $artifact ('yolo.' + $item.Id.Replace('/','.')) $generatedAt
    $fileName = $item.Id.Replace('/','-') + '.modelpack.json'
    $json = ($manifest | ConvertTo-Json -Depth 30) + [Environment]::NewLine
    $expected[(Join-Path $yoloReleases $fileName)] = $json
    $planEntries.Add([ordered]@{ collection='yolo'; tag='models-20260817.yolo.1'; modelId=$item.Id; manifestFile='yolo/releases/' + $fileName; localPath=$item.Path; licenseSlug=$item.License; licenseExpression=$license.Expression })
}

$detrMap = @{
    'deim/v2/detect/external' = @{ Path='DEIMv2\DEIMv2.onnx'; License='apache-deimv2' }
    'pp-yoloe/plus-crn-l/external' = @{ Path='ppyoloe\ppyoloe_plus_crn_l_80e_coco.onnx'; License='apache-paddledetection' }
    'rf-detr/detect/external' = @{ Path='rf-detr\rf-detr.onnx'; License='apache-rf-detr' }
    'rf-detr/segment/external' = @{ Path='rf-detr\rf-detr-seg.onnx'; License='apache-rf-detr' }
    'rt-detr/r50vd-decoded-vector-onnx/external' = @{ Path='RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.onnx'; License='apache-paddledetection' }
    'rt-detr/r50vd-decoded-vector-ir/external' = @{ Path='RT-DETR\RTDETR\rtdetr_r50vd_6x_coco_quant.xml'; License='apache-paddledetection' }
    'rt-detr/r50vd-raw-query/external' = @{ Path='RT-DETR\RTDETR_cropping\rtdetr_r50vd_6x_coco.onnx'; License='apache-paddledetection' }
}
foreach ($candidate in Get-ChildItem -LiteralPath $detrCandidates -Filter '*.modelpack.json' -File) {
    $old = Get-Content -Raw -LiteralPath $candidate.FullName | ConvertFrom-Json
    if (-not $detrMap.ContainsKey($old.modelId)) { continue }
    $map = $detrMap[$old.modelId]
    $license = Get-LicenseInfo $map.License
    $id = ([string]$old.modelId) -replace '/external$',''
    $oldArtifact = $old.artifacts[0]
    $artifact = New-PublicArtifact $oldArtifact $license
    $project = [string]$old.source.projectUrl
    $revision = [string]$old.exporter.sourceRevision
    $source = New-SourceRecord $project $revision ([string]$old.source.author) $license
    $manifest = [ordered]@{
        schemaVersion='2.0'; modelId=$id; name=([string]$old.name -replace ' external candidate',''); family=$old.family; task=$old.task; modelVersion=$old.modelVersion
        exporter=[ordered]@{name=$old.exporter.name;version=$old.exporter.version;sourceRevision=$revision}; source=$source; generatedAt=$generatedAt; profileId=([string]$old.profileId -replace '/external',''); inputs=@($old.inputs); outputs=@($old.outputs); artifacts=@($artifact); extensions=[ordered]@{'deploysharp.publication-status'='alpha-preview';'deploysharp.downloadable'='true'}
    }
    $fileName = $id.Replace('/','-') + '.modelpack.json'
    $json = ($manifest | ConvertTo-Json -Depth 30) + [Environment]::NewLine
    $expected[(Join-Path $detrReleases $fileName)] = $json
    $planEntries.Add([ordered]@{ collection='detr'; tag='models-20260817.detr.1'; modelId=$id; manifestFile='detr/releases/' + $fileName; localPath=$map.Path; licenseSlug=$map.License; licenseExpression=$license.Expression })
}

if ($Check) {
    foreach ($item in $expected.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $item.Key) -or [IO.File]::ReadAllText($item.Key) -ne $item.Value) { throw "Generated detector ModelPack is stale: $($item.Key)" }
    }
} else {
    [IO.Directory]::CreateDirectory($yoloReleases) | Out-Null
    [IO.Directory]::CreateDirectory($detrReleases) | Out-Null
    foreach ($item in $expected.GetEnumerator()) { [IO.File]::WriteAllText($item.Key, $item.Value, [Text.UTF8Encoding]::new($false)) }
    [IO.File]::WriteAllText($planPath, (([ordered]@{ schemaVersion='1.0'; generatedAt=$generatedAt; collections=@([ordered]@{id='yolo';tag='models-20260817.yolo.1';assetRoot='yolo'},[ordered]@{id='detr';tag='models-20260817.detr.1';assetRoot='detr'}); models=$planEntries } | ConvertTo-Json -Depth 20) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
}
