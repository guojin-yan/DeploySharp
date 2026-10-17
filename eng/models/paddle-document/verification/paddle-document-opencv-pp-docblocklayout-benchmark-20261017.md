# PP-DocBlockLayout OpenCV DNN benchmark (2026-10-17)

This report records the exact `paddle-doc/pp-docblocklayout` ONNX artifact on the named Windows host. It is a prepared-input latency measurement and bounded ORT parity check, not a layout accuracy result or a ranking of OpenCV against other backends.

| Item | Value |
| --- | --- |
| Device | `JYPPX`, Windows 10 build `10.0.26200`, 16 logical processors |
| Runtime | .NET `10.0.12`, `JYPPX.OpenCV.runtime.win-x64` `5.0.0` |
| Source revision | `8795baa1efa512b387f464b9d9aa57700fc452b4` |
| Model | `E:\Model\PaddleDocument\onnx\pp-docblocklayout.onnx` |
| Model SHA-256 | `abbf5febf79a35c9f329b4f591d440c7a4b3ae90cbb042bc8835900b49582218` |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |

## Protocol and result

The test creates one `PreparedVisualInput` before timing and reuses it for one OpenCV DNN session. It performs 5 warm-ups followed by 50 measured synchronous `VisualPipeline.Run` calls. Timing includes OpenCV DNN execution and DeploySharp Paddle NMS decoding; image decode/preprocessing, model load, session creation, hashing and the one-time ORT parity reference are excluded. Percentiles use nearest-rank.

| Backend | Batch / sessions | High-confidence parity | P50 (ms) | P95 (ms) | Mean (ms) | Min / max (ms) |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| OpenCV DNN CPU | 1 / 1 | 1 region; score ≤ `0.001`, coordinates ≤ `0.5 px` vs ORT CPU | 765.6829 | 1132.6003 | 805.6070 | 672.4254 / 1231.6583 |

The decoded output contains 300 candidates, of which one has score ≥ `0.5`; its high-confidence fingerprint is `0475bc22a7cfc634932ec8940607ff53475b2dd619f2333ac0c387484c8c9b60`. All 50 raw samples are retained in the machine-readable report [`paddle-document-opencv-pp-docblocklayout-benchmark-20261017.json`](paddle-document-opencv-pp-docblocklayout-benchmark-20261017.json).

The result is limited to this exact export, OpenCV runtime and input. It does not establish dataset accuracy, cross-device performance, GPU performance or support for other PP-DocBlockLayout exports.
