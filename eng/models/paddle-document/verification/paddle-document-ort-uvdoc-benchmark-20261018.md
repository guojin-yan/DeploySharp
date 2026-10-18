# UVDoc ONNX Runtime CPU benchmark (2026-10-18)

Formal `5 warm-up + 50 measured` evidence for the exact `paddle-doc/uvdoc` ONNX artifact.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `bus.jpg`, SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Model | `uvdoc.onnx`, SHA-256 `10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338` |
| Backend | ONNX Runtime CPU (`Microsoft.ML.OnnxRuntime 1.28.0`) |
| Contract | Input `[1,3,640,640]`; decoded output `640×640×3`, all values finite |
| OpenVINO parity | Max absolute difference `0.0730591`; mean absolute difference `0.00176066`; within declared `0.2/0.01` diagnostic bounds |
| Timing | P50 **147.8045 ms**, P95 **156.4142 ms**, mean 148.1594 ms, min/max 138.8546/157.6856 ms |

Timing includes ONNX Runtime CPU execution and `PaddleDocumentUnwarpingDecoder`, excluding image preparation, model/session creation and the one-time OpenVINO reference. The raw-output comparison is bounded numeric parity only: it does not establish pixel equivalence, visual quality or cross-device ranking.

Machine-readable samples: [`paddle-document-ort-uvdoc-benchmark-20261018.json`](paddle-document-ort-uvdoc-benchmark-20261018.json).

## Reproduction

Place `uvdoc.onnx` under `E:\Model\PaddleDocument\onnx` and use `E:\Data\image\bus.jpg`. From the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ORT_BENCHMARK = '1'
$env:DEPLOYSHARP_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOrtUvDocBenchmarkTests
```

The JSON record contains runtime/device metadata, source revision, exact model/input hashes, all 50 measured samples, percentiles and the bounded parity values. It does not establish unwarping quality, pixel identity, cross-device ranking or support for other graph exports.
