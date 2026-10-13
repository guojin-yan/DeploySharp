# Formula ORT CUDA lifecycle option probe (2026-10-14)

This is a targeted runtime-lifecycle probe, not a new formula quality or performance result. It uses `pp-formulanet-plus-l.onnx`, ORT CUDA 1.23.2, the same three `realFormula` samples and the same DeploySharp decoder as the bounded parity report.

| Configuration | Result |
| --- | --- |
| Default CUDA provider options | Reproduces invalid `Int64` values in the same-process three-sample run (`OverflowException` in the formula decoder). |
| `enable_cuda_graph=0;do_copy_in_default_stream=1;cudnn_conv_algo_search=HEURISTIC` | Same invalid `Int64` output; no parity pass. |
| `preferPreallocatedCpuOutputs=true` | Not applicable: the formula output sequence dimension is dynamic, so the adapter correctly keeps ORT's dynamic-output allocation path. No preallocation claim is made. |
| `enable_mem_arena=0` | Rejected by ORT 1.23.2 as an unknown CUDA provider option; the error is surfaced instead of being ignored. |

The new `OnnxRuntimeOptions` escape hatches copy and freeze provider/session dictionaries and apply them only when explicitly supplied. They preserve the default path and are intended for reproducible investigations. The probe does **not** establish that CUDA Graph, allocator, stream, or output-copy configuration is the root cause. Current status remains process-isolated bounded execution for Plus-L/L/UniMERNet and ordinary multi-sample CUDA Session support remains open.

The formula decoder now rejects an `Int64` value outside the CLR token-index range as a structured `DS-VISUAL` decode failure with the tensor index and value. It never silently clamps, wraps, or synthesizes an EOS token.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_LIMIT = '3'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_MODELS = 'pp-formulanet-plus-l'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_OPTIONS = 'enable_cuda_graph=0;do_copy_in_default_stream=1;cudnn_conv_algo_search=HEURISTIC'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_PREALLOCATE_CPU_OUTPUTS = '1'
$env:DEPLOYSHARP_PADDLE_FORMULA_ORT_CUDA_REPORT_PATH = 'E:\DeploySharpEvidence\formula-cuda-prealloc-plus-l.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-build --no-restore --framework net10.0 `
  -p:DeploySharpFormulaOrtCuda=true `
  --filter 'FullyQualifiedName~SixFormulaModelsRunOnOrtCudaAndMatchCpuBounded'
```

The external raw JSON is kept outside the repository; this summary records only the reproducible status and boundary.
