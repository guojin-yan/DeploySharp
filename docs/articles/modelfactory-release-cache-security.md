# ModelFactory Release admission, download, and cache security / ModelFactory Release 准入、下载与缓存安全

## Publication admission / 发布准入

A `Supported` catalog entry must have all of the following: an implemented DeploySharp model/backend path, portable format, immutable Release metadata, ModelPack manifest, model files, reproducible exporter record, redistribution permission, SPDX license, test input, reproducible expected-result asset, SHA256/size, and generated documentation path. At this stage only GGUF with `llama-sharp` has current backend evidence. ONNX and OpenVINO may be recorded as Preview; TensorRT engine/plan is allowed only as non-portable External metadata. / `Supported` 目录条目必须同时具有：已实现的 DeploySharp 模型/后端路径、可移植格式、不可变 Release 元数据、ModelPack 清单、模型文件、可复现导出记录、再分发许可、SPDX license、测试输入、可复现预期结果资产、SHA256/大小和生成文档路径。当前阶段只有 GGUF 与 `llama-sharp` 具备后端证据。ONNX 和 OpenVINO 可记录为 Preview；TensorRT engine/plan 只能作为不可移植 External 元数据。

Release URLs must use `https://github.com/{owner}/{repository}/releases/download/{models-YYYYMMDD.revision}/{asset}` with no credentials, query, fragment, branch, `latest`, or redirect. The catalog records the source revision and Release commit. Test images follow the same source, license, hash, and tag rules as model files. / Release URL 必须使用 `https://github.com/{owner}/{repository}/releases/download/{models-YYYYMMDD.revision}/{asset}`，且不得含凭据、query、fragment、分支、`latest` 或重定向。目录记录源代码修订和 Release 提交。测试图片与模型文件遵循相同的来源、license、hash 和标签规则。

No upload is automatic. Before creating a real Release, a human must verify repository target, tag uniqueness, every asset size/SHA256, source license and redistribution terms, conversion provenance, test result, and catalog diff. A token is never stored in the catalog, package, cache metadata, logs, or repository. / 不会自动上传。创建真实 Release 前，人工必须复核仓库目标、标签唯一性、每个资产的大小/SHA256、来源许可证和再分发条款、转换来源、测试结果及目录差异。Token 绝不存入目录、包、缓存元数据、日志或仓库。

## HTTP state machine / HTTP 状态机

- Assets stream directly to bounded temporary files; they are not buffered as complete models in memory. / 资产直接流式写入有界临时文件，不会作为完整模型缓冲在内存中。
- 408, 429, and 5xx responses, request timeouts, and transport failures use bounded exponential retry and honor `Retry-After`; other 4xx, redirects, path errors, and integrity failures are not retried. / 408、429、5xx、请求超时和传输故障使用有界指数重试并遵循 `Retry-After`；其他 4xx、重定向、路径错误和完整性失败不重试。
- Caller cancellation ends that caller's wait. A shared underlying download may continue for another caller and remains invisible until verification completes. / 调用方取消会结束该调用方等待；共享底层下载可继续服务其他调用方，并在验证完成前保持不可见。
- Query strings are rejected and diagnostic URIs are sanitized, preventing tokens from entering technical details. / 拒绝 query string，并对诊断 URI 脱敏，防止 token 进入技术详情。

## Cache and cleanup / 缓存与清理

The application chooses a parent cache root. ModelFactory owns only `.deploysharp-model-factory/v1` beneath it. A marker protects ownership; normalized paths, reparse checks, random sibling temporary files, SHA256/size verification, flush, atomic rename, and a final completion marker protect visibility. / 应用选择父缓存根。ModelFactory 只拥有其下的 `.deploysharp-model-factory/v1`。所有权标记、规范化路径、重解析检查、同目录随机临时文件、SHA256/大小验证、刷新、原子重命名和最终完成标记共同保护缓存可见性。

`CleanCacheAsync` supports inactivity age, byte budget, catalog revision, Release tag, and dry run. It never deletes the application cache root or siblings outside the managed namespace. / `CleanCacheAsync` 支持非活动时长、字节预算、目录修订、Release 标签和 dry run。它绝不删除应用缓存根或管理命名空间以外的同级内容。

## TFM and network matrix / TFM 与网络矩阵

| Asset / 资产 | Support / 支持 | Notes / 说明 |
|---|---|---|
| `netstandard2.0` | .NET Framework 4.6.1–4.8.1, .NET Core 3.1, .NET 5–7 | Uses portable `HttpClient` and System.Text.Json assets; legacy TLS/proxy behavior remains OS/runtime-owned. / 使用可移植 HttpClient 和 System.Text.Json 资产；旧 TLS/代理行为由操作系统/运行时负责。 |
| `net8.0` | .NET 8 | Direct modern asset. / 直接现代资产。 |
| `net9.0` | .NET 9 | Direct modern asset. / 直接现代资产。 |
| `net10.0` | .NET 10 | Direct modern asset. / 直接现代资产。 |

Internally-created clients support an `IWebProxy` option and disable automatic redirects. An application-supplied `HttpClient` keeps application ownership and handler policy, but ModelFactory still rejects a final URI different from the catalog URI. / 内部创建的客户端支持 `IWebProxy` 选项并关闭自动重定向。应用传入的 `HttpClient` 保持应用所有权及 handler 策略，但 ModelFactory 仍拒绝与目录 URI 不同的最终 URI。
