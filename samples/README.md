# Samples / 示例

`CoreContractInspection` is a minimal net8.0 runnable example that uses only the backend-neutral Core contract. It intentionally does not download a model or claim inference support. Run it from the repository root with:

```powershell
dotnet run --project samples/CoreContractInspection/CoreContractInspection.csproj -c Release
```

Backend-specific examples remain represented by the package-only consumers under `tests/clean-consumer/`, where their native/runtime ownership and verification boundaries are explicit.

`MultimodalContractInspection` runs the independent backend-neutral Multimodal contract with an in-memory adapter. `OpenCvDnnContractInspection` loads the pinned 297-byte classification ONNX fixture through the real OpenCV DNN backend and checks its `ReduceMean` golden. Both are CPU-only and do not download model assets. / `MultimodalContractInspection` 使用内存适配器运行独立的后端中立多模态合同；`OpenCvDnnContractInspection` 通过真实 OpenCV DNN 后端加载固定的 297 字节分类 ONNX 夹具并校验 `ReduceMean` golden。两者均仅使用 CPU，不下载模型资产。

`VisualProfileInspection`, `LlmPromptInspection`, and `ModelFactoryCatalogInspection` cover deterministic Visual profile registration, LLM prompt formatting, and validation of the embedded official model catalog. / `VisualProfileInspection`、`LlmPromptInspection` 与 `ModelFactoryCatalogInspection` 分别覆盖确定性的 Visual Profile 注册、LLM 提示词格式化和内置官方模型目录验证。

`ModelReleaseInference` is the shortest end-to-end path from an immutable GitHub Release asset to CPU inference. It downloads and verifies a BRIA RMBG 1.4/2.0 or Anomalib PaDiM package through ModelFactory, runs the existing Visual + ONNX Runtime contracts, and writes a portable PGM alpha/anomaly mask. It requires the application-owned ONNX Runtime and Windows x64 OpenCV runtime packages; models are never stored in the repository. / `ModelReleaseInference` 是从不可变 GitHub Release 资产到 CPU 推理的最短端到端路径。它通过 ModelFactory 下载并校验 BRIA RMBG 1.4/2.0 或 Anomalib PaDiM，运行现有 Visual + ONNX Runtime 合同，并写出可移植的 PGM Alpha/异常掩码。它要求应用自行提供 ONNX Runtime 与 Windows x64 OpenCV runtime 包；模型不会存入仓库。

```powershell
dotnet run --project samples/ModelReleaseInference -- --model-id bria/rmbg-2.0 --precision int8 --quantization dynamic --image E:\Model\anomalib\Padim\images\your-image.jpg
dotnet run --project samples/ModelReleaseInference -- --model-id anomalib/padim/mvtec-bottle --image E:\Model\anomalib\Padim\images\your-image.jpg
```
