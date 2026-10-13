# PaddleOCR P3 concurrency and lifecycle contract (2026-10-13)

## Result

The current OCR scheduling contract is covered by focused Release/net10.0 tests:

| Scope | Result |
| --- | ---: |
| Visual OCR and VisualPipeline contracts | `95 passed / 0 failed / 0 skipped` |
| ONNX Runtime CPU OCR and lifecycle | `5 passed / 0 failed / 0 skipped` |
| OpenVINO CPU OCR and lifecycle | `4 passed / 0 failed / 0 skipped` |
| OpenVINO provider/session-pool contracts | `9 passed / 0 failed / 0 skipped` |

The suites cover true recognition Batch input, independent single-channel
Session creation, bounded work queues, input-order restoration, queued and
active cancellation, inference failure recovery, prefetch input ownership, and
idempotent disposal. The Visual tests use deterministic providers; the backend
tests load the repository's ONNX/IR fixtures with the real CPU adapters.

The dynamic Batch contract is also exercised with ten batches of four
heterogeneous SROIE crops for PP-OCRv5 Mobile and PP-OCRv6 Small. ORT and
OpenVINO retain batch `4`, with zero input/shape/text mismatches; see the
[dynamic Batch report](paddleocr-recognition-dynamic-batch-ort-openvino-20261002.md).

## Device observations

The existing real-device comparisons show why pool size is a deployment setting,
not a global default. On the same Windows host and input, two independently
created ORT CPU stage Sessions reduced total P50/P95 from `662.330/1130.299 ms`
to `422.132/548.821 ms`, while two OpenVINO CPU Sessions increased it from
`313.503/398.880 ms` to `371.550/527.348 ms`. Both kept the result contract
SHA unchanged. The detailed [ORT record](paddleocr-v5-mobile-ort-session-pool-20260929.md)
and [OpenVINO record](paddleocr-v5-mobile-openvino-session-pool-20260929.md)
preserve the exact protocol and boundary.

## Reproduction

```powershell
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter 'FullyQualifiedName~OcrTests|FullyQualifiedName~VisualPipelineTests'

dotnet test tests/DeploySharp.Backend.OnnxRuntime.Tests/DeploySharp.Backend.OnnxRuntime.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter 'FullyQualifiedName~OcrIntegrationTests|FullyQualifiedName~LifecycleTests'

dotnet test tests/DeploySharp.Backend.OpenVINO.Tests/DeploySharp.Backend.OpenVINO.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter 'FullyQualifiedName~OcrIntegrationTests|FullyQualifiedName~LifecycleTests'
```

The machine-readable result is
[`paddleocr-p3-concurrency-contract-20261013.json`](paddleocr-p3-concurrency-contract-20261013.json).

## Boundary

This closes the bounded P3 contract evidence for the listed OCR paths. It does
not close long-running soak, cross-device scaling, GPU throughput, CER/WER,
quality evaluation, or every PP-Structure model/backend combination. The
remaining plan items must continue to use exact model/runtime evidence rather
than inheriting this contract result.
