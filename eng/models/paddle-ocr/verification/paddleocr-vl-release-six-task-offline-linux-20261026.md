# PaddleOCR-VL 1.6 Release Bundle: six real tasks on Linux

Date: 2026-10-26. This is a fixed-input generation and Release-provenance check for ONNX Runtime CPU. It extends the earlier [single-OCR Release run](paddleocr-vl-release-offline-generation-linux-20261026.md).

The public `models-paddleocr` Release Bundle was downloaded into a fresh client cache and size/SHA-checked on Windows, then copied unchanged to the authorized Ubuntu 22.04.5 host because that host could not connect to GitHub. The copied cache's five graphs and nine manifest artifacts were rehashed. Each task used the production `--release <cache-root> <image> <task> --offline --raw-exif` entry, which obtains the verified Bundle through `PaddleOcrReleaseClient`, decodes with OpenCV and generates with `PaddleOcrVlOnnxSession` on ORT 1.28.0 CPU. No host-local export replaced a published graph. `--raw-exif` preserves encoded pixels to match the pinned official Pillow reference; the normal upright-page mode remains the sample default.

| Task | Official image | Tokens | Token/text reference | Generation (ms) | Peak RSS (KiB) |
| --- | --- | ---: | --- | ---: | ---: |
| OCR | `demo_1.jpg` | 162 | both match, EOS | 19,311.2 | 6,668,880 |
| Table | `table_recognition.jpg` | 71 | both match, EOS | 7,628.7 | 5,264,392 |
| Formula | `general_formula_rec_001.png` | 80 | both match, EOS | 8,537.7 | 6,569,792 |
| Chart | `chart_parsing_02.png` | 137 | both match, EOS | 20,865.2 | 6,118,308 |
| Spotting | `demo_1.jpg` | 286 | both match, EOS | 97,389.3 | 10,582,344 |
| Seal | `seal_text_det.png` | 29 | both match, EOS | 14,987.2 | 7,830,304 |

All six process exit codes were `0`. The Token ID CSV and UTF-8 text SHA-256 values match all six cases in the [fixed official-weight reference](paddleocr-vl-reference-summary-linux-20261023.json), not merely the token counts. The [machine report](paddleocr-vl-release-six-task-offline-linux-20261026.json) records each source image SHA, output hashes, process wall time, RSS, Bundle/assembly/native-library identities and protocol. Recheck all rows against the reference with:

```powershell
pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Confirm-PaddleOcrVlReleaseGeneration.ps1
```

The Linux host had an Intel Core i7-1165G7, about 15 GiB physical RAM and no swap. The `net8.0` sample used .NET 10.0.9 with explicit major roll-forward; each task started a fresh process with four ORT inference threads. Each row is **one correctness run without warm-up**. The listed generation time excludes startup, model load and Release-cache verification; process wall and peak RSS include them. CPU clocks were not locked. These observations are not P50/P95, throughput, cross-backend speed comparisons, memory ceilings or long-running stability evidence.

This closes the six fixed 1.6 tasks' *downloaded-Release-cache-to-real-image* generation gap for ORT CPU. It does not establish arbitrary input/EXIF/ICC parity, labelled OCR/table/formula/chart/seal/spotting accuracy, page-level layout quality, TensorRT/OpenVINO execution from this cache, other VL weights, general model/backend availability or formal performance.
