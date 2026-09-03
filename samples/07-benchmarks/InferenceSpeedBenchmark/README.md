# Inference speed benchmark / 推理速度基准

This complete sample measures one fixed classification graph through the available DeploySharp backends after the model session is loaded. It records warmup count, timed iterations, min/P50/P95/max/average latency, throughput, managed allocations, OS, architecture, process architecture, and .NET runtime in an optional JSON report. / 本完整示例在模型 Session 加载后，使用同一份固定分类图测量可用的 DeploySharp 后端，记录预热次数、计时次数、最小/P50/P95/最大/平均延迟、吞吐、托管分配、操作系统、架构、进程架构和 .NET 运行时，并可写入 JSON 报告。

## Run / 运行

From the repository root:

~~~powershell
dotnet run --project samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj -c Release -- --backend all --warmup 10 --iterations 100 --output artifacts/benchmark.json
~~~

Use <code>--backend onnxruntime</code>, <code>--backend opencv-dnn</code>, or <code>--backend openvino</code> to measure one backend. The sample returns a non-zero exit code only when every selected backend is unavailable. An unavailable native runtime is recorded as <code>status=unavailable</code> rather than as a zero or fabricated timing.

Validate the command-line contract without native model loading with <code>pwsh -NoProfile -File eng/benchmarks/Test-InferenceSpeedBenchmark.ps1</code>. / 可使用 <code>pwsh -NoProfile -File eng/benchmarks/Test-InferenceSpeedBenchmark.ps1</code> 在不加载原生模型的情况下验证命令行参数合同。

## Measurement boundary / 测量边界

- Session loading, OpenVINO compilation, native library loading, model download, preprocessing, and postprocessing are outside the timed loop.
- The timed loop calls the same named-tensor input with synchronous inference. The tiny fixture is a contract benchmark, not an algorithm-quality or production-model claim.
- Compare reports only when model bytes, input shape, precision, build configuration, warmup, iteration count, thread settings, native runtime, driver, power mode, and machine state are recorded and held constant.
- The project file currently names the Windows x64 OpenCV and OpenVINO runtime packages used by this Alpha. To measure another platform, replace those two application-owned runtime references with the matching package/RID for that machine before restoring; the backend contracts and benchmark code remain unchanged. The current <code>2.0.0-alpha.1</code> release publishes Windows x64 verification; other platforms are measurement hooks and remain unverified until a report is produced.

See the [performance benchmarking guide](../../../docs/articles/performance-benchmarking.md), [platform support](../../../docs/articles/platform-support.md), and the [model/backend matrix](../../../docs/model-backend-verification-matrix.md) for the test method and current status.
