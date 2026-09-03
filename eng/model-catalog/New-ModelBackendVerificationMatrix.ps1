[CmdletBinding()]
param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\..\src\DeploySharp.ModelFactory\catalog\deploysharp-official-catalog.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\docs\model-backend-verification-matrix.md'),
    [string]$CasesPath = (Join-Path $PSScriptRoot '..\..\samples\06-models\cases')
)

$ErrorActionPreference = 'Stop'
$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json

# Status is intentionally keyed by the official catalog model/artifact identity.
# A check mark means the exact catalog SHA-256 artifact was executed on this
# workstation and its typed pipeline contract completed. A cross means it was
# executed and failed. A dash means no exact validation was performed.
$verified = @{
    'yolo/v11/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v12/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v26/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v10/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v13/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v5/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v6/detect/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v7/detect/base' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v8/detect/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v9/detect/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v8/classify/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v5/segment/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v8/segment/n' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v9/segment/c' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v11/segment/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v26/segment/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v8/pose/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v11/pose/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v26/pose/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v8/obb/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v11/obb/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'yolo/v26/obb/s' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'deim/v2/detect' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✓' }
    'pp-yoloe/plus-crn-l' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✓' }
    'rf-detr/detect' = @{ ort = '✓'; openvino = '✗'; opencv = '✗'; tensorrt = '✓' }
    'rf-detr/segment' = @{ ort = '✓'; openvino = '✗'; opencv = '✗'; tensorrt = '✓' }
    'rt-detr/r50vd-decoded-vector-ir' = @{ openvino = '✓' }
    'rt-detr/r50vd-decoded-vector-onnx' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✓' }
    'rt-detr/r50vd-raw-query' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'paddleocr/ppocrv5/mobile-cls' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'paddleocr/ppocrv5/mobile-det' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'paddleocr/ppocrv5/mobile-rec' = @{ ort = '✓'; openvino = '✓'; opencv = '✗'; tensorrt = '✓' }
    'paddleocr/ppocrv5/server-cls' = @{ ort = '✓'; opencv = '✓'; tensorrt = '✓' }
    'paddleocr/ppocrv5/server-det' = @{ ort = '✓'; openvino = '✓'; opencv = '✗'; tensorrt = '✓' }
    'paddleocr/ppocrv5/server-rec' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✓' }
    'anomalib/padim/mvtec-bottle' = @{ ort = '✓'; openvino = '✓'; opencv = '✗'; tensorrt = '✓' }
    'bria/rmbg-1.4' = @{ ort = '✓'; openvino = '✓'; opencv = '✓'; tensorrt = '✓' }
    'bria/rmbg-2.0|onnx.fp32' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✓' }
    'bria/rmbg-2.0|onnx.dynamic-int8' = @{ ort = '✓'; opencv = '✗'; tensorrt = '✗' }
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# 模型 × 后端本机验证矩阵')
$lines.Add('')
$lines.Add('验证日期：2026-08-25 至 2026-09-01；机器：Windows x64、OpenCV 5.0 CPU、TensorRT 11.0 + CUDA 12.9 + NVIDIA RTX 3060 Laptop GPU。本表只记录官方 catalog 中与本机文件 SHA-256 完全匹配的模型工件。')
$lines.Add('')
$lines.Add('符号：`✓` = 构建/加载并实际推理通过；`✗` = 已进行精确兼容性验证但当前后端不能完成推理；`—` = 本机没有对应工件或该工件格式不适用于该后端。')
$lines.Add('')
$lines.Add('| 模型 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN | TensorRT | LLamaSharp |')
$lines.Add('|---|:---:|:---:|:---:|:---:|:---:|')

$rows = @()
$caseVerification = @{}
foreach ($entry in $catalog.entries) {
    foreach ($artifact in $entry.artifacts) {
        $key = $entry.modelId
        $label = $entry.modelId
        if ($entry.artifacts.Count -gt 1) {
            $key = $entry.modelId + '|' + $artifact.artifactId
            $label += ' (' + $artifact.artifactId + ')'
        }
        $result = if ($verified.ContainsKey($key)) { $verified[$key] } elseif ($verified.ContainsKey($entry.modelId)) { $verified[$entry.modelId] } else { @{} }
        $ort = if ($result.ContainsKey('ort')) { $result.ort } else { '—' }
        $ov = if ($result.ContainsKey('openvino')) { $result.openvino } else { '—' }
        $opencv = if ($result.ContainsKey('opencv')) { $result.opencv } else { '—' }
        $tensorrt = if ($result.ContainsKey('tensorrt')) { $result.tensorrt } else { '—' }
        $rows += [pscustomobject]@{ label = $label; ort = $ort; openvino = $ov; opencv = $opencv; tensorrt = $tensorrt }
        $lines.Add(('| {0} | {1} | {2} | {3} | {4} | — |' -f $label, $ort, $ov, $opencv, $tensorrt))
        if (-not $caseVerification.ContainsKey($entry.modelId)) {
            $caseVerification[$entry.modelId] = [System.Collections.Generic.List[string]]::new()
        }
        $caseVerification[$entry.modelId].Add(('| {0} | {1} | {2} | {3} | {4} | — |' -f $artifact.artifactId, $ort, $ov, $opencv, $tensorrt))
    }
}

$lines.Add('')
$lines.Add('## 本轮实际执行的验证入口')
$lines.Add('')
$lines.Add('- YOLO 检测 10 个模型，以及分类/分割/姿态/OBB 12 个模型：`tests/DeploySharp.Visual.OpenCV.Tests` 中 `OpenCvYoloExternalIntegrationTests`、`OpenCvYoloMultiTaskIntegrationTests`；ORT 与 OpenVINO CPU 均通过。')
$lines.Add('- 便携检测模型：`tests/DeploySharp.Backend.OnnxRuntime.Tests` 的 external matrix；DEIMv2、PP-YOLOE、RF-DETR 检测/分割、RT-DETR 两种 ONNX 合同通过 ORT CPU。')
$lines.Add('- PaddleOCR、PaDiM、RMBG：`tests/DeploySharp.Backend.OnnxRuntime.Tests` 与 `tests/DeploySharp.Backend.OpenVINO.Tests` 的 external tests；server OCR 的 OpenVINO recognition/orientation 因 golden 输入文件缺失保持 `—`。')
$lines.Add('- OpenCV DNN：`OfficialModelIntegrationTests` 与 `AdditionalOfficialModelProbeTests` 对 38 个 ONNX 工件逐项验证；25 个工件通过 DeploySharp provider，13 个工件因算子、动态形状或 v1 张量合同限制标记为 `✗`。')
$lines.Add('- TensorRT：`OfficialModelIntegrationTests` 使用 TensorRT 11 builder 将 38 个 ONNX 工件逐项转换为临时 engine 并执行 CUDA 推理；37 个通过，RMBG 2.0 dynamic-int8 保持 `✗`。PP-YOLOE 通过构建器内置的安全 ONNX 兼容 pass 修复缺失的 Squeeze axes。')
$lines.Add('- RF-DETR OpenVINO 的 `✗` 来自真实执行时的 `Backend input metadata is incompatible with the visual profile`；这是当前适配器输入元数据校验问题，不是下载缺失。')
$lines.Add('- OpenVINO 项目直接 `dotnet test --no-restore` 还会受到本机 NuGet lock 的 `NU1403` 内容哈希问题影响；本表使用已构建的测试 DLL 完成了上述 native CPU 执行。')
$lines.Add('')
$lines.Add('## OpenCV / TensorRT 兼容性说明')
$lines.Add('')
$lines.Add('- OpenCV YOLOv7：OpenCV 5.0 的图内 NMS/Gather 尾部仍触发 `GatherLayerImpl` shape 校验；精确工件 Profile 显式绑定原始检测头 `[1,25200,85]` 并执行 DeploySharp 托管 NMS，完整流水线与 ONNX Runtime 结果一致，因此标记 `✓`。')
$lines.Add('- OpenCV RF-DETR detect/segment：ONNX `Split` 的双输入形式不能由当前 OpenCV importer 转换。')
$lines.Add('- OpenCV raw RT-DETR：兼容层将可证明的标量 float 常量 `Expand` 语义等价改写为 `ConstantOfShape` 后，完整流水线输出已全部有限并通过。')
$lines.Add('- OpenCV RMBG 2.0 dynamic-int8：不支持 `DynamicQuantizeLinear`；DEIM、PP-YOLOE、decoded RT-DETR 需要非图像辅助输入，PaDiM 需要布尔输出，均超出 OpenCV DNN v1 的静态 float32 NCHW 合同。')
$lines.Add('- TensorRT YOLOv7：输出为数据依赖的 `[-1,7]`，适配器使用 TensorRT 最大输出缓冲区上界并保留运行时 shape 通知；本机 bridge 未提供 shape 通知时返回安全的上界形状。')
$lines.Add('- TensorRT PP-YOLOE：原始 PaddleDetection opset-11 图的 `Squeeze.3`/`Squeeze.5` 缺失 axes；builder 自动补充 Gather(axis=1) 后的 `axes=[1]`，不改变源 artifact SHA-256，随后完成 engine 构建和 CUDA enqueue。')
$lines.Add('- TensorRT RMBG 2.0 dynamic-int8：TensorRT 11 parser 不支持该导出图中的 `DynamicQuantizeLinear` 与 `ConvInteger`；请使用同一 ModelPack 的 `onnx.fp32` 变体（本机已通过）。')
$lines.Add('')
$lines.Add('## 后端基础测试（不等同于模型级 `✓`）')
$lines.Add('')
$lines.Add('- OpenCV DNN：基础固定装置测试与 catalog external tests 均通过；模型级结果由精确 SHA-256、实际 `Forward` 和 provider 合同共同判定。')
$lines.Add('- TensorRT：managed provider/契约/缓存/生命周期测试 `53/53` 通过；新增 external tests 覆盖 TensorRT 11 ONNX parser、builder、engine 反序列化、绑定和 CUDA enqueue。')
$lines.Add('- LlamaSharp：基础测试 `9` 通过、真实 GGUF 集成测试 `1` 跳过；本机没有官方 catalog 对应的 Qwen GGUF 工件，因此模型列保持 `—`。')
$lines.Add('')
$lines.Add('## OpenCV 与 TensorRT 复现命令')
$lines.Add('')
$lines.Add('OpenCV DNN 使用 CPU；测试会校验本机模型 SHA-256，并执行 provider 或记录确定的兼容性失败：')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add('$env:DEPLOYSHARP_OPENCV_RUN_EXTERNAL = ''1''')
$lines.Add('dotnet test tests/DeploySharp.Backend.OpenCV.Tests/DeploySharp.Backend.OpenCV.Tests.csproj --filter ''TestCategory=ExternalModels''')
$lines.Add('```')
$lines.Add('')
$lines.Add('TensorRT 测试需要匹配的 TensorRT 11 bridge、TensorRT 11.0 runtime 和 CUDA 12.9；每个 ONNX 会转换为临时 engine，执行一次 CUDA 推理后清理：')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add('$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = ''1''')
$lines.Add('$env:JYPPX_NATIVE_BRIDGE_PATH = ''<bridge-package>\runtimes\win-x64\native\jyppxtrtbridge.dll''')
$lines.Add('$env:JYPPX_TENSORRT_ROOT = ''<TensorRT-11.0-root>''')
$lines.Add('$env:JYPPX_CUDA_ROOT = ''<CUDA-12.9-root>''')
$lines.Add('$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_CUDA_ROOT\bin;$env:PATH"')
$lines.Add('dotnet test tests/DeploySharp.Backend.TensorRT.Tests/DeploySharp.Backend.TensorRT.Tests.csproj --filter ''TestCategory=ExternalModels''')
$lines.Add('```')
$lines.Add('')
$lines.Add('本轮 bridge：`JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt11.0.cuda12.9.cudnn9.22.Bridge` `4.0.0-preview.1`，native DLL SHA-256 为 `a94d1e5fe4454c9402979ae050f7a64d74c7f51bd6bb2d74936531f608a9ef6f`。完整 TensorRT external matrix 需要较长时间，RMBG 2.0 FP32 单个 engine 约 1.07 GB。')
$lines.Add('')
$lines.Add('## 尚未覆盖')
$lines.Add('')
$lines.Add('- CLIP、SAM v1、BLIP 与 Qwen GGUF 没有本机 catalog 对应工件；本机只有 SAM2 等不同模型，不能替代官方模型。')
$lines.Add('- OpenCV DNN 与 TensorRT 的 `—` 只剩本机没有精确工件的 Qwen/CLIP/SAM/BLIP，以及仅提供 OpenVINO IR 的 RT-DETR 工件。')
$lines.Add('- Linux/macOS/ARM、其他 GPU/NPU 设备仍未验证；本表结论只适用于上述 Windows 机器与运行时版本。')

$dir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.File]::WriteAllLines($fullOutputPath, $lines, [System.Text.UTF8Encoding]::new($false))

foreach ($entry in $catalog.entries) {
    $slug = $entry.modelId -replace '/', '--'
    $readmePath = Join-Path $CasesPath ($slug + '\README.md')
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) { continue }
    $section = [System.Collections.Generic.List[string]]::new()
    $section.Add('## Backend verification')
    $section.Add('')
    $section.Add('Local verification date: 2026-08-25. Results are generated from the exact official-catalog artifact identity and SHA-256.')
    $section.Add('')
    $section.Add('| Artifact | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN | TensorRT | LLamaSharp |')
    $section.Add('| --- | :---: | :---: | :---: | :---: | :---: |')
    foreach ($row in $caseVerification[$entry.modelId]) { $section.Add($row) }
    $section.Add('')
    $section.Add('`✓` means build/load and real inference passed; `✗` means exact compatibility validation failed on the tested runtime; `—` means no matching local artifact or the artifact format does not apply.')
    $section.Add('')
    if ($entry.modelId -eq 'pp-yoloe/plus-crn-l') {
        $section.Add('TensorRT note: the exact catalog ONNX SHA-256 was used. DeploySharp repairs the PaddleDetection opset-11 export''s missing `Squeeze` axes in memory (`Gather(axis=1)` -> `axes=[1]`) before parsing, then builds and runs the TensorRT 11 engine without changing the source artifact.')
        $section.Add('')
    }
    elseif ($entry.modelId -eq 'bria/rmbg-2.0') {
        $section.Add('TensorRT note: `onnx.fp32` passed engine build and CUDA inference. `onnx.dynamic-int8` remains unsupported by TensorRT 11 because its exact graph contains `DynamicQuantizeLinear` and `ConvInteger`; use the fp32 variant for TensorRT.')
        $section.Add('')
    }
    $section.Add('See [the model/backend verification matrix](../../../../docs/model-backend-verification-matrix.md) for the tested machine, failure reasons, and reproduction commands.')

    $readme = Get-Content -LiteralPath $readmePath -Raw
    $marker = [Environment]::NewLine + '## Backend verification'
    $index = $readme.IndexOf($marker, [StringComparison]::Ordinal)
    if ($index -ge 0) { $readme = $readme.Substring(0, $index).TrimEnd() } else { $readme = $readme.TrimEnd() }
    $updated = $readme + [Environment]::NewLine + [Environment]::NewLine + ($section -join [Environment]::NewLine) + [Environment]::NewLine
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($readmePath), $updated, [System.Text.UTF8Encoding]::new($false))
}
Write-Output ("Wrote {0} rows to {1}" -f $rows.Count, $fullOutputPath)
