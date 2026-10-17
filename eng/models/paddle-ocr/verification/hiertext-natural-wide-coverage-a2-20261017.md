# HierText natural wide-line coverage audit for A2 (2026-10-17)

This report scans the pinned HierText annotation source using a target recognition height of **48** and keeps every natural line whose projected recognition width is at least **3200**. The projection is a geometry signal, not a claim that the line contains that many characters.

- Annotation source: F:\OCRBenchmarkTesting\artifacts\cache\hiertext\validation-70b6620b2b112597d8219e11eee9773a1403827c.jsonl.gz (SHA-256 ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a, source revision 70b6620b2b112597d8219e11eee9773a1403827c, annotation version v1.0).
- Candidate lines: **13** across **9** images; local images present: **9**; missing: **0**.
- Candidate image IDs found in the supplied Open Images metadata CSV: **0**. The local metadata has no matching per-image license/landing rows; the official CVDF object path is recorded for local acquisition, but attribution and redistribution remain a separate boundary.
- Maximum projected width: **4725**; maximum annotated text length among candidates: **182** characters.

| Image ID | Paragraph/line | Text length | Projected width | Local image | SHA-256 |
| --- | ---: | ---: | ---: | --- | --- |
| 0532d7adab8961c4 | 39/0 | 161 | 3596 | yes | aea8b717df3dbaea |
| 0532d7adab8961c4 | 40/0 | 172 | 3592 | yes | aea8b717df3dbaea |
| 0532d7adab8961c4 | 41/0 | 149 | 3256 | yes | aea8b717df3dbaea |
| 08cc7d6ebd7c4d93 | 34/0 | 155 | 3509 | yes | ebce98d5656e2923 |
| 3c027cacb77e35cd | 50/0 | 2 | 4464 | yes | 586358ca3a6cc33a |
| 584eda70fc6b1a5b | 11/0 | 47 | 3439 | yes | 497842fd6999c1fb |
| 584eda70fc6b1a5b | 11/1 | 45 | 3587 | yes | 497842fd6999c1fb |
| 6ce679754fa8bc1c | 0/0 | 132 | 4725 | yes | 53b1fdd6dab541ae |
| a4e2a75d729799a9 | 15/0 | 182 | 3652 | yes | 9497bbb771f8ca2b |
| dd838509911a8f86 | 0/0 | 121 | 3306 | yes | a8ea038b1ba003a5 |
| dd838509911a8f86 | 9/0 | 114 | 3357 | yes | a8ea038b1ba003a5 |
| e04931ec29a0bf9c | 19/0 | 138 | 3236 | yes | 2cc9af4a1e6fae43 |
| e1154c00eb8b9e8c | 30/0 | 164 | 4032 | yes | 8e6140c192188b0f |

## Interpretation and next step

The candidates are useful for testing geometry-driven SlidingWindow behavior, overlap seam attribution, source-region mapping and memory budgets. They do **not** close the A2 requirement for a naturally occurring continuous line of at least 3,200 characters. This audit does not claim OCR quality: detector/recognizer invocation, CER/WER, seam merge result and backend parity belong to a separate execution report.
