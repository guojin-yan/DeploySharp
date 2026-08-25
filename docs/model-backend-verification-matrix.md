# 模型 × 后端本机验证矩阵

验证日期：2026-08-25；机器：Windows x64、OpenCV 5.0 CPU、TensorRT 11.0 + CUDA 12.9 + NVIDIA RTX 3060 Laptop GPU。本表只记录官方 catalog 中与本机文件 SHA-256 完全匹配的模型工件。

符号：`✓` = 构建/加载并实际推理通过；`✗` = 已进行精确兼容性验证但当前后端不能完成推理；`—` = 本机没有对应工件或该工件格式不适用于该后端。

| 模型 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN | TensorRT | LLamaSharp |
|---|:---:|:---:|:---:|:---:|:---:|
| llm/qwen2.5-0.5b-instruct-q4-k-m | — | — | — | — | — |
| vision-language/clip-vit-b-32 | — | — | — | — | — |
| segmentation/sam-v1-vit-b | — | — | — | — | — |
| generative-vision-language/blip-caption-base | — | — | — | — | — |
| yolo/v11/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v12/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v26/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v10/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v13/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v5/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v6/detect/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v7/detect/base | ✓ | ✓ | ✗ | ✓ | — |
| yolo/v8/detect/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v9/detect/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v8/classify/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v5/segment/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v8/segment/n | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v9/segment/c | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v11/segment/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v26/segment/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v8/pose/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v11/pose/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v26/pose/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v8/obb/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v11/obb/s | ✓ | ✓ | ✓ | ✓ | — |
| yolo/v26/obb/s | ✓ | ✓ | ✓ | ✓ | — |
| deim/v2/detect | ✓ | — | ✗ | ✓ | — |
| pp-yoloe/plus-crn-l | ✓ | — | ✗ | ✓ | — |
| rf-detr/detect | ✓ | ✗ | ✗ | ✓ | — |
| rf-detr/segment | ✓ | ✗ | ✗ | ✓ | — |
| rt-detr/r50vd-decoded-vector-ir | — | ✓ | — | — | — |
| rt-detr/r50vd-decoded-vector-onnx | ✓ | — | ✗ | ✓ | — |
| rt-detr/r50vd-raw-query | ✓ | ✓ | ✗ | ✓ | — |
| paddleocr/ppocrv5/mobile-cls | ✓ | ✓ | ✓ | ✓ | — |
| paddleocr/ppocrv5/mobile-det | ✓ | ✓ | ✓ | ✓ | — |
| paddleocr/ppocrv5/mobile-rec | ✓ | ✓ | ✗ | ✓ | — |
| paddleocr/ppocrv5/server-cls | ✓ | — | ✓ | ✓ | — |
| paddleocr/ppocrv5/server-det | ✓ | ✓ | ✗ | ✓ | — |
| paddleocr/ppocrv5/server-rec | ✓ | — | ✗ | ✓ | — |
| anomalib/padim/mvtec-bottle | ✓ | ✓ | ✗ | ✓ | — |
| bria/rmbg-1.4 | ✓ | ✓ | ✓ | ✓ | — |
| bria/rmbg-2.0 (onnx.fp32) | ✓ | — | ✗ | ✓ | — |
| bria/rmbg-2.0 (onnx.dynamic-int8) | ✓ | — | ✗ | ✗ | — |

## 本轮实际执行的验证入口

- YOLO 检测 10 个模型，以及分类/分割/姿态/OBB 12 个模型：`tests/DeploySharp.Visual.OpenCV.Tests` 中 `OpenCvYoloExternalIntegrationTests`、`OpenCvYoloMultiTaskIntegrationTests`；ORT 与 OpenVINO CPU 均通过。
- 便携检测模型：`tests/DeploySharp.Backend.OnnxRuntime.Tests` 的 external matrix；DEIMv2、PP-YOLOE、RF-DETR 检测/分割、RT-DETR 两种 ONNX 合同通过 ORT CPU。
- PaddleOCR、PaDiM、RMBG：`tests/DeploySharp.Backend.OnnxRuntime.Tests` 与 `tests/DeploySharp.Backend.OpenVINO.Tests` 的 external tests；server OCR 的 OpenVINO recognition/orientation 因 golden 输入文件缺失保持 `—`。
- OpenCV DNN：`OfficialModelIntegrationTests` 与 `AdditionalOfficialModelProbeTests` 对 38 个 ONNX 工件逐项验证；25 个工件通过 DeploySharp provider，13 个工件因算子、动态形状或 v1 张量合同限制标记为 `✗`。
- TensorRT：`OfficialModelIntegrationTests` 使用 TensorRT 11 builder 将 38 个 ONNX 工件逐项转换为临时 engine 并执行 CUDA 推理；37 个通过，RMBG 2.0 dynamic-int8 保持 `✗`。PP-YOLOE 通过构建器内置的安全 ONNX 兼容 pass 修复缺失的 Squeeze axes。
- RF-DETR OpenVINO 的 `✗` 来自真实执行时的 `Backend input metadata is incompatible with the visual profile`；这是当前适配器输入元数据校验问题，不是下载缺失。
- OpenVINO 项目直接 `dotnet test --no-restore` 还会受到本机 NuGet lock 的 `NU1403` 内容哈希问题影响；本表使用已构建的测试 DLL 完成了上述 native CPU 执行。

## OpenCV / TensorRT 失败原因

- OpenCV YOLOv7：OpenCV 5.0 `GatherLayerImpl` 输出形状校验失败。
- OpenCV RF-DETR detect/segment：ONNX `Split` 的双输入形式不能由当前 OpenCV importer 转换。
- OpenCV raw RT-DETR：多维 `Unsqueeze` 尚未实现；mobile-rec、server-det、server-rec 与 RMBG 2.0 FP32 则失败于动态 `Shape`。
- OpenCV RMBG 2.0 dynamic-int8：不支持 `DynamicQuantizeLinear`；DEIM、PP-YOLOE、decoded RT-DETR 需要非图像辅助输入，PaDiM 需要布尔输出，均超出 OpenCV DNN v1 的静态 float32 NCHW 合同。
- TensorRT YOLOv7：输出为数据依赖的 `[-1,7]`，适配器使用 TensorRT 最大输出缓冲区上界并保留运行时 shape 通知；本机 bridge 未提供 shape 通知时返回安全的上界形状。
- TensorRT PP-YOLOE：原始 PaddleDetection opset-11 图的 `Squeeze.3`/`Squeeze.5` 缺失 axes；builder 自动补充 Gather(axis=1) 后的 `axes=[1]`，不改变源 artifact SHA-256，随后完成 engine 构建和 CUDA enqueue。
- TensorRT RMBG 2.0 dynamic-int8：TensorRT 11 parser 不支持该导出图中的 `DynamicQuantizeLinear` 与 `ConvInteger`；请使用同一 ModelPack 的 `onnx.fp32` 变体（本机已通过）。

## 后端基础测试（不等同于模型级 `✓`）

- OpenCV DNN：基础固定装置测试与 catalog external tests 均通过；模型级结果由精确 SHA-256、实际 `Forward` 和 provider 合同共同判定。
- TensorRT：managed provider/契约/缓存/生命周期测试 `53/53` 通过；新增 external tests 覆盖 TensorRT 11 ONNX parser、builder、engine 反序列化、绑定和 CUDA enqueue。
- LlamaSharp：基础测试 `9` 通过、真实 GGUF 集成测试 `1` 跳过；本机没有官方 catalog 对应的 Qwen GGUF 工件，因此模型列保持 `—`。

## OpenCV 与 TensorRT 复现命令

OpenCV DNN 使用 CPU；测试会校验本机模型 SHA-256，并执行 provider 或记录确定的兼容性失败：

```powershell
$env:DEPLOYSHARP_OPENCV_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Backend.OpenCV.Tests/DeploySharp.Backend.OpenCV.Tests.csproj --filter 'TestCategory=ExternalModels'
```

TensorRT 测试需要匹配的 TensorRT 11 bridge、TensorRT 11.0 runtime 和 CUDA 12.9；每个 ONNX 会转换为临时 engine，执行一次 CUDA 推理后清理：

```powershell
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<bridge-package>\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = '<TensorRT-11.0-root>'
$env:JYPPX_CUDA_ROOT = '<CUDA-12.9-root>'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_CUDA_ROOT\bin;$env:PATH"
dotnet test tests/DeploySharp.Backend.TensorRT.Tests/DeploySharp.Backend.TensorRT.Tests.csproj --filter 'TestCategory=ExternalModels'
```

本轮 bridge：`JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt11.0.cuda12.9.cudnn9.22.Bridge` `4.0.0-preview.1`，native DLL SHA-256 为 `a94d1e5fe4454c9402979ae050f7a64d74c7f51bd6bb2d74936531f608a9ef6f`。完整 TensorRT external matrix 需要较长时间，RMBG 2.0 FP32 单个 engine 约 1.07 GB。

## 尚未覆盖

- CLIP、SAM v1、BLIP 与 Qwen GGUF 没有本机 catalog 对应工件；本机只有 SAM2 等不同模型，不能替代官方模型。
- OpenCV DNN 与 TensorRT 的 `—` 只剩本机没有精确工件的 Qwen/CLIP/SAM/BLIP，以及仅提供 OpenVINO IR 的 RT-DETR 工件。
- Linux/macOS/ARM、其他 GPU/NPU 设备仍未验证；本表结论只适用于上述 Windows 机器与运行时版本。
