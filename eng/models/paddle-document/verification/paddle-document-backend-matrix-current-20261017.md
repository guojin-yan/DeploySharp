# PP-Structure backend status — current index (2026-10-17)

Generated 2026-10-17T09:50:33Z from source revision 7b58886344a0d8059f72c519a31081ede9ec407a. The authoritative matrix is [eng/models/paddle-document/verification/paddle-document-backend-matrix-20260929.json](paddle-document-backend-matrix-20260929.json), whose artifact rows are intentionally preserved as the source of truth.

This index covers **31 exact artifact/backend rows** on the named Windows host. It is an execution/contract index, not a claim that every PP-Structure task is production-ready.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| onnxruntimeCpu | 31 | 0 | 0 |
| openvinoCpu | 23 | 8 | 0 |
| opencvDnnCpu | 11 | 20 | 0 |
| tensorRtCuda | 21 | 10 | 0 |

## Device and runtime scope

- Device: **JYPPX**; OS build 10.0.26200; GPU NVIDIA GeForce RTX 3060 Laptop GPU; driver 576.02.
- ORT CPU: Microsoft.ML.OnnxRuntime 1.28.0; OpenVINO CPU: OpenVINO runtime 2026.2.1 / OpenVINO C# API 3.3.1; OpenCV DNN CPU: OpenCV 5.0.0; TensorRT CUDA: TensorRT 10.11.0.33-cu12 / API 10 bridge for legacy records; TensorRT 11.0.0.114-cu12 / API 11 bridge for the 2026-10-11 layout record.
- GPU rows are valid only for the named TensorRT/CUDA installation and the exact rebuilt engines. CPU rows are independent of the GPU.

## Exact unsupported rows

- **onnxruntimeCpu**: none.
- **openvinoCpu**: paddle-table/slanext-wired, paddle-table/slanext-wireless, paddle-formula/pp-formulanet-plus-s, paddle-formula/pp-formulanet-plus-m, paddle-formula/pp-formulanet-plus-l, paddle-formula/pp-formulanet-s, paddle-formula/pp-formulanet-l, paddle-formula/unimernet.
- **opencvDnnCpu**: paddle-doc/uvdoc, paddle-doc/pp-doclayout-m, paddle-doc/pp-doclayout-s, paddle-doc/picodet-layout-1x, paddle-doc/picodet-layout-1x-table, paddle-doc/picodet-s-layout-3cls, paddle-doc/picodet-l-layout-3cls, paddle-doc/picodet-s-layout-17cls, paddle-doc/picodet-l-layout-17cls, paddle-table/slanext-wired, paddle-table/slanext-wireless, paddle-table/slanext-wired-openvino-compat, paddle-table/slanext-wireless-openvino-compat, paddle-formula/pp-formulanet-plus-s, paddle-formula/pp-formulanet-plus-m, paddle-formula/pp-formulanet-plus-l, paddle-formula/pp-formulanet-s, paddle-formula/pp-formulanet-l, paddle-formula/unimernet, paddle-chart/pp-chart2table.
- **tensorRtCuda**: paddle-table/slanext-wired, paddle-table/slanext-wireless, paddle-table/slanext-wired-openvino-compat, paddle-table/slanext-wireless-openvino-compat, paddle-formula/pp-formulanet-plus-s, paddle-formula/pp-formulanet-plus-m, paddle-formula/pp-formulanet-plus-l, paddle-formula/pp-formulanet-s, paddle-formula/pp-formulanet-l, paddle-formula/unimernet.

## Dynamic Batch snapshot

The matrix carries 12 exact dynamic-Batch artifact rows. These are narrower binding/row-isolation contracts and must not be read as a complete PP-Structure throughput or quality benchmark.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| onnxruntimeCpu | 12 | 0 | 0 |
| openvinoCpu | 6 | 0 | 6 |
| opencvDnnCpu | 2 | 10 | 0 |
| tensorRtCuda | 6 | 6 | 0 |

## Evidence audit

All 31 matrix-level evidence references resolve locally. The audit does not validate external URLs or the semantic correctness of a report.

## Formal performance evidence coverage

The following exact evidence bundles declare at least 5 warm-ups and 50 measured calls. This is a coverage index, not a ranking and not a substitute for a full 31-artifact × backend matrix.

| Evidence bundle | Scope | Backends | Models | Protocol |
| --- | --- | --- | --- | --- |
| official-classification | Official document-orientation and table-classification examples | onnxruntime-cpu, openvino-cpu, opencv-dnn-cpu | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-table/pp-lcnet-x1-0-table-cls | 5 warm-up + 50 measured |
| multipage-orientation-layout | Two-page orientation to layout task chain | onnxruntime-cpu, openvino-cpu | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| multipage-orientation-layout-tensorrt | Two-page orientation to layout task chain with TensorRT CUDA | tensorrt-cuda | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| rtdetr-h-layout-3cls-tensorrt-dynamic-batch | RT-DETR-H 3-class layout dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/rt-detr-h-layout-3cls | 5 warm-up + 50 measured |
| rtdetr-h-layout-17cls-tensorrt-dynamic-batch | RT-DETR-H 17-class layout dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/rt-detr-h-layout-17cls | 5 warm-up + 50 measured |
| table-task-chain-ort | Table classification, cell detection and original SLANeXt HTML task chain | onnxruntime-cpu | paddle-table/pp-lcnet-x1-0-table-cls<br>paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/slanext-wired | 5 warm-up + 50 measured |
| table-task-chain-openvino | Table classification, cell detection and OpenVINO-compatible SLANeXt HTML task chain | openvino-cpu | paddle-table/pp-lcnet-x1-0-table-cls<br>paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/slanext-wired-openvino-compat | 5 warm-up + 50 measured |
| table-cell-tensorrt | Wired and wireless table-cell detector parity | tensorrt-cuda | paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/rt-detr-l-wireless-cell-det | 5 warm-up + 50 measured |
| chart2table-fixed-sample | Fixed ChartQA Chart2Table single-sample generation | onnxruntime-cpu, openvino-cpu, tensorrt-cuda | paddle-chart/pp-chart2table | 5 warm-up + 50 measured |

| Backend | Evidence rows | Distinct model/backend pairs | Distinct models |
| --- | ---: | ---: | ---: |
| onnxruntime-cpu | 8 | 6 | 6 |
| opencv-dnn-cpu | 2 | 2 | 2 |
| openvino-cpu | 8 | 6 | 6 |
| tensorrt-cuda | 7 | 7 | 7 |

Formal coverage is still incomplete for the remaining layout variants, seal/UVDoc/SLANeXt/FormulaNet families, OpenCV DNN where the matrix says unsupported, second-device reproducibility, locked-clock comparison, and long-duration throughput. Those rows remain governed by the exact backend matrix above.

## Boundaries still open

- Quality: A2 natural continuous ≥3,200-character ground truth, A3 natural rotation/inversion labels, B1b/B2/B3 natural degradation/enhancement safety, formula semantic quality, and ChartQA split accuracy.
- Performance: one unified formal 5-warm-up/50-measured protocol for the full PP-Structure set, a second-device matrix, and long-duration/throughput stability.
- `pass` means only that the exact artifact/backend/device/runtime contract executed with the evidence linked by the matrix; it does not promote neighboring exports, runtimes or model families.
