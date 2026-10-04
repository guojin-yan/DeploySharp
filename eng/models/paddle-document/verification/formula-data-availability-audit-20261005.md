# Formula evaluation data availability audit

Generated `2026/10/4 18:55:06 +00:00`. This is an inventory of local candidate files; it is not an accuracy report.

| Measure | Count |
| --- | ---: |
| Formula image candidates | 1 |
| Label/manifest candidates with label-like fields | 0 |
| Multi-equation label candidates | 0 |
| Official single-image sample candidates | 1 |

Admission: **blocked**. No formula-named multi-equation label manifest was found in the audited roots; current evidence remains a single official sample and controlled variants.

| Source | Exists | Candidate files | Path |
| --- | :---: | ---: | --- |
| `OCRBenchmarkTesting` | True | 0 | `F:\OCRBenchmarkTesting` |
| `PaddleDocumentValidation` | True | 6 | `E:\Model\PaddleDocument\validation` |

| Source | Kind | Relative path | Size | Label signal | Records | SHA-256 |
| --- | --- | --- | ---: | :---: | ---: | --- |
| `PaddleDocumentValidation` | preprocessed-tensor | `formula-384x384.f32` | 589824 | False | 0 | `0fe7a0de96771ad78630147069c260c373309ef9f332473d7b3b5ea786e1606d` |
| `PaddleDocumentValidation` | preprocessed-tensor | `formula-672x192.f32` | 516096 | False | 0 | `3537000712f24514e95b8e6526696ff9651389ed43f84c225b9a0da8dad0d756` |
| `PaddleDocumentValidation` | preprocessed-tensor | `formula-768x768.f32` | 2359296 | False | 0 | `39feb34ad82952c86cbefb314cd416436a93e43fa7bf79927012c6dfd550b145` |
| `PaddleDocumentValidation` | other | `formula-processors-paddlex.py` | 37283 | False | 0 | `4a2c197634a210a90cca7bf3f417ce594b610db0058d2ccfa060dc2dcfb752eb` |
| `PaddleDocumentValidation` | text-or-manifest | `formula-reference.json` | 1082 | False | 1 | `a042d6a5c9caca5509d9c5a8e46f62e63fa394633c96cc3c20f25056ee4c19e1` |
| `PaddleDocumentValidation` | image | `general_formula_rec_001.png` | 2346 | False | 0 | `7885d4a349edcdfbfbc439305b8b554c2500f095949bc53d14835656b902dbcc` |

A candidate file is not admitted automatically: license, schema, image-to-label alignment and multi-equation coverage still require manual review. Rerun this script after adding a legally usable formula dataset; only then create a separate normalized LaTeX/CER report.
