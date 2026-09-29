# FUNSD reading-order C3 smoke evidence (2026-09-29)

One real FUNSD test form (`funsd-test-82504862`) was run through PP-OCRv5 Mobile DET → optional CLS → REC on ONNX Runtime CPU. The page has 63 non-ignored FUNSD word/entity regions; the detector returned 26 text-line regions, with 11 IoU-0.5 matches.

The matched prediction order was compared with the source manifest's annotation order. Pairwise order agreement was `83.64%`, but the matched order was not monotonic (`[0, 7, 8, 1, 15, 16, 17, 18, 2, 22, 21]`). Detection recall was only `17.46%` under the word-versus-line IoU mismatch, so this is diagnostic evidence rather than a reading-order quality score.

The run also produced matched CER `23.33%` and end-to-end CER `173.56%`; these values expose the expected granularity mismatch and must not be presented as FUNSD model accuracy. Full hashes and protocol fields are in [funsd-reading-order-c3-20260929.json](funsd-reading-order-c3-20260929.json).

This evidence confirms that the current OCR output can be inspected for reading order, but it does not validate C3. A valid C3 acceptance still requires line/paragraph-level labels or a document-layout model that supplies authoritative regions and reading order on multi-column pages.
