# MathNet realFormula — error-pattern diagnostics

This post-hoc report stratifies the six already committed ORT CPU prediction sets against the pinned 121-row reference manifest. It does not rerun models. Source labels remain outside Git; only derived counts and worst-case image IDs are included here.

Dataset: MathNet realFormula v1, `121` annotated samples; manifest SHA-256 `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`. Source: [Zenodo DOI](https://doi.org/10.5281/zenodo.11296815).

Reference strata in the normalized annotation strings: 116 without explicit line-break/alignment markers, 5 with such markers, 5 array/alignment environments, 0 containing selected math-font commands. Categories overlap; this is a syntax scan, not an image-level typography annotation.

The character CER in the inference report is unchanged. This adds a bounded character edit rate `distance / max(reference length, prediction length)` and splits results by reference features. A second sensitivity column strips only selected flat font wrappers with brace-free payloads (`\mathrm`, `\mathbf`, `\boldsymbol`, `\mathbb`, `\mathcal`, `\mathfrak`, `\mathscr`) before whitespace removal. For example, the normalized label and Plus-L output for `211111875-19.png` become identical after stripping those selected flat wrappers from both strings. This is not a safe general LaTeX canonicalizer: it can erase meaningful typography. Neither score is the normalized token EditScore used in the MathNet study, a TeX parser result, a rendered-image comparison, or a mathematical-equivalence judgment.

## `paddle-formula/pp-formulanet-plus-s`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 14/121 (11.6%) | 32.23% | 21/121 (17.4%) | 24.37% | 121/121 |
| single-line | 116 | 14/116 (12.1%) | 31.89% | 21/116 (18.1%) | 23.81% | 116/116 |
| multi-line | 5 | 0/5 (0.0%) | 39.92% | 0/5 (0.0%) | 37.35% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 39.92% | 0/5 (0.0%) | 37.35% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 9/51 (17.6%) | 31.15% | 13/51 (25.5%) | 21.47% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 5/66 (7.6%) | 32.83% | 8/66 (12.1%) | 26.02% | 66/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 36.00% | 0/4 (0.0%) | 34.05% | 4/4 |

Non-EOS samples: none.

Worst cases by bounded character edit rate: 221200157-0.png (CER 218%, edit 72%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 220800300-41.png (CER 210%, edit 71%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 211211751-175.png (CER 130%, edit 68%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 150%, edit 67%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 211205994-13.png (CER 192%, edit 67%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 220706512-24.png (CER 113%, edit 62%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 220612152-139.png (CER 110%, edit 61%, tokens 45, multi-line False, array/alignment False, math-font False, EOS True); 221107903-61.png (CER 100%, edit 59%, tokens 28, multi-line False, array/alignment False, math-font False, EOS True); 211201486-39.png (CER 94%, edit 58%, tokens 115, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 90%, edit 58%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True)

## `paddle-formula/pp-formulanet-plus-m`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 11/121 (9.1%) | 32.41% | 17/121 (14.0%) | 23.48% | 121/121 |
| single-line | 116 | 11/116 (9.5%) | 32.00% | 17/116 (14.7%) | 22.81% | 116/116 |
| multi-line | 5 | 0/5 (0.0%) | 41.89% | 0/5 (0.0%) | 38.95% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 41.89% | 0/5 (0.0%) | 38.95% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 7/51 (13.7%) | 30.11% | 10/51 (19.6%) | 19.92% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 4/66 (6.1%) | 33.94% | 7/66 (10.6%) | 25.49% | 66/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 36.50% | 0/4 (0.0%) | 35.64% | 4/4 |

Non-EOS samples: none.

Worst cases by bounded character edit rate: 211111875-19.png (CER 320%, edit 76%, tokens 41, multi-line False, array/alignment False, math-font False, EOS True); 221200157-0.png (CER 208%, edit 71%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 220800300-41.png (CER 237%, edit 70%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 210805783-0.png (CER 230%, edit 70%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 211205994-13.png (CER 204%, edit 68%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 150%, edit 67%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 211103142-26.png (CER 137%, edit 61%, tokens 65, multi-line False, array/alignment False, math-font False, EOS True); 211211751-175.png (CER 158%, edit 61%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 230101946-47.png (CER 154%, edit 61%, tokens 100, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 75%, edit 60%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True)

## `paddle-formula/pp-formulanet-plus-l`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 16/121 (13.2%) | 30.77% | 22/121 (18.2%) | 21.14% | 121/121 |
| single-line | 116 | 16/116 (13.8%) | 30.46% | 22/116 (19.0%) | 20.77% | 116/116 |
| multi-line | 5 | 0/5 (0.0%) | 37.92% | 0/5 (0.0%) | 29.70% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 37.92% | 0/5 (0.0%) | 29.70% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 9/51 (17.6%) | 28.55% | 13/51 (25.5%) | 17.80% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 7/66 (10.6%) | 32.83% | 9/66 (13.6%) | 23.67% | 66/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 25.05% | 0/4 (0.0%) | 22.01% | 4/4 |

Non-EOS samples: none.

Worst cases by bounded character edit rate: 211111875-19.png (CER 340%, edit 77%, tokens 41, multi-line False, array/alignment False, math-font False, EOS True); 220800300-41.png (CER 233%, edit 70%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 162%, edit 69%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 221200157-0.png (CER 161%, edit 66%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 211205994-13.png (CER 177%, edit 65%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 211211751-175.png (CER 161%, edit 62%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 211105960-60.png (CER 149%, edit 61%, tokens 49, multi-line False, array/alignment False, math-font False, EOS True); 230101946-47.png (CER 154%, edit 61%, tokens 100, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 75%, edit 60%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True); 211103142-26.png (CER 120%, edit 58%, tokens 65, multi-line False, array/alignment False, math-font False, EOS True)

## `paddle-formula/pp-formulanet-s`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 12/121 (9.9%) | 31.30% | 16/121 (13.2%) | 23.79% | 121/121 |
| single-line | 116 | 12/116 (10.3%) | 30.58% | 16/116 (13.8%) | 23.01% | 116/116 |
| multi-line | 5 | 0/5 (0.0%) | 48.12% | 0/5 (0.0%) | 41.85% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 48.12% | 0/5 (0.0%) | 41.85% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 8/51 (15.7%) | 30.23% | 10/51 (19.6%) | 20.93% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 4/66 (6.1%) | 32.05% | 6/66 (9.1%) | 25.51% | 66/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 32.79% | 0/4 (0.0%) | 31.93% | 4/4 |

Non-EOS samples: none.

Worst cases by bounded character edit rate: 220212793-38.png (CER 824%, edit 93%, tokens 64, multi-line True, array/alignment True, math-font False, EOS True); 211200471-3.png (CER 412%, edit 82%, tokens 106, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 162%, edit 69%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 220800300-41.png (CER 180%, edit 67%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 221200157-0.png (CER 173%, edit 63%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 211205994-13.png (CER 156%, edit 62%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 211211751-175.png (CER 158%, edit 61%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 211103142-26.png (CER 134%, edit 61%, tokens 65, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 79%, edit 61%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True); 211201486-39.png (CER 97%, edit 60%, tokens 115, multi-line False, array/alignment False, math-font False, EOS True)

## `paddle-formula/pp-formulanet-l`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 17/121 (14.0%) | 32.19% | 21/121 (17.4%) | 24.51% | 120/121 |
| single-line | 116 | 17/116 (14.7%) | 31.78% | 21/116 (18.1%) | 24.17% | 115/116 |
| multi-line | 5 | 0/5 (0.0%) | 41.66% | 0/5 (0.0%) | 32.56% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 41.66% | 0/5 (0.0%) | 32.56% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 11/51 (21.6%) | 29.10% | 14/51 (27.5%) | 20.25% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 6/66 (9.1%) | 34.40% | 7/66 (10.6%) | 27.44% | 65/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 35.04% | 0/4 (0.0%) | 30.57% | 4/4 |

Non-EOS samples (decoder did not emit EOS): 211110912-2.png (prediction length 1759, reference length 175, bounded edit 92%, LaTeX tokens 99)

Worst cases by bounded character edit rate: 220800300-41.png (CER 971%, edit 100%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 211200471-3.png (CER 1,048%, edit 94%, tokens 106, multi-line False, array/alignment False, math-font False, EOS True); 211110912-2.png (CER 924%, edit 92%, tokens 99, multi-line False, array/alignment False, math-font False, EOS False); 211205994-13.png (CER 429%, edit 82%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 211211751-175.png (CER 376%, edit 79%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 221200157-0.png (CER 208%, edit 71%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 150%, edit 67%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 211206793-14.png (CER 181%, edit 64%, tokens 32, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 79%, edit 61%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True); 220808291-57.png (CER 136%, edit 58%, tokens 54, multi-line False, array/alignment False, math-font False, EOS True)

## `paddle-formula/unimernet`

| Reference stratum | N | Raw exact | Raw bounded edit rate | Font-wrapper-stripped exact | Font-wrapper-stripped bounded edit rate | EOS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| all | 121 | 18/121 (14.9%) | 30.29% | 22/121 (18.2%) | 22.37% | 121/121 |
| single-line | 116 | 18/116 (15.5%) | 29.82% | 22/116 (19.0%) | 21.78% | 116/116 |
| multi-line | 5 | 0/5 (0.0%) | 41.31% | 0/5 (0.0%) | 35.85% | 5/5 |
| contains-array-or-alignment | 5 | 0/5 (0.0%) | 41.31% | 0/5 (0.0%) | 35.85% | 5/5 |
| short (≤40 LaTeX tokens) | 51 | 13/51 (25.5%) | 28.81% | 16/51 (31.4%) | 19.85% | 51/51 |
| medium (41–120 LaTeX tokens) | 66 | 5/66 (7.6%) | 31.56% | 6/66 (9.1%) | 24.08% | 66/66 |
| long (>120 LaTeX tokens) | 4 | 0/4 (0.0%) | 28.26% | 0/4 (0.0%) | 26.19% | 4/4 |

Non-EOS samples: none.

Worst cases by bounded character edit rate: 221000868-38.png (CER 245%, edit 71%, tokens 19, multi-line False, array/alignment False, math-font False, EOS True); 230101454-4.png (CER 150%, edit 67%, tokens 34, multi-line False, array/alignment False, math-font False, EOS True); 220800300-41.png (CER 203%, edit 67%, tokens 64, multi-line False, array/alignment False, math-font False, EOS True); 221200157-0.png (CER 169%, edit 67%, tokens 36, multi-line False, array/alignment False, math-font False, EOS True); 211206793-14.png (CER 181%, edit 64%, tokens 32, multi-line False, array/alignment False, math-font False, EOS True); 210800257-7.png (CER 163%, edit 62%, tokens 28, multi-line False, array/alignment False, math-font False, EOS True); 211103142-26.png (CER 134%, edit 61%, tokens 65, multi-line False, array/alignment False, math-font False, EOS True); 221211724-27.png (CER 79%, edit 61%, tokens 40, multi-line False, array/alignment False, math-font False, EOS True); 211205994-13.png (CER 135%, edit 60%, tokens 50, multi-line False, array/alignment False, math-font False, EOS True); 211200471-3.png (CER 60%, edit 60%, tokens 106, multi-line False, array/alignment False, math-font False, EOS True)

## Interpretation boundary

A high string error can include both genuine symbol/structure errors and canonicalization differences (for example style commands, optional braces, array layout, and multi-line formatting). This report identifies where to inspect; it cannot reclassify those cases as correct. FormulaNet-L EOS completion and very long repetitive outputs remain a separate decoder/model-quality issue. More authoritative follow-up requires official PaddleX prediction normalization parity, a reproducible TeX render/visual comparison or vetted semantic normalizer, and a held-out/domain-matched quality set.

The original per-sample output and raw diagnostics are linked in the [six-model inference report](formula-realformula-six-models-ort-20261007.md).
