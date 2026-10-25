# PaddleOCR-VL 1.6 OpenCV and TensorRT component boundary

Date: 2026-10-25. Source HEAD at test time: `570134aee5303486cf2b3f5ad48f2c31378455cc` plus the uncommitted probe sources. The isolated test assembly SHA-256 is recorded in each raw report. Input is the fixed four-graph FP32 1.6 ONNX Bundle described in [the bundle report](paddleocr-vl-release-bundle-20261025.md). These tests do not establish a complete PaddleOCR-VL backend.

## Host and raw evidence

Windows 10.0.26200, .NET 10.0.12, RTX 3060 Laptop (6 GB), NVIDIA driver 576.02. OpenCV native version 5.0.0; TensorRT 10.11.0.33-cu12, CUDA 12.9, cuDNN 9.22 and matching `jyppxtrtbridge.dll`. Each graph ran in a fresh process. The full JSON, stdout/stderr and built plans remain outside the Git repository:

- `E:\DeploySharpEvidence\paddleocr-vl-opencv-importer-confirm-20261025\summary.json`
- `E:\DeploySharpEvidence\paddleocr-vl-backend-probes-matched-20261025\summary.json`
- `E:\DeploySharpEvidence\paddleocr-vl-component-parity-final-20261025\summary.json`

The raw summary SHA-256 values for each graph report are shown below. This checked-in report is a compact index, not a replacement for the native diagnostics or a redistributable TensorRT engine.

| Graph | OpenCV DNN 5.0 original ONNX import | OpenCV report SHA-256 | TensorRT 10.11 Builder | TensorRT component inference |
| --- | --- | --- | --- | --- |
| embedding | imported, no inference | `ac24fef6817f4b10a0ab849c14b8e79e1a5527e9f1ba98f111239dc53417f67c` | Engine built, `cf9f4cd171addd4735b207293e08656d7b22162204322cbc21ead6198245e2f9` | `[1,1]` Int64 token input, `[1,1,1024]` Float32 output; exact ORT CPU output, max absolute difference `0` |
| head | imported, no inference | `43117496ddbb0418e87ea20959a815c3052dbf1875985e9fa93623d377fb44ff` | Engine built, `4478473aae4fea9a9a3f72f06da30979fa0f9dab4ca49de08794b048a02e103d` | Deterministic `[1,1,1024]` Float32 input, `[1,1,103424]` logits; max absolute difference `6.985e-9`, same argmax, 0 elements outside tolerance |
| vision | failed at dynamic `Shape` ONNX importer node | `1a0f0379ee1aebc26d18f47f6f147597fc2e163268da99e581786fae8727c944` | Engine built, `cd94f62e4c3582920eec37fcd916c597f77e5c0eda105428dbada5b58bcbdfd7` | Official `ocr-demo-1` patches `[36,36,3,14,14]`; output `[324,1024]`; maximum absolute difference `0.000342369`, RMSE `8.09e-6`, 0 elements outside tolerance |
| decoder | failed at dynamic `Shape` ONNX importer node | `8bc094ee2dddfb68f4130591854641d70152f7ced43ecce982f5d7c1d05727ea` | Engine built with positive-length past-KV profile, `928fbbfa54686a8f3a0b7f830a08c009dedd64041f20e83c0e8f23862b50e9db` | Synthetic one-token query and one-token past KV; hidden state plus all 36 present caches compared, 19,456 elements; maximum absolute difference `0.000553131`, RMSE `3.22e-5`, 0 elements outside tolerance |

The final TensorRT component JSON report SHA-256 values are embedding `4713a28a9e2f3633ab562b5646b618f4e45bc9c48afc6cde42f2adb614673981` and head `574f3b9200da2720f4e419a15cf6851d4063a30fd9d7a3078ee0a3297b4b3536`.
Follow-up component reports are `E:\DeploySharpEvidence\paddleocr-vl-vision-parity-20261025\TensorRT-vision\probe.json` (SHA-256 `a5b366f0bba4a9bba2bc48fb4734aa4f7dcd86a90c1a6e7159db98b488e01f47`) and `E:\DeploySharpEvidence\paddleocr-vl-decoder-positive-kv-20261025\TensorRT-decoder\probe.json` (SHA-256 `8826929d396343926fe6704f6b879d18ca53dc373d7c0252bc78e4ce6acf348b`). The vision input manifest and patch hash are pinned in its report; the decoder input is deterministic synthetic data, not an official generated-token fixture.

The OpenCV failure is the native `ONNXImporter::parseShape` assertion `!isDynamicShape` on the unmodified vision and decoder graphs. No altered ONNX graph, shape specialization or OpenCV graph-parity path was validated. Imported embedding/head graphs were not forwarded through the production OpenCV adapter. The fixed four-graph Bundle is therefore **not supported by OpenCV DNN 5.0**; this is an exact importer result, not a statement about all future OpenCV versions or rewritten exports.

The TensorRT Builder produced four nonempty plans, and the production TensorRT Session loaded and ran each component. All four outputs were compared to the same original ONNX graphs through ONNX Runtime CPU. The declared element tolerance was `abs(error) <= 0.001 + 0.001 * abs(reference)` for embedding/head/decoder and `0.003 + 0.001 * abs(reference)` for vision; all comparisons passed. This remains component evidence only. The decoder test starts with one past token, while the production generator requires a zero-length KV cache on its first step. The current managed profile/session contract rejects that prefill shape, and native zero-length profile acceptance has not been established. There is no TensorRT four-graph generation, EOS/token/text parity, or performance claim. The support matrix remains `△` for TensorRT.

## Reproduction

Use the fixed local bundle and a matching native runtime. The probe script builds its test project and writes an isolated JSON report and logs for every graph. Use a fresh output directory for each invocation.

```powershell
./eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlBackendProbe.ps1 `
  -BundleRoot 'E:\Model\PaddleOCR-VL\bundle\paddleocr-vl-1.6' `
  -OutputRoot 'E:\DeploySharpEvidence\vl-opencv-recheck' `
  -Backend OpenCV -Graph embedding,head,vision,decoder

./eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlBackendProbe.ps1 `
  -BundleRoot 'E:\Model\PaddleOCR-VL\bundle\paddleocr-vl-1.6' `
  -OutputRoot 'E:\DeploySharpEvidence\vl-trt-builder-recheck' `
  -Backend TensorRT -Graph embedding,head,vision,decoder -TensorRtApi 10 `
  -TensorRtRoot 'D:\Program Files\TensorRT-10.11.0.33-cu12' `
  -CudaRoot 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' `
  -CudnnRoot 'D:\Program Files\cuDNN-9.22.0-cuda12.9' `
  -NativeBridgeRoot 'C:\Users\guoji\.nuget\packages\jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge\4.0.0\runtimes\win-x64\native'

./eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlBackendProbe.ps1 `
  -BundleRoot 'E:\Model\PaddleOCR-VL\bundle\paddleocr-vl-1.6' `
  -OutputRoot 'E:\DeploySharpEvidence\vl-trt-component-recheck' `
  -Backend TensorRT -Graph embedding,head,vision,decoder -ProbeMode ComponentInference `
  -EngineRoot 'E:\DeploySharpEvidence\vl-trt-builder-recheck' `
  -FixtureRoot 'E:\DeploySharpEvidence\paddleocr-vl-processor-20261024\managed-processor-fixtures-20261024' `
  -TensorRtApi 10 -TensorRtRoot 'D:\Program Files\TensorRT-10.11.0.33-cu12' `
  -CudaRoot 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9' `
  -CudnnRoot 'D:\Program Files\cuDNN-9.22.0-cuda12.9' `
  -NativeBridgeRoot 'C:\Users\guoji\.nuget\packages\jyppx.tensorrt.csharp.api.runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.bridge\4.0.0\runtimes\win-x64\native'
```

The plans are specific to the GPU/runtime combination and are not Release assets. The nine ONNX Bundle assets remain locally staged, not remotely downloadable from `models-paddleocr` as of this report.
