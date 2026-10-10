# PP-Structure dynamic-batch coverage summary (2026-10-10)

This is a coverage index for PP-Structure assets whose exported graph exposes a dynamic first input axis and whose DeploySharp profile/decoder has a meaningful batch contract. It is not a claim that every backend supports dynamic batch, nor a throughput or accuracy benchmark. Every status below is for the exact artifact and runtime named in the linked evidence.

## Coverage index

| Model family | Batch contract exercised | ORT CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | Evidence boundary |
| --- | --- | :---: | :---: | :---: | :---: | --- |
| PP-LCNet document orientation / table classification | batch=2, distinct rows | ✓ | ✓ | ✓ | △ | TensorRT vendor engine build succeeded, but the DeploySharp batch=2 bridge/runtime path is blocked by `0xC06D007E`; no inference evidence. |
| PP-DocLayout-L / PP-DocLayout-Plus-L / PP-DocBlockLayout | batch=2, auxiliary `im_shape`/`scale_factor` where applicable | ✓ | ✓ | ✗ | △ | OpenCV batch=2 fails with `DS-OCV-8004`; this does not change existing single-image OpenCV entries. |
| RT-DETR-H layout (3-class / 17-class) | batch=2, Paddle NMS and `bbox_num` row split | ✓ | ✓ | ✗ | △ | OpenCV `forward_many` fails with `DS-OCV-8004`; no TensorRT batch=2 evidence. |
| RT-DETR-L table cell (wired / wireless) | batch=2, three-input geometry contract and Paddle NMS | ✓ | ✓ | ✗ | △ | OpenCV batch=2 fails with `DS-OCV-8004`; TensorRT batch=1/engine evidence must not be promoted to batch=2. |
| SLANeXt wired / wireless | batch=2, structure tokens and cell rows | ✓ | ✓* | △ | △ | OpenVINO uses the separately generated Loop-compatible graph; original Release graphs remain blocked by the current importer. |
| PP-FormulaNet Plus-S/M/L, FormulaNet-S/L, UniMERNet | batch=2, independent EOS/token rows | ✓ | ✗ | ✗ | △ | ORT rows match independent batch=1 runs; OpenVINO importer/native failures and OpenCV importer failures are recorded separately. |
| PP-OCRv4 seal detector (mobile / server) | batch=2 probability maps and page indexes | ✓ | ✓ | ✓ | △ | No seal ground truth is implied; TensorRT batch=2 remains unverified. |
| UVDoc unwarping | batch=2, independent corrected-image rows | ✓ | ✓ | ✗ | △ | OpenCV importer fails at `PaddingLayerImpl`; TensorRT evidence is batch=1 only. |
| Chart2Table | model batch remains 1; sequence/KV axes are dynamic | — | — | ✗ | ✓ | Autoregressive generation is intentionally serialized per image; OpenCV prefill/decode rank contract is unsupported. |
| PP-DocLayout-M/S and PicoDet layout exports | static model batch=1 | — | — | — | — | Use independent sessions/page concurrency instead of claiming model Batch support. |

`✓*` means the derived Loop-compatible OpenVINO artifact, not the original SLANeXt Release ONNX. `△` means no real dynamic-batch inference evidence for that exact artifact/backend; `✗` means the exact dynamic-batch attempt was run and failed; `—` means model batch is not applicable because the graph is static or the family is intentionally serialized.

## Evidence map

- ONNX axis inventory: [`paddle-document-onnx-batch-axis-audit-20261005.json`](paddle-document-onnx-batch-axis-audit-20261005.json)
- PP-LCNet orientation/table classification: [`paddle-document-dynamic-batch-ort-openvino-20261005.md`](paddle-document-dynamic-batch-ort-openvino-20261005.md), [`paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.md), and the TensorRT batch probe [`paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md`](paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md)
- Layout and RT-DETR batch rows: [`paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.md), [`paddle-document-additional-layout-dynamic-batch-opencv-20261007.md`](paddle-document-additional-layout-dynamic-batch-opencv-20261007.md), [`paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-20261007.md`](paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-20261007.md), [`paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.md), and [`paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.md`](paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.md)
- SLANeXt: [`paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md`](paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md) and [`paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md`](paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md)
- Formula, seal and UVDoc: [`formula-dynamic-batch-six-models-ort-20261007.md`](formula-dynamic-batch-six-models-ort-20261007.md), [`paddle-document-formula-dynamic-batch-opencv-20261007.md`](paddle-document-formula-dynamic-batch-opencv-20261007.md), [`paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md), and [`paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md`](paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md)

## Interpretation

The summary closes the **inventory and representative execution-coverage** item for dynamic PP-Structure Batch on this Windows host. It does not close the remaining TensorRT dynamic-batch bridge failure, OpenCV importer limitations, formula quality, long-text quality, model-level accuracy, cross-device performance, or long-running stability gates. Static exports remain valid through independent session/page concurrency and are not silently relabelled as model Batch support.

