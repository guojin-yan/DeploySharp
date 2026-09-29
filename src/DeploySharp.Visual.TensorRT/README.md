# DeploySharp.Visual.TensorRT

TensorRT-specific visual integration for device-resident preprocessing and admitted postprocessing. The pipeline uploads compact BGR `UInt8` pixels, runs resize/letterbox, RGB/BGR conversion, and per-channel normalization on CUDA, then enqueues TensorRT on the same stream. Compatible RMBG alpha outputs remain on that stream for validation and source restoration. Compatible packed YOLO instance segmentation keeps candidate/prototype outputs on CUDA for filtering, deterministic greedy NMS (up to 32768 candidates), mask combination, source restoration, and thresholding. Incompatible contracts retain CPU decoding/NMS. Existing CPU-prepared `VisualPipeline` paths remain available as the compatibility fallback.

The initial contract supports one static Float32 NCHW image input, batch one, linear `Resize`, centered `Letterbox`, or bottom-right padding. CUDA, TensorRT, NVRTC, the native bridge, and compatible serialized engines remain application-owned.

`TensorRtVisualPipeline` owns one context/stream and is intentionally serialized because its device buffers are reused. For concurrent video frames or ROI crops, construct one independent pipeline per TensorRT context and pass them to `TensorRtVisualPipelinePool`; the pool bounds leases, preserves input order for `RunManyAsync`/`RunRoiManyAsync`, and disposes every owned pipeline after active calls finish. The pool does not clone a context or claim that one pipeline is concurrently safe.

CUDA postprocessing defaults to `TensorRtCudaVisualPostprocessingMode.WhenSupported`; use `Disabled` for an exact CPU-postprocessing comparison. `UsesCudaPostprocessing` exposes the admission result. Profiles that would increase managed materialization, including the default PaDiM raw-plus-restored-map contract, stay on the CPU decoder.

Packed YOLO segmentation returns owned dense masks by default. Enable `YoloPackedDecoderOptions(generateRle: true)` only when the additional row-major RLE representation is required; leaving it disabled avoids a redundant full-mask scan and allocation.

## PP-Chart2Table autoregressive TensorRT

For the PP-Chart2Table four-engine bundle, use `PaddleChart2TableTensorRtDeviceSession` instead of the backend-neutral `PaddleChart2TableOnnxSession` when running TensorRT. The specialized path keeps the 48 KV tensors in two CUDA cache banks and reads back only vocabulary logits per decode step. Vision, token embedding, and prefill use the same TensorRT provider; the provider must outlive the session.

```csharp
using var provider = new TensorRtBackendProvider(
    new TensorRtBackendOptions(TensorRtApiVersion.TensorRt10, cudaTargetArchitecture: "sm_86"));
using var session = new PaddleChart2TableTensorRtDeviceSession(
    provider,
    chartTensorRtBundle,
    new BackendRequest(BackendCapabilities.TensorInference, TensorRtBackendProvider.BackendId, "cuda"));

PaddleChart2TableGenerationResult result = session.Generate(
    preparedChartInput,
    chartTokenizer,
    maximumNewTokens: 256);
```

The bundle must contain four `tensorrt-engine` artifacts. The checked RTX 3060 Laptop sample produced the same complete table with strict FP32 language plans and with TF32 tactics enabled; TF32 is an optional measured plan variant, not a dataset-wide precision guarantee. See the [Chart2Table validation record](../../eng/models/paddle-document/verification/chart2table-component-validation.json) for graph/plan hashes, output, and timing boundaries.
