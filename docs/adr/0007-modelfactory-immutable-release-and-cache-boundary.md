# ADR 0007: ModelFactory immutable Release and cache boundary / ModelFactory 不可变 Release 与缓存边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-04

## Decision / 决策

`JYPPX.DeploySharp.ModelFactory` consumes only validated catalog snapshots. Downloadable entries point to immutable GitHub Release tags matching `models-YYYYMMDD.revision`; branch, `latest`, query-token, redirect, and mutable URLs are rejected. Every asset has a catalog path, exact byte size, SHA256, media type, source/license, and release tag. Downloaded ModelPack manifests are parsed again by `JYPPX.DeploySharp.ModelPack.Json` before a model is returned. / `JYPPX.DeploySharp.ModelFactory` 只消费已验证目录快照。可下载条目必须指向符合 `models-YYYYMMDD.revision` 的不可变 GitHub Release 标签；分支、`latest`、带 query token、重定向和可变 URL 均被拒绝。每个资产都有目录路径、精确字节大小、SHA256、媒体类型、来源/license 和 Release 标签。返回模型前，下载的 ModelPack 清单必须再次由 `JYPPX.DeploySharp.ModelPack.Json` 解析。

The cache lives only below `<application cache root>/.deploysharp-model-factory/v1`, guarded by an ownership marker. Selection keys include catalog revision, release tag, artifact identity, asset SHA256, and normalized path. Each file is streamed to a random sibling temporary file, verified, flushed, and atomically renamed; a completion marker is written only after ModelPack validation. Offline mode never accesses the network. Cleanup refuses paths outside the managed namespace and refuses reparse-point entries. / 缓存只位于 `<应用缓存根>/.deploysharp-model-factory/v1` 下，并由所有权标记保护。选择键包含目录修订、Release 标签、工件身份、资产 SHA256 和规范化路径。每个文件流式写入同目录随机临时文件，验证、刷新并原子重命名；只有 ModelPack 验证成功后才写完成标记。离线模式绝不访问网络。清理拒绝管理命名空间以外的路径以及重解析点条目。

Concurrent callers for the same selection share one underlying operation. Caller cancellation stops that caller's wait but does not corrupt or cancel work still needed by another caller. Disposal cancels owned operations and prevents new work. / 同一选择的并发调用方共享一个底层操作。调用方取消只停止该调用方等待，不破坏或取消其他调用方仍需要的工作。释放会取消自有操作并阻止新工作。

The package targets `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`, matching ModelPack.Json and verified `System.Text.Json 10.0.10` assets. Older compatible applications consume `netstandard2.0`; .NET Framework 4.6 is not supported by this package. / 本包目标为 `netstandard2.0`、`net8.0`、`net9.0` 和 `net10.0`，与 ModelPack.Json 和已验证的 `System.Text.Json 10.0.10` 资产一致。兼容的旧应用消费 `netstandard2.0`；本包不支持 .NET Framework 4.6。

## Consequences / 影响

- Only GGUF + `llama-sharp` has current Supported admission evidence. ONNX and OpenVINO catalog records remain Preview until their backends are implemented and tested. / 当前只有 GGUF + `llama-sharp` 具备 Supported 准入证据；ONNX 和 OpenVINO 目录记录在后端实现并测试前保持 Preview。
- TensorRT `.engine`/`.plan` is accepted only as a non-portable External record and is never a generally downloadable Supported artifact. / TensorRT `.engine`/`.plan` 仅可作为不可移植 External 记录，绝不作为通用可下载 Supported 工件。
- No real Release is created or uploaded by this module. Publication remains an explicit human-approved operation after license, repository, size, and hash review. / 本模块不创建或上传真实 Release。发布必须在许可证、仓库、大小和 hash 复核后由人工明确批准。
