# RT-DETR-H layout 3-class OpenCV DNN benchmark (2026-10-17)

This report records the exact `paddle-doc/rt-detr-h-layout-3cls` ONNX artifact on the named Windows host. It is a prepared-input latency measurement and bounded ORT parity check, not a layout accuracy result or a ranking of OpenCV against other backends.

| Item | Value |
| --- | --- |
| Device | `JYPPX`, Windows 10 build `10.0.26200`, 16 logical processors |
| Runtime | .NET `10.0.12`, `JYPPX.OpenCV.runtime.win-x64` `5.0.0` |
| Source revision | `68d2c8559cac893aa04b4322e2ec00bc2ca05780` |
| Model | `E:\Model\PaddleDocument\onnx\rt-detr-h-layout-3cls.onnx` |
| Model SHA-256 | `e003528247ea0324eba25c94127a1ff5ef48f8a83ae725a8f42f8f0a1d551d8b` |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |

## Protocol and result

The test creates one `PreparedVisualInput` before timing and reuses it for one OpenCV DNN session. It performs 5 warm-ups followed by 50 measured synchronous `VisualPipeline.Run` calls at 640×640, including geometry auxiliary inputs and Paddle NMS decoding. Image decode/preprocessing, model load, session creation, hashing and the one-time ORT parity reference are excluded. Percentiles use nearest-rank.

| Backend | Batch / sessions | High-confidence parity | P50 (ms) | P95 (ms) | Mean (ms) | Min / max (ms) |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| OpenCV DNN CPU | 1 / 1 | 1 region; score ≤ `0.001`, coordinates ≤ `0.5 px` vs ORT CPU | 1927.6786 | 2115.1677 | 1952.4688 | 1851.7435 / 2316.0313 |

The decoded output contains 300 candidates, of which one has score ≥ `0.5`; its fingerprint and all 50 samples are retained in [`paddle-document-opencv-rt-detr-h-layout-3cls-benchmark-20261017.json`](paddle-document-opencv-rt-detr-h-layout-3cls-benchmark-20261017.json). This result is limited to the exact export, importer/runtime, profile and input; it does not establish dataset accuracy, GPU performance or cross-device behavior.
