# OpenVINO 后端入门

本文介绍在 Windows x64 上使用 OpenVINO 后端执行 DeploySharp 张量和视觉推理。页面适用于 `2.0.0-alpha.1`；OpenVINO 原生运行时由应用自行部署，模型文件也由应用管理。

## 安装

安装 DeploySharp 适配器和与目标应用匹配的 OpenVINO 运行时：

~~~powershell
dotnet add package JYPPX.DeploySharp.Backend.OpenVINO --version 2.0.0-alpha.1
dotnet add package OpenVINO.runtime.win --version 2026.2.1
~~~

适配器使用托管的 `JYPPX.OpenVINO.CSharp.API`，不会替应用携带 OpenVINO 原生库、设备插件、GenAI 或 OpenCV。发布应用时，应把所选运行时的 DLL 与应用一起部署，并确认进程能够找到它们。

## 最小张量推理

下面的示例使用一个本地 ONNX 文件执行命名张量推理。输入和输出名称必须与模型实际元数据完全一致：

~~~csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OpenVINO;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;

using var backends = new BackendRegistry();
backends.UseOpenVino(new OpenVinoOptions(device: "CPU"));

var artifact = new ModelArtifact(
    new ModelId("examples/classifier"),
    "onnx",
    @"models\classifier.onnx",
    preferredBackend: OpenVinoBackendProvider.BackendId);
var request = new BackendRequest(
    BackendCapabilities.TensorInference,
    OpenVinoBackendProvider.BackendId,
    "CPU");

using IInferenceSession session = backends.CreateSession(
    artifact, request, new SessionOptions(maxConcurrency: 1));
var tensor = new Tensor<float>(
    new TensorShape(1, 3, 224, 224),
    new float[1 * 3 * 224 * 224]);
InferenceOutputs outputs = await session.RunAsync(
    InferenceInputs.Create("images", tensor),
    CancellationToken.None);
float[] scores = (float[])outputs.GetRequired("scores").Buffer;
~~~

`OpenVinoOptions(device: "CPU")` 与 `BackendRequest` 中的设备名称应保持一致。使用 GPU、NPU 或其他插件时，需要同时满足本机插件、模型格式和目标设备的运行时条件；本页的 Windows Alpha 证据以 CPU 为准。

## OpenVINO IR 与 ModelPack

使用 OpenVINO IR 时，将格式写为 `openvino-ir`，并把 `.xml` 作为入口文件；同目录的 `.bin` 是必需文件。`ModelPack.Json` 清单应列出所有文件的相对路径、字节大小和 SHA256，验证完成后再选择后端。不要把 `.xml` 单独复制到部署目录。

ONNX 文件可以直接交给 OpenVINO 适配器转换并加载，但应在目标设备上固定输入形状和精度，再按[设备性能实测](device-performance-benchmarks.md)的口径测量。动态维度、字符串张量、`Float16`、`BFloat16` 和零秩输入是否可用取决于模型图与当前托管包装器；遇到不匹配时，先查看[OpenVINO 兼容性](openvino-compatibility.md)中的诊断说明。

## Visual 使用

视觉任务还需要安装 `JYPPX.DeploySharp.Visual`，注册 `VisualModelProfile`，并将图像适配为带坐标变换的 `PreparedVisualInput`。解码器会把检测框、分割区域、姿态点、OCR 多边形或 Alpha 蒙版还原到源图坐标；应用不应在结果外再次缩放坐标。

批量输入只有在模型合同声明动态 batch 时才进入同一个张量 batch。对于固定 `batch=1` 的模型，使用有限数量的独立 `VisualPipeline` 实例并发处理，不要在多个线程中同时写入同一个有状态 session。

## 常见问题

- 找不到插件 DLL：检查 OpenVINO 运行时目录是否在应用目录或进程搜索路径中，并确认 x64 与进程位数一致。
- 输入名称或形状错误：从 `session.Metadata` 读取真实名称、元素类型和维度，逐项构造 `InferenceInputs`。
- IR 缺少 `.bin`：补齐同版本导出的权重文件，并在 ModelPack 清单中声明它。
- 动态形状不匹配：先为目标分辨率建立静态 Profile；只有模型和后端都声明支持时才启用动态尺寸。

更多模型状态请查看[模型支持指南](model-support.md)，各模型与后端的单元格以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
