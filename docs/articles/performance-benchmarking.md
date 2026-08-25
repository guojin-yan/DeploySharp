# Inference performance benchmarking / 推理性能基准

DeploySharp is a model deployment library, so backend correctness and measured runtime behavior are separate release concerns. This guide defines the repeatable speed test used by the repository. It is intentionally based on a small pinned classification graph so contributors can run it without downloading a multi-gigabyte production model.

DeploySharp 是模型部署库，因此后端正确性和运行时性能是两类独立的发布信息。本指南定义仓库使用的可复现速度测试。测试故意使用固定的小型分类图，贡献者无需下载数 GB 的生产模型即可运行。

## Run the benchmark / 运行基准

From the repository root:

~~~powershell
dotnet run --project samples/07-benchmarks/InferenceSpeedBenchmark/InferenceSpeedBenchmark.csproj -c Release -- --backend all --warmup 10 --iterations 100 --output artifacts/benchmark.json
~~~

Select one backend with <code>--backend onnxruntime</code>, <code>--backend opencv-dnn</code>, or <code>--backend openvino</code>. The runner returns a non-zero exit code only when every selected backend is unavailable. Missing or ABI-incompatible native libraries are recorded as <code>unavailable</code> with the exception message.

## What is measured / 测量什么

| Field | Meaning |
| --- | --- |
| Warmup | Inferences discarded before timing to settle JIT and backend caches |
| Min, P50, P95, max | Distribution of synchronous inference-only wall-clock latency in milliseconds |
| Average and throughput | Arithmetic mean and <code>1000 / average_ms</code>; throughput is not a concurrent-load claim |
| Managed allocation | Bytes allocated on the benchmark thread per timed inference |
| Environment | OS description, OS architecture, process architecture, .NET runtime, backend, and device |

Session creation, model parsing/compilation, native library loading, model download, preprocessing, postprocessing, and output inspection are outside the timed loop. The model bytes, input shape, precision, build configuration, thread settings, native runtime, driver, power mode, warmup, and iteration count must remain fixed when comparing two reports.

## Current release evidence / 当前版本证据

The first release is verified on Windows 10/11 x64. The table below is filled only from an actual benchmark report; a dash means that this platform or backend has not been measured in this repository.

| Platform | ONNX Runtime CPU | OpenCV DNN CPU | OpenVINO CPU | Report |
| --- | --- | --- | --- | --- |
| Windows 10/11 x64 | P50 0.0177 ms; P95 0.0404 ms; 47,339/s | P50 0.0065 ms; P95 0.0111 ms; 130,736/s | P50 0.0389 ms; P95 0.1317 ms; 19,082/s | Local Release run on 2026-08-25; 10 warmups/100 iterations |
| Linux x64 | - | - | - | Deferred in 2.0.0-alpha.1 |
| macOS x64/ARM64 | - | - | - | Deferred in 2.0.0-alpha.1 |
| Windows ARM64 | - | - | - | Deferred in 2.0.0-alpha.1 |

The Windows row is a single local Release measurement on Windows 10 x64, .NET 8.0.28, with the pinned classification fixture and the repository's current ONNX Runtime 1.28.0, OpenCV 5.0.0-preview.1, and OpenVINO 3.3.0/2026.2.1 package set. It is a reproducibility reference, not a universal performance promise. Publish a result only with the JSON report and its environment details. Do not compare the tiny fixture with production-model throughput or claim algorithm quality from it.

## Related pages / 相关页面

- Sample source: <code>samples/07-benchmarks/InferenceSpeedBenchmark</code>
- [Platform and backend support](platform-support.md)
- [Performance and model fidelity](performance-and-model-fidelity.md)
- [Model/backend verification matrix](../model-backend-verification-matrix.md)
