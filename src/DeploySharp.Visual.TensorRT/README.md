# DeploySharp.Visual.TensorRT

TensorRT-specific visual integration for device-resident preprocessing and admitted postprocessing. The pipeline uploads compact BGR `UInt8` pixels, runs resize/letterbox, RGB/BGR conversion, and per-channel normalization on CUDA, then enqueues TensorRT on the same stream. Compatible RMBG alpha outputs remain on that stream for validation and source restoration. Compatible packed YOLO instance segmentation also keeps candidate/prototype outputs on CUDA for filtering, mask combination, source restoration, and thresholding while retaining deterministic CPU NMS. Existing CPU-prepared and CPU-decoded `VisualPipeline` paths remain available as the compatibility fallback.

The initial contract supports one static Float32 NCHW image input, batch one, linear `Resize`, centered `Letterbox`, or bottom-right padding. CUDA, TensorRT, NVRTC, the native bridge, and compatible serialized engines remain application-owned.

CUDA postprocessing defaults to `TensorRtCudaVisualPostprocessingMode.WhenSupported`; use `Disabled` for an exact CPU-postprocessing comparison. `UsesCudaPostprocessing` exposes the admission result. Profiles that would increase managed materialization, including the default PaDiM raw-plus-restored-map contract, stay on the CPU decoder.

Packed YOLO segmentation returns owned dense masks by default. Enable `YoloPackedDecoderOptions(generateRle: true)` only when the additional row-major RLE representation is required; leaving it disabled avoids a redundant full-mask scan and allocation.
