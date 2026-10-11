# PP-LCNet TensorRT dynamic-batch DeploySharp evidence (2026-10-11)

## Result

After registering the caller-selected TensorRT, CUDA and cuDNN directories with the Windows DLL loader before bridge initialization, the opt-in DeploySharp integration test completed the exact PP-LCNet table classifier with a real batch of two:

- DeploySharp TensorRT 10.11 / CUDA 12.9 / cuDNN 9.22 on Windows x64, RTX 3060 Laptop (`compute_86`)
- `x=[2,3,224,224]` → `fetch_name_0=[2,2]`
- both rows decoded as `wired_table`
- ORT-to-TensorRT raw-output maximum absolute difference: `5.5168e-5` and `6.3999e-4`
- configured parity tolerance: `0.01`
- engine built by `TensorRtOnnxEngineBuilder`, SHA-256 `c2d1fff5171797cf79016c02c6f9f5ec3440afbcc50d5a546c62c9796b28e395`, `10,929,788` bytes

This closes the previous **DeploySharp bridge/runtime initialization blocker for this exact TensorRT 10.11 combination**. It does not promote all PP-Structure TensorRT dynamic Batch combinations: orientation, seal, layout, cell, UVDoc, formula and Chart2Table still require their own exact engine and decoder evidence.

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '10'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<matching trt10.11 bridge>\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-10.11.0.33-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
dotnet test tests\DeploySharp.Visual.TensorRT.Tests\DeploySharp.Visual.TensorRT.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TableClassificationRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows'
```

The test uses `E:\Model\PaddleDocument\onnx\pp-lcnet-x1-0-table-cls.onnx` (SHA-256 `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`) and two non-overlapping regions from `table_recognition.jpg` (SHA-256 `acd113bb3a89b488941ee0962776a28e45897fa2802cd306ec3bb68d9043115c`). The regions are execution probes, not table labels; this report makes no accuracy or throughput claim.

The machine-readable record is [`paddle-document-table-classification-tensorrt-dynamic-batch-20261011.json`](paddle-document-table-classification-tensorrt-dynamic-batch-20261011.json).
