# PaddleOCR quality evidence status (2026-10-27 increment)

This increment extends the [2026-10-26 index](paddleocr-quality-status-20261026.md); it does not rewrite historical reports or change backend-support claims.

| Scope | New evidence | Status |
| --- | --- | --- |
| PP-OCRv6 Small/Medium natural wide-line A2 seam | Same independent HierText test selection: 17 lines, 12 main-quality lines. Clamp character edits remain Small `56/1945`, Medium `45/1945`; SlidingWindow improves Small `101 -> 58/1945`, Medium `134 -> 91/1945`, with 4/24 main model-lines better, 0 worse. Post-change OpenVINO/OpenCV DNN each match ORT `68/68` merged text/class/metrics/seam decisions and `70/70` raw-window text/class hashes. The pinned validation trace replay retains its previous Small/Medium main-quality results. | [Detailed follow-up](paddleocr-natural-wide-seam-candidate-20261027.md). Clamp stays default. This is exact three-backend CPU REC-only parity; TensorRT is recorded separately below. No whole-page quality or formal speed claim; A2 remains open. |
| PP-OCRv6 Small/Medium TensorRT 11 natural wide-line REC | Same 17-line independent test selection and production Visual assembly as ORT. SHA-pinned prebuilt Engines complete all `68` calls. Main-quality text/class/metric/seam decisions match `48/48`; full cohort has `64/68` text/class, `66/68` metric, `68/68` seam and `68/70` raw-window text/class matches. Four text differences are confined to officially illegible diagnostic lines. | [Detailed follow-up](paddleocr-natural-wide-seam-candidate-20261027.md). Exact main-quality parity only, not full-row parity, DET/CLS/page accuracy or formal P50/P95; A2 remains open. |

The [machine-readable increment](paddleocr-quality-status-20261027.json) records the exact fractions, baseline and candidate report hashes. A3, B1b-B3, formula/chart quality, broader VL quality and multi-backend formal performance retain their prior status.
