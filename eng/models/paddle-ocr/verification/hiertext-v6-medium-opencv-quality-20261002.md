# PP-OCR v6 Medium OpenCV DNN HierText quality smoke (2026-10-02)

This is the OpenCV DNN run for the same 24-page HierText `sample-003` selection used by the ORT/OpenVINO v6 Medium records. The images and annotations stay outside the repository at `F:\OCRBenchmarkTesting`; the source audit marks the selection smoke-only. The result is an execution and quality observation for this exact OpenCV 5.0 importer/runtime, not a general accuracy or performance claim.

## Protocol and provenance

- Model: PP-OCRv6 Medium, OpenCV DNN CPU, full `det -> crop -> rec -> merge` pipeline.
- SlidingWindow overlap `0.2`, maximum 32 windows per region, maximum 1,024 regions, recognition maximum width `320`.
- One warm-up and one measured iteration per page; recognition batch `1`; one inference channel. The batch is fixed at one for the OpenCV contract; the result is not a formal 5-warm-up/50-iteration benchmark.
- 24 pages completed; process failures `0`; empty outputs `0`.
- Detector SHA-256: `eb13b44b25bb36f89528b68720af8a61d9cf381176107f465db1757b65d086e1`.
- Recognizer SHA-256: `9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba`.
- Source manifest SHA-256: `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- Selected manifest SHA-256: `2c3933fbd5f29c2d4054357518feae96eb1bedd4b009333e105bfeb267159316`.
- Source revision: `6deb9ce28e49e0764f16f2b86de0d975481b35cc`; benchmark assembly SHA-256: `dcd8bd822201b454713ec7534e717c7275378596cbf2cf09e3e64d001eb2dcf3`.
- Host: Windows 10 (`10.0.26200`), x64, 16 logical processors; OpenCV DNN CPU runtime.

## Quality results

| Metric | OpenCV DNN CPU |
| --- | ---: |
| Detection TP / FP / FN at IoU 0.5 | 459 / 478 / 561 |
| Detection precision / recall / F1 | 48.99% / 45.00% / 46.91% |
| Matched-region CER / WER | 11.89% / 30.48% |
| Matched-region exact text rate | 50.33% (231 / 459) |
| End-to-end CER / WER | 71.14% / 96.89% |
| End-to-end correct regions | 231 / 1,020 |
| Total latency P50 / P95 | 11,971.02 / 26,997.76 ms |
| Detection latency P50 / P95 | 1,685.09 / 2,453.75 ms |
| Recognition latency P50 / P95 | 10,324.83 / 25,017.57 ms |

Detection geometry is identical to the ORT reference for this run: TP/FP/FN, all geometry coverage counters and region counts match. OCR text is not bit-identical across backends: page-level ordered text differs on 23/24 pages and 174 of 937 corresponding regions differ in text. Polygon coordinates have maximum absolute difference `0`; confidence has maximum absolute difference `0.59039062`. These are observed backend differences, not evidence that OpenCV is unsupported.

The ≥32-character bucket contains 42 lines and 467 end-to-end edits; the ≥128-character bucket contains 4 lines and 79 edits. No natural line in the selection reaches 3,200 characters, so A2 remains open.

## Reproduction

Use the v6 Medium ORT command in [the public quality record](hiertext-v6-medium-public-ocr-20261002.md), change `-Backend onnxruntime` to `-Backend opencv-dnn`, use a distinct output directory, set `JYPPX_OPEN_CV_RUNTIME_PATH` to the benchmark output `runtimes\win-x64\native` directory, and keep `-BatchSize 1`. Score the output with `Evaluate-DeploySharpPublicOcrDataset.py` and compare it with the ORT/OpenVINO prediction documents.
