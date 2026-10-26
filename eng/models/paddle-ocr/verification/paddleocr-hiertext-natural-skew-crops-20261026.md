# PP-OCRv6 Small natural-skew crop comparison (2026-10-26)

## Scope and protocol

This is a recognizer-only paired comparison on the same decoded HierText validation images. The preselected `sample-002` and `sample-003` manifests contain 243 eligible horizontal text lines whose annotated quadrilateral top edge has an absolute angle of 5-20 degrees. Two quadrilaterals extend beyond the source image, leaving 241 lines on 18 images. Each line is recognized twice with the same PP-OCRv6 Small recognizer: first with its annotated perspective quadrilateral, then with that quadrilateral's axis-aligned bounding rectangle. Selection does not use either prediction. Detection and automatic orientation are not run.

The local dataset, source images and row-level predictions remain outside Git under `F:\OCRBenchmarkTesting` and `E:\DeploySharpEvidence\paddleocr-hiertext-natural-skew-20261026`. The dataset revision is `70b6620b2b112597d8219e11eee9773a1403827c`; the two manifest SHA-256 values are `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7` and `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`. Source image hashes are checked before inference. The recognizer ONNX SHA-256 is `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`; dictionary SHA-256 is `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.

Both runs use one session, at most 16 regions per recognition call, the catalog crop profile (height 48, maximum width 3200), and case-sensitive character/word edit distance. `PolygonBatchMilliseconds` and `BoxBatchMilliseconds` in the raw row records are the elapsed time for the **entire 1-16-region call**, repeated on its member rows for provenance; they must not be summed or interpreted as per-line latency. The runs have no warm-up/5+50 protocol and are not formal speed results. The test assembly SHA-256 is `2ef304b804d9682937538811df66735e993ac9a71925476e01857db5fabbeaae`; the source marker is `172e605-dirty-natural-skew` because this test had not yet been committed when the raw reports were produced.

## Results

| Slice | Lines | Reference chars | Polygon edits / CER | Box edits / CER | Reference words | Polygon edits / WER | Box edits / WER |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| sample-002 | 56 | 269 | 91 / 33.8290% | 87 / 32.3420% | 64 | 39 / 60.9375% | 42 / 65.6250% |
| sample-003 | 185 | 996 | 157 / 15.7631% | 232 / 23.2932% | 248 | 98 / 39.5161% | 108 / 43.5484% |
| Combined | 241 | 1265 | 248 / 19.6047% | 319 / 25.2174% | 312 | 137 / 43.9103% | 150 / 48.0769% |

For character edits, polygon crops are better on 38 lines, bounding boxes on 29, and tied on 174. The aggregate improvement is not uniform: polygon crops are slightly worse on `sample-002` by character edits and substantially better on `sample-003`. ONNX Runtime CPU and OpenVINO CPU produce the same prediction text and edit counts for all 241 paired lines; this does not claim identical floating-point confidences or timings.

Independent parsing of both raw JSON reports recomputed all totals and the two sample slices, and compared row identity, reference, two predictions and edit counts across backends. The raw report SHA-256 values are:

| Backend | Raw JSON SHA-256 |
| --- | --- |
| ONNX Runtime CPU | `f5165cc772ac5b713bff7d2ad41d1acd69ff4d036a82fcb2df6f237cf4f94dde` |
| OpenVINO CPU | `47b4eda4059f7ffa68f02d05075881107bd79e8c930646847c270365519d4457` |

## Reproduction and boundary

Set `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN=1`, `DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT` to the dataset root and `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_OUTPUT` to a new, empty evidence directory. Run the `PaddleOcrHierTextNaturalSkewIntegrationTests` filter in `tests/DeploySharp.Visual.OpenCV.Tests` for `net10.0`; both CPU backends are data rows. The test deliberately refuses to overwrite existing evidence. The selection-only and external runs passed `1/1` and `2/2`, respectively.

The angle comes from the annotated top edge, **not** an independently labelled upright/inverted orientation. Supplying a known polygon demonstrates the effect of rectification; it does not measure DET recall, automatic angle prediction, 0/180-degree repair, whole-page CER/WER, or formal backend speed. The A3 natural-orientation acceptance gate remains open. The [machine summary](paddleocr-hiertext-natural-skew-crops-20261026.json) contains exact counts and evidence hashes.
