# PP-OCRv6 Small natural-skew crop comparison (2026-10-26)

## Scope and protocol

This is a recognizer-only paired comparison on the same decoded HierText validation images. The preselected `sample-002` and `sample-003` manifests contain 243 eligible horizontal text lines whose annotated quadrilateral top edge has an absolute angle of 5-20 degrees. Two quadrilaterals extend beyond the source image, leaving 241 lines on 18 images. Each line is recognized twice with the same PP-OCRv6 Small recognizer: first with its annotated perspective quadrilateral, then with that quadrilateral's axis-aligned bounding rectangle. Selection does not use either prediction. Detection and automatic orientation are not run.

The local dataset, source images and row-level predictions remain outside Git under `F:\OCRBenchmarkTesting` and `E:\DeploySharpEvidence\paddleocr-hiertext-natural-skew-20261026`. The dataset revision is `70b6620b2b112597d8219e11eee9773a1403827c`; the two manifest SHA-256 values are `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7` and `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`. Source image hashes are checked before inference. The recognizer ONNX SHA-256 is `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`; dictionary SHA-256 is `769e7fa79bb297b5f18d8dbd149e364a45bc61f2b3f574e5ea836f0b261c23a6`.

The initial ORT/OpenVINO runs used one session and a maximum model batch of 16. A follow-up uses the same selected lines, models, crop profile (height 48, maximum width 3200) and metrics, but sets model batch to 1 on ORT, OpenVINO and OpenCV DNN; this is the common dynamic-width contract accepted by OpenCV DNN. The detection session is ORT CPU in the follow-up but `RecognizeOnlyAsync` never executes it. Each outer `RecognizeOnlyAsync` call still receives 1-16 regions; the follow-up runs their model inference with at most one item per internal batch. `PolygonBatchMilliseconds` and `BoxBatchMilliseconds` measure the **entire outer recognition call** and are repeated on its member rows for provenance; they must not be summed or interpreted as per-line latency. Neither run has a warm-up/5+50 protocol or is a formal speed result.

The initial test assembly SHA-256 is `2ef304b804d9682937538811df66735e993ac9a71925476e01857db5fabbeaae` with source marker `172e605-dirty-natural-skew`. The follow-up assembly SHA-256 is `6fef36523948e36fff3a4c5a93ab1068468e41383bd947197683fabe7ed146ac` with source marker `0db0b13-dirty-batch1-opencv`; both raw report sets were produced before their respective test changes were committed.

The follow-up row-level reports are stored separately under `E:\DeploySharpEvidence\paddleocr-hiertext-natural-skew-batch1-20261026`; neither set of raw predictions is committed.

## Results

| Slice | Lines | Reference chars | Polygon edits / CER | Box edits / CER | Reference words | Polygon edits / WER | Box edits / WER |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| sample-002 | 56 | 269 | 91 / 33.8290% | 87 / 32.3420% | 64 | 39 / 60.9375% | 42 / 65.6250% |
| sample-003 | 185 | 996 | 157 / 15.7631% | 232 / 23.2932% | 248 | 98 / 39.5161% | 108 / 43.5484% |
| Combined | 241 | 1265 | 248 / 19.6047% | 319 / 25.2174% | 312 | 137 / 43.9103% | 150 / 48.0769% |

For character edits, polygon crops are better on 38 lines, bounding boxes on 29, and tied on 174. The aggregate improvement is not uniform: polygon crops are slightly worse on `sample-002` by character edits and substantially better on `sample-003`. ONNX Runtime, OpenVINO and OpenCV DNN CPU produce the same prediction text and edit counts for all 241 paired lines under Batch=1; this does not claim identical floating-point confidences or timings.

Independent parsing recomputed all totals and sample slices. The three Batch=1 reports agree on all 241 row identities, references, polygon and box predictions, and character/word edit counts. The new ORT outputs also match the earlier Batch=16 ORT outputs on both prediction texts for all 241 rows. This is not a claim of bitwise-identical floating-point confidence or timing. Raw report SHA-256 values are:

| Protocol / backend | Raw JSON SHA-256 |
| --- | --- |
| Batch=16 / ONNX Runtime CPU | `f5165cc772ac5b713bff7d2ad41d1acd69ff4d036a82fcb2df6f237cf4f94dde` |
| Batch=16 / OpenVINO CPU | `47b4eda4059f7ffa68f02d05075881107bd79e8c930646847c270365519d4457` |
| Batch=1 / ONNX Runtime CPU | `d6dd89a57062ec0814bab4421d016f3b4161ee5a35d63c9db951b3517a73010b` |
| Batch=1 / OpenVINO CPU | `e1f4cdc8cb60c7bf62bb5c3cd2f4936012b1ae7bf6afbe2d5bf35cb08c36f244` |
| Batch=1 / OpenCV DNN CPU | `461789b3162f51371c82dd0b1575b412358e684b5f55a10903661309f94ebff3` |

## Reproduction and boundary

Set `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_RUN=1`, `DEPLOYSHARP_PADDLEOCR_HIERTEXT_ROOT` to the dataset root and `DEPLOYSHARP_PADDLEOCR_HIERTEXT_SKEW_OUTPUT` to a new, empty evidence directory. Run the `PaddleOcrHierTextNaturalSkewIntegrationTests` filter in `tests/DeploySharp.Visual.OpenCV.Tests` for `net10.0`; ORT, OpenVINO and OpenCV DNN are data rows. The test refuses to overwrite existing evidence. The selection-only and current external runs passed `1/1` and `3/3`, respectively; the earlier two-backend run passed `2/2`.

The angle comes from the annotated top edge, **not** an independently labelled upright/inverted orientation. Supplying a known polygon demonstrates the effect of rectification; it does not measure DET recall, automatic angle prediction, 0/180-degree repair, whole-page CER/WER, or formal backend speed. The A3 natural-orientation acceptance gate remains open. The [machine summary](paddleocr-hiertext-natural-skew-crops-20261026.json) contains exact counts and evidence hashes.
