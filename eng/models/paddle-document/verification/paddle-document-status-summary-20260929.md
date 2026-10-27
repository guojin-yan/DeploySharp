# PP-Structure release-status summary (updated 2026-10-27)

This is the compact release interpretation of the authoritative [31-entry backend matrix](paddle-document-backend-matrix-20260929.json), its generated [current index](paddle-document-backend-matrix-current-20261017.md), and the latest [PaddleOCR quality index](../../paddle-ocr/verification/paddleocr-quality-status-20261027.md). Status always applies to an exact artifact, backend, runtime, device and evidence scope; it is not model-family extrapolation.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| ONNX Runtime CPU | 31 | 0 | 0 |
| OpenVINO CPU | 23 | 8 | 0 |
| OpenCV DNN CPU | 11 | 20 | 0 |
| TensorRT CUDA | 21 | 10 | 0 |

These counts describe registered execution contracts, not a promise of dynamic Batch, end-to-end task quality or equal performance. The matrix also tracks 12 narrower dynamic-Batch rows separately: ORT CPU has 12 pass rows; OpenVINO CPU has 6 pass and 6 unverified rows; OpenCV DNN CPU has 2 pass and 10 unsupported rows; TensorRT CUDA has 6 pass and 6 unsupported rows. These values must not be folded into the 31-asset counts above.

## Four release-status classes

### 1. Verified execution

- The exact backend totals in the table above have real execution evidence. ORT CPU covers all 31 registered PP-Structure artifacts; the other backends cover only their listed pass rows.
- The official wired table image has a four-stage classification -> cell detection -> SLANeXt -> per-cell PP-OCR result on ORT CPU and OpenVINO CPU, with 13/13 aligned cells and matching filled HTML. See the [wired table record](paddle-structure-table-cell-ocr-20261027.md).
- All 84 currently passing primary artifact/backend pairs map to a formal 5-warm-up + 50-measured evidence bundle, but the inputs, devices and timing boundaries differ. This is coverage, not a fair speed ranking.

### 2. Experimental or task-limited

- The wireless table route is verified only through classification, cell detection and structure generation. On the fixed `524509` image, threshold `0.5` gives 44 detections for 45 structure cells; threshold `0.4` gives 45 detections but fails with `overlapping-cells:0`. Cell OCR and filled HTML remain intentionally skipped. See the [wireless table diagnosis](paddle-structure-table-wireless-20261027.md).
- The two `slanext-*-openvino-compat` artifacts are separately derived OpenVINO compatibility graphs. Their pass rows do not make the original Loop graphs OpenVINO-compatible.
- Formula and Chart2Table pass rows prove only their linked graph/decoder/generation contracts. Mathematical equivalence, ChartQA split accuracy and broad document-task quality remain open.
- PaddleOCR-VL Release download and selected task executions are available, but the model remains experimental: its fixed Formula corpus and two-page Spotting evidence expose unresolved semantic/quality limits. It is not promoted to universal PP-Structure support.

### 3. Explicitly unsupported on the recorded runtimes

- OpenVINO CPU: 8 exact rows, comprising the two original SLANeXt Loop graphs and six formula graphs.
- OpenCV DNN CPU: 20 exact rows, including UVDoc, eight layout graphs, four SLANeXt graphs, six formula graphs and Chart2Table. The [current index](paddle-document-backend-matrix-current-20261017.md) is the canonical full list.
- TensorRT CUDA: 10 exact rows, comprising four SLANeXt variants and six formula graphs.
- These are attempted importer/runtime failures for the recorded versions. They should remain unsupported until a new export or runtime change produces new exact evidence; rerunning unchanged artifacts does not close them.

### 4. Quality or data insufficient

- No row in the current 31-artifact execution matrix is `unverified`; this class concerns task-level acceptance rather than graph launch.
- Wireless four-stage OCR, annotated table accuracy, formula semantic quality, ChartQA split quality and representative PaddleOCR-VL page/Spotting quality remain unverified or below acceptance.
- PaddleOCR A2 natural wide-line/seam acceptance, A3 natural orientation/inversion, and B1b/B2/B3 degradation/enhancement safety remain open. Clamp and disabled enhancement remain the conservative defaults.
- A unified comparable performance matrix, second-device reproduction and long-duration stability remain future validation work. Existing formal bundles must not be combined into a cross-backend leaderboard.

## Exact backend evidence scope

- ORT CPU has graph/decoder evidence for all 31 matrix entries.
- OpenVINO has exact evidence for document orientation, UVDoc, all twelve registered layout artifacts, PP-DocLayout-L, both derived SLANeXt compatibility graphs, table classification, both wired/wireless table-cell artifacts, both seal models and Chart2Table; formula imports are explicit unsupported rows. The layout and table-cell execution reports are [`paddle-document-openvino-layout-matrix-20260930.md`](paddle-document-openvino-layout-matrix-20260930.md) and [`paddle-document-openvino-table-cell-matrix-20260930.md`](paddle-document-openvino-table-cell-matrix-20260930.md).
- OpenCV DNN has exact pass evidence for orientation, PP-DocLayout-L/Plus-L, PP-DocBlockLayout, two RT-DETR layout models, both table-cell models, table classification and both PP-OCRv4 seal models. The seal evidence is a real Batch=2 execution/row-isolation contract in [`paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md). Twenty exact artifacts are explicitly unsupported by the current OpenCV 5.0 importer/runtime. Chart2Table fails the current N-D auxiliary adapter contract; all four original/derived SLANeXt graphs reach OpenCV import and fail because a graph-valued `Loop.body` is not implemented. See [`chart2table-opencv-isolated-20261013.md`](chart2table-opencv-isolated-20261013.md) and [`slanext-opencv-isolated-20261013.md`](slanext-opencv-isolated-20261013.md). No OpenCV rows remain unverified in this 31-entry matrix.
- TensorRT CUDA has exact pass evidence for 21 assets: both PP-LCNet classifiers, UVDoc, all 12 layout artifacts, both RT-DETR table-cell models, both seal models and the four-graph Chart2Table bundle. Four SLANeXt variants and six formula graphs are explicit current-runtime Builder/importer unsupported rows. There are no `unverified` TensorRT rows in the current 31-entry matrix, but pass still means only the evidence scope attached to each exact artifact, not quality or cross-device support.

## Status semantics

`pass` means the exact artifact/backend/device/runtime combination has real execution evidence. `unsupported` means it was attempted and failed at the current importer/runtime. `unverified` means code/assets exist but no exact real execution evidence has been registered. These counts do not establish dataset accuracy, general platform support or performance rankings.

For the current development closeout, unsupported rows are known importer/runtime limitations rather than pending measurements. Existing reports are sufficient to preserve their present status; a new graph export, runtime change or materially different adapter is required before retesting them. The open quality and performance items above are tracked as later research/acceptance work and do not justify repeating unchanged full-model runs.
