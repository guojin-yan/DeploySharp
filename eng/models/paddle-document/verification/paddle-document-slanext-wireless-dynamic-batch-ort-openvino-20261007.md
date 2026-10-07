# Wireless SLANeXt dynamic Batch decoding evidence (2026-10-07)

This record verifies dynamic input batching for the wireless SLANeXt table-structure export. Two identical rows from the official table-recognition image run through the official ONNX graph with ONNX Runtime CPU and a separately alpha-renamed OpenVINO-compatible graph with OpenVINO CPU. The test covers input preparation, real batch binding, the two-output table decoder, row isolation and markup consistency.

The OpenVINO run uses the derived compatibility artifact because the original Release graph is rejected by the current OpenVINO `Loop` importer. This single-host, one-image result is execution and decoder evidence; it does not measure table accuracy or throughput and does not qualify other runtime/device combinations.

## Artifacts and protocol

| Backend | Artifact | SHA-256 | Input | Decoder outputs |
| --- | --- | --- | --- | --- |
| ONNX Runtime CPU | `E:\\Model\\PaddleDocument\\onnx\\slanext-wireless.onnx` | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | `[2,3,512,512]` | `fetch_name_0`, `fetch_name_1` |
| OpenVINO CPU | `E:\\Model\\PaddleDocument\\onnx-normalized-rerun-20260929\\slanext-wireless-openvino-compat.onnx` | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | `[2,3,512,512]` | `fetch_name_0`, `fetch_name_1` |

Input image: `E:\\Model\\PaddleDocument\\validation\\table_recognition.jpg`, SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`. `CreateTableStructure(..., maximumBatch: 2)` binds two full-source ROI rows. The decoder returns one result per row.

## Results

| Backend | Result rows | Token counts | Cell counts | Markup SHA-256 for both rows | Status |
| --- | ---: | --- | --- | --- | --- |
| ONNX Runtime CPU | 2 | `24, 24` | `13, 13` | `53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557` | pass |
| OpenVINO CPU (compatibility graph) | 2 | `24, 24` | `13, 13` | `53de44f9af566b55f7a583489d157d4e75e5ba6a834a77f0245712b16e3d2557` | pass |

The two identical rows preserved token indices, token counts, cell counts and HTML markup across both backends. The machine-readable report is [`paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json`](paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json). The opt-in test is `OfficialDynamicBatchWirelessSlaNextRunsOnOrtAndOpenVino` in `PaddleDocumentDynamicBatchIntegrationTests.cs`; set `DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL=1` to execute it.

The wired counterpart is recorded in [the wired SLANeXt dynamic Batch report](paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md). Both variants still require their explicit compatibility graphs for OpenVINO; these runs do not make the original graphs directly importable.
