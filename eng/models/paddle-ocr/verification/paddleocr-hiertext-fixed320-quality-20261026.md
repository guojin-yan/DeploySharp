# PP-OCRv6 Small Fixed320 natural-skew quality check (2026-10-26)

## Protocol

This follow-up uses the same 241 preselected HierText validation lines on 18 images as the [natural-skew crop comparison](paddleocr-hiertext-natural-skew-crops-20261026.md). The source image, annotation, PP-OCRv6 Small recognizer, dictionary, perspective-polygon versus axis-aligned-box crop geometry, model Batch=1 and session count=1 are unchanged. Only the recognition crop profile changes from dynamic width (height 48, maximum width 3200) to `WithFixedWidth(320)`. Detection and automatic orientation are not executed.

The selection is 243 eligible lines from `sample-002` and `sample-003`, minus two out-of-bounds quadrilaterals. Dataset revision: `70b6620b2b112597d8219e11eee9773a1403827c`; manifest SHA-256: `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7` and `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`. Recognizer ONNX SHA-256: `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`; dictionary SHA-256: `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.

The five external test rows passed `5/5` on Windows 10.0.26200 / .NET 10.0.12: dynamic ORT CPU, OpenVINO CPU and OpenCV DNN CPU, plus Fixed320 ORT CPU and OpenCV DNN CPU. The source marker is `ba4a4b1-dirty-fixed320`; test assembly SHA-256 is `5cd94d10a809c5aade5413c5dbe76d868738ef3562cf7cc3fd5a8736ca7c668b`. The raw per-line predictions remain outside Git at `E:\DeploySharpEvidence\paddleocr-hiertext-natural-skew-fixed320-20261026`. Every new dynamic-width prediction and edit count matches the previous Batch=1 report for all 241 rows on each of the three backends.

## Results

| Width and crop | Lines | Character edits / 1265 | CER | Word edits / 312 | WER |
| --- | ---: | ---: | ---: | ---: | ---: |
| Dynamic, perspective polygon | 241 | 248 | 19.60% | 137 | 43.91% |
| Fixed320, perspective polygon | 241 | 223 | 17.63% | 131 | 41.99% |
| Dynamic, axis-aligned box | 241 | 319 | 25.22% | 150 | 48.08% |
| Fixed320, axis-aligned box | 241 | 274 | 21.66% | 143 | 45.83% |

Fixed320 ORT CPU and OpenCV DNN CPU agree on all 241 polygon and box predictions and their edit counts. Independent row-sum and sample-slice recomputation gives:

| Sample | Lines | Dynamic polygon / box character edits | Fixed320 polygon / box character edits | Dynamic polygon / box word edits | Fixed320 polygon / box word edits |
| --- | ---: | ---: | ---: | ---: | ---: |
| sample-002 | 56 | 91 / 87 | 80 / 81 | 39 / 42 | 40 / 38 |
| sample-003 | 185 | 157 / 232 | 143 / 193 | 98 / 108 | 91 / 105 |

Relative to dynamic width, Fixed320 improves polygon character edits on 28 lines, worsens 13, and ties 200; axis-aligned box edits improve on 40, worsen 25, and tie 176. Polygon prediction text changes on 72 lines and box text on 102. The aggregate gain is therefore not a per-line guarantee: `sample-002` polygon word edits rise from 39 to 40, and individual character-edit regressions reach three edits. Under Fixed320, polygon crops have fewer character edits on 38 lines, boxes on 24 and equal edits on 179.

| Backend and width | Raw JSON SHA-256 |
| --- | --- |
| ORT CPU, dynamic | `b914114be4b8da8565edae1120f3e318be1847c3f66a8d076c4e6f7510c4fe5f` |
| OpenVINO CPU, dynamic | `adf4940c020548e5eb44c6e919b327fadb316b585c44b9a381a35ce647375eda` |
| OpenCV DNN CPU, dynamic | `d1d0fcbcb7b527860384433da74efb0ba3bf3ff6d5041218d0beba98546f085b` |
| ORT CPU, Fixed320 | `47a7aa152f4f355df125feea178ee51f6b3068303a4040e2ca77e9655cc1d47f` |
| OpenCV DNN CPU, Fixed320 | `5210b34bec9f8b452a3d1988ddc426ea7fda0ffd79b3b9daeef42227ad82e93e` |

## Interpretation and reproduction

This is a bounded recognizer-only quality comparison on natural skew geometry, not a trained or held-out choice of the best global width. A separate [ten-page SROIE Fixed320 check](sroie-v6-small-opencv-fixed320-quality-20261013.md) found a quality regression, so dynamic width remains the default and Fixed320 is opt-in after application-specific labelled validation. OpenCV DNN avoids repeated dynamic-shape graph specialization when width is fixed, but this run has no warm-up or `5+50` timing protocol; its outer call wall-clock fields are not formal latency or per-line measurements. This result does not establish detection recall, independent upright/inverted orientation accuracy, whole-page CER/WER or the A3 orientation gate.

For reproduction, set `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN=1`, point `DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT` at the pinned HierText dataset and `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_OUTPUT` at a new evidence directory, then run the `NaturalSkewLinesComparePolygonWithAxisAlignedBounds` filter in `DeploySharp.Visual.OpenCV.Tests` on Release/net10.0. The test refuses to overwrite any existing report. The [machine-readable summary](paddleocr-hiertext-fixed320-quality-20261026.json) records the exact boundaries and hashes.
