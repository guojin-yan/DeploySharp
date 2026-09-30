# PP-Structure backend status summary (2026-09-30)

This is a readable summary of the authoritative [31-entry backend matrix](paddle-document-backend-matrix-20260929.json). Status is exact artifact × backend evidence, not model-family extrapolation.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| ONNX Runtime CPU | 31 | 0 | 0 |
| OpenVINO CPU | 22 | 8 | 1 |
| OpenCV DNN CPU | 3 | 1 | 27 |
| TensorRT CUDA | 6 | 0 | 25 |

## Supported evidence scope

- ORT CPU has graph/decoder evidence for all 31 matrix entries.
- OpenVINO has exact evidence for document orientation, UVDoc, all twelve registered layout artifacts, PP-DocLayout-L, both derived SLANeXt compatibility graphs, table classification/cell wired, both seal models and Chart2Table; formula imports are explicit unsupported rows. The twelve-layout execution report is [`paddle-document-openvino-layout-matrix-20260930.md`](paddle-document-openvino-layout-matrix-20260930.md).
- OpenCV DNN has exact evidence for orientation, PP-DocLayout-L and table classification. Chart2Table Vision/Embedding load in isolation, but full autoregressive generation remains unverified because the current C# Mat bridge only admits 2-D auxiliary tensors while Prefill/KV Decode require rank 3/4.
- TensorRT CUDA has exact evidence for orientation, PP-DocLayout-L, table classification, both seal models and the four-graph Chart2Table bundle. Other PP-Structure engines remain unverified.

## Interpretation

`pass` means the exact artifact/backend/device/runtime combination has real execution evidence. `unsupported` means it was attempted and failed at the current importer/runtime. `unverified` means code/assets exist but no exact real execution evidence has been registered. These counts do not establish dataset accuracy, general platform support or performance rankings.
