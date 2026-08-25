# Release and platform status / 发布与平台状态

## Current release / 当前版本

`2.0.0-alpha.1` is a Windows-focused open-source engineering preview on branch `DeploySharpV2.0`. The current release target is Windows 10/11 x64; Linux, macOS, ARM, and NPU validation are deferred and do not block this Alpha. / `2.0.0-alpha.1` 是 `DeploySharpV2.0` 分支上以 Windows 为重点的开源工程预览版。当前发布目标为 Windows 10/11 x64；Linux、macOS、ARM 和 NPU 验证暂缓，不阻塞本次 Alpha。

The catalog contains 42 public Preview entries. Existing model source and license fields remain informational metadata, but source/license review is no longer a release-admission requirement for this open-source Alpha. Model download integrity is still enforced with the catalog, ModelPack, file size, and SHA-256. / catalog 当前包含 42 个公开 Preview 条目。已有的模型来源和许可证字段继续作为说明信息保留，但来源/许可证审核不再是本次开源 Alpha 的发布准入条件。模型下载仍通过 catalog、ModelPack、文件大小和 SHA-256 执行完整性校验。

## Windows backend status / Windows 后端状态

| Component | Target frameworks | Current Windows evidence |
| --- | --- | --- |
| Core, Visual | `net46`-`net481`, `netstandard2.0`, `netcoreapp3.1`, `net5.0`-`net10.0` | Managed build and test coverage across the declared matrix |
| ModelPack.Json, ModelFactory | `netstandard2.0`, `net8.0`, `net9.0`, `net10.0` | Catalog validation, download/cache contracts, and package-only consumers |
| LLM, LLamaSharp | Package-specific subset through `net10.0` | Managed contracts plus a real Windows CPU GGUF path |
| ONNX Runtime | `netstandard2.0`, `net8.0` | Windows x64 CPU model execution |
| OpenVINO | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | Windows x64 CPU model execution |
| Visual.OpenCV | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | Windows x64 image loading and preprocessing |
| OpenCV DNN | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | 25 of 38 tested ONNX artifacts execute successfully on CPU |
| TensorRT | `net8.0` | 37 of 38 tested ONNX artifacts build and execute with TensorRT 11/CUDA 12.9; RMBG 2.0 dynamic-int8 is unsupported |
| Multimodal | `netstandard2.0`, `netcoreapp3.1`, `net5.0`-`net10.0` | Managed orchestration, streaming, cancellation, clean consumer, and sample |

Exact model results and reproduction commands are recorded in the [model/backend verification matrix](../model-backend-verification-matrix.md). A `-` in that matrix means untested or not applicable; it is not converted into a positive support claim. / 精确模型结果和复现命令记录在[模型与后端验证矩阵](../model-backend-verification-matrix.md)中。矩阵中的 `-` 表示未测试或不适用，不会被解释为已支持。

## Alpha release checklist / Alpha 发布清单

The following items must be closed before publishing the Windows Alpha packages: / 发布 Windows Alpha 包前需要完成以下事项：

- Restore the locked dependency graph from a clean or isolated NuGet cache. / 在干净或隔离的 NuGet 缓存中完成 locked restore。
- Build and test the complete Windows solution with no warnings or failures. / Windows 全解决方案构建与测试无警告、无失败。
- Run the package audit and package-only clean-consumer matrix. / 完成包内容审计和纯包 clean consumer 矩阵。
- Keep the 42-entry catalog, generated model table, model cases, and README counts consistent. / 保持 42 条 catalog、生成模型表、模型案例和 README 数字一致。
- Record explicit authorization for the selected publication channel. / 对选定发布渠道记录明确发布授权。

Model source/license review and non-Windows platform validation are explicitly outside this Alpha checklist. Existing metadata and historical evidence are retained, but they do not block publication. / 模型来源/许可证审核以及非 Windows 平台验证明确不属于本次 Alpha 清单。已有元数据和历史证据继续保留，但不阻塞发布。

## Deferred scope / 暂缓范围

- Linux, macOS, Windows ARM64, Android, and other operating-system/RID combinations.
- NPU and untested GPU/provider combinations.
- A stable/GA compatibility promise; public APIs may still change during the Alpha cycle.
- NuGet.org stable release, long-term support policy, and cross-platform support matrix.

These items can be resumed after the Windows Alpha is published without changing the current Windows support statement. / 以上工作可在 Windows Alpha 发布后继续，不影响当前 Windows 支持声明。
