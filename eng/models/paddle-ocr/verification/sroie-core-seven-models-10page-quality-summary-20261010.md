# PP-OCR core seven-model SROIE ten-page quality summary (2026-10-10)

This summary places the seven catalogued PP-OCR v4/v5/v6 detector-recognizer combinations on one ten-page SROIE selection: v4 Mobile/Server, v5 Mobile/Server and v6 Tiny/Small/Medium. Each linked record runs the complete `det → crop → optional cls → rec → merge` pipeline with ORT CPU and OpenVINO CPU, batch `16`, one inference channel and the same bounded SlidingWindow policy.

## Smoke summary

| Model | Pages | Regions | Det F1 | Matched CER/WER | End-to-end CER/WER | ORT P50/P95 (ms) | OpenVINO P50/P95 (ms) | Evidence |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| PP-OCRv4 Mobile | 10/10 | 555 | 91.52% | 37.59% / 71.88% | 43.78% / 77.87% | 4347.26 / 5796.95 | 2032.36 / 3267.12 | [record](sroie-v4-mobile-10page-quality-parity-20261010.md) |
| PP-OCRv4 Server | 10/10 | 534 | 89.59% | 36.22% / 75.55% | 44.89% / 82.64% | 8063.73 / 10209.76 | 5901.97 / 19871.63 | [record](sroie-v4-server-10page-quality-parity-20261010.md) |
| PP-OCRv5 Mobile | 10/10 | 538 | 91.11% | 38.09% / 69.80% | 45.11% / 76.40% | 2640.82 / 3289.84 | 1113.67 / 1468.35 | [record](sroie-v5-mobile-10page-quality-parity-20261010.md) |
| PP-OCRv5 Server | 10/10 | 534 | 92.19% | 38.99% / 68.46% | 45.77% / 74.38% | 5446.37 / 6584.76 | 3537.17 / 4442.24 | [record](sroie-v5-server-10page-quality-parity-20261010.md) |
| PP-OCRv6 Tiny | 10/10 | 536 | 88.13% | 33.39% / 56.81% | 43.73% / 68.14% | 336.38 / 488.29 | 196.15 / 249.63 | [record](sroie-v6-tiny-10page-quality-parity-20261010.md) |
| PP-OCRv6 Small | 10/10 | 531 | 91.15% | 30.42% / 46.03% | 37.63% / 55.74% | 1140.60 / 1839.85 | 647.60 / 781.49 | [current record](sroie-v6-small-10page-quality-parity-20261010.md) |
| PP-OCRv6 Medium | 10/10 | 541 | 90.49% | 29.97% / 44.33% | 37.41% / 54.82% | 4450.38 / 6082.79 | 2533.57 / 3240.18 | [record](sroie-v6-medium-10page-quality-parity-20261010.md) |

All fourteen model/backend runs completed without failures or empty pages. Every pair had `10/10` ordered page-text matches and zero indexed region-text mismatches; the individual JSON files preserve the exact confidence drift and model hashes.

## How to read this table

- SROIE provides word-level boxes while PP-OCR emits text-line quadrilaterals. IoU, CER and WER here are reproducible diagnostics under an intentional word-vs-line mismatch, not official SROIE accuracy.
- P50/P95 values are one-shot page observations from one warm-up and one measured iteration. They are not a 5-warm-up/50-iteration performance ranking, and OpenVINO's lower value on this host is not a universal hardware claim.
- The records were produced at nearby source revisions; each machine-readable report records its own `sourceRevision`. This table is a linked evidence index, not an atomic clean-checkout benchmark.
- The ten pages contain 21 regions with at least 32 characters but no natural region at least 128 or 3,200 characters. The formal long-text and quality gates remain open.
- This summary only covers ORT CPU and OpenVINO CPU. It does not promote CUDA, TensorRT or OpenCV DNN for all seven models, and it does not establish PP-Structure support.

## Reproduction boundary

Use the reproduction section of each linked record. The two five-page manifests are merged only after the runner verifies matching protocol and provenance, and the source images, annotations, predictions and raw run directories remain local. The machine-readable summary is [sroie-core-seven-models-10page-quality-summary-20261010.json](sroie-core-seven-models-10page-quality-summary-20261010.json).
