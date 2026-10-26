# PaddleOCR-VL FUNSD Spotting labelled-page diagnostic

Date: 2026-10-26. This is the first independent word-annotation check of the production PaddleOCR-VL-1.6 Spotting path. It is a **single preselected page**, not a FUNSD split score or a release-quality pass. The full 25,734-byte per-region report, including predicted text, quadrilaterals, linked label IDs and unmatched IDs, remains outside Git at `E:\DeploySharpEvidence\paddleocr-vl-funsd-spotting-20261026\onnxruntime-cpu-first-page.json` (SHA-256 `f88cee914acda1731f6e29ef62ddeef918d24ac067c79b8a9472c3cac58263da`). No FUNSD image or annotation is republished here.

## Fixed input and execution

- Selection: the first nonempty record of `F:\OCRBenchmarkTesting\data\annotations\manifests\funsd-test-full-001.jsonl`, **before seeing predictions**. Manifest SHA-256 `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`; page `funsd-test-82504862`, 754x1000, image SHA-256 `9a66fa4013bf93b9ba4e959feef7d6b2e278b714ca9d3d347b35aeb305b92626`, 63 non-ignored word annotations. The source revision is `c31735649e4f441bcbb4fd0f379574f7520b42286e80b01d80b445649d54761f`. This is an existing public test image; training overlap is unknown.
- Model: fixed `paddleocr-vl/1.6` checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`; four dynamic ONNX graph hashes and tokenizer hash are in the raw report. Production C# `PaddleOcrVlOnnxSession`, `OpenCvPaddleOcrVlInputFactory` and `PaddleOcrVlSpottingParser` ran on Windows 10.0.26200 / .NET 10.0.12 / x64 with ONNX Runtime CPU, four inference threads and raw encoded-pixel orientation. Source HEAD was `0f124f8`; the raw report pins Visual, OpenCV adapter and test assembly SHA-256 values, so the uncommitted test build is identifiable beyond HEAD.
- Generation used greedy decoding, a 2,048-token cap and a 15-minute timeout. The result reached EOS with 411 tokens and 26 valid Spotting regions. The observed generation call took `337,485.1555 ms`; model loading, image preparation and test setup are excluded. This single unlocked, non-repeated observation is **not** a P50/P95 or backend speed result.

## Predeclared annotation comparison

FUNSD labels individual words, while Spotting may return whole lines. To avoid falsely requiring one prediction per word, each labelled word centroid is associated with the smallest-area predicted quadrilateral containing it. Ties use output order; overlapping candidates, uncovered words and predictions without associated words are reported separately. Associated reference words follow the FUNSD `reading_order`. Both sides undergo NFKC, whitespace collapse and invariant uppercase before Unicode-scalar CER and whitespace-token WER. Uncovered labels contribute deletions; unmatched predictions contribute insertions. The raw report retains every association and edit count.

| Diagnostic | Result |
| --- | ---: |
| Labelled words / predicted regions | 63 / 26 |
| Covered / uncovered labelled words | 45 / 18 |
| Predictions without an assigned label | 5 |
| Labels inside multiple predictions | 0 |
| Associated character edits / reference characters | 263 / 406 (`64.78%`) |
| Associated word edits / reference words | 47 / 63 (`74.60%`) |

These rates are **centroid-associated diagnostics**, not official FUNSD detection IoU/F1 or general Spotting CER/WER. A polygon near a word but missing its centroid counts as a localization miss; a large line polygon may own several words. In this page, one generated `COURT:` region has no associated word even though the label text exists, and a `CASE NAME:` prediction associates only with `CASE`; this illustrates why transcript and localization failures must be inspected together. The full-page slice exposes a real quality gap despite earlier fixed-image Token parity with the official model. This new page has **not** yet been compared against an official full-weight Spotting run, so the gap must not be attributed solely to the upstream model or solely to this export.

## Reproduction and remaining gate

The executable test is `PaddleOcrVlFunsdSpottingQualityIntegrationTests.EvaluateFirstFunsdTestPageAgainstIndependentWordAnnotations`. It requires the pinned FUNSD manifest/images, matching four-graph Bundle and official tokenizer directory. Set `DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_RUN=1`, `DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT`, `DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT`, and optionally `DEPLOYSHARP_PADDLE_FUNSD_ROOT` and `DEPLOYSHARP_PADDLEOCR_VL_FUNSD_SPOTTING_REPORT_PATH`; run the named test in `DeploySharp.Visual.OpenCV.Tests`. A Windows restore is needed if this checkout's previous `obj` assets targeted Linux native packages. The two deterministic association tests passed `2/2`, and the external run passed `1/1`.

Next, compare this page's generated Token IDs/text with the pinned official weights, then expand the predeclared selection across pages and backends with bounded output handling. One page cannot close independent Spotting localization/text quality, reading order, multi-page, table/Chart/seal semantics, controlled performance, or V7 online Bundle delivery.
