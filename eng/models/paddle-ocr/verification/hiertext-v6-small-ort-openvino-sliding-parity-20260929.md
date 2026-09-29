# PP-OCR v6 Small ORT/OpenVINO HierText SlidingWindow parity (2026-09-29)

The same 24-page HierText `sample-003` selection was run with PP-OCRv6 Small on ONNX Runtime CPU and OpenVINO CPU. Images and annotations remain outside this repository under `F:\OCRBenchmarkTesting`.

## Fixed inputs and protocol

- Detector SHA-256: `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`.
- Recognizer SHA-256: `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`.
- Manifest SHA-256: `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`.
- One warm-up and one measured iteration per page, recognition batch 16, one inference channel.
- SlidingWindow overlap `0.2`, maximum 32 windows per region, maximum 1,024 regions, maximum width 320.
- ORT run directory: `artifacts/public-ocr-evaluation/hiertext-v6-small-sliding-20260929-rerun`.
- OpenVINO run directory: `artifacts/public-ocr-evaluation/hiertext-v6-small-openvino-sliding-20260929`.

## Results

| Backend | Pages | TP / FP / FN | Matched CER / WER | End-to-end CER / WER | ≥32-char edits | ≥128-char edits |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ONNX Runtime CPU | 24/24 | 436 / 377 / 584 | 12.10% / 28.50% | 73.65% / 93.50% | 501 / 2,378 | 100 / 520 |
| OpenVINO CPU | 24/24 | 436 / 377 / 584 | 12.10% / 28.50% | 73.65% / 93.50% | 501 / 2,378 | 100 / 520 |

The per-page recognized text sequence matched on all 24 pages. The serialized prediction files have different SHA-256 values because their run metadata and timing fields contain backend-specific values; the comparison is made on the ordered region text sequence, while the evaluator metrics and region counts are identical.

This is CPU backend parity for this exact model, dataset selection and runtime configuration. It does not establish numeric tensor equality, GPU parity, long-text accuracy beyond the available labels, or a release-quality dataset score. The selection has no text longer than 3,200 characters.

## Reproduction

Use the commands in [the v6 Small quality record](hiertext-v6-small-public-ocr-20260929.md), replacing `-Backend onnxruntime` with `-Backend openvino` for the second run. Keep the same manifest, model root, SlidingWindow parameters and output settings.
