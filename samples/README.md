# Samples / 示例

`CoreContractInspection` is a minimal net8.0 runnable example that uses only the backend-neutral Core contract. It intentionally does not download a model or claim inference support. Run it from the repository root with:

```powershell
dotnet run --project samples/CoreContractInspection/CoreContractInspection.csproj -c Release
```

Backend-specific examples remain represented by the package-only consumers under `tests/clean-consumer/`, where their native/runtime ownership and verification boundaries are explicit.

`MultimodalContractInspection` runs the independent backend-neutral Multimodal contract with an in-memory adapter. `OpenCvDnnContractInspection` loads the pinned 297-byte classification ONNX fixture through the real OpenCV DNN backend and checks its `ReduceMean` golden. Both are CPU-only and do not download model assets. / `MultimodalContractInspection` 使用内存适配器运行独立的后端中立多模态合同；`OpenCvDnnContractInspection` 通过真实 OpenCV DNN 后端加载固定的 297 字节分类 ONNX 夹具并校验 `ReduceMean` golden。两者均仅使用 CPU，不下载模型资产。

`VisualProfileInspection`, `LlmPromptInspection`, and `ModelFactoryCatalogInspection` cover deterministic Visual profile registration, LLM prompt formatting, and validation of the embedded official model catalog. / `VisualProfileInspection`、`LlmPromptInspection` 与 `ModelFactoryCatalogInspection` 分别覆盖确定性的 Visual Profile 注册、LLM 提示词格式化和内置官方模型目录验证。
