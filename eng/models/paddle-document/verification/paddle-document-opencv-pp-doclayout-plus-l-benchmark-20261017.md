# PP-DocLayout-Plus-L OpenCV DNN benchmark (2026-10-17)

This report records the exact `paddle-doc/pp-doclayout-plus-l` ONNX artifact on the named Windows host. It is a prepared-input latency measurement and bounded ORT parity check, not a layout accuracy result or a ranking of OpenCV against other backends.

| Item | Value |
| --- | --- |
| Device | `JYPPX`, Windows 10 build `10.0.26200`, 16 logical processors |
| Runtime | .NET `10.0.12`, `JYPPX.OpenCV.runtime.win-x64` `5.0.0` |
| Source revision | `c0983459b772b932acaef690f27e6bc852fa499b` |
| Model | `E:\Model\PaddleDocument\onnx\pp-doclayout-plus-l.onnx` |
| Model SHA-256 | `d67689f6325ec0d4cc812308dbd1c84bc801e76731c0f183297013d6b59e0048` |
| Input | `E:\Data\image\bus.jpg` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |

## Protocol and result

The test creates one `PreparedVisualInput` before timing and reuses it for one OpenCV DNN session. It performs 5 warm-ups followed by 50 measured synchronous `VisualPipeline.Run` calls. Timing includes OpenCV DNN execution and DeploySharp Paddle NMS decoding; image decode/preprocessing, model load, session creation, hashing and the one-time ORT parity reference are excluded. Percentiles use nearest-rank. The official Plus-L canvas is 800×800.

| Backend | Batch / sessions | High-confidence parity | P50 (ms) | P95 (ms) | Mean (ms) | Min / max (ms) |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| OpenCV DNN CPU | 1 / 1 | 1 region; score ≤ `0.001`, coordinates ≤ `0.5 px` vs ORT CPU | 1043.8911 | 1167.5941 | 1066.9535 | 978.8148 / 1319.9421 |

The decoded output contains 300 candidates, of which one has score ≥ `0.5`; its high-confidence fingerprint is `75899bcd81bfd527526caca919cacd2c5957d242494e5a4206235f96011dea8c`. All 50 raw samples are retained in the machine-readable report [`paddle-document-opencv-pp-doclayout-plus-l-benchmark-20261017.json`](paddle-document-opencv-pp-doclayout-plus-l-benchmark-20261017.json).

The result is limited to this exact export, OpenCV runtime, 800×800 profile and input. It does not establish dataset accuracy, cross-device performance, GPU performance or support for other PP-DocLayout-Plus-L exports.
