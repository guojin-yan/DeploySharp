# TensorRT ONNX-to-engine builder / TensorRT ONNX 转 engine 构建器

Stage 51 adds an explicit managed build surface to the already admitted `JYPPX.DeploySharp.Backend.TensorRT` net8 package. This is an Owner-directed capability change: the two unresolved upstream proofs still block formal publication, but they do not block local adapter or builder source. The exact dependency remains the repository-signed NuGet.org `JYPPX.TensorRT.CSharp.API 4.0.0`. / 阶段 51 在已准入的隔离 net8 包中新增显式 managed builder。两项上游 proof 继续只阻止正式发布，不阻止本地 adapter 或 builder 源码。

## Build contract / 构建合同

`TensorRtOnnxEngineBuilder.Build` accepts a Core `ModelArtifact` whose format is `onnx`, one caller-selected `.engine` or `.plan` path, and `TensorRtOnnxEngineBuildOptions`. The managed boundary validates regular-file status, extension, size and optional SHA256 before native initialization. It supports TensorRT API line selection, workspace memory-pool limit, builder optimization level, runtime-default precision, weakly typed strict FP32 or FP16 for TensorRT 8/10, graph-defined explicit Q/DQ INT8, and one min/opt/max optimization profile covering every dynamic non-shape input. Explicit INT8 is accepted only when the parsed network contains both Quantize and Dequantize layers. The first version intentionally rejects shape-tensor inputs and external-data ONNX sidecars. / builder 接收 `onnx` 工件、调用方输出路径和构建选项，在 native 初始化前校验文件、扩展名、大小与 SHA256。支持 API line、workspace、优化级别、runtime default、TRT8/10 弱类型严格 FP32/FP16、图定义 Q/DQ INT8，以及覆盖全部动态普通输入的 min/opt/max profile；只有解析后的 network 同时含 Quantize/Dequantize layer 才接受显式 INT8。首版明确拒绝 shape-tensor 输入和 external-data sidecar。

The engine is built with `BuildSerializedNetwork`, copied to a unique temporary file in the caller's output directory, flushed, size-checked and SHA256-hashed, then atomically moved to the final path. Failure cleanup deletes the temporary file and attempts to dispose every managed TensorRT wrapper. The builder never places the result in a DeploySharp package, official catalog, inventory, model Release, or repository path by policy. / engine 通过 serialized-network API 构建，写入同目录唯一临时文件，flush、长度与 SHA256 校验后原子移动；失败时清除临时文件并释放全部 wrapper。策略上绝不把结果放入 DeploySharp NuGet、official catalog、inventory、通用模型 Release 或仓库。

```csharp
var profile = new TensorRtOnnxInputProfile(
    "images",
    new TensorShape(1, 3, 224, 224),
    new TensorShape(4, 3, 512, 512),
    new TensorShape(8, 3, 1024, 1024));

var options = new TensorRtOnnxEngineBuildOptions(
    apiVersion: TensorRtApiVersion.TensorRt10,
    precision: TensorRtOnnxEnginePrecision.Float16,
    workspaceBytes: 2UL * 1024 * 1024 * 1024,
    inputProfiles: new[] { profile });

var source = new ModelArtifact(modelId, "onnx", onnxPath, onnxSha256);
TensorRtOnnxEngineBuildResult result = new TensorRtOnnxEngineBuilder()
    .Build(source, externalEnginePath, options);
```

## Cache identity and ownership / Cache identity 与所有权

The result records ONNX/output size and SHA256 plus `BuildInputsSha256`. The latter binds the ONNX hash, exact managed dependency ID/version/contentHash, builder-contract version, API line, precision policy, workspace, optimization level, strongly typed policy and sorted profiles. It is deliberately not a complete engine cache key. A reusable cache lookup must append the exact TensorRT, CUDA, cuDNN, NVIDIA driver, native bridge, GPU architecture/device identity and any plugin/tactic identity that affects compatibility. `.engine/.plan` remains device/runtime/profile-bound External local-cache data owned by the application. / 结果记录 ONNX/engine 大小与 SHA256，以及绑定 managed 依赖 ID/version/contentHash 和全部 managed 构建输入的 `BuildInputsSha256`。它不是完整 cache key；可复用 cache 还必须追加精确 TensorRT/CUDA/cuDNN/driver/bridge/GPU 和插件/tactic identity。engine 继续是应用持有的设备绑定 External 本地缓存。

## CUDA/RTC next layer / CUDA/RTC 下一层

CUDA and NVRTC support is planned as a separate managed preprocessing/postprocessing execution layer over caller-owned streams and device buffers. Its public surface will require explicit source/header/options hashes, compiled PTX/CUBIN identity, launch dimensions, synchronization, buffer ownership and disposal contracts. Stage 51 does not add placeholders, native probes or packaged native files. / CUDA/NVRTC 将作为调用方 stream/device buffer 上的独立 managed 前后处理执行层，公共合同需固定 source/header/options、PTX/CUBIN、launch、同步、buffer 所有权与释放规则；本阶段不增加占位 API、native probe 或 native payload。

Formal blocker delta remains retained 2, new 0, disappeared 0, and the TensorRT retained package JSON is unchanged because the upstream package/Release identity did not change. Real GPU build/inference is skipped/blocked until one exact local ONNX/plan and a recordable native/GPU matrix are authorized; no TensorRT algorithm or performance result is claimed. / 正式 blocker 仍为 retained 2/new 0/disappeared 0，上游身份未变，因此 retained JSON 不改写。真实 GPU 构建/推理在精确模型和 runtime matrix 获授权前继续 skip/blocked，不声称算法或性能通过。
