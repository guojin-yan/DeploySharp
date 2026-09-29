# HierText angle and orientation label audit (2026-09-29)

This audit checks whether the local public HierText selections can support the PaddleOCR plan's A3 angle/CER/WER gate. It does not run a model; it records the labels that actually exist in the source manifests.

## Selection and counts

- Manifests: `hiertext-validation-sample-002.jsonl` and `hiertext-validation-sample-003.jsonl` under `F:\OCRBenchmarkTesting`.
- Images: 34.
- Valid non-ignored text lines: 1,557.
- Official `vertical=true` lines: 18.
- Official `handwritten=true` lines: 47.
- Images with nonzero Open Images source rotation metadata: 0.
- Lines with an upright-versus-inverted direction class: 0.
- Lines with a source `text_direction` value other than horizontal: 18.
- Image orientation buckets: 27 `horizontal_or_mixed`, 7 `vertical_present`.

## Boundary

The official annotations provide vertical and handwriting flags, but they do not provide an upright-versus-inverted class, a measured baseline angle, a perspective distortion target, or a corrected-text reference for comparing affine versus perspective recovery. Polygon geometry alone cannot be used to invent those labels. The current selection can support vertical-line slicing and descriptive quality analysis, but it cannot complete A3's required repair-rate, CER/WER, rejection-rate and coordinate-error comparison for tilted, perspective or inverted text.

The existing v6 Small ORT/OpenVINO/OpenCV HierText records therefore remain long-text and cross-backend evidence. They do not claim an angle-correction improvement. A3 still needs a source with attributable angle/rotation ground truth or an explicitly generated, labeled transformation set whose source text and transformation parameters are preserved.
