# Release 模型推理快速开始

本文演示如何把 ModelFactory 目录中的模型下载到应用缓存，再交给 DeploySharp 视觉和 ONNX Runtime 合同执行。适用于 `2.0.0-alpha.1` 的 Windows x64 Preview 工件；模型下载、校验和推理后端是三个独立步骤。

## 前置条件

- .NET 8 SDK；
- 能访问目标 Release 的网络，或已有完整的离线缓存；
- 与模型输入尺寸匹配的本地图片；
- 应用自行部署的 ONNX Runtime、OpenCV 和其他 native runtime。

## 使用 Release 推理案例

案例位于 `samples/06-models/release-inference`。以 BRIA RMBG 2.0 为例：

~~~powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none --image E:\Model\anomalib\Padim\images\your-image.jpg
~~~

案例会将模型下载到应用缓存，校验 ModelPack、文件大小和 SHA256，然后执行背景 Alpha 推理，默认输出 `deploysharp-alpha.pgm`。RMBG 1.4 使用 `--model-id bria/rmbg-1.4`；PaDiM 使用 `anomalib/padim/mvtec-bottle`。

## 离线复用

首次在线运行成功后，可以指定应用自己的缓存目录并切换到离线模式：

~~~powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image E:\Model\anomalib\Padim\images\your-image.jpg --cache D:\DeploySharpCache --offline
~~~

`--offline` 会强制使用已完成并已验证的本地包；缺少文件、大小或 SHA256 不匹配时直接失败，不会静默联网下载。

## 支持边界

模型目录中的 `Preview` 条目需要显式打开 Preview 查询；目录中未出现的模型不能用相近名称替代。TensorRT Engine、Tokenizer、OpenVINO XML/BIN 和其他 sidecar 仍由应用按对应后端要求准备。具体模型与后端状态以[模型支持指南](model-support.md)、[官方模型目录](model-catalog.md)和[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。
