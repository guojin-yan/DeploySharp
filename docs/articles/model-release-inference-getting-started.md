# Model Release Inference Quick Start / 模型 Release 推理快速开始

This sample connects the published ModelFactory catalog to a real CPU inference call. The model is downloaded into the application-owned cache, checked against the catalog size/SHA256 and ModelPack manifest, then passed to the existing Visual and ONNX Runtime contracts. No model file is committed to Git. / 此示例将已发布 ModelFactory 目录连接到真实 CPU 推理调用。模型下载到应用自有缓存，并按目录中的大小/SHA256 和 ModelPack 清单校验，再交给现有 Visual 与 ONNX Runtime 合同执行。模型文件不会提交到 Git。

## Prerequisites / 前置条件

- .NET 8 SDK
- Windows x64 for the checked OpenCV runtime package
- A local input image
- Network access on the first run; `--offline` works after the cache is complete

The sample uses the existing `Microsoft.ML.OnnxRuntime` CPU runtime and `JYPPX.OpenCV.runtime.win-x64`. Native runtimes remain application-owned. / 示例使用现有 `Microsoft.ML.OnnxRuntime` CPU runtime 和 `JYPPX.OpenCV.runtime.win-x64`；native runtime 仍由应用所有。

## BRIA RMBG / BRIA RMBG

```powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none --image E:\Model\anomalib\Padim\images\your-image.jpg
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image E:\Model\anomalib\Padim\images\your-image.jpg
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-1.4 --precision fp32 --quantization none --image E:\Model\anomalib\Padim\images\your-image.jpg
```

The command writes `deploysharp-alpha.pgm` by default. The output is a grayscale alpha mask with the source-image dimensions. `PGM` can be opened by common image tools or converted to PNG by the caller. The BRIA 2.0 Release evidence is verified at the inspected 1024x1024 contract; arbitrary dynamic sizes are not claimed. / 命令默认写出 `deploysharp-alpha.pgm`，它是与源图尺寸相同的灰度 Alpha 掩码。常见图像工具可以打开 `PGM`，调用方也可以将其转换为 PNG。BRIA 2.0 Release 证据在已检查的 1024x1024 合同上验证，不宣称任意动态尺寸。

## PaDiM / PaDiM

```powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id anomalib/padim/mvtec-bottle --precision fp32 --quantization none --image E:\Model\anomalib\Padim\images\your-image.jpg
```

The command writes `deploysharp-anomaly-mask.pgm` by default and prints the image score, anomalous-pixel ratio, canonical result SHA256, and output path. The published PaDiM artifact is the MVTec AD `bottle` preview package. / 命令默认写出 `deploysharp-anomaly-mask.pgm`，并打印图像分数、异常像素比例、规范结果 SHA256 和输出路径。已发布 PaDiM 工件是 MVTec AD `bottle` Preview 包。

## Cache and offline reuse / 缓存与离线复用

Use `--cache <path>` to choose an application-owned cache. After a successful online run, repeat the same command with `--offline` to require the verified local package and prevent network fallback: / 使用 `--cache <path>` 指定应用自有缓存。在线运行成功后，加上 `--offline` 再次运行即可强制使用已校验本地包，禁止联网回退：

```powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image E:\Model\anomalib\Padim\images\your-image.jpg --cache D:\DeploySharpCache --offline
```
