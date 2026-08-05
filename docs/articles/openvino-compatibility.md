# OpenVINO compatibility and lifecycle / OpenVINO 兼容性与生命周期

## Verified package boundary / 已验证包边界

| Component / 组件 | Verified version / 已验证版本 | Direct assets or RID / 直接资产或 RID | DeploySharp conclusion / DeploySharp 结论 |
|---|---:|---|---|
| `JYPPX.OpenVINO.CSharp.API` | 3.3.0 | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | Adapter direct dependency; no native runtime / 适配器直接依赖；不含原生运行时 |
| `OpenVINO.runtime.win` | 2026.2.1 | `win-x64` | Explicit consumer dependency; CPU is tested / 消费者显式依赖；已实测 CPU |
| OpenVINO device plug-ins | 2026.2.1 runtime payload | CPU, AUTO, GPU, NPU and supporting frontends are present in the Windows package / Windows 包中存在这些插件和前端 | Only CPU is admitted by this adapter / 本适配器仅准入 CPU |
| OpenVINO GenAI | not referenced / 未引用 | separate product assets / 独立产品资产 | Not a transitive dependency / 不是传递依赖 |

The backend directly publishes every exact managed desktop asset listed above. Compatibility consumption is possible where NuGet selects an asset, but DeploySharp does not turn end-of-life framework support into an upstream maintenance promise. The validated native baseline is Windows x64 CPU only; Linux, macOS, ARM, AUTO, GPU, and NPU need independent clean-consumer runners before capability admission. / 后端直接发布上述每个托管桌面资产。NuGet 可在兼容框架中选择资产，但 DeploySharp 不会把已停止生命周期框架的包兼容性描述成上游维护承诺。当前原生验证基线仅为 Windows x64 CPU；Linux、macOS、ARM、AUTO、GPU 与 NPU 必须取得独立 clean-consumer runner 证据后才能准入。

Managed 3.3.0 must be paired with runtime 2026.2.x; session creation performs an OpenVINO C ABI preflight before wrapper static initialization. Missing library, wrong architecture, or incompatible version maps to `DS-NATIVE-6001` rather than entering an unsafe wrapper path. / 托管 3.3.0 必须配套 runtime 2026.2.x；会话创建会在包装器静态初始化前执行 OpenVINO C ABI 预检。缺失库、错误架构或不兼容版本映射为 `DS-NATIVE-6001`，不会进入不安全的包装器路径。

NuGet.org has served multiple byte sets under `JYPPX.OpenVINO.CSharp.API 3.3.0`. On 2026-08-05 the flat-container package was 2,230,243 bytes with SHA512/base64 `aAiKwd7O...PUKHWg==`, while registration/cache metadata could still advertise an older hash and cause `NU1403`. Never bypass this check. The durable upstream fix is a new immutable package version, not another overwrite of `3.3.0`; until registration converges, clean-machine restore is externally constrained. / NuGet.org 已在同一 `JYPPX.OpenVINO.CSharp.API 3.3.0` 标识下提供过多组不同字节。2026-08-05 flat-container 包为 2,230,243 字节，SHA512/base64 为 `aAiKwd7O...PUKHWg==`，但 registration/缓存元数据仍可能公布旧哈希并触发 `NU1403`。不得绕过此检查；持久的上游修复应发布新的不可变版本，而不是再次覆盖 `3.3.0`。registration 收敛前，全新环境还原受此外部状态限制。

The 2026-08-05 stage-12 recheck found the stable official `openvino-csharp-api-v3.3.1` release and runtime `2026.3.0`. The managed 3.3.1 nupkg was independently read as 2,232,928 bytes with SHA512/base64 `yzibDFfv1LA5xzCot1aYDcd1w8FJu0geU2PRCF+R4DW3AIElqI3fDH+dQuQKDQ+SpVK1QPInEuGs8bS8V3+VFg==`; it contains the same 15 managed TFM assets and no runtime payload. However, the official managed and runtime flat-container `.nupkg.sha512` endpoints still returned HTTP 404, so package metadata was not yet converged. DeploySharp did not disable hash verification, upgrade packages, or rewrite lock files; it retained the audited 3.3.0 + 2026.2.1 cache for reproducible local gates. / 2026-08-05 阶段 12 复核发现官方稳定 Release `openvino-csharp-api-v3.3.1` 与 runtime `2026.3.0`。独立读取的托管 3.3.1 nupkg 为 2,232,928 字节，SHA512/base64 为 `yzibDFfv1LA5xzCot1aYDcd1w8FJu0geU2PRCF+R4DW3AIElqI3fDH+dQuQKDQ+SpVK1QPInEuGs8bS8V3+VFg==`，包含相同 15 个托管 TFM 资产且无 runtime payload。但官方托管包与 runtime 的 flat-container `.nupkg.sha512` 端点仍返回 HTTP 404，包元数据尚未收敛。DeploySharp 未关闭哈希验证、未升级包或改写 lock file，继续使用已审计的 3.3.0 + 2026.2.1 缓存执行可复现本地门禁。

## Execution and ownership / 执行与所有权

- `Run` uses native synchronous `Infer`; cancellation is observed before and after that native boundary. / `Run` 使用原生同步 `Infer`；取消在原生边界前后观察。
- `RunAsync` uses native `StartAsync`, timed `WaitFor`, and `Cancel`; it does not use `Task.Run`. / `RunAsync` 使用原生 `StartAsync`、定时 `WaitFor` 与 `Cancel`，不使用 `Task.Run`。
- Every call creates an independent `InferRequest` and owned input Tensor. `SessionOptions.MaxConcurrency` bounds calls without sharing mutable request state. / 每次调用创建独立 `InferRequest` 和自有输入 Tensor；`SessionOptions.MaxConcurrency` 限制并发，但不共享可变请求状态。
- Outputs are copied into Core-owned arrays before request disposal and remain valid after session disposal. / 输出在请求释放前复制到 Core 自有数组，会话释放后仍有效。
- Dispose rejects new work, cancels active async requests, waits for every concurrency slot, releases compiled model/model/core in reverse order, and is idempotent. / Dispose 拒绝新工作、取消活动异步请求、等待全部并发槽位、按逆序释放编译模型/模型/Core，并保持幂等。

`OpenVinoOptions` accepts only CPU and an allowlisted set of compile properties: performance hint, stream count, inference thread count, absolute cache directory, and profiling. Duplicate, unknown, empty, or overlapping strongly typed properties are rejected. No silent device fallback occurs. / `OpenVinoOptions` 仅接受 CPU 与准入的编译属性：性能提示、流数量、推理线程数、绝对缓存目录和 profiling。重复、未知、空值或与强类型配置重叠的属性会被拒绝，不发生静默设备回退。

## Diagnostics and fixture evidence / 诊断与夹具证据

Configuration is `DS-OV-5101`; model/compile is `DS-OV-5102`; input name/type/shape is `DS-OV-5103`; unsupported element type is `DS-OV-5104`; inference is `DS-OV-5105`; cancellation is `DS-OV-5106`; disposed objects are `DS-OV-5107`; device/plug-in failure is `DS-OV-5108`; IR sidecar failure is `DS-OV-5109`; native runtime/ABI failure is `DS-NATIVE-6001`. Inner exceptions, model/backend/tensor/device, operation, and sanitized technical details are preserved. / 对应稳定诊断依次为配置 `DS-OV-5101`、模型/编译 `DS-OV-5102`、输入名称/类型/形状 `DS-OV-5103`、不支持元素类型 `DS-OV-5104`、推理 `DS-OV-5105`、取消 `DS-OV-5106`、对象释放 `DS-OV-5107`、设备/插件 `DS-OV-5108`、IR sidecar `DS-OV-5109` 与原生运行时/ABI `DS-NATIVE-6001`。内部异常、模型/后端/张量/设备、操作及脱敏技术详情会被保留。

`eng/test-models/Generate-OnnxRuntimeFixtures.py` creates the ONNX contracts. `eng/test-models/Generate-OpenVinoFixtures.py`, pinned to OpenVINO 2026.2.1, converts classification, semantic segmentation, Pose, instance segmentation, and OBB Direct graphs to `.xml + .bin` with `compress_to_fp16=False`. Both manifests record sizes and SHA256. These Apache-2.0 fixtures are adapter contracts, not algorithm models, benchmark results, official catalog entries, or Release assets. / 两个生成脚本分别创建 ONNX 合同，并使用锁定的 OpenVINO 2026.2.1 将分类、语义分割、Pose、实例分割与 OBB Direct 图转换为 `compress_to_fp16=False` 的 `.xml + .bin`；清单记录大小和 SHA256。这些 Apache-2.0 夹具只是适配器合同，不是算法模型、性能结果、官方目录条目或 Release 资产。
