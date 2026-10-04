# PP-Structure TensorRT orientation evidence (2026-10-04)

The opt-in `PaddleDocumentTensorRtExternalIntegrationTests.DocumentOrientationRunsThroughCudaPreprocessTensorRtAndDecoder` test passed on machine `JYPPX` after switching to the runtime that matches the checked-in bridge.

## Runtime and inputs

- Windows, TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, bridge package `jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge/4.0.0`.
- CUDA target: `compute_86`; API line: TensorRT 10.
- Model: `paddle-doc/pp-lcnet-x1-0-doc-ori`.
- ONNX SHA-256: `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`.
- Validation image: `E:\Model\PaddleDocument\validation\img_rot180_demo.jpg`.
- Image SHA-256: `c5a77e031470e13878ff4f28a06ca843fd455d95c20b1b49b486681e346209ed`.

## Result

The DeploySharp TensorRT builder created an Engine of `10,270,852` bytes in `63,634.811 ms`; the steady-state protocol used 5 warmups, 50 measured iterations, batch 1, one Session and optimization level 3.

| Metric | Result |
|---|---:|
| P50 total | `1.1231 ms` |
| P95 total | `1.8312 ms` |
| Last preprocessing | `0.039 ms` |
| Last inference | `0.891 ms` |
| Last postprocessing | `0.007 ms` |
| Image decode | `103.153 ms` |

The CUDA result selected class index `2` with score `0.89481854`; the CPU reference score was `0.89238550`, an absolute difference of `0.00243304` within the test tolerance `0.005`. The generated Engine SHA-256 is `cc7e1454489680630a18a1d6c403668b9d7571dc8d4410c9d33d75bb21ef2547`.

## Reproduction

Set `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1`, `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API=10`, and point `JYPPX_NATIVE_BRIDGE_PATH`, `JYPPX_TENSORRT_ROOT`, `JYPPX_CUDA_ROOT` and `JYPPX_CUDNN_ROOT` at matching consumer-owned installations. Then run:

```powershell
dotnet test .\tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj `
  --configuration Release --no-restore `
  --filter 'FullyQualifiedName~DocumentOrientationRunsThroughCudaPreprocessTensorRtAndDecoder'
```

## Boundary

This is one model, one image and one Windows device. The earlier TensorRT 11 attempt was correctly blocked with `DS-TRT-5005` because the available bridge is TRT 10.11; this report must not be read as TRT 11 support. It also does not provide GPU clock telemetry, multi-session throughput, or evidence for the remaining PP-Structure models.
