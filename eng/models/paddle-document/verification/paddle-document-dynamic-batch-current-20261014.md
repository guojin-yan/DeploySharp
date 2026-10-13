# PP-Structure dynamic Batch current evidence (2026-10-14)

## Scope

This archive records the external run of `PaddleDocumentDynamicBatchIntegrationTests` on one Windows host. The test command was:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL='1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  --filter "FullyQualifiedName~PaddleDocumentDynamicBatchIntegrationTests"
```

The test ran against source revision `c7589d257a566a245f921039fd2c14ab1b364167` and was archived with the later documentation-only revision `188b06942090162f4be64b445835f153e7f1d891`. The host reported Windows NT `10.0.26200.0`, .NET SDK `10.0.401`, `Release/net10.0`, and `--no-restore`. Wall time was approximately 11 minutes 35 seconds.

Result: **23 passed / 0 failed / 0 skipped**. The 23 raw JSON records are preserved under [`dynamic-batch-20261014/`](dynamic-batch-20261014/). The machine-readable aggregate is [`paddle-document-dynamic-batch-current-20261014.json`](paddle-document-dynamic-batch-current-20261014.json).

The maintained PP-OCR/PP-Structure Markdown scope was rechecked after this update: [`document-link-audit-20261014-dynamic-batch.json`](document-link-audit-20261014-dynamic-batch.json) reports 678 local links and 0 broken links. External URLs and runtime-generated paths are outside that audit.

## Backend and model-family summary

| Family | ORT CPU | OpenVINO CPU | OpenCV DNN CPU | What the result proves |
| --- | :---: | :---: | :---: | --- |
| PP-LCNet document/table classification | ✓ | ✓ | ✓ | Batch binding, distinct rows, decoder mapping and bounded raw-output parity for the exact artifacts |
| PP-DocLayout / PP-DocBlockLayout / RT-DETR layout | ✓ | ✓ | ✗¹ | Batch geometry binding and Paddle NMS row isolation; OpenCV exact dynamic forward is blocked |
| PP-OCRv4 seal mobile/server | ✓ | ✓ | ✓ | Batch mask execution and source/page row mapping for the two exact seal graphs |
| RT-DETR table-cell detector | ✓ | ✓ | ✗¹ | Three-input batch binding and cell-decoder row isolation; OpenCV exact dynamic forward is blocked |
| FormulaNet Plus-S/M/L, S/L and UniMERNet | ✓² | — | ✗³ | Dynamic token-row/EOS contract only; no formula semantic-quality claim |
| SLANeXt wired/wireless | ✓ | ✓⁴ | — | Table decoder row isolation; OpenVINO uses the derived compatibility graph |
| UVDoc | ✓ | ✓ | — | Output shape, row isolation and bounded numeric parity |

¹ OpenCV 5.0.0 returned `DS-OCV-8004` (`Requested blob not found`) for the exact dynamic-batch forward combinations. The result is kept as a precise unsupported boundary; no third-party importer investigation was performed.

² Each FormulaNet/UniMERNet JSON contains two dynamic-batch rows with distinct decoded token sequences and EOS state. These are token/row contracts, not rendering, mathematical equivalence or accuracy evidence.

³ The six formula graphs were also probed at batch=1. OpenCV 5.0.0 stopped before inference with `DS-OCV-8002` at the `ConstantOfShape`/allocation importer boundary. This does not change ORT support or generalize to every OpenCV version.

⁴ The original SLANeXt graphs remain blocked by the OpenVINO `Loop` importer. The successful rows use the separately generated alpha-renamed compatibility graphs; their SHA values and conversion manifest remain independent of the official Release assets.

## Evidence inventory

The archive contains the exact JSON emitted by the test process, including model SHA-256 values, input/output shapes, per-row hashes, decoder counts, status strings and precise OpenCV errors. The principal records are:

- `paddle-document-dynamic-batch-ort-openvino.json`, `paddle-document-layout-dynamic-batch-ort-openvino.json`, and `paddle-document-rtdetr-layout-dynamic-batch-ort-openvino.json` for the common classification/layout contracts.
- `paddle-document-additional-layout-dynamic-batch-ort-openvino.json` and its OpenCV companion for PP-DocLayout-Plus-L and PP-DocBlockLayout.
- `paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json` for the two classifiers and all three CPU backends.
- `paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json` for PP-OCRv4 mobile/server seal masks.
- `paddle-document-table-cell-dynamic-batch-ort-openvino.json` and the OpenCV boundary record for wired/wireless RT-DETR cell detectors.
- Six `paddle-document-formula-*-dynamic-batch-ort-20261007.json` files plus the dynamic and batch-one OpenCV records for FormulaNet/UniMERNet.
- `paddle-document-slanext-dynamic-batch-ort-openvino.json`, `paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json`, and `paddle-document-uvdoc-dynamic-batch-ort-openvino.json` for table structure and unwarping.

## Interpretation boundaries

This is a one-host execution/row-isolation snapshot. It does not establish labeled accuracy, CER/WER, throughput, formal P50/P95 performance, GPU utilization, clean-checkout reproducibility, TensorRT coverage or cross-device portability. In particular, FormulaNet/UniMERNet dynamic rows do not close the open formula rendering/semantic-quality gate. TensorRT dynamic Batch results are maintained in their separate per-model reports and are deliberately not counted in this aggregate.
