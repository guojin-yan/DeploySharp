# PP-OCRv6 Small Session pool and prefetch contract (2026-10-18)

The opt-in test `PaddleOcrSessionPoolExternalIntegrationTests.PpOcrV6SmallUsesIndependentSessionsAndPrefetchedConcurrentFrames` ran the real PP-OCRv6 Small `DET → CLS → REC → merge` pipeline on three local images with ONNX Runtime CPU.

| Item | Value |
| --- | --- |
| Host | JYPPX, Windows 10.0.26200, .NET 10.0.401 |
| Backend | ONNX Runtime CPU 1.28.0 |
| Pipeline concurrency | 2 end-to-end OCR calls |
| Independent sessions | DET 2, CLS 2, REC 2 |
| Prefetch | 2 input-preparation tasks |
| Inputs | `demo_1.jpg` (500×500), `demo_2.jpg` (350×350), `demo_3.jpg` (643×722) |
| Observed preparation overlap | 3 active preparations at peak |
| Result order | 3/3 preserved in input order |
| Result contract | 3/3 returned regions and recognized text; 3 distinct result contracts |
| Observed wall time | 1331.547 ms for this single three-image invocation |

The test also sets `disposeInputOnCompletion=true`; all callback-created inputs are owned and released by `RunPrefetchedAsync` on the successful path. The implementation is in [`PaddleOcrSessionPoolExternalIntegrationTests.cs`](../../../tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrSessionPoolExternalIntegrationTests.cs).

Reproduce on a machine with the same external assets:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_SESSION_POOL_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleOcrSessionPoolExternalIntegrationTests
```

This is a real-model execution, ordering, overlap and ownership contract for one Windows host. It is not a formal `5 warm-up + 50 measured` benchmark, a throughput ranking, a long-running soak, an accuracy score, a GPU result, or evidence for other models/backends. The complete SHA and machine-readable fields are in [`paddleocr-v6-small-session-pool-prefetch-20261018.json`](paddleocr-v6-small-session-pool-prefetch-20261018.json).
