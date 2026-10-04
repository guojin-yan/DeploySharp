# PP-Structure TensorRT table classification evidence (2026-10-04)

The opt-in `PaddleDocumentTensorRtExternalIntegrationTests.TableClassificationRunsThroughTensorRtAndPreservesOrtScore` test passed on machine `JYPPX` with the TensorRT 10.11 bridge that matches the installed runtime.

## Runtime and inputs

- Windows, TensorRT `10.11.0.33-cu12`, CUDA `12.9`, cuDNN `9.22`, bridge package `jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge/4.0.0`.
- Model: `paddle-table/pp-lcnet-x1-0-table-cls`.
- ONNX SHA-256: `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`.
- Validation image: `E:\Model\PaddleDocument\validation\table_recognition.jpg`.
- Image SHA-256: `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`.

## Result

The DeploySharp TensorRT builder created an Engine of `8,593,404` bytes in `58,024.8733 ms`. The steady-state protocol used 5 warmups and 50 measured iterations with batch 1 and one Session.

| Metric | Result |
|---|---:|
| P50 total | `0.9974 ms` |
| P95 total | `1.1903 ms` |
| TensorRT label | `wired_table` |
| TensorRT score | `0.8517079` |
| ORT CPU reference score | `0.8442081` |
| Absolute score difference | `0.0074998` |
| Contract tolerance | `0.01` |

The score difference is within the existing contract tolerance. The generated Engine SHA-256 is `f80df8411b34714cfa77eddefa7a1c2afaca70e83dceeaf85601b76545e6f195`.

## Reproduction

Use the same runtime variables as the [orientation reproduction](paddle-document-tensorrt-orientation-20261004.md), set `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL=1` and `DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API=10`, then run:

```powershell
dotnet test .\tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj `
  --configuration Release --no-restore `
  --filter 'FullyQualifiedName~TableClassificationRunsThroughTensorRtAndPreservesOrtScore'
```

## Boundary

This is one model, one image and one Windows device. It validates the table-classification contract and score tolerance only; it does not validate table-cell recall, SLANeXt HTML quality, TRT 11 bridge compatibility, GPU clock control or the complete PP-Structure TensorRT matrix.
