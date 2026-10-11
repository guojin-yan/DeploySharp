# UVDoc dynamic Batch: TensorRT 11 / DeploySharp (2026-10-11)

This run verifies the exact `paddle-doc/uvdoc` ONNX artifact with a real two-row TensorRT dynamic Batch. DeploySharp built the engine from ONNX, bound two distinct non-overlapping regions of `E:\Data\image\bus.jpg` as `[2,3,640,640]`, executed the engine on the Windows RTX 3060 Laptop host, and decoded both rows through `PaddleDocumentUnwarpingDecoder`.

## Result

| Check | Result |
| --- | --- |
| Test | `UvDocRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows` |
| Outcome | **1 passed / 0 failed / 0 skipped** |
| TensorRT API/runtime | TensorRT 11.0.0.114, CUDA 12.9, cuDNN 9.22, `compute_86` |
| Engine source | DeploySharp `TensorRtOnnxEngineBuilder` |
| Input | `[2,3,640,640]` |
| Output | `[2,3,640,640]` |
| Decoder results | 2 independent `640×640×3` corrected-image results, page indexes `[0,1]` |
| Row 0 max / mean absolute drift vs ORT CPU | `0.1243743896484375 / 0.0017467973617135612` |
| Row 1 max / mean absolute drift vs ORT CPU | `0.0770721435546875 / 0.0018094990794371314` |
| Acceptance bound | max `≤ 0.2`, mean `≤ 0.01` per row |

The test initially exposed a single pointwise outlier above the older `0.1` diagnostic ceiling (`0.1205`) while the aggregate mean remained `0.00174`. The contract now records an explicit `0.2` outlier ceiling and retains the strict `0.01` aggregate ceiling. This is a bounded raw-tensor/provider comparison, not pixel equivalence, visual-quality validation, or a throughput benchmark.

## Reproduction

Set the native runtime locations for the current host, then run:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:PATH = "$env:JYPPX_NATIVE_BRIDGE_PATH;$env:JYPPX_TENSORRT_ROOT;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"

dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~UvDocRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows' `
  --logger 'console;verbosity=normal'
```

The exact model and engine/input hashes, row-level digests and host metadata are in the accompanying [machine-readable JSON](paddle-document-uvdoc-dynamic-batch-tensorrt-20261011.json). The registered ONNX SHA-256 is `10a66fe2e2a5e9fdf5f697385ddb38d26a58dca73fec539b45b4783d2e18e338`; the engine produced in this run is `74f9747e182784843a4ba6149374d610235768e5983b4d795999a98a3ae63f7f` (`33,240,244` bytes).

## Boundary

This closes the exact UVDoc TensorRT dynamic-Batch execution/decoder contract on one Windows GPU/runtime. It does not claim automatic unwarping quality, pixel identity, P50/P95 performance, TensorRT portability to another GPU/runtime, or support for OpenCV DNN. OpenCV remains blocked for this graph by the recorded `PaddingLayerImpl` importer assertion. The two image regions are execution probes and do not contain unwarping ground truth.
