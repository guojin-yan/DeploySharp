# PP-DocLayout-L FUNSD layout evidence (2026-09-29)

PP-DocLayout-L was run with ONNX Runtime CPU and OpenVINO CPU on the same three FUNSD pages used for the C3 OCR smoke. Both backends emitted 30, 25 and 19 regions at score threshold `0.3`; the concise cross-backend parity summary is [funsd-doclayout-l-multibackend-20260929.json](funsd-doclayout-l-multibackend-20260929.json). Detailed per-region ORT output remains in the original machine-readable report.

The model output order was not simply top-left order on any of the three pages. That is expected for a detection model: consumers must apply a layout-aware reading-order policy after region detection. This evidence proves that the layout model can supply typed page regions; it does not claim region accuracy because no aligned 23-class layout ground truth was used in this run.

The report is [funsd-doclayout-l-ort-evidence.json](funsd-doclayout-l-ort-evidence.json). It complements, rather than replaces, the OCR reading-order boundary in [funsd-reading-order-c3-3pages-20260929.md](funsd-reading-order-c3-3pages-20260929.md).

Reproduction:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentFunsdLayoutEvidenceTests
```
