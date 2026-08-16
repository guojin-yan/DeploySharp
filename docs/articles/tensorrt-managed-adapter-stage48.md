# TensorRT managed adapter admission / TensorRT managed adapter 准入

Stage 48 changes the admission boundary: the two unresolved upstream proofs remain formal publication blockers, but they no longer block local implementation of the isolated managed adapter. The exact dependency remains the NuGet.org repository-signed `JYPPX.TensorRT.CSharp.API 4.0.0` package with SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038` and contentHash `jJeYAI80eoneM1uqQrxeCtxf0OaxbHwG6jnSXAa1Bz3AQunsyPWWNPIEQs4M8lu5E8hjgzQ1hy6nJU3ktjYrow==`. / 阶段 48 调整准入边界：两项上游 proof 继续阻止正式发布，但不再阻止隔离 managed adapter 的本地实现。依赖仍是精确 NuGet.org repository-signed 4.0.0 包。

## Adapter contract / 适配器合同

`JYPPX.DeploySharp.Backend.TensorRT` targets `net8.0` and exposes `TensorRtBackendProvider`, options, stable diagnostics, artifact validation, and explicit registry registration. It accepts only `tensorrt-engine` artifacts with `.engine` or `.plan` extensions and verifies regular-file status, size, and optional SHA256 before native deserialization. The provider accepts only `cuda`, owns one execution context, and does not advertise backend asynchronous execution because host output materialization synchronizes the stream. / 独立包目标为 `net8.0`，公开 provider/options/diagnostics/validator/registry。只接收 `tensorrt-engine` 的 `.engine/.plan`，在 native 反序列化前校验文件、大小与可选 SHA256；只接受 `cuda`，单 context 串行执行，不虚假声明异步能力。

The session uses the published `TensorRtRuntime.Deserialize`, `TensorRtEngine.CreateExecutionContext`, `TensorRtInferenceBindings`, and `CudaMemory` APIs. Core Float32, Int8, UInt8, Boolean, Int32, and Int64 named tensors are mapped; Half/BFloat16/Float8/packed types are rejected until Core has an exact lossless representation. Only device-resident linear, non-vectorized, non-shape-I/O bindings are accepted; static dimensions and optimization-profile bounds are checked before enqueue. Dynamic output buffers are allocated only after TensorRT shape inference reports concrete shapes. The adapter never calls `TensorRtBuilder` and never creates or writes an engine. / Session 使用已发布的 runtime/engine/context/bindings/memory API，映射六类可无损表达的 Core 张量；其余类型稳定拒绝。仅接受 device linear、非 vectorized、非 shape-I/O binding；enqueue 前校验静态维度与 profile 边界，动态输出仅在 shape inference 得到具体 shape 后分配。适配器不调用 builder，也不创建或写入 engine。

## Ownership and blockers / 所有权与 blocker

Core and ModelPack contain no TensorRT dependency. CUDA, cuDNN, TensorRT, NVIDIA driver, native bridge, GPU selection, runtime matrix, and `.engine/.plan` lifecycle remain consumer-owned. Plans are External device/runtime/profile-bound local cache data and cannot enter DeploySharp NuGet packages, inventory, official catalog, or a general Release. TensorRT-LLM is out of scope. / Core 与 ModelPack 不依赖 TensorRT；native graph、GPU/runtime matrix 与 plan 生命周期均由 consumer 持有。plan 只能是 External 本地 cache，不进入 NuGet、inventory、official catalog 或通用 Release；不提供 TensorRT-LLM。

The retained release blockers are unchanged: `formal-v4.0.0-release-package-binding-incomplete` and `package-build-lock-assets-unavailable`. Package license and Owner decision remain disappeared historical entries. The retained TensorRT JSON is unchanged because the upstream package and Release binding identities did not change. / 两项 release blocker 保持不变；许可证与 Owner decision 继续是历史 disappeared 项。上游包与 Release identity 未变，因此 retained JSON 不改写。

## Verification / 验证

- Adapter Release build: passed with zero warnings and errors.
- Managed contract tests: `4 passed / 0 skipped / 0 failed`.
- Package-only consumer: passed with marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external gpu=not-run`.
- Candidate package: 8 entries, one net8 managed DLL/XML, no native/model/engine/plan payload.
- Stage 35: 10 packages, 83 TFMs, 10 locks/assets, 5/5 negative scenarios; unsigned and dirty remain release blockers.
- Stage 36: 10 packages, 83 contracts, 48 managed dependencies, 4 consumer-owned native runtimes, 83/83 SourceLink/PDB/API, 7/7 negative scenarios.
- Full solution: `382 passed / 50 skipped / 0 failed`.
- Inventory: 69 entries; exact Qwen admission remains `ADMITTED missing=none`; official catalog remains empty.
- NuGet vulnerable/deprecated: zero; outdated is report-only and no dependency was upgraded.

No exact TensorRT plan, matching GPU, unique CUDA/cuDNN/TensorRT/bridge matrix, or recordable runtime identity was authorized. Engine build/cache/inference, algorithm validation, and performance validation are therefore skipped/blocked, not passed. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 未授权精确 plan、匹配 GPU 与唯一 runtime identity，因此 engine build/cache/infer、算法和性能为 skip/blocked，而不是通过。未执行任何 Git/GitHub 发布写入。
