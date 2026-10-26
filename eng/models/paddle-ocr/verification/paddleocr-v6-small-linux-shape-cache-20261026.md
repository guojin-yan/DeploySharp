# PP-OCRv6 Small Linux CPU full-pipeline and OpenCV shape-cache verification

Date: 2026-10-26. Device: `ygj-NUC11PHi7`, Ubuntu 22.04 / Linux 6.8 x64, .NET 10.0.9, eight logical processors. CPU clocks were not locked; this is not a controlled cross-backend ranking. The input is `demo_1.jpg` (SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`). PP-OCRv6 Small DET/REC ONNX SHA-256 values are `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e` and `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.

Each row is the complete DET -> crop -> REC -> merge pipeline, not isolated model inference. Protocol: five warm-ups and 50 measured calls, batch 1, two independent stage Sessions, prepared detector input and decoded source reused, dynamic recognition width capped at 3200, 16 detected regions. Model/Session load and original image decode are excluded from the warm-call total; `preprocess_ms=0` reflects that reuse. The text SHA-256 is `fc7a2479fe0bcdf5ffb655b8d605e0c0f74e67120595974d5722fb3d30f703d9` and the semantic-result SHA-256 is `3182faaec7b51fe20660c58c4a8c6a8507a614328e43273a912c018b5b5d8ab1` in every row.

| Backend/configuration | P50 ms | P95 ms | Evidence |
| --- | ---: | ---: | --- |
| ONNX Runtime CPU | 309.153 | 345.692 | [CSV](paddleocr-v6-small-linux-cpu-max3200-20261026.csv) |
| OpenVINO CPU | 239.854 | 284.206 | [CSV](paddleocr-v6-small-linux-cpu-max3200-20261026.csv) |
| OpenCV DNN CPU, default cache 1 | 1439.253 | 1510.367 | [CSV](paddleocr-v6-small-linux-cpu-max3200-20261026.csv) |
| OpenCV DNN CPU, shape cache 16, run 1 | 426.472 | 485.785 | [CSV](paddleocr-v6-small-linux-opencv-cache16-20261026.csv) |
| OpenCV DNN CPU, shape cache 16, run 2 | 479.831 | 546.506 | [CSV](paddleocr-v6-small-linux-opencv-cache16-rss-20261026.csv) |
| OpenCV DNN CPU, shape cache 8 | 1406.747 | 1640.434 | [CSV](paddleocr-v6-small-linux-opencv-cache8-20261026.csv) |

Each CSV has an adjacent `.environment.json` with source marker, runtime assembly identity, image/model hashes and full protocol. All six pairs pass `Test-PaddleOcrBenchmarkReport.ps1 -ReportPath <csv> -RequireSourceRevision` with status `formal-ready`. The initial OpenVINO attempt failed to locate its native runtime because the launch environment omitted the runtime directory from `LD_LIBRARY_PATH`; the corrected run above passed. That launch failure is not a model incompatibility.

OpenCV DNN specializes a network for each dynamic input shape. A bounded per-Session LRU cache now retains 1-32 such networks; default 1 preserves prior behavior and memory usage. The benchmark accepts `DEPLOYSHARP_PADDLEOCR_OPENCV_SHAPE_CACHE=16` as an explicit opt-in. Only the recognition Session uses the cache. Cache 16 reduced repeated graph loads on this particular 16-region input; cache 8 did not cover enough of its working set. Retained native networks cost memory, and the runs were sequential on an unlocked CPU. These data do not justify making 16 the default or claiming a universal speedup. The associated source marker is `6a06930+working-tree-opencv-shape-cache`; use the eventual commit containing the cache implementation for subsequent reproductions.
