# Formula models: dynamic Batch execution evidence (2026-10-07)

## Scope and result

All six official formula ONNX exports with dynamic first-axis inputs were exercised with a real batch of two distinct prepared rows through DeploySharp's ORT CPU backend and the formula decoder. For every model, each batched row reached EOS and its token IDs and decoded LaTeX exactly matched the same row run independently at batch one. The test therefore confirms dynamic binding and decoder row isolation for these exact assets; it does not measure formula accuracy, throughput, or support on another backend.

The rows are the top and bottom bands of the single official `general_formula_rec_001.png` example. They are deliberately different execution inputs, but are not separately annotated equations and must not be interpreted as a quality set.

| Model | Input → output | Decoded tokens (row 0 / row 1) | Both EOS; exact single-run parity |
|---|---|---:|---|
| PP-FormulaNet Plus-S | `[2,1,384,384]` → `[2,180]` | 175 / 66 | Yes |
| PP-FormulaNet-S | `[2,1,384,384]` → `[2,1026]` | 197 / 1022 | Yes |
| PP-FormulaNet Plus-M | `[2,1,384,384]` → `[2,176]` | 174 / 142 | Yes |
| PP-FormulaNet Plus-L | `[2,1,768,768]` → `[2,175]` | 173 / 61 | Yes |
| PP-FormulaNet-L | `[2,1,768,768]` → `[2,1024]` | 1022 / 1022 | Yes |
| UniMERNet | `[2,1,192,672]` → `[2,788]` | 204 / 786 | Yes |

Each test also asserts that row inputs and decoded token sequences differ, preventing a duplicated-row result from passing as isolation evidence. The long sequences for FormulaNet-S/L are close to the decoder's bounded output length; reaching EOS is recorded for this exact run and does not establish behavior on longer formulas.

## Reproduction

From the repository root, with the official model files under `E:\Model\PaddleDocument\onnx` and tokenizers under `E:\Model\PaddleDocument\source`:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --configuration Release --no-restore `
  --filter 'FullyQualifiedName~DynamicBatchMatchesIndependentRunsOnOrtCpu' `
  --logger 'console;verbosity=minimal'
```

To retain each JSON report in the repository verification directory, set the model-specific report variable used by the corresponding test before running it. Exact model/tokenizer/image SHA-256 values and per-row hashes are recorded in the [Plus-S](paddle-document-formula-plus-s-dynamic-batch-ort-20261007.json), [FormulaNet-S](paddle-document-formula-net-s-dynamic-batch-ort-20261007.json), [Plus-M](paddle-document-formula-plus-m-dynamic-batch-ort-20261007.json), [Plus-L](paddle-document-formula-plus-l-dynamic-batch-ort-20261007.json), [FormulaNet-L](paddle-document-formula-net-l-dynamic-batch-ort-20261007.json), and [UniMERNet](paddle-document-unimernet-dynamic-batch-ort-20261007.json) machine-readable reports.

## Boundaries

- Backend: ONNX Runtime CPU only; OpenVINO formula import/parity remains unsupported for the six exact formula exports, while OpenCV DNN and TensorRT remain unverified.
- This is batch execution/row-isolation evidence, not a throughput measurement, labeled formula accuracy evaluation, or approval to mark other model/backend pairs as supported.
- Each batch result matched its corresponding independent run; results should not be generalized to different model hashes, preprocessing, hardware, or runtime versions.
