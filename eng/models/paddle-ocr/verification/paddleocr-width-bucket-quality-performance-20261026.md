# PP-OCRv6 Small optional width buckets: quality and OpenCV DNN latency (2026-10-26)

## Contract and quality

`TextCropProfile.WithWidthBuckets(...)` is opt-in. It rounds a dynamic, aspect-preserving recognition crop up to the next tensor width and pads the remainder. Above `MaximumWidth`, the existing Clamp/Reject/SlidingWindow policy still applies. Fixed width and wider-retry candidates do not inherit the bucket list. The default PaddleOCR profile remains exact dynamic width, height 48 and maximum width 3200.

The labelled comparison reuses the [pinned natural-skew HierText selection](paddleocr-hiertext-natural-skew-crops-20261026.md): 241 validation lines from 18 images, 1,265 reference characters and 312 whitespace-delimited words. Selection uses annotation geometry and text, not model output. `RecognizeOnlyAsync` runs PP-OCRv6 Small with Batch=1/one Session on each true quadrilateral and its axis-aligned bounding box; DET and automatic orientation are omitted. The new bucket list is `48,64,96,128,192,256,320,448,640,960,1280,1920,2560,3200`. Seven external test rows passed on Windows 10.0.26200 / .NET 10.0.12: dynamic ORT/OpenVINO/OpenCV, Fixed320 ORT/OpenCV, and bucketed ORT/OpenCV. Test assembly SHA-256: `fe11574497b5a1f815050c70d9ac18ed7be0da22085064a16cc6f8cab9a1b406`.

| Crop and width | Character edits / 1265 | CER | Word edits / 312 | WER |
| --- | ---: | ---: | ---: | ---: |
| Polygon, dynamic | 248 | 19.60% | 137 | 43.91% |
| Polygon, bucketed | 241 | 19.05% | 139 | 44.55% |
| Polygon, Fixed320 | 223 | 17.63% | 131 | 41.99% |
| Box, dynamic | 319 | 25.22% | 150 | 48.08% |
| Box, bucketed | 275 | 21.74% | 145 | 46.47% |
| Box, Fixed320 | 274 | 21.66% | 143 | 45.83% |

Bucketed ORT and OpenCV agree on every polygon and box prediction (`241/241` each). Against dynamic width, polygon character edits improve on 16 lines, worsen on 10 and tie on 215; box edits improve on 30, worsen on 11 and tie on 200. The worst polygon line loses one extra character edit. Aggregate CER improvement is not a WER improvement or a per-line accuracy guarantee. Source manifests and model/dictionary hashes are in the linked selection report. The raw bucketed ORT/OpenCV JSON files remain outside Git at `E:\DeploySharpEvidence\paddleocr-hiertext-width-buckets-20261026`; their SHA-256 values are `e033b2d37a11d940a6a3e27d6be7ecd4d71f6ee72fec29b2f900fdd682655b47` and `f679da48a1692bb1a2ed6a54c2c4e0216f44168c0dab09ece6dd9297283b2dff`. Reproduce with `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN=1`, the pinned HierText root and the `NaturalSkewLinesComparePolygonWithAxisAlignedBounds` filter in `DeploySharp.Visual.OpenCV.Tests`; use a fresh output directory.

## Full-pipeline latency

The same Windows host (AMD Ryzen 7 5800H, 16 logical processors) ran the complete PP-OCRv6 Small OpenCV DNN CPU pipeline on `E:\Data\ocr\demo_1.jpg` (SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`). Both rows used Clamp, maximum width 320, static REC Batch=1, two independently created stage Sessions, prepared-input reuse, 5 warm-ups and 50 measured calls; model, input, stage settings and thread defaults were identical. The source marker was `cc45a2b-dirty-width-buckets`; CSV/environment files retain exact model and assembly hashes. Both reports passed `Test-PaddleOcrBenchmarkReport.ps1` as `formal-ready`, with `1/1` pass rows and one stable result variant each.

| Width strategy | Submitted width shapes | P50 | P95 | Managed pipeline allocation/call | Ordered text SHA |
| --- | --- | ---: | ---: | ---: | --- |
| Dynamic | 188, 229, 253, 282, 320 | 1061.834 ms | 1156.418 ms | 2,125,355,814 bytes | `538ee815...c41675` |
| Buckets `48,64,96,128,192,256,320` | 192, 256, 320 | 974.565 ms | 1244.388 ms | 1,641,184,046 bytes | `538ee815...c41675` |

The bucketed row lowers P50 by 8.22% and reported managed allocation by 22.78%, but raises P95 by 7.61%; strict and semantic contract hashes differ because tensor widths differ, while ordered text hashes match. This is one sequential host/image comparison, not a stable speedup or an all-model recommendation. The test image has five dynamic widths, so only three distinct shapes remain after bucketing. The labelled quality comparison above is recognition-only on different images; neither result can replace whole-page quality validation or cross-device timing.

Retained reports: [dynamic CSV](paddleocr-v6-small-opencv-width-buckets-formal-20261026-dynamic.csv), [dynamic environment](paddleocr-v6-small-opencv-width-buckets-formal-20261026-dynamic.csv.environment.json), [dynamic validation](paddleocr-v6-small-opencv-width-buckets-formal-20261026-dynamic.validation.json), [bucketed CSV](paddleocr-v6-small-opencv-width-buckets-formal-20261026-bucketed.csv), [bucketed environment](paddleocr-v6-small-opencv-width-buckets-formal-20261026-bucketed.csv.environment.json), [bucketed validation](paddleocr-v6-small-opencv-width-buckets-formal-20261026-bucketed.validation.json). Width JSON sidecars are retained outside Git under `E:\DeploySharpEvidence\paddleocr-width-buckets-formal-20261026` with SHA-256 `04b30234bb124819551ac734be4953fb935623c6cabd9c39f798e60bdfdce6a1` (dynamic) and `484e997623ea793f44d1951229e668c84e73f31c4e7129819f5b1070d5f6850c` (bucketed).
