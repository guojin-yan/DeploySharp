# PP-OCRv4 server seal OpenCV DNN benchmark (2026-10-18)

This is a formal 5-warm-up/50-measured benchmark for the exact `paddle-seal/ppocrv4-server` ONNX artifact and its dedicated `PaddleDocumentSealDecoder` profile.

| Field | Value |
| --- | --- |
| Host | `JYPPX`, Windows 10 build `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors |
| Input | `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c` |
| Model | `ppocrv4-server-seal-det.onnx`, SHA-256 `f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7` |
| Backend | OpenCV DNN CPU (`JYPPX.OpenCV.runtime.win-x64 5.0.0`), fusion + Winograd + dynamic-shape specialization enabled |
| Contract | Input `[1,3,224,224]`; output probability map `[1,1,224,224]`; dedicated seal decoder |
| Protocol | 5 warm-ups + 50 measured calls; batch 1; one Session; one `PreparedVisualInput` reused |
| Result | 224×224 mask; 1 decoded region on this sample |
| Timing | P50 **298.2772 ms**, P95 **329.3094 ms**, mean 299.3065 ms, min/max 272.5770/353.9986 ms |

An ORT CPU reference produced the same mask dimensions and 1 region. OpenCV and ORT probability-map values are not asserted to be numerically identical; the report establishes execution, output-shape and decoder-contract evidence only. It is not a seal recall/accuracy score or a cross-device ranking.

Machine-readable samples: [`paddle-document-opencv-ppocrv4-server-seal-det-benchmark-20261018.json`](paddle-document-opencv-ppocrv4-server-seal-det-benchmark-20261018.json).
