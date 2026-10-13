# FormulaNet / UniMERNet ORT CUDA bounded parity

## Scope

This record covers the six exact formula ONNX artifacts in the local `PaddleDocument` model root:

- `pp-formulanet-plus-s`
- `pp-formulanet-plus-m`
- `pp-formulanet-plus-l`
- `pp-formulanet-s`
- `pp-formulanet-l`
- `unimernet`

The probe compares ONNX Runtime CPU and the opt-in ONNX Runtime CUDA provider on the same prepared `realFormula` images. It checks raw output shape/value compatibility, token IDs and decoded LaTeX. It is an execution/parity record, not a formula accuracy score, TeX semantic-equivalence result, or performance benchmark.

## Runtime and protocol

| Field | Value |
| --- | --- |
| Host | Windows 10.0.26200 x64, RTX 3060 Laptop |
| Managed runtime | .NET 10.0.12 |
| ORT CUDA package | `Microsoft.ML.OnnxRuntime.Gpu.Windows` 1.23.2 |
| CUDA provider | CUDA 12.x local installation |
| Dataset | MathNet `realFormula` public manifest, external to the repository |
| Input | DeploySharp formula preprocessing and the model-specific input size |
| Test | `PaddleDocumentFormulaVariantIntegrationTests.SixFormulaModelsRunOnOrtCudaAndMatchCpuBounded` |

The CUDA package is an explicit restore variant. The default test graph remains on the repository CPU ORT line. Reproduce with:

```powershell
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIMIT = '3'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_MODELS = 'pp-formulanet-plus-s,pp-formulanet-plus-m,pp-formulanet-plus-l,pp-formulanet-s,pp-formulanet-l,unimernet'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_REPORT_PATH = 'E:\DeploySharpEvidence\formula-ort-cuda-six-models-3samples-20261014.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore --framework net10.0 `
  -p:DeploySharpFormulaOrtCuda=true `
  --filter 'FullyQualifiedName~SixFormulaModelsRunOnOrtCudaAndMatchCpuBounded'
```

## Results

The checked-in machine-readable result is [`formula-realformula-ort-cuda-20261014.json`](formula-realformula-ort-cuda-20261014.json). The final bounded run uses three manifest samples per model. In addition, `pp-formulanet-l` and `unimernet` were rerun independently after an earlier transient report showing invalid `Int64` token values.

| Model | CUDA/CPU tokens | CUDA/CPU LaTeX | Raw output | Status |
| --- | ---: | ---: | --- | --- |
| `pp-formulanet-plus-s` | 3/3 | 3/3 | comparable, max abs diff 0 | pass |
| `pp-formulanet-plus-m` | 3/3 | 3/3 | comparable, max abs diff 0 | pass |
| `pp-formulanet-plus-l` | 1/1 | 1/1 | comparable, max abs diff 0 | bounded pass; fresh process required |
| `pp-formulanet-s` | 3/3 | 3/3 | comparable, max abs diff 0 | pass |
| `pp-formulanet-l` | 1/1 | 1/1 | comparable, max abs diff 0 | bounded pass; fresh process required |
| `unimernet` | 1/1 | 1/1 | comparable, max abs diff 0 | bounded pass; fresh process required |

The initial six-model probe produced report rows with very large CUDA `Int64` values when autoregressive models were exercised repeatedly in one process. A fresh process per sample produces matching CPU/CUDA token IDs, LaTeX and raw outputs. The accepted evidence therefore remains single-sample process isolation for Plus-L, FormulaNet-L and UniMERNet; shared multi-sample process/session support is still an open runtime-lifecycle issue. The original test report writer also had a bug: incompatible raw shapes were represented by `Infinity`, which JSON cannot serialize. The test now records `rawOutputsComparable=false` and a nullable difference instead of failing report serialization or silently treating incompatible tensors as equal.

## Boundaries

- This closes a bounded ORT CUDA execution/parity check only. It does not close the existing formula quality gate: the 121-image realFormula CPU reports still show low exact/CER metrics and are not a release accuracy claim.
- It does not change the current OpenVINO or OpenCV DNN formula `unsupported` entries, and it does not change the TensorRT 10.11/11 Builder importer blockers. No TensorRT Engine, formula Decoder or TensorRT performance result is implied.
- The result is tied to the exact ONNX/tokenizer SHA values written to the JSON and to the local ORT/CUDA runtime. Other CUDA versions, GPUs, providers or exported graphs require a fresh run.
- No formula reference LaTeX is copied into the repository; the external manifest remains the source of labels.
