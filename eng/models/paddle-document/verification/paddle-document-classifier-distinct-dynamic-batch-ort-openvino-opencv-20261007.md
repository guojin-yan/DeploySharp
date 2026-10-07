# PP-LCNet classifier distinct-row dynamic Batch across CPU backends (2026-10-07)

## Scope

This run exercises the official PP-LCNet document-orientation and table-classification ONNX exports with batch=2 on ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU. Unlike the earlier duplicate-row batch probe, this run uses two distinct non-overlapping horizontal regions from one source image and verifies that both prepared tensor rows and both raw output rows remain distinct before decoding.

This is model execution and row-isolation evidence for these two exact artifacts. The image regions are execution probes, not annotated orientation or table-classification ground truth. The test does not assert cross-backend numerical parity, accuracy, throughput or cross-device behavior; raw output SHA values differ slightly across runtime implementations even when top labels agree.

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

All six model/backend combinations passed the batch=2 input/output shape checks and full classification decoder. Within every combination, the two input-row SHA values and the two raw-logit-row SHA values differ, so the measured rows were not duplicates and the backend did not return a repeated raw output row. Top labels observed for each region also matched across these three backend runs.

| Backend | Model | Top labels (row 0 / row 1) | Scores (row 0 / row 1, rounded) | Distinct input/output rows |
|---|---|---|---|---|
| ONNX Runtime CPU | Document orientation | `0_degree / 0_degree` | `0.9243435 / 0.92513317` | pass |
| ONNX Runtime CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477461` | pass |
| OpenVINO CPU | Document orientation | `0_degree / 0_degree` | `0.9243434 / 0.92513305` | pass |
| OpenVINO CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477462` | pass |
| OpenCV DNN CPU | Document orientation | `0_degree / 0_degree` | `0.9243435 / 0.92513317` | pass |
| OpenCV DNN CPU | Table classification | `wired / wired` | `0.9560565 / 0.9477461` | pass |

The observed label agreement is not an accuracy result: the source bands have no ground-truth labels, and this test does not enforce a tolerance-based cross-backend parity contract. Exact per-row logits digests, scores, model hashes and input hashes are retained in the [machine-readable report](paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json).

## Reproduction

Place the two models under `E:\Model\PaddleDocument\onnx` and retain the source image path above. Run from the repository root:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_CLASSIFIER_DISTINCT_DYNAMIC_BATCH_REPORT_PATH = (Join-Path (Get-Location) 'eng/models/paddle-document/verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json')
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --configuration Release --no-restore `
  --filter FullyQualifiedName~OfficialDynamicBatchClassifiersPreserveDistinctRowsAcrossCpuBackends --verbosity minimal
```

The opt-in integration test passed `1/1`, covering two exact models across three CPU backends. This report does not establish classification quality or a performance ranking.
