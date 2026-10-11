# PP-Structure table pipeline: ORT/OpenVINO (2026-10-11)

This record covers one real Windows execution of the complete table path:

`table_recognition.jpg → PP-LCNet table classification → RT-DETR-L wired cell detection → SLANeXt wired structure recognition → HTML`

Both backends passed the same result contract:

| Backend | Class | Cell-detector candidates | Structure cells | Tokens | HTML length | Total |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | `wired_table` | 300 | 13 | 24 | 165 | 2001.241 ms |
| OpenVINO CPU | `wired_table` | 300 | 13 | 24 | 165 | 2127.139 ms |

The OpenVINO SLANeXt stage uses the separately derived `slanext-wired-openvino-compat.onnx` graph. The original SLANeXt Loop graph remains importer-blocked in the current OpenVINO runtime; the compatibility graph is therefore bound to its own measured SHA-256 and compatibility ID rather than the source graph declaration.

Input SHA-256: `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`.

The result is execution and decoder-contract evidence on one Windows host. `300` is the threshold-zero RT-DETR candidate count; `13` is the number of source-space cells emitted by SLANeXt. Neither is cell recall or table accuracy. Both rows completed without decoder warnings. The timings are single-run observations, not P50/P95 performance evidence. OpenCV DNN and TensorRT full table pipelines remain separate matrix items and are not promoted by this record.

Machine-readable details, including every model SHA-256, are in [paddle-document-table-pipeline-ort-openvino-20261011.json](paddle-document-table-pipeline-ort-openvino-20261011.json). Reproduce with:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_TABLE_PIPELINE = '1'
dotnet test tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore `
  --filter FullyQualifiedName~PaddleDocumentTablePipelineIntegrationTests `
  --logger "console;verbosity=detailed"
```
