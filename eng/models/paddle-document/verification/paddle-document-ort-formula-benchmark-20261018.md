# Paddle formula models — ONNX Runtime CPU formal benchmark (2026-10-18)

This report records one fixed official PaddleX formula image with the six exact local ONNX exports on the JYPPX Windows host. Each model used one ONNX Runtime CPU session and one prepared input, with **5 warm-up calls + 50 measured calls**. The measured interval includes synchronous `session.Run` and `PaddleDocumentFormulaDecoder`; image decode/preprocessing, model/session creation and report writing are excluded. P50/P95 use nearest-rank percentiles.

| Model | Input | Output tokens | P50 (ms) | P95 (ms) | Mean (ms) |
| --- | ---: | ---: | ---: | ---: | ---: |
| PP-FormulaNet Plus-S | 384×384 | 197 | 390.54 | 617.81 | 426.55 |
| PP-FormulaNet Plus-M | 384×384 | 197 | 2,421.44 | 2,728.27 | 2,445.04 |
| PP-FormulaNet Plus-L | 768×768 | 197 | 4,686.38 | 5,013.51 | 4,716.15 |
| PP-FormulaNet-S | 384×384 | 213 | 425.34 | 462.82 | 429.87 |
| PP-FormulaNet-L | 768×768 | 197 | 4,644.89 | 5,421.60 | 4,751.15 |
| UniMERNet | 672×192 | 208 | 11,796.76 | 12,938.26 | 11,918.61 |

All 300 measured invocations preserved each model's token-count, token-ID SHA-256, EOS-completion and decoded-LaTeX SHA-256 contract. That demonstrates repeatable execution for these exact exports and this host; it does **not** establish formula recognition accuracy or mathematical equivalence. The separate realFormula quality gate remains blocked: the benchmark uses a single official sample and does not alter its 121-sample CER/exact-match findings.

## Device and runtime

- Windows `10.0.26200`, .NET `10.0.12`, x64, 16 logical processors, AMD Ryzen family 25 model 80.
- ONNX Runtime CPU, `Microsoft.ML.OnnxRuntime 1.28.0`.
- Active Windows power plan was Balanced. This benchmark did not configure a CPU frequency lock and did not capture clock telemetry; latency is a host observation, not a locked-frequency ranking.
- Library source revision: `156568db53ae8afb5a907db94c9e9e4d67936f4e`. The benchmark harness was a working-tree file at run time; its SHA-256 is recorded in the JSON.

## Reproduction

Ensure the six ONNX files and official PaddleX `inference.yml` tokenizers are under `E:\Model\PaddleDocument\onnx` and `E:\Model\PaddleDocument\source`, and the official validation image is at `E:\Model\PaddleDocument\validation\general_formula_rec_001.png`. Then run:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_FORMULA_ORT_BENCHMARK = '1'
$env:DEPLOYSHARP_SOURCE_REVISION = (git rev-parse HEAD).Trim()
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --filter 'FullyQualifiedName~PaddleDocumentOrtFormulaBenchmarkTests'
```

The full machine-readable report contains model/input/tokenizer hashes, output fingerprints, all 50 measured samples per model and the exact execution boundary: [JSON](paddle-document-ort-formula-benchmark-20261018.json).
