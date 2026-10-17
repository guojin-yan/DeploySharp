# PP-DocLayout-L OpenCV DNN benchmark (2026-10-17)

This report records one exact `paddle-doc/pp-doclayout-l` ONNX artifact on the named Windows host. It is a prepared-input latency measurement and bounded ORT parity check, not a layout accuracy result or a ranking of OpenCV against other backends.

| Item | Value |
| --- | --- |
| Device | `JYPPX`, Windows 10 build `10.0.26200`, 16 logical processors |
| Runtime | .NET `10.0.12`, `JYPPX.OpenCV.runtime.win-x64` `5.0.0` |
| Source revision | `2f9ded27a1c32c5b60cd095b2bb1018a3c486ec1` |
| Model | `E:\Model\PaddleDocument\onnx\pp-doclayout-l.onnx` |
| Model SHA-256 | `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9` |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |

## Protocol and result

The test creates one `PreparedVisualInput` before timing and reuses it for one OpenCV DNN session. It performs 5 warm-ups followed by 50 measured synchronous `VisualPipeline.Run` calls. Timing includes OpenCV DNN execution and DeploySharp Paddle NMS decoding; image decode/preprocessing, model load, session creation, hashing and the one-time ORT parity reference are excluded. Percentiles use nearest-rank.

| Backend | Batch / sessions | High-confidence parity | P50 (ms) | P95 (ms) | Mean (ms) | Min / max (ms) |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| OpenCV DNN CPU | 1 / 1 | 1 region; score ≤ `0.001`, coordinates ≤ `0.5 px` vs ORT CPU | 730.3262 | 796.5752 | 735.6268 | 694.7635 / 817.9322 |

The decoded output contains 300 candidates, of which one has score ≥ `0.5`. The high-confidence result fingerprint is `f0ce64ece1c94946089484af63200f6041ace642fc5cf87fcf509cbcf7b075c3`.

OpenCV logs an importer diagnostic for an unused graph output named `fetch_name_1`; the contract intentionally selects `fetch_name_0`. The selected output executes successfully and the bounded ORT parity contract passes. This does not claim that every output variant or OpenCV importer version is supported.

The machine-readable samples, environment and protocol are in [`paddle-document-opencv-pp-doclayout-l-benchmark-20261017.json`](paddle-document-opencv-pp-doclayout-l-benchmark-20261017.json). GPU clocks are not applicable to this CPU backend; no lock-frequency claim is made.

