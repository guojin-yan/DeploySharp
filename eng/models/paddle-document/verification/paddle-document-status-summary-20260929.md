# PP-Structure backend status summary (updated 2026-10-13)

This is a readable summary of the authoritative [31-entry backend matrix](paddle-document-backend-matrix-20260929.json). Status is exact artifact × backend evidence, not model-family extrapolation.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| ONNX Runtime CPU | 31 | 0 | 0 |
| OpenVINO CPU | 23 | 8 | 0 |
| OpenCV DNN CPU | 11 | 20 | 0 |
| TensorRT CUDA | 21 | 10 | 0 |

## Supported evidence scope

- ORT CPU has graph/decoder evidence for all 31 matrix entries.
- OpenVINO has exact evidence for document orientation, UVDoc, all twelve registered layout artifacts, PP-DocLayout-L, both derived SLANeXt compatibility graphs, table classification, both wired/wireless table-cell artifacts, both seal models and Chart2Table; formula imports are explicit unsupported rows. The layout and table-cell execution reports are [`paddle-document-openvino-layout-matrix-20260930.md`](paddle-document-openvino-layout-matrix-20260930.md) and [`paddle-document-openvino-table-cell-matrix-20260930.md`](paddle-document-openvino-table-cell-matrix-20260930.md).
- OpenCV DNN has exact pass evidence for orientation, PP-DocLayout-L/Plus-L, PP-DocBlockLayout, two RT-DETR layout models, both table-cell models, table classification and both PP-OCRv4 seal models. The seal evidence is a real Batch=2 execution/row-isolation contract in [`paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md`](paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md). Twenty exact artifacts are explicitly unsupported by the current OpenCV 5.0 importer/runtime. Chart2Table fails the current N-D auxiliary adapter contract; all four original/derived SLANeXt graphs reach OpenCV import and fail because a graph-valued `Loop.body` is not implemented. See [`chart2table-opencv-isolated-20261013.md`](chart2table-opencv-isolated-20261013.md) and [`slanext-opencv-isolated-20261013.md`](slanext-opencv-isolated-20261013.md). No OpenCV rows remain unverified in this 31-entry matrix.
- TensorRT CUDA has exact pass evidence for 21 assets: both PP-LCNet classifiers, UVDoc, all 12 layout artifacts, both RT-DETR table-cell models, both seal models and the four-graph Chart2Table bundle. Four SLANeXt variants and six formula graphs are explicit current-runtime Builder/importer unsupported rows. There are no `unverified` TensorRT rows in the current 31-entry matrix, but pass still means only the evidence scope attached to each exact artifact, not quality or cross-device support.

## Interpretation

`pass` means the exact artifact/backend/device/runtime combination has real execution evidence. `unsupported` means it was attempted and failed at the current importer/runtime. `unverified` means code/assets exist but no exact real execution evidence has been registered. These counts do not establish dataset accuracy, general platform support or performance rankings.
