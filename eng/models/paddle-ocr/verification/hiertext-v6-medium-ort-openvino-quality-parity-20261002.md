# PP-OCR v6 Medium HierText ORT/OpenVINO quality parity (2026-10-02)

This record compares the same 24-page HierText `sample-003` smoke selection outside the repository at `F:\OCRBenchmarkTesting` with PP-OCRv6 Medium. ONNX Runtime CPU and OpenVINO CPU use the same DeploySharp pipeline, model files, SlidingWindow settings and selected manifest. The data remains smoke-only under the source audit; it is not a release-accuracy or leaderboard result.

## Protocol and provenance

- Pipeline: `det -> crop -> rec -> merge`, SlidingWindow overlap `0.2`, maximum 32 windows per region, maximum 1,024 regions, recognition maximum width `320`.
- One warm-up and one measured iteration per page; recognition batch `16`; one inference channel.
- Source manifest SHA-256: `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- Selected manifest SHA-256: `2c3933fbd5f29c2d4054357518feae96eb1bedd4b009333e105bfeb267159316`.
- Detector SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source revision: `7a8183ccc0159039f4ee243ede87010900f5aa37`; benchmark assembly SHA-256: `dcd8bd822201b454713ec7534e717c7275378596cbf2cf09e3e64d001eb2dcf3`.
- Host: Windows 10 (`10.0.26200`), x64, 16 logical processors; both runs used CPU providers.

## Cross-backend result

| Metric | ONNX Runtime CPU | OpenVINO CPU |
| --- | ---: | ---: |
| Completed pages / failures / empty outputs | 24 / 0 / 0 | 24 / 0 / 0 |
| Detection TP / FP / FN at IoU 0.5 | 459 / 478 / 561 | 459 / 478 / 561 |
| Detection F1 | 46.91% | 46.91% |
| Matched-region CER / WER | 12.12% / 32.58% | 12.12% / 32.58% |
| End-to-end CER / WER | 71.76% / 98.42% | 71.76% / 98.42% |
| Total latency P50 / P95 | 1,928.53 / 4,687.44 ms | 1,325.58 / 2,646.71 ms |
| Detection latency P50 / P95 | 608.65 / 905.09 ms | 394.62 / 627.83 ms |
| Recognition latency P50 / P95 | 1,325.49 / 4,013.38 ms | 775.58 / 2,182.93 ms |

The 24 page-level ordered text sequences match exactly. Across 937 corresponding predicted regions, region text and region counts match with zero mismatches; polygon coordinates have maximum absolute difference `0` and confidence has maximum absolute difference `2.57e-5`. These observations establish parity for this exact host, model, manifest and single-iteration protocol. They do not establish dataset-level accuracy, GPU parity, or a formal performance ranking.

The selection contains 42 lines with at least 32 characters and 4 lines with at least 128 characters, but no natural line at or above 3,200 characters. The A2 natural-long-text gate therefore remains open.

## Reproduction

Run the ORT and OpenVINO commands from [the v6 Medium public quality record](hiertext-v6-medium-public-ocr-20261002.md), changing only `-Backend onnxruntime` to `-Backend openvino` and using distinct output directories. Score both output directories with `Evaluate-DeploySharpPublicOcrDataset.py`, then compare their `evaluation-corrected.json` files and the `predictions-corrected.json` image/region sequences.
