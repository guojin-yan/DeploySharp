# FUNSD three-page reading-order C3 smoke (2026-09-29)

Three real FUNSD test forms were run with PP-OCRv5 Mobile, ORT CPU, SlidingWindow and the same one-iteration smoke protocol. The pages produced 97 predicted text-line regions against 388 word/entity annotations; 35 predictions matched at IoU 0.5. Aggregate detection recall was `9.02%`, matched CER `35.29%`, and end-to-end CER `202.17%` under the intentionally mismatched word/line evaluator.

Matched reading-order pair accuracy was `70.92%`; none of the three pages had a nondecreasing matched annotation order. Per-page pair accuracy was `83.64%`, `69.52%`, and `55.56%`. This is stronger evidence than the prior one-page smoke that generic OCR sorting cannot be used as a document reading-order acceptance criterion. It also confirms that low detector recall and word/line granularity mismatch dominate this diagnostic.

The result does not complete C3. A valid acceptance still needs line/paragraph-level ground truth and a layout-aware pipeline for multi-column, table, title, and vertical-text semantics. Full hashes and per-page order sequences are in [funsd-reading-order-c3-3pages-20260929.json](funsd-reading-order-c3-3pages-20260929.json).

Reproduction uses the same command as the one-page FUNSD smoke, with `-MaxImages 3` and output directory `artifacts/public-ocr-evaluation/funsd-v5-mobile-ort-c3-3pages-20260929`, followed by `Evaluate-DeploySharpPublicOcrDataset.py` using the generated `selected-manifest.jsonl`.
