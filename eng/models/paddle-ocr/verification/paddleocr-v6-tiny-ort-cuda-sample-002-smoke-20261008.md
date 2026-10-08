# PP-OCRv6 Tiny ORT CUDA loader and execution smoke (2026-10-08)

This record isolates the first CUDA blocker found while expanding the seven-model CPU matrix. On `JYPPX` (RTX 3060 Laptop, driver 576.02, CUDA 12.9, cuDNN 9.2.0.82), the initial ORT CUDA attempt failed while loading `onnxruntime_providers_cuda.dll`: Windows reported that `cublasLt64_12.dll` was missing even though the file existed under CUDA 12.9.

The fix registers application-selected CUDA dependency directories with Windows `AddDllDirectory` before ONNX Runtime creates its CUDA session. It accepts the optional `DEPLOYSHARP_ONNXRUNTIME_CUDA_DLL_DIRECTORIES` path-list, registers `CUDA_PATH\\bin`, and discovers PATH entries containing cuDNN 8 or 9. The change is in `OnnxRuntimeNativePreflight` and is used only for the CUDA provider; CPU behavior is unchanged.

## Result

After rebuilding the benchmark, PP-OCRv6 Tiny completed one full `det -> crop -> rec -> merge` page on ORT CUDA:

| Item | Result |
|---|---:|
| Page execution | `1/1` |
| Failed / empty pages | `0 / 0` |
| Regions | `12` |
| Preprocess / DET / REC / total | `40.395 / 14.970 / 11.791 / 67.194 ms` |
| CUDA result text SHA-256 | `8e0d017562da59cc7ac0e2bea16cbb3b8d617739dfd005bded9eb1ade64f73c9` |
| Same-image ORT CPU text SHA-256 | `8e0d017562da59cc7ac0e2bea16cbb3b8d617739dfd005bded9eb1ade64f73c9` |
| Text sequence parity | `match` |

The CPU reference used the same v6 Tiny ONNX assets, image and recognition batch. Contract hashes intentionally differ because the backend/runtime is part of that contract. The complete machine-readable provenance, model/image hashes, loader error and boundaries are in the [JSON evidence](paddleocr-v6-tiny-ort-cuda-sample-002-smoke-20261008.json).

## Reproduction

```powershell
dotnet build .\tools\DeploySharp.PaddleOcrBenchmark\DeploySharp.PaddleOcrBenchmark.csproj -c Release --no-restore

& .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-002.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v6 -Variant tiny -Backend onnxruntime-cuda `
  -StartIndex 0 -MaxImages 1 -Warmup 0 -Iterations 1 `
  -BatchSize 16 -InferenceChannels 1 -MaximumRegions 1024 `
  -OverflowMode Clamp -PipelineTimeoutMs 60000 -ContinueOnFailure `
  -OutputDirectory 'artifacts/public-ocr-evaluation/my-v6-tiny-cuda-smoke'
```

If CUDA/cuDNN are installed outside the normal environment, set the directory list in the same PowerShell process before running:

```powershell
$env:DEPLOYSHARP_ONNXRUNTIME_CUDA_DLL_DIRECTORIES = 'D:\cuda\bin;D:\cudnn\bin'
```

This is a functional one-page smoke only. It does not close the CUDA quality matrix, establish a 5-warmup/50-iteration benchmark, or validate TensorRT.
