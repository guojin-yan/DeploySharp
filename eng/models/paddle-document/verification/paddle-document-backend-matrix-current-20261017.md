# PP-Structure backend status — current index

Generated 2026-10-18T02:30:34Z from source revision 1d7edf67bbffcb12c4366ce518d0e4406f44574f. The authoritative matrix is [eng/models/paddle-document/verification/paddle-document-backend-matrix-20260929.json](paddle-document-backend-matrix-20260929.json), whose artifact rows are intentionally preserved as the source of truth.

This index covers **31 exact artifact/backend rows** on the named Windows host. It is an execution/contract index, not a claim that every PP-Structure task is production-ready.

| Backend | pass | unsupported | unverified |
| --- | ---: | ---: | ---: |
| onnxruntimeCpu | 31 | 0 | 0 |
| openvinoCpu | 23 | 8 | 0 |
| opencvDnnCpu | 11 | 20 | 0 |
| tensorRtCuda | 21 | 10 | 0 |

## Device and runtime scope

- Device: **JYPPX**; OS build 10.0.26200; GPU NVIDIA GeForce RTX 3060 Laptop GPU; driver 576.02.
- ORT CPU: Microsoft.ML.OnnxRuntime 1.28.0; OpenVINO CPU: OpenVINO runtime 2026.2.1 / OpenVINO C# API 3.3.1; OpenCV DNN CPU: OpenCV 5.0.0; TensorRT CUDA: TensorRT 10.11.0.33-cu12 / API 10 bridge for legacy records; TensorRT 11.0.0.114-cu12 / API 11 bridge for the 2026-10-11 layout and 2026-10-17 PP-OCRv4 seal records.
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

All 33 matrix-level evidence references resolve locally. The audit does not validate external URLs or the semantic correctness of a report.

## Formal performance evidence coverage

The following exact evidence bundles declare at least 5 warm-ups and 50 measured calls. This is a coverage index, not a ranking and not a substitute for a full 31-artifact × backend matrix.

| Evidence bundle | Scope | Backends | Models | Protocol |
| --- | --- | --- | --- | --- |
| official-classification | Official document-orientation and table-classification examples | onnxruntime-cpu, openvino-cpu, opencv-dnn-cpu | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-table/pp-lcnet-x1-0-table-cls | 5 warm-up + 50 measured |
| pp-doclayout-l-opencv-dnn | PP-DocLayout-L single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| pp-docblocklayout-opencv-dnn | PP-DocBlockLayout single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-doc/pp-docblocklayout | 5 warm-up + 50 measured |
| pp-doclayout-plus-l-opencv-dnn | PP-DocLayout-Plus-L single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-doc/pp-doclayout-plus-l | 5 warm-up + 50 measured |
| rt-detr-h-layout-3cls-opencv-dnn | RT-DETR-H 3-class layout single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-doc/rt-detr-h-layout-3cls | 5 warm-up + 50 measured |
| rt-detr-h-layout-17cls-opencv-dnn | RT-DETR-H 17-class layout single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-doc/rt-detr-h-layout-17cls | 5 warm-up + 50 measured |
| rt-detr-l-wired-cell-opencv-dnn | RT-DETR-L wired-cell detector single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-table/rt-detr-l-wired-cell-det | 5 warm-up + 50 measured |
| rt-detr-l-wireless-cell-opencv-dnn | RT-DETR-L wireless-cell detector single-image OpenCV DNN CPU execution with bounded ORT parity | opencv-dnn-cpu | paddle-table/rt-detr-l-wireless-cell-det | 5 warm-up + 50 measured |
| document-orientation-tensorrt10-single-image | PP-LCNet document orientation single-image TensorRT 10.11/API 10 execution and bounded CPU score comparison | tensorrt-cuda | paddle-doc/pp-lcnet-x1-0-doc-ori | 5 warm-up + 50 measured |
| table-classification-tensorrt10-single-image | PP-LCNet table classification single-image TensorRT 10.11/API 10 execution and bounded CPU score comparison | tensorrt-cuda | paddle-table/pp-lcnet-x1-0-table-cls | 5 warm-up + 50 measured |
| multipage-orientation-layout | Two-page orientation to layout task chain | onnxruntime-cpu, openvino-cpu | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| multipage-orientation-layout-tensorrt | Two-page orientation to layout task chain with TensorRT CUDA | tensorrt-cuda | paddle-doc/pp-lcnet-x1-0-doc-ori<br>paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| static-layout-tensorrt11 | Five PP-DocLayout and PP-DocBlockLayout static Batch=1 models with bounded ORT parity | tensorrt-cuda | paddle-doc/pp-doclayout-l<br>paddle-doc/pp-doclayout-plus-l<br>paddle-doc/pp-doclayout-m<br>paddle-doc/pp-doclayout-s<br>paddle-doc/pp-docblocklayout | 5 warm-up + 50 measured |
| pp-docblocklayout-tensorrt11-dynamic-batch2 | PP-DocBlockLayout dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/pp-docblocklayout | 5 warm-up + 50 measured |
| pp-doclayout-l-tensorrt11-dynamic-batch2 | PP-DocLayout-L dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| pp-doclayout-plus-l-tensorrt11-dynamic-batch2 | PP-DocLayout-Plus-L dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/pp-doclayout-plus-l | 5 warm-up + 50 measured |
| rtdetr-h-17cls-tensorrt11-dynamic-batch2 | RT-DETR-H 17-class layout dynamic Batch=2 with bounded ORT parity on TensorRT 11 | tensorrt-cuda | paddle-doc/rt-detr-h-layout-17cls | 5 warm-up + 50 measured |
| rtdetr-h-3cls-tensorrt11-dynamic-batch2 | RT-DETR-H 3-class layout dynamic Batch=2 with bounded ORT parity on TensorRT 11 | tensorrt-cuda | paddle-doc/rt-detr-h-layout-3cls | 5 warm-up + 50 measured |
| picodet-static-layout-tensorrt11 | Six static PicoDet layout models with bounded ORT parity | tensorrt-cuda | paddle-doc/picodet-layout-1x<br>paddle-doc/picodet-layout-1x-table<br>paddle-doc/picodet-s-layout-3cls<br>paddle-doc/picodet-l-layout-3cls<br>paddle-doc/picodet-s-layout-17cls<br>paddle-doc/picodet-l-layout-17cls | 5 warm-up + 50 measured |
| rtdetr-h-layout-3cls-tensorrt-dynamic-batch | RT-DETR-H 3-class layout dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/rt-detr-h-layout-3cls | 5 warm-up + 50 measured |
| rtdetr-h-layout-17cls-tensorrt-dynamic-batch | RT-DETR-H 17-class layout dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-doc/rt-detr-h-layout-17cls | 5 warm-up + 50 measured |
| table-task-chain-ort | Table classification, cell detection and original SLANeXt HTML task chain | onnxruntime-cpu | paddle-table/pp-lcnet-x1-0-table-cls<br>paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/slanext-wired | 5 warm-up + 50 measured |
| table-task-chain-openvino | Table classification, cell detection and OpenVINO-compatible SLANeXt HTML task chain | openvino-cpu | paddle-table/pp-lcnet-x1-0-table-cls<br>paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/slanext-wired-openvino-compat | 5 warm-up + 50 measured |
| table-cell-tensorrt | Wired and wireless table-cell detector parity | tensorrt-cuda | paddle-table/rt-detr-l-wired-cell-det<br>paddle-table/rt-detr-l-wireless-cell-det | 5 warm-up + 50 measured |
| seal-mobile-tensorrt11-dynamic-batch2 | PP-OCRv4 mobile seal detector dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-seal/ppocrv4-mobile | 5 warm-up + 50 measured |
| seal-server-tensorrt11-dynamic-batch2 | PP-OCRv4 server seal detector dynamic Batch=2 with bounded ORT parity | tensorrt-cuda | paddle-seal/ppocrv4-server | 5 warm-up + 50 measured |
| seal-mobile-opencv-dnn | PP-OCRv4 mobile seal detector single-image OpenCV DNN CPU execution with shape/decoder contract reference | opencv-dnn-cpu | paddle-seal/ppocrv4-mobile | 5 warm-up + 50 measured |
| seal-server-opencv-dnn | PP-OCRv4 server seal detector single-image OpenCV DNN CPU execution with shape/decoder contract reference | opencv-dnn-cpu | paddle-seal/ppocrv4-server | 5 warm-up + 50 measured |
| pp-doclayout-l-openvino-dnn | PP-DocLayout-L single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/pp-doclayout-l | 5 warm-up + 50 measured |
| pp-docblocklayout-openvino-dnn | PP-DocBlockLayout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/pp-docblocklayout | 5 warm-up + 50 measured |
| pp-doclayout-plus-l-openvino-dnn | PP-DocLayout-Plus-L single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/pp-doclayout-plus-l | 5 warm-up + 50 measured |
| rt-detr-h-layout-3cls-openvino-dnn | RT-DETR-H 3-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/rt-detr-h-layout-3cls | 5 warm-up + 50 measured |
| rt-detr-h-layout-17cls-openvino-dnn | RT-DETR-H 17-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/rt-detr-h-layout-17cls | 5 warm-up + 50 measured |
| rt-detr-l-wired-cell-openvino-dnn | RT-DETR-L wired-cell detector single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-table/rt-detr-l-wired-cell-det | 5 warm-up + 50 measured |
| rt-detr-l-wireless-cell-openvino-dnn | RT-DETR-L wireless-cell detector single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-table/rt-detr-l-wireless-cell-det | 5 warm-up + 50 measured |
| seal-mobile-openvino-dnn | PP-OCRv4 mobile seal detector single-image OpenVINO CPU execution with shape/decoder contract reference | openvino-cpu | paddle-seal/ppocrv4-mobile | 5 warm-up + 50 measured |
| seal-server-openvino-dnn | PP-OCRv4 server seal detector single-image OpenVINO CPU execution with shape/decoder contract reference | openvino-cpu | paddle-seal/ppocrv4-server | 5 warm-up + 50 measured |
| pp-doclayout-m-openvino | PP-DocLayout-M single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/pp-doclayout-m | 5 warm-up + 50 measured |
| pp-doclayout-s-openvino | PP-DocLayout-S single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/pp-doclayout-s | 5 warm-up + 50 measured |
| picodet-layout-1x-openvino | PicoDet layout-1x single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-layout-1x | 5 warm-up + 50 measured |
| picodet-layout-1x-table-openvino | PicoDet layout-1x-table single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-layout-1x-table | 5 warm-up + 50 measured |
| picodet-s-layout-3cls-openvino | PicoDet-S 3-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-s-layout-3cls | 5 warm-up + 50 measured |
| picodet-l-layout-3cls-openvino | PicoDet-L 3-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-l-layout-3cls | 5 warm-up + 50 measured |
| picodet-s-layout-17cls-openvino | PicoDet-S 17-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-s-layout-17cls | 5 warm-up + 50 measured |
| picodet-l-layout-17cls-openvino | PicoDet-L 17-class layout single-image OpenVINO CPU execution with bounded ORT parity | openvino-cpu | paddle-doc/picodet-l-layout-17cls | 5 warm-up + 50 measured |
| uvdoc-openvino | UVDoc single-image OpenVINO CPU unwarping execution, decoder contract and bounded raw-output parity with ORT | openvino-cpu | paddle-doc/uvdoc | 5 warm-up + 50 measured |
| slanext-wireless-openvino-compat | Wireless SLANeXt derived OpenVINO compatibility graph with table decoder and exact source-graph ORT markup parity | openvino-cpu | paddle-table/slanext-wireless-openvino-compat | 5 warm-up + 50 measured |
| pp-doclayout-m-ort-cpu | PP-DocLayout-M single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/pp-doclayout-m | 5 warm-up + 50 measured |
| pp-doclayout-s-ort-cpu | PP-DocLayout-S single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/pp-doclayout-s | 5 warm-up + 50 measured |
| picodet-layout-1x-ort-cpu | PicoDet layout-1x single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-layout-1x | 5 warm-up + 50 measured |
| picodet-layout-1x-table-ort-cpu | PicoDet layout-1x-table single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-layout-1x-table | 5 warm-up + 50 measured |
| picodet-s-layout-3cls-ort-cpu | PicoDet-S 3-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-s-layout-3cls | 5 warm-up + 50 measured |
| picodet-l-layout-3cls-ort-cpu | PicoDet-L 3-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-l-layout-3cls | 5 warm-up + 50 measured |
| picodet-s-layout-17cls-ort-cpu | PicoDet-S 17-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-s-layout-17cls | 5 warm-up + 50 measured |
| picodet-l-layout-17cls-ort-cpu | PicoDet-L 17-class layout single-image ONNX Runtime CPU execution with bounded OpenVINO parity | onnxruntime-cpu | paddle-doc/picodet-l-layout-17cls | 5 warm-up + 50 measured |
| chart2table-fixed-sample | Fixed ChartQA Chart2Table single-sample generation | onnxruntime-cpu, openvino-cpu, tensorrt-cuda | paddle-chart/pp-chart2table | 5 warm-up + 50 measured |

| Backend | Evidence rows | Distinct model/backend pairs | Distinct models |
| --- | ---: | ---: | ---: |
| onnxruntime-cpu | 16 | 14 | 14 |
| opencv-dnn-cpu | 11 | 11 | 11 |
| openvino-cpu | 27 | 23 | 23 |
| tensorrt-cuda | 27 | 20 | 20 |

Formal single-model performance evidence now covers every currently passing OpenVINO CPU and OpenCV DNN CPU artifact in the 31-row matrix. ONNX Runtime CPU still lacks formal 5/50 records for 17 passing artifacts, and TensorRT CUDA lacks one for UVDoc. These are performance-evidence gaps only; backend pass/unsupported state is unchanged. The original SLANeXt graphs and FormulaNet/OpenCV importer boundaries remain as recorded in the artifact matrix. No result here establishes dataset accuracy, second-device reproducibility, locked-clock performance, or long-duration throughput.

## Boundaries still open

- Quality: A2 natural continuous ≥3,200-character ground truth, A3 natural rotation/inversion labels, B1b/B2/B3 natural degradation/enhancement safety, formula semantic quality, and ChartQA split accuracy.
- Performance: one unified formal 5-warm-up/50-measured protocol for the full PP-Structure set, a second-device matrix, and long-duration/throughput stability.
- `pass` means only that the exact artifact/backend/device/runtime contract executed with the evidence linked by the matrix; it does not promote neighboring exports, runtimes or model families.
