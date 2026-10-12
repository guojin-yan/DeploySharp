# PicoDet PP-Structure static Batch=1 TensorRT 11 verification

Date: 2026-10-12
Host: Windows 10.0.26200 x64, RTX 3060 Laptop (`compute_86`)
Runtime: TensorRT `11.0.0.114-cu12`, CUDA `12.9`, cuDNN `9.22`, .NET `10.0.401`
Input: `E:\Data\image\bus.jpg`
Test: `PaddleDocumentTensorRtExternalIntegrationTests.PicoDetLayoutRunsStaticBatchOneOnTensorRtAndMatchesOrt`

## Scope and protocol

The six official PicoDet layout ONNX artifacts were each handled in an isolated run. DeploySharp's `TensorRtOnnxEngineBuilder` created a fresh TensorRT 11 Engine from the exact ONNX file; no optimization profile was supplied because the exported `image` and `scale_factor` inputs are statically `batch=1`. The same prepared input was executed through ONNX Runtime CPU and TensorRT CUDA, and both outputs were decoded by `PaddleDocumentNmsDecoder`.

Each run used five warm-up calls and fifty measured calls. The timing includes the existing TensorRT session and Paddle NMS decoder path; it is not a kernel-only benchmark. The ORT comparison uses score tolerance `0.01` and greedy global same-class IoU matching with a `0.80` IoU floor. To avoid making a low-score fused-NMS boundary decision look like a graph failure, the evidence records both raw candidate counts and the high-confidence (`score >= 0.05`) count used for matching.

## Results

| Model | Input | Raw ORT/TRT | Confident ORT/TRT | Min IoU | P50/P95 (ms) | Engine SHA-256 |
|---|---:|---:|---:|---:|---:|---|
| `picodet-layout-1x` | 608x800 | 18 / 18 | raw-count parity run | 0.999584 | 10.9028 / 13.0822 | `2705f28799202461e3c70a8f0d6462678dbb0bcff0c9dbd6234597381e01bacf` |
| `picodet-layout-1x-table` | 608x800 | 3 / 3 | 0 / 0 | — | 13.3740 / 18.3881 | `8048913f5bca1744e368df81b3d0a3bdfb877e77d7afed431e4a9e71fe79e1b4` |
| `picodet-s-layout-3cls` | 480x480 | 66 / 66 | raw-count parity run | 0.810328 | 6.4182 / 7.8753 | `0f67226cd438ebdf328f618b257268a2f31e7f4d1f290317a4c6d21b84a65f05` |
| `picodet-l-layout-3cls` | 640x640 | 76 / 76 | raw-count parity run | 0.999202 | 10.0988 / 14.0942 | `e58bdc56b22c3eaef92be30046dc8c7d55a1d64946606355367e2e7db21be923` |
| `picodet-s-layout-17cls` | 480x480 | 77 / 78 | 4 / 4 | 0.999989 | 6.4061 / 11.4737 | `96331cc046f1cc054962dfce290e66b6f001ba8c8ffb94ea06c597f3701eecfd` |
| `picodet-l-layout-17cls` | 640x640 | 63 / 62 | 11 / 11 | 0.998691 | 10.0628 / 30.0795 | `b6b38dff1b15d7895d73042f51692ba10d07c7d49653752a81a1a070bd71d0c1` |

The two 17-class models differ by one raw low-score row between ORT and TensorRT, while every candidate in the declared confidence band matches. The table-only model has no candidate at or above `0.05` on this image, so no IoU value is defined for that band; the raw row count and execution path still match.

## Boundary

This closes the exact ONNX→TensorRT 11→Paddle NMS execution contract for six PicoDet artifacts on this Windows host. It does not establish layout accuracy, recall, a dataset metric, dynamic Batch support, cross-device Engine portability, or support for a different TensorRT/CUDA stack. PicoDet remains a static `batch=1` export; callers should use independent Sessions or page-concurrency scheduling for parallel pages. OpenCV DNN and other untested backends retain the states in the backend matrix.

Machine-readable details, model/input SHA-256 values, counts and timing samples are in [`paddle-document-picodet-static-tensorrt11-20261012.json`](paddle-document-picodet-static-tensorrt11-20261012.json).
