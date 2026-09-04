# Special visual pipeline benchmark

This runner measures the complete public multi-artifact workflows that cannot be represented by the single-model visual benchmark:

- CLIP ViT-B/32: image encoding, three-prompt text encoding, and zero-shot scoring.
- SAM v1 ViT-B: image encoding, point/box multimask decoding, and feedback refinement.
- BLIP caption base: image encoding and complete greedy caption generation.

Model loading is excluded. Every timed call includes image decode/preprocessing and all inference/postprocessing steps. The output fingerprint must remain identical across measured iterations.

The CSV includes `total_ms`, `total_p50_ms`, and `total_p95_ms` for each passing workflow. Percentiles use the sorted timed calls after warm-up; `preprocess_ms`, `primary_inference_ms`, `secondary_inference_ms`, and `postprocess_ms` remain arithmetic means. / CSV 对每条通过的完整流程同时输出 `total_ms`、`total_p50_ms` 和 `total_p95_ms`。百分位数只使用预热后的计时调用并按耗时排序计算；`preprocess_ms`、`primary_inference_ms`、`secondary_inference_ms` 和 `postprocess_ms` 仍为算术平均值。

~~~powershell
dotnet run --project tools/DeploySharp.SpecialVisualBenchmark/DeploySharp.SpecialVisualBenchmark.csproj -c Release -- `
  --kind all `
  --backend all `
  --model-root E:\DeploySharp-Models `
  --image E:\Data\image\bus.jpg `
  --warmup 3 `
  --iterations 10
~~~

`--image` is optional. When omitted, the runner downloads and SHA-256 verifies `bus.jpg` from the dedicated `test-assets.1` Release, caching it under `%LOCALAPPDATA%\DeploySharp\TestImages`. `--sam-image` defaults to the same verified image and can be overridden independently.

ONNX Runtime CPU/CUDA and OpenVINO execute the complete ONNX-bound pipelines. OpenCV DNN v1 is explicitly unsupported because text/prompt/decoder graphs require integer or non-image multi-input tensors. TensorRT and TensorRT CUDA are explicitly unsupported until the public multi-artifact profiles admit engine-format artifacts without weakening their audited identity contracts. Unsupported combinations are emitted as rows rather than omitted.
