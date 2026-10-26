# PaddleOCR-VL automatic-layout routing: two labelled FUNSD pages

Date: 2026-10-26. This is an offline quality diagnostic using the already completed [public Bundle + PP-DocLayout-L two-page run](paddleocr-vl-release-detected-pages-linux-20261026.md). No detector, VL graph or image decoder was run again.

The annotation source is the local `funsd-test-full-001.jsonl` manifest from `F:\OCRBenchmarkTesting`, SHA-256 `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`. It provides original FUNSD word boxes and source-image SHA-256 values. The two selected output JSON files have SHA-256 `e4bd438fb8c9f061059aae702361b1bfcf4b81ecaf8d08b83370627e27a06dcf` (page 7) and `0e0de6175c04ef97b0ba77ad9829d9c92ce20071751ea06a64dfb8c72bf19f27` (page 3). Source SHA, dimensions, detector identity, detector-to-VL region binding and routing mode are checked before scoring. Raw scans, labels, generated text and page exports stay outside Git; only the [count summary](paddleocr-vl-funsd-layout-routing-coverage-20261026.json) is committed.

For each non-ignored annotated word, the evaluator uses the center of its axis-aligned polygon bounds. A word is `completed` if its center lies in any region with EOS-complete VL generation; otherwise `failedOnly`, `skippedOnly` and `uncovered` are mutually exclusive in that order. Additional overlap counters do not enter the partition. Region counts can overlap and must not be added as unique words.

| FUNSD page / original ID | Annotated words | Completed-region coverage | Skipped-only | Failed-only | Uncovered | Completed and skipped | Multiple completed regions |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 / `86220490` | 75 | 1 | 74 | 0 | 0 | 0 | 1 |
| 7 / `87147607` | 204 | 204 | 0 | 0 | 0 | 204 | 6 |
| **Total** | **279** | **205** | **74** | **0** | **0** | **204** | **7** |

Page 3's skipped `image` region contains the centers of 74 annotated words. Its successful `text` and `aside_text` regions both cover the same one word, while the `number` region covers none. Page 7's skipped `image` region covers all 204 words but exactly overlaps a successful `table` region, so those words are **not** counted as omitted by the router; six also lie in a successful `table_title` region. This distinction is why a raw skipped-region count is not a quality metric.

This result is a **routing-coverage failure on page 3**, not a measured 74-word transcription error rate. A region covering a word does not prove the generated text contains it, and a skipped region may contain non-text content. The FUNSD manifest's `reading_order` is official entity-record order followed by word order, not independent page reading-order ground truth, so no order score is reported. Two preselected pages cannot establish representative layout recall, CER/WER, table-content quality or a production fallback policy. The existing `taskByCategory` override lets callers explicitly route `image` to another VL task, but doing so may duplicate page-7 content and needs separate labelled evaluation before changing defaults.

Reproduce using the [offline evaluator](../scripts/Audit-PaddleOcrVlLayoutWordCoverage.py) and the two preserved page exports:

```powershell
python eng/models/paddle-ocr/scripts/Audit-PaddleOcrVlLayoutWordCoverage.py `
  --annotations F:\OCRBenchmarkTesting\data\annotations\manifests\funsd-test-full-001.jsonl `
  --page E:\DeploySharpEvidence\paddleocr-vl-release-detected-pages-20261026\page-7.json `
  --page E:\DeploySharpEvidence\paddleocr-vl-release-detected-pages-20261026\page-3.json `
  --output E:\DeploySharpEvidence\paddleocr-vl-release-detected-pages-20261026\layout-word-coverage.json
```

The evaluator uses Python's standard library. Five synthetic contracts cover overlapping skipped/completed regions, failed-region precedence, mismatched source images, duplicate pages and detector/routed-region drift. This is a geometric quality diagnostic, not a new inference benchmark; the previous single-run timings remain outside formal P50/P95.
