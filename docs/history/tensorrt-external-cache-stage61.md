# TensorRT local engine and CUDA artifact cache / TensorRT 本地 engine 与 CUDA 工件缓存

Stage 61 provides two explicit layers in `JYPPX.DeploySharp.Backend.TensorRT`:

- `TensorRtExternalCacheStore` is the bounded local storage primitive for PTX, CUBIN, `.engine` and `.plan` entries.
- `TensorRtLocalSessionFactory` resolves or creates those artifacts and can create a TensorRT session from a caller-supplied ONNX model.

Existing compiler, builder, provider, session and inference APIs remain persistence-free. Cache I/O begins only after the application constructs and calls the store or facade. / Stage 61 提供有界本地存储原语与显式编排门面；既有 compiler、builder、provider、session 和 inference API 仍不隐式访问缓存。

## Cache root / 缓存目录

`TensorRtLocalCacheOptions` and `TensorRtExternalCacheStore` accept only an absolute application-selected root. When omitted from the facade options, the facade uses the current user's local application data directory:

```text
Windows: %LOCALAPPDATA%\JYPPX\DeploySharp\TensorRT
Other platforms: LocalApplicationData/JYPPX/DeploySharp/TensorRT
```

The directory is created only when the facade is constructed. Cache data is consumer-owned runtime state and is excluded from NuGet, Git, release assets, model inventory and the official catalog. Different processes must not write the same cache root concurrently. Same-process factories are deduplicated by normalized root, artifact kind and key. / 目录仅在显式构造门面时创建；缓存是 consumer-owned runtime state。不同进程不得并发写同一 root，同一进程按 root、工件类别与 key 去重。

## Compatibility identities / 兼容身份

CUDA lookup keys bind role, source and complete header names/content in canonical ordinal include-name order, compiler options and identity, target architecture, artifact kind, kernel identity, CUDA runtime and driver identity, GPU architecture or caller-defined compatibility class, and native bridge identity.

Engine keys bind ONNX SHA256 and managed build inputs, managed package and API contract, TensorRT/CUDA/cuDNN/driver/bridge identities, GPU compatibility class and compute capability, platform, profiles, workspace, flags and adapter schema. Physical GPU UUIDs are intentionally not accepted. Compatible devices can therefore reuse an entry, while different GPU model/class, architecture, compute capability, driver or runtime inputs still generate different keys. / key 有意不绑定物理 GPU UUID；兼容设备可复用，不兼容的型号、架构、compute capability、driver 或 runtime 会生成不同 key。

## Storage protocol / 存储协议

The stable layout is `deploysharp-tensorrt-cache-v1/{cuda|engine}/{key[0..2]}/{key}/`. Writers publish an immutable generation and then atomically replace `current.json` in the same directory. Readers enforce bounded sizes, strict versioned JSON, fixed relative names, exact identity fields and SHA256/length checks before returning a hit.

Directory substitution, traversal, reparse points, incomplete generations and swapped or tampered content are rejected. Windows readers also require a link count of one for metadata and payload files. Rejected-entry keep/delete/quarantine and valid-payload replacement remain explicit policies. / reader 在返回命中前执行限长、严格 schema、固定相对路径、完整 identity 与 SHA256/长度检查，并拒绝目录替换、路径逃逸、reparse、hard-link、未完成或篡改内容。

## Explicit facade / 显式门面

```csharp
using var factory = new TensorRtLocalSessionFactory(
    new TensorRtLocalCacheOptions(cacheRootPath));

using TensorRtLocalEngineResult engine = factory.ResolveOrBuildEngine(
    onnxArtifact,
    buildOptions,
    engineIdentity,
    cancellationToken);

using TensorRtLocalSessionResult session = factory.CreateSessionFromOnnx(
    onnxArtifact,
    buildOptions,
    engineIdentity,
    backendRequest,
    SessionOptions.Default,
    cancellationToken);
```

A validated hit does not start TensorRT or NVRTC. A miss invokes the corresponding builder or compiler once. Same-key waiters share the completed payload or the same factory exception; different keys do not block each other. Cancellation is propagated and never treated as cache corruption. If native loading rejects the resolved artifact, the facade invalidates that exact key, rebuilds or recompiles once, and propagates a second failure without another retry. The application continues to own native runtimes, models, streams, device memory and all cache roots. / 命中不启动 builder/compiler；同 key 等待方共享结果或同一 factory 异常，不同 key 互不阻塞；取消不会被当作缓存损坏。native load 失败时仅对精确 key 执行一次 invalidate 与重建/重编译，第二次失败直接抛出。

Applications may manually copy a serialized engine and load it through the existing `TensorRtBackendProvider` without using this cache. Cache identity checks are managed preconditions only; final engine compatibility is always decided by TensorRT native deserialization. DeploySharp NuGet packages contain no native runtime, engine, plan, PTX or CUBIN payload. / 调用方可手工复制 engine 并通过现有 provider 直接加载；最终兼容性始终由 TensorRT native deserialize 决定。DeploySharp NuGet 不携带 native runtime、engine、plan、PTX 或 CUBIN。

## Validation boundary / 验证边界

Managed-only tests cover PTX/CUBIN and engine/plan CRUD, per-dimension deterministic compatibility keys, manifest integrity, limits, traversal, hardlink and reparse rejection, cancellation, same-key success/failure deduplication, different-key independence, facade hit/miss behavior and the one-retry load rule. GPU/native execution is a separate environment-dependent proof. Core and ModelPack remain TensorRT-free. / managed-only 测试覆盖两类缓存、逐维兼容 key、安全边界、取消、同 key 成功/失败去重、不同 key 独立、门面 hit/miss 与单次重试；真实 GPU 验证取决于本机精确环境。Core 与 ModelPack 继续零 TensorRT 依赖。
