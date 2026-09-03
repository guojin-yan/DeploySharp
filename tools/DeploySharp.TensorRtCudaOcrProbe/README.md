# TensorRT CUDA OCR kernel probe

This small tool compiles the five optional DeploySharp OCR CUDA kernels through the consumer-owned NVRTC bridge:

- fused normalize/letterbox;
- quadrilateral-to-homography;
- perspective crop and normalization (including the fused quadrilateral-to-crop path);
- block-parallel greedy CTC argmax, blank collapse, and confidence output.

Run it on a machine with the TensorRT/CUDA bridge configured:

```powershell
dotnet run --project tools/DeploySharp.TensorRtCudaOcrProbe -c Release -- --load
```

`--load` additionally loads the generated PTX into CUDA device 0. The probe does not allocate OCR tensors or run an application model. It only validates the NVRTC source and driver module boundary. The regular backend-neutral OCR path remains unchanged.

Use `--execute` to allocate small device buffers and launch normalize, homography, crop, and CTC kernels on one stream:

```powershell
dotnet run --project tools/DeploySharp.TensorRtCudaOcrProbe -c Release -- --execute
```

Use `--benchmark` for a GPU-only steady-state timing run. It uses a 1920x1080 source, 736x736 detector tensor, 16 crops of 48x320, and 16x40x18385 CTC logits. Warm-up and iteration counts can be changed with `DEPLOYSHARP_CUDA_OCR_BENCH_WARMUP` and `DEPLOYSHARP_CUDA_OCR_BENCH_ITERATIONS`.

```powershell
dotnet run --project tools/DeploySharp.TensorRtCudaOcrProbe -c Release -- --benchmark
```

The output contains separate `normalize_letterbox`, `crop_split`, `crop_fused`, `ctc_decode`, and `prepost_fused` `CUDA_OCR_KERNEL_TIMING` rows. Values include one final stream synchronization and exclude image decoding, TensorRT engine loading, and host-to-device copies.

The reference RTX 2060 run (5 warm-ups, 30 measurements) reported 0.072 ms normalize/letterbox, 0.118 ms split crop, 0.056 ms fused crop, 0.595 ms CTC decode, and 0.738 ms for the fused pre/post chain. These are CUDA-event device times, not end-to-end OCR latency.

Set `DEPLOYSHARP_TENSORRT_ENGINE` to a static-shape TensorRT engine to additionally exercise `ITensorRtDeviceInferenceSession.RunDevice` with caller-owned input/output buffers on that same stream. Set `DEPLOYSHARP_CUDA_ARCHITECTURE` to the GPU's compatible `compute_XX` target; for an RTX 2060 use `compute_75`.
