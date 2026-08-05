# ONNX Runtime compatibility and lifecycle / ONNX Runtime 兼容性与生命周期

## Verified package boundary / 已验证包边界

| Component / 组件 | Verified version / 已验证版本 | Direct assets or RIDs / 直接资产或 RID | DeploySharp conclusion / DeploySharp 结论 |
|---|---:|---|---|
| `Microsoft.ML.OnnxRuntime.Managed` | 1.28.0 | `netstandard2.0`, `net8.0` desktop; specialized .NET 9 mobile assets / 桌面及专用移动资产 | Direct backend dependency / 后端直接依赖 |
| `Microsoft.ML.OnnxRuntime` | 1.28.0 | `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-arm64`, Android, iOS / 这些 RID 与移动平台 | User-selected CPU runtime; Windows x64 is CI-tested here / 用户选择的 CPU 运行时；本仓库 CI 实测 Windows x64 |
| `Microsoft.ML.OnnxRuntime.Gpu` | 1.28.0 | Meta-package over version-matched managed and platform GPU packages / 对齐版本的托管与平台 GPU 元包 | Not declared or tested by DeploySharp yet / DeploySharp 尚未声明或测试 |
| `Microsoft.ML.OnnxRuntime.DirectML` | 1.24.4 | Windows plus `Microsoft.AI.DirectML`; version differs from current stable CPU/GPU / Windows 与 DirectML 依赖，版本不同 | Not declared; no silent fallback / 不声明，不静默回退 |

The backend itself ships `netstandard2.0` and `net8.0`. Modern applications may consume the best compatible asset, but the adapter does not claim a direct build for every .NET Framework, .NET Core, or .NET 5-10 TFM. Compatibility does not imply that an end-of-life runtime is supported by Microsoft. / 后端自身发布 `netstandard2.0` 与 `net8.0`。现代应用可选择最佳兼容资产，但适配器不宣称为每个 .NET Framework、.NET Core 或 .NET 5-10 TFM 提供直接构建。包兼容不代表 Microsoft 继续支持已停止生命周期的运行时。

## Execution semantics / 执行语义

- `Run` calls native synchronous inference and links a per-call cancellation token to `RunOptions.Terminate`. / `Run` 调用原生同步推理，并把每次调用的取消 token 连接到独立 `RunOptions.Terminate`。
- `RunAsync` uses real native ORT async only for static outputs, a non-cancellable caller token, and intra-op threads not explicitly set to one. / 仅当输出静态、调用方 token 不可取消且算子内线程数未显式设为 1 时，`RunAsync` 使用真正的 ORT 原生异步。
- Dynamic outputs, cancellable calls, and single-thread sessions use synchronous native fallback on the calling continuation. No `Task.Run` is used. / 动态输出、可取消调用与单线程会话在调用延续上使用同步原生 fallback，不使用 `Task.Run`。
- `SessionOptions.MaxConcurrency` is enforced by a semaphore. Each call owns its `RunOptions` and bound ORT values. / 通过信号量执行 `SessionOptions.MaxConcurrency`；每次调用独占自己的 `RunOptions` 和绑定 ORT 值。
- Returned tensors own copied managed arrays and remain valid after session disposal. Dispose is idempotent, rejects new work, and waits for active native work to unwind. / 返回张量拥有复制后的托管数组，在会话释放后仍有效。Dispose 幂等、拒绝新工作并等待活动原生调用退出。

## Configuration / 配置

`OnnxRuntimeOptions` controls graph optimization, intra/inter-op thread counts, sequential or parallel graph execution, memory patterns, CPU arena, log severity, log ID, and profiling path prefix without exposing vendor objects. CPU is the only accepted device. Profiling requires both Core profiling and an explicit output prefix. / `OnnxRuntimeOptions` 在不暴露厂商对象的情况下控制图优化、算子内/算子间线程、顺序或并行计算图执行、内存模式、CPU arena、日志级别、日志 ID 与 Profiling 路径前缀。CPU 是唯一接受的设备。Profiling 同时要求 Core 开启并提供显式输出前缀。

Operator and opset compatibility belongs to the selected ONNX Runtime version. Invalid protobuf, unsupported operator/opset, and load failures map to `DS-ORT-5002`; input name/type/shape failures map to `DS-ORT-5003`; unsupported stable bridge types map to `DS-ORT-5004`; execution failures map to `DS-ORT-5005`; cancellation maps to `DS-ORT-5006`; disposed objects map to `DS-ORT-5007`; provider errors map to `DS-ORT-5008`; missing or wrong-architecture native libraries map to `DS-NATIVE-6001`. Original exceptions and technical details are preserved. / 算子与 opset 兼容性由所选 ONNX Runtime 版本决定。无效 protobuf、不支持的算子/opset 与加载故障映射为 `DS-ORT-5002`；输入名称/类型/形状错误映射为 `DS-ORT-5003`；稳定桥接不支持类型映射为 `DS-ORT-5004`；执行故障映射为 `DS-ORT-5005`；取消为 `DS-ORT-5006`；对象已释放为 `DS-ORT-5007`；Provider 错误为 `DS-ORT-5008`；缺失或架构错误的原生库为 `DS-NATIVE-6001`。原始异常与技术详情会被保留。

## Reproducible fixtures / 可复现夹具

`eng/test-models/Generate-OnnxRuntimeFixtures.py` uses pinned `onnx==1.22.0` dependencies and ONNX checker to create classification, detection, dynamic-shape, multi-type/multi-I/O, cancellation, and serialization fixtures. `tests/assets/onnxruntime/fixtures.json` records their byte sizes and SHA256 values. They are Apache-2.0 adapter fixtures, not official algorithm models, catalog entries, performance samples, or GitHub Release assets. / `eng/test-models/Generate-OnnxRuntimeFixtures.py` 使用锁定的 `onnx==1.22.0` 依赖和 ONNX checker 创建分类、检测、动态形状、多类型/多输入输出、取消与串行化夹具。`tests/assets/onnxruntime/fixtures.json` 记录其字节大小与 SHA256。它们是 Apache-2.0 适配器夹具，不是官方算法模型、目录条目、性能样例或 GitHub Release 资产。
