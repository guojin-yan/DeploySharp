# FormulaNet-L token-diversity diagnostic

## Scope

This is a descriptive follow-up to the four-image FormulaNet-L generation-limit probe. It reads the existing raw token IDs; it does not run inference, change the model, decode new predictions, or define a production stopping rule. The selected traces use one ONNX export and ONNX Runtime CPU.

Generated tokens are the raw sequence after BOS and before EOS (when present). They are partitioned into non-overlapping 128-token windows. A window is flagged only for this exploratory report when it has at most four unique token IDs and a unique 4-gram ratio no greater than 0.10. Exact suffix repetition is searched only up to 64 tokens and reported when at least three cycles are present. These thresholds are descriptive, not a general degeneration detector, confidence score, or accuracy metric.

## Results

| Image | Raw IDs / EOS | First flagged window | Unique IDs / unique 4-grams | Exact terminal suffix | CER |
| --- | ---: | ---: | ---: | --- | ---: |
| `211110912-2.png` | 1,025 / none | 256 | 3 / 3 of 125 (2.4%) | none found | 924.00% |
| `220808471-63.png` | 220 / index 219 | none (one full window) | 25 / 80 of 125 (64.0%) | none found | 58.87% |
| `220800300-41.png` | 1,024 / index 1,023 | 0 | 3 / 3 of 125 (2.4%) | `[243,82,23]` × 340 | 971.43% |
| `211200471-3.png` | 1,024 / index 1,023 | 256 | 3 / 3 of 125 (2.4%) | none found | 1,047.93% |

The three low-diversity traces have very poor character CER in this selected set, while the shorter trace has lower CER and no flagged window. This is a hypothesis for broader investigation only: four purposively selected samples cannot establish causation, prevalence, or a model-wide relationship. EOS completion is not a correctness signal. In particular, do not synthesize EOS, suppress `missing-eos:sequence-may-be-truncated`, or treat a low-diversity flag as a user-facing quality verdict.

For `220800300-41.png`, the raw trace SHA-256 is `dfa93329bc557d03e56a0e8c6b6981d474d2d520320c682db14662d5045ca25d`; its terminal suffix is exactly 1,020 tokens (340 repetitions of the three-token suffix pattern). The original no-EOS sample has no exact short terminal period under the same 64-token search, despite its low-diversity window beginning at generated offset 256.

## Reproducibility

The checked-in [machine-readable report](formula-formulanet-l-token-diversity-20261007.json) records the model SHA, input file names and hashes, per-sample raw-token hashes, window summaries, and boundary. The source raw traces remain in the local external validation directory and are not included in the repository. Re-run with those reports using:

```powershell
C:\ProgramData\Anaconda3\python.exe eng/models/paddle-document/scripts/Analyze-FormulaTokenDiversity.py `
  --input-reports `
    E:\Model\PaddleDocument\validation\formula-formulanet-l-eos-token-trace-rerun-20261007.json `
    E:\Model\PaddleDocument\validation\formula-formulanet-l-generation-limit-multisample-20261007.json `
  --output E:\Model\PaddleDocument\validation\formula-formulanet-l-token-diversity-20261007.json
```

To regenerate the checked-in report, use the same command with the output path set to `eng/models/paddle-document/verification/formula-formulanet-l-token-diversity-20261007.json`.

**Boundary:** one FormulaNet-L export, four selected realFormula traces, and ORT CPU. No causal conclusion, dataset-level accuracy estimate, general detector claim, cross-backend result, or model-quality pass is implied.
