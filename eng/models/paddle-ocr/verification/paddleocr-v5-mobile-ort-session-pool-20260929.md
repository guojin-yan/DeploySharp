# PP-OCRv5 Mobile ORT session-pool comparison (2026-09-29)

This record connects the existing Session pool and concurrency contracts to a real Windows device measurement. It compares one independently-created stage Session with two independently-created stage Sessions using the same model, image and formal 5/50 protocol.

## Fixed configuration

- Device: JYPPX, Windows 10 build `10.0.26200`, x64, 16 logical processors.
- Backend: ONNX Runtime CPU.
- Model: PP-OCRv5 Mobile; detector/recognizer/classifier ONNX assets remain the local `E:\Model\paddleocr` files. The v5 Mobile recognizer SHA-256 is `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`.
- Input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`.
- Warm-up/measurement: 5 / 50, recognition batch 4, prepared input reused, no autotune or inter-test delay.
- Both runs returned 16 regions and used the same source text and result contract SHA.

## Results

| Stage Sessions | Total P50 (ms) | Total P95 (ms) | Recognition (ms) | Recognition inference work (ms) | Result contract SHA |
|---:|---:|---:|---:|---:|---|
| 1 | 662.330 | 1130.299 | 567.586 | 531.821 | `10d8953bb20df31f2ca831cd95d0dbd7a47c567d05540c130fd4f3d3876714f6` |
| 2 | 422.132 | 548.821 | 333.904 | 590.659 | `10d8953bb20df31f2ca831cd95d0dbd7a47c567d05540c130fd4f3d3876714f6` |

The two-session run reduced total P50 by `36.27%` and P95 by `51.44%`. Recognition wall time fell because independent stage Sessions overlap work; the summed inference-work field is not a wall-clock latency and can increase when work is concurrent. The exact result contract remained byte-identical.

These are steady-state observations on one device and one image. They do not establish a universal optimum, long-run soak stability, GPU scaling, or cross-device throughput. The formal TensorRT matrix and the public CPU quality records remain separate evidence sets.

## Reproduction

Use the formal runner with `DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY=1` and `2`, keeping all other variables identical:

```powershell
dotnet restore tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -p:DeploySharpPaddleOcrCuda12=true --locked-mode
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --no-restore -p:DeploySharpPaddleOcrCuda12=true
```

Then set `DEPLOYSHARP_PADDLEOCR_BACKENDS=onnxruntime`, `DEPLOYSHARP_PADDLEOCR_VERSIONS=v5-mobile`, `DEPLOYSHARP_PADDLEOCR_WARMUP=5`, `DEPLOYSHARP_PADDLEOCR_ITERATIONS=50`, `DEPLOYSHARP_PADDLEOCR_BATCH_SIZE=4`, `DEPLOYSHARP_PADDLEOCR_REUSE_INPUT=1`, and the image path above before invoking the benchmark DLL. Validate each CSV with `Test-PaddleOcrBenchmarkReport.ps1`.
