# RT-DETR-L wired-cell detector OpenCV DNN benchmark (2026-10-18)

This report records the exact `paddle-table/rt-detr-l-wired-cell-det` ONNX artifact on the named Windows host. It is a prepared-input latency measurement and bounded ORT parity check, not a table-cell recall or accuracy result.

| Item | Value |
| --- | --- |
| Device | `JYPPX`, Windows 10 build `10.0.26200`, 16 logical processors |
| Runtime | .NET `10.0.12`, `JYPPX.OpenCV.runtime.win-x64` `5.0.0` |
| Source revision | `f0c2717d0433bd4a0d4a347faa3ce62c893fb8cb` |
| Model | `E:\Model\PaddleDocument\onnx\rt-detr-l-wired-cell-det.onnx` |
| Model SHA-256 | `c390d3c0252e7eeeca9bdaf67f77c9e37f2e25343e53edf2d7c0462f286c74d6` |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |

## Protocol and result

One prepared input and one OpenCV DNN session are reused for 5 warm-ups followed by 50 measured synchronous calls at 640×640. Timing includes OpenCV execution and the DeploySharp Paddle NMS Decoder, and excludes decode/preprocessing, model/session creation, hashing and the one-time ORT reference. Percentiles use nearest-rank.

| Backend | Batch / sessions | ORT parity | P50 (ms) | P95 (ms) | Mean (ms) | Min / max (ms) |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| OpenCV DNN CPU | 1 / 1 | 2 high-confidence regions; score ≤ `0.001`, coordinates ≤ `0.5 px` | 675.4472 | 730.0830 | 686.7020 | 646.4545 / 958.5652 |

The decoder returned 300 candidates and 2 candidates at score ≥ `0.5`. The high-confidence fingerprint and all samples are in [`paddle-document-opencv-rt-detr-l-wired-cell-det-benchmark-20261018.json`](paddle-document-opencv-rt-detr-l-wired-cell-det-benchmark-20261018.json). This does not establish table-cell recall, dataset accuracy or cross-device performance.
