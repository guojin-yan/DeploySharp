# MathNet realFormula — six Paddle formula models (ORT CPU)

This report evaluates each local ONNX checkpoint on all 121 manually annotated formulas in MathNet realFormula v1. The dataset is from arXiv papers and is published on Zenodo under CC-BY-4.0. Attribution: Schmitt-Koopmann et al., *MER dataset realFormula*, 2024, [DOI](https://doi.org/10.5281/zenodo.11296815). The benchmark data remains outside this repository; only per-sample predictions, hashes, and metrics are stored here.

Pinned data: archive SHA-256 `2ea6d4b1bac80eceda734989d2cd03c9619d9ffdb5d036c6d9b89d8a4c07b7bd`; annotation CSV SHA-256 `b7a4679f7d9203f1040c3945aaceca5b73c3d110a70dd35487d4ec5462d8d6f1`; image manifest SHA-256 `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`.

Environment: `Microsoft Windows 10.0.26200`, OS arch `X64`, process arch `X64`, 16 logical processors, `.NET 10.0.12`.

| Model | Exact | Token exact | Character CER | Token error | EOS | Empty |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `paddle-formula/pp-formulanet-plus-s` | 14/121 (11.57%) | 14/121 (11.57%) | 57.04% | 28.66% | 121/121 | 0 |
| `paddle-formula/pp-formulanet-plus-m` | 11/121 (9.09%) | 11/121 (9.09%) | 58.34% | 36.27% | 121/121 | 0 |
| `paddle-formula/pp-formulanet-plus-l` | 16/121 (13.22%) | 16/121 (13.22%) | 54.60% | 40.51% | 121/121 | 0 |
| `paddle-formula/pp-formulanet-s` | 12/121 (9.92%) | 12/121 (9.92%) | 61.58% | 37.51% | 121/121 | 0 |
| `paddle-formula/pp-formulanet-l` | 17/121 (14.05%) | 17/121 (14.05%) | 89.86% | 43.52% | 120/121 | 0 |
| `paddle-formula/unimernet` | 18/121 (14.88%) | 18/121 (14.88%) | 51.59% | 26.25% | 121/121 | 0 |

Character CER is Levenshtein distance after removing Unicode whitespace; token error is computed over whitespace-separated LaTeX tokens. The realFormula labels are canonicalized by their dataset authors, but different LaTeX strings can encode visually equivalent mathematics. These metrics do not parse TeX or establish mathematical equivalence. All six models reached EOS on most/all examples and produced no empty outputs; this only describes decoder completion, not correctness.

Per-sample JSON reports (including image/model/tokenizer hashes, reference-label hash, prediction, edit distances, EOS and warnings):

- [`paddle-formula/pp-formulanet-plus-s`](formula-realformula-pp-formulanet-plus-s-ort-20261007.json)
- [`paddle-formula/pp-formulanet-plus-m`](formula-realformula-pp-formulanet-plus-m-ort-20261007.json)
- [`paddle-formula/pp-formulanet-plus-l`](formula-realformula-pp-formulanet-plus-l-ort-20261007.json)
- [`paddle-formula/pp-formulanet-s`](formula-realformula-pp-formulanet-s-ort-20261007.json)
- [`paddle-formula/pp-formulanet-l`](formula-realformula-pp-formulanet-l-ort-20261007.json)
- [`paddle-formula/unimernet`](formula-realformula-unimernet-ort-20261007.json)

Reproduce data preparation with `eng/models/paddle-document/scripts/Prepare-RealFormulaDataset.ps1 -Download -Extract`. Then run `PaddleDocumentFormulaVariantIntegrationTests.SixFormulaModelsEvaluateRealFormulaDatasetOnOrtCpu` once per model by setting `DEPLOYSHARP_PADDLE_REAL_FORMULA=1`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT`, `DEPLOYSHARP_PADDLE_REAL_FORMULA_MODELS`, and `DEPLOYSHARP_PADDLE_REAL_FORMULA_REPORT_PATH`; use `Summarize-RealFormulaEvaluation.ps1` to validate and rebuild this summary.

**Limitations:** training-corpus overlap between the tested checkpoints and realFormula is unknown; do not describe this as a verified held-out split. The evaluation is ORT CPU only and uses current local artifacts. It does not establish OpenVINO, OpenCV DNN, TensorRT accuracy, cross-backend parity, speed, or general PaddleOCR formula accuracy. The poor exact-match/CER values indicate follow-up is needed on canonicalization/model suitability and long outputs; do not hide them with single-image smoke results.
