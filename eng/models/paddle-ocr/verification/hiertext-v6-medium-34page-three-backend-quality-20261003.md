# PP-OCR v6 Medium HierText 34-page three-backend quality summary (2026-10-03)

This report combines the same two disjoint HierText selections used by the ORT/OpenVINO 34-page summary: 24 pages from `sample-003` and 10 pages from `sample-002`. The source images, annotations, raw predictions and per-page reports remain outside Git at `F:\OCRBenchmarkTesting`; this is smoke evidence, not a leaderboard or release-accuracy claim.

## Protocol

- Model: PP-OCRv6 Medium, full `det -> crop -> rec -> merge`, SlidingWindow overlap `0.2`, maximum `32` windows per region, maximum `1,024` regions, recognition maximum width `320`.
- ORT/OpenVINO use recognition batch `16`; OpenCV DNN uses batch `1` because the current importer contract is static. All runs use one warm-up, one measured iteration per page and one inference channel.
- Host: Windows 10 `10.0.26200`, x64, 16 logical processors (`JYPPX`), CPU providers only.
- Detector SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source and selected-manifest SHA values are retained in the [machine-readable summary](hiertext-v6-medium-34page-three-backend-quality-20261003.json).

## Aggregate quality and latency

| Metric | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU |
| --- | ---: | ---: | ---: |
| Completed pages / failures / empty outputs | 34 / 0 / 0 | 34 / 0 / 0 | 34 / 0 / 0 |
| Detection TP / FP / FN at IoU 0.5 | 646 / 722 / 911 | 646 / 722 / 911 | 646 / 722 / 911 |
| Detection precision / recall / F1 | 47.22% / 41.49% / 44.17% | 47.22% / 41.49% / 44.17% | 47.22% / 41.49% / 44.17% |
| Matched-region CER / WER | 13.04% / 35.63% | 13.04% / 35.63% | 12.85% / 33.91% |
| Matched-region exact text | 319 / 646 (49.38%) | 319 / 646 (49.38%) | 331 / 646 (51.24%) |
| End-to-end CER / WER | 80.56% / 105.48% | 80.56% / 105.48% | 80.08% / 104.34% |
| Total latency P50 / P95 | 1,687.40 / 3,572.89 ms | 1,131.57 / 2,255.66 ms | 9,646.35 / 24,180.50 ms |
| Detection latency P50 / P95 | 599.20 / 882.76 ms | 389.24 / 623.40 ms | 1,654.11 / 2,438.44 ms |
| Recognition latency P50 / P95 | 967.85 / 2,850.17 ms | 647.83 / 1,903.57 ms | 7,532.73 / 21,686.31 ms |

Detection geometry and quality counters are identical across the three backends for these two selections. ORT/OpenVINO ordered page text and region counts match `34/34`, with polygon drift `0` and confidence drift at most `2.57e-5`. OpenCV DNN has the same region counts on all `34/34` pages, but its ordered page text matches ORT on only `11/34` pages; 178 of 1,368 corresponding region texts differ. The current comparison has maximum polygon drift `0.02765 px` and confidence drift `0.59039062`. These are observed backend differences and should be considered when exact text parity matters.

The combined selection contains 42 ground-truth lines with at least 32 characters and 4 with at least 128 characters. It contains no natural line at or above 3,200 characters; A2 remains open. The one-iteration timings are observations and do not replace the formal 5-warm-up/50-iteration performance protocol.

## Reproduction

Run the ORT/OpenVINO commands from [the 34-page parity record](hiertext-v6-medium-34page-ort-openvino-quality-20261002.md), then run the same two manifests with `-Backend opencv-dnn`, distinct output directories, and `-BatchSize 1`. Score each output with `Evaluate-DeploySharpPublicOcrDataset.py`; do not copy source images or raw predictions into the repository.
