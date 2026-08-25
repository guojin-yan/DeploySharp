# TensorRT CUDA/RTC managed preprocessing and postprocessing / TensorRT CUDA/RTC managed 前后处理

Stage 52 adds an independent CUDA/RTC execution layer to the admitted net8 `JYPPX.DeploySharp.Backend.TensorRT` package. It is not a Core backend and is not implicitly coupled to `TensorRtBackendProvider`, `TensorRtSession` or `TensorRtOnnxEngineBuilder`. The two missing upstream proofs continue to block formal publication only; inference and builder admission are unchanged. / 阶段 52 在已准入的隔离 net8 TensorRT 包中新增独立 CUDA/RTC 执行层。它不是 Core backend，也不与 provider、session 或 builder 隐式耦合。两项上游 proof 继续只阻止正式发布，不影响 inference/builder 准入。

## Audited 4.0.0 API map / 已审计的 4.0.0 API 映射

The net8 `JYPPX.CudaSharp.dll` (327,168 bytes, SHA256 `a53db8bf6d45becf407e7d3660a66335d22fc90bf1ba47f1d09f019b6fca0a0`) and XML (523,364 bytes, SHA256 `07f1c03aad6b60be3af60d3aed1eccbd38d14d22d92225f7c7ed963a96e8d1e3`) were checked against the package's `673e120...` source. / net8 DLL/XML 已与包绑定提交的源码交叉核对。

| Purpose / 用途 | Exact managed API / 精确 managed API | Ownership / 所有权 |
| --- | --- | --- |
| Driver capability | `CudaDriver.GetCapability()` returning version, module/function/typed-launch/context/event support and loaded-library diagnostic | Copied snapshot / 复制型快照 |
| Runtime/device identity | `CudaDevice.RuntimeVersion`, `DriverVersion`, `GetInfo(int)`; `CudaDeviceInfo` includes ordinal/name/compute capability | Copied values; current device remains application state / 复制值；当前设备仍是应用状态 |
| Stream | `CudaStream`, `DeviceOrdinal`, `Synchronize()`, `Dispose()` | Caller-owned for Stage 52; never disposed by DeploySharp / Stage 52 中由调用方持有 |
| Device memory | `CudaMemory.SizeInBytes`, `GetPointerAttributes()`, `Dispose()` | Caller-owned; launch takes a SafeHandle lease only / 调用方持有；launch 仅租用 SafeHandle |
| RTC source/options | `CudaRtcProgramSource`, `CudaRtcHeader`, `CudaRtcCompileOptions` | Immutable copied snapshots / 不可变复制型快照 |
| RTC compile/result | `CudaRtcCompiler.Compile`, `CudaRtcCompilationResult`, `FindArtifact` | Temporary native program is disposed by wrapper; result/log/artifacts are copied data / 临时 program 由 wrapper 释放；结果为复制数据 |
| RTC artifact | `CudaRtcArtifact.ToArray()` plus source/header/options/artifact SHA256, compiler version and target | Copied bytes; no native handle / 复制字节，无 native handle |
| Module load | `CudaDriverModule.Load(byte[], int)` | `TensorRtCudaCompiledKernel` owns/disposes the module / compiled kernel 持有并释放 module |
| Typed launch | `CudaDriverModule.Launch(string, CudaKernelLaunchConfiguration, CudaStream, CudaKernelArgument[])` | Returned `CudaDriverKernelLaunch` leases module/stream/memory and synchronizes on disposal / launch owner 租住全部参与者并在释放时同步 |

No native signature is projected through DeploySharp. The implementation uses only these observed public managed contracts. / DeploySharp 不投射任何猜测的 native 签名，只使用已观察到的 managed 合同。

## Public contract and execution / 公共合同与执行

`TensorRtCudaRtcKernelDefinition`, `TensorRtCudaRtcHeader` and `TensorRtCudaRtcCompileOptions` retain exact source, ordered headers, complete compiler options, requested PTX/CUBIN kind, kernel name/name expression and explicit target architecture. `TensorRtCudaRtcCompiler.Compile` compiles in memory and returns `TensorRtCudaRtcArtifact`; no file or long-lived cache is written. CUBIN requires `sm_XX`; PTX accepts an explicit `compute_XX` or `sm_XX` target. / definition/options 固定源码、header、完整选项、工件类型、kernel 名称与显式架构；编译只返回内存工件，不写文件或长期 cache。

`TensorRtCudaBufferDescriptor` fixes name, unmanaged element type, fully static shape, exact byte offset/length and read/write access. `TensorRtCudaDeviceBuffer` borrows `CudaMemory`, verifies allocation bounds and records the actual device ordinal. Kernel arguments are either copied typed scalars or borrowed device buffers. `TensorRtCudaKernelLaunchOptions` fixes grid, block, dynamic shared memory and synchronization as `CallerManaged`, `KernelCompletion` or `StreamCompletion`. There is no default stream. / buffer descriptor 固定名称、类型、静态 shape、精确 range 与访问模式；device buffer 借用调用方 memory 并记录实际 device。launch 固定 grid/block/shared memory 与显式同步策略，不存在默认 stream。

```csharp
var source = new TensorRtCudaRtcKernelDefinition(
    TensorRtCudaKernelRole.Preprocessing,
    cudaSource,
    kernelName: "prepare",
    headers: virtualHeaders);
var compile = new TensorRtCudaRtcCompileOptions("compute_86");
TensorRtCudaRtcArtifact ptx = TensorRtCudaRtcCompiler.Compile(source, compile);

using TensorRtCudaCompiledKernel kernel = TensorRtCudaCompiledKernel.Load(ptx, deviceOrdinal);
var input = new TensorRtCudaDeviceBuffer(
    new TensorRtCudaBufferDescriptor("images", TensorElementType.Float32, shape, TensorRtCudaBufferAccess.ReadWrite),
    callerOwnedMemory);
using TensorRtCudaKernelLaunch launch = kernel.Launch(
    callerOwnedStream,
    new TensorRtCudaKernelLaunchOptions(gridX, blockX, TensorRtCudaSynchronizationMode.KernelCompletion),
    new[] { TensorRtCudaKernelArgument.FromDeviceBuffer(input) });
```

The caller must keep the stream and every buffer logically valid until the launch owner is synchronized/disposed. DeploySharp never disposes them. The compiled kernel owns only its module and refuses disposal while launches are active. Inference/builder lifecycle is separate; a caller sharing stream/buffer state must explicitly order preprocessing, inference and postprocessing and must prevent concurrent reuse. / 调用方必须保持 stream/buffer 有效直到 launch 同步/释放；DeploySharp 不释放它们。compiled kernel 只持有 module，并在 active launch 存在时拒绝释放。共享 inference 状态时由调用方显式排序并防止并发复用。

## Cache identity / Cache identity

The default path has no persistent kernel cache. `TensorRtCudaKernelCacheIdentity` computes a SHA256 only; it performs no I/O. The key binds source/header/options/artifact hashes, PTX/CUBIN kind, compiler version and exact binary identity, target architecture, kernel name, CUDA runtime version/binary identity, CUDA driver version/identity, GPU architecture/identity and native-bridge identity. A future cache writer must be opt-in, caller-path-only, External/local, size/hash validated and atomically replaced in the same directory. / 默认不提供长期 kernel cache。identity 只计算 SHA256、不执行 I/O，并绑定完整 compiler/CUDA/driver/GPU/bridge 字段；未来 writer 必须 opt-in、调用方路径、External/local、长度/hash 校验及同目录原子替换。

## Validation and release status / 验证与发布状态

Managed CUDA/RTC, inference and builder contract tests pass without native initialization. The package-only consumer references the new surface without compiling/loading/launching. No exact authorized local kernel, matching NVIDIA GPU, unique CUDA/driver/native-bridge matrix or recordable runtime identity was supplied, so `CUDA/RTC GPU validation skipped/blocked`; no CUDA/TensorRT algorithm or performance result is claimed. No PTX/CUBIN/fatbin, native runtime, model, engine or plan is packaged. / managed 合同与纯包 consumer 不初始化 native。因缺少获授权 kernel、匹配 GPU、唯一 runtime matrix 与可记录身份，结论为 `CUDA/RTC GPU validation skipped/blocked`，不声称算法或性能通过；没有打包任何 native 或工件。

Final validation is `15 passed / 0 skipped / 0 failed` for focused TensorRT contracts and `393 passed / 50 skipped / 0 failed` for the full solution. Stage 35 passes 10 packages, 83 TFMs, semantic comparison `10/10` and all five negative scenarios. Stage 36 passes 48 managed dependencies, 4 consumer-owned native runtimes, 83 API contracts, SourceLink/PDB `83/83` and all seven negative scenarios. The 31-project package-only matrix reports 17 passes, 11 expected external skips and 3 expected external blocks. / 最终 focused TensorRT 合同为 `15/0/0`，全解决方案为 `393/50/0`；Stage 35/36 正向与全部负向通过，31 项纯包 consumer 为 17 pass、11 个预期 external skip、3 个预期 external blocker。

Read-only Release/tag review found identity unchanged: Release ID `368273346`, `immutable=false`, `updated_at=2026-08-11T00:49:26Z`, 20 unchanged assets, and tag commit `673e120...`. Blocker delta is retained 2, new 0, disappeared 0. Full package admission was therefore not rerun and the retained JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. `formal publication blocked` remains accurate. / Release/tag identity 未变，因此未重跑完整 package admission、未改写 retained JSON；正式发布仍被两项 proof 阻止。
