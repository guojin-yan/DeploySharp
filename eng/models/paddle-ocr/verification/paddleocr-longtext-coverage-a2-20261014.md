# PaddleOCR long-text coverage audit (2026-10-14)

This report inventories every local public JSONL manifest currently available to the OCR benchmark harness. It does not run OCR and does not turn page-level character totals into continuous-line accuracy.

- Manifest files: **8**; page records: **243**
- Eligible non-empty, non-ignored lines: **32694**; ignored: **731**; empty non-ignored: **0**
- Maximum natural annotated line in this inventory: **132 characters**
- Maximum page aggregate across annotated lines: **2348 characters**
- Natural annotated lines >= 3,200 characters: **0**
- Page records with aggregate >= 3,200 characters: **0** (not valid continuous-line samples)

| Manifest | Dataset / split | Eligible lines | Maximum line | Maximum page aggregate | SHA-256 |
|---|---|---:|---:|---:|---|
| funsd-test-full-001.jsonl | FUNSD / test | 6938 | 25 | 2348 | `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3` |
| funsd-test-smoke-001.jsonl | FUNSD / test | 1769 | 15 | 1123 | `a5e30927be9695d05e103aef98ab8b6db636070c57ea4c55acb0200bebfddefa` |
| funsd-train-full-001.jsonl | FUNSD / train | 18488 | 30 | 1677 | `61ae95f7efa80c5978495c9ff6b7aa289651bef2e6deda104999cdb5fc154f0c` |
| funsd-train-smoke-001.jsonl | FUNSD / train | 3400 | 26 | 1565 | `4357f96cb2ee996c33da3ec56aeb80b4c5efd1bfa85f4325957b4c66457b697c` |
| hiertext-validation-sample-002.jsonl | google-research-datasets/hiertext / validation | 537 | 27 | 883 | `e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7` |
| hiertext-validation-sample-003.jsonl | google-research-datasets/hiertext / validation | 1020 | 132 | 2335 | `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e` |
| sroie-train-20260923T072455614249Z.jsonl | jsdnrs/ICDAR2019-SROIE / train | 267 | 37 | 712 | `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1` |
| sroie-train-20260923T072550009854Z.jsonl | jsdnrs/ICDAR2019-SROIE / train | 275 | 40 | 810 | `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358` |

## Boundary

The current local inventory contains no natural annotated line at or above 3,200 characters. Page totals, concatenated crops and synthetic compositions change the OCR task and cannot close A2. The report is a data-availability result, not an accuracy or backend-support claim; source images and annotations stay outside Git/Release.
