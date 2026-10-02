# PP-OCR v6 Medium HierText 34-page ORT/OpenVINO quality summary (2026-10-02)

This summary aggregates the two fixed HierText selections already executed under one protocol: 24 pages from `sample-003` and 10 pages from `sample-002`. The selections contain 34 unique pages and 1,557 labeled regions. Images, annotations, raw predictions and per-page reports stay outside Git at `F:\OCRBenchmarkTesting`; this is smoke evidence, not a leaderboard or release-accuracy claim.

## Protocol and provenance

- Model: PP-OCRv6 Medium, `det -> crop -> rec -> merge`, SlidingWindow overlap `0.2`, maximum `32` windows per region, maximum `1,024` regions, recognition maximum width `320`.
- One warm-up and one measured iteration per page; recognition batch `16`; one inference channel. The timings are page-level observations and do not replace the formal 5-warm-up/50-iteration performance protocol.
- Host: Windows 10 `10.0.26200`, x64, 16 logical processors (`JYPPX`), CPU providers only.
- Detector SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source manifest SHA-256 values and selected-manifest SHA values are retained in the [machine-readable summary](hiertext-v6-medium-34page-ort-openvino-quality-20261002.json).

## Aggregate results

| Metric | ONNX Runtime CPU | OpenVINO CPU |
| --- | ---: | ---: |
| Completed pages / failures / empty outputs | 34 / 0 / 0 | 34 / 0 / 0 |
| Detection TP / FP / FN at IoU 0.5 | 646 / 722 / 911 | 646 / 722 / 911 |
| Detection precision / recall / F1 | 47.22% / 41.49% / 44.17% | 47.22% / 41.49% / 44.17% |
| Matched-region CER / WER | 13.04% / 35.63% | 13.04% / 35.63% |
| Matched-region exact text | 319 / 646 (49.38%) | 319 / 646 (49.38%) |
| End-to-end CER / WER | 80.56% / 105.48% | 80.56% / 105.48% |
| Total latency P50 / P95 | 1,687.40 / 3,572.89 ms | 1,131.57 / 2,255.66 ms |
| Detection latency P50 / P95 | 599.20 / 882.76 ms | 389.24 / 623.40 ms |
| Recognition latency P50 / P95 | 967.85 / 2,850.17 ms | 647.83 / 1,903.57 ms |

The two backends have identical aggregate quality counters. Across the two source runs, ordered page text sequences and region counts match `34/34`; polygon coordinates have maximum absolute difference `0`, and confidence drift is at most `2.57e-5`. This is parity for the exact model, host, source selections and smoke protocol, not a claim that all devices or providers will have the same numerical behavior.

The combined selection contains 42 ground-truth lines with at least 32 characters and 4 with at least 128 characters. It contains no natural line at or above 3,200 characters; A2 remains open. Geometry coverage and source-level details remain in each per-selection report and are diagnostic rather than a replacement for IoU.

## Reproduction

Run the commands in [the sample-003 v6 Medium record](hiertext-v6-medium-public-ocr-20261002.md) and [the sample-002 parity record](hiertext-v6-medium-sample-002-ort-openvino-quality-parity-20261002.md) with the two source manifests and distinct output directories. Aggregate only the resulting `evaluation-corrected.json` and page CSV files; do not copy the source images or raw predictions into the repository.
