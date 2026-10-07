# PP-LCNet classifier distinct-row dynamic Batch across CPU backends (2026-10-07)

## Scope

This run exercises the official PP-LCNet document-orientation and table-classification ONNX exports with batch=2 on ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU. Unlike the earlier duplicate-row batch probe, this run uses two distinct non-overlapping horizontal regions from one source image and verifies that both prepared tensor rows and both raw output rows remain distinct before decoding.

This is model execution, row-isolation and bounded raw-output numerical-parity evidence for these two exact artifacts. The image regions are execution probes, not annotated orientation or table-classification ground truth. Raw output SHA values differ across runtime implementations because the floating-point values are not bit-identical; the test compares each batch row against ONNX Runtime CPU and enforces a maximum absolute error of `1e-4`. This does not establish accuracy, throughput or cross-device behavior.

## Environment and inputs

| Item | Value |
|---|---|
| Host OS | Windows build `10.0.26200`, x64 process |
| .NET | `10.0.12`, target `net10.0`, Release |
| ONNX Runtime | `1.28.0` |
| OpenVINO C# API / runtime | `3.3.1` / `2026.2.1` |
| OpenCV C# API / runtime | `5.0.0` |
| Source image | `E:\Model\PaddleDocument\validation\table_recognition.jpg` |
| Source image SHA-256 | `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c` |
| Batch rows | top and bottom non-overlapping horizontal bands |
| Input tensor | `[2,3,224,224]` Float32 |

| Model | ONNX SHA-256 |
|---|---|
| `paddle-doc/pp-lcnet-x1-0-doc-ori` | `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0` |
| `paddle-table/pp-lcnet-x1-0-table-cls` | `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` |

## Results

All six model/backend combinations passed the batch=2 input/output shape checks and full classification decoder. Within every combination, the two input-row SHA values and the two raw-logit-row SHA values differ, so the measured rows were not duplicates and the backend did not return a repeated raw output row. Top labels matched across all three backends, and every raw output row stayed within the explicit `1e-4` max-absolute-error tolerance against the ONNX Runtime CPU reference.

| Backend | Model | Top labels (row 0 / row 1) | Scores (row 0 / row 1) | Max abs diff vs ORT (row 0 / row 1) |
|---|---|---|---|---|
| ONNX Runtime CPU | Document orientation | `0_degree / 0_degree` | `0.9243435 / 0.92513317` | `0 / 0` (reference) |
| ONNX Runtime CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477461` | `0 / 0` (reference) |
| OpenVINO CPU | Document orientation | `0_degree / 0_degree` | `0.9243434 / 0.92513305` | `1.1921e-7 / 1.1921e-7` |
| OpenVINO CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477462` | `3.3528e-8 / 1.1921e-7` |
| OpenCV DNN CPU | Document orientation | `0_degree / 0_degree` | `0.9243435 / 0.92513317` | `1.3039e-8 / 2.4214e-8` |
| OpenCV DNN CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477461` | `1.1176e-8 / 3.7253e-8` |

The observed label agreement and numerical parity are not accuracy results: the source bands have no ground-truth labels. The measured maximum error is below `1.2e-7` on this host, with a conservative enforced tolerance of `1e-4`; this tolerance is not a guarantee for other devices or runtime versions. Exact per-row logits, digests, scores, model hashes, input hashes and measured error are retained in the [machine-readable report](paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json).

## Reproduction

Place the two models under `E:\Model\PaddleDocument\onnx` and retain the source image path above. Run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_CLASSIFIER_DISTINCT_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchClassifiersPreserveDistinctRowsAcrossCpuBackends --verbosity minimal
```

The opt-in integration test passed `1/1`, covering two exact models across three CPU backends and enforcing the per-row raw-output tolerance. This report does not establish classification quality or a performance ranking.
