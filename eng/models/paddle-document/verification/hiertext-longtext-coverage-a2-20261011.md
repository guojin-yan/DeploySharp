# HierText long-text coverage audit (2026-10-11)

This is a data-availability audit for the fixed local HierText manifests. It does not run OCR and does not turn page-level character totals into long-line accuracy.

- Manifests: 2; pages: 34
- Maximum annotated continuous line: **132 characters**
- Maximum page aggregate across annotated lines: **2335 characters**
- Natural annotated lines >= 3,200 characters: **0**
- Pages with aggregate >= 3,200 characters: **0** (not valid continuous-line samples)

| Manifest | Image | Instances | Page aggregate chars | Maximum line chars |
|---|---|---:|---:|---:|
| hiertext-validation-sample-002.jsonl | `hiertext-validation-5c4d5de59518fe4d` | 12 | 67 | 10 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-d989ba898a556309` | 63 | 717 | 31 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-c237f9cec8e311b0` | 20 | 339 | 49 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-c2291a13c825966f` | 30 | 484 | 27 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-b4cc87af903b8270` | 35 | 200 | 20 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-a2364c31de73114d` | 34 | 263 | 27 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-973b7f62446f02cd` | 61 | 545 | 19 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-945782e585e04e0e` | 22 | 157 | 44 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-870b721faf7b475d` | 59 | 162 | 29 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-86760f63da399e07` | 72 | 201 | 25 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-83499e8f67512938` | 65 | 100 | 9 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-7d9138e29dd9b711` | 52 | 140 | 19 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-7a7d0f471fe900e2` | 64 | 2335 | 132 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-62bd21d5af1428d9` | 15 | 89 | 12 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-4e0277825c80fed8` | 34 | 322 | 25 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-466c6ce044b8bd46` | 25 | 201 | 19 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-3ea969425b1c88bd` | 50 | 104 | 14 |
| hiertext-validation-sample-003.jsonl | `hiertext-validation-291e9c3df3a9ffb5` | 48 | 192 | 26 |
| hiertext-validation-sample-002.jsonl | `hiertext-validation-97a0add3f8f47b65` | 32 | 134 | 7 |
| hiertext-validation-sample-002.jsonl | `hiertext-validation-d14658b78cec2cf7` | 29 | 184 | 12 |

## Boundary

The fixed selection has no natural annotated line at or above 3,200 characters. Combining separate lines or pages would change the task and cannot close the A2 continuous-text gate. A legal, attributable long-line corpus is still required; the source manifest and images remain outside Git/Release.
