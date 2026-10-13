# PP-OCR v4/v5/v6 DET TensorRT 动态 Batch → DB Decoder（2026-10-12）

## 结论

在 Windows x64、RTX 3060 Laptop（`compute_86`）、TensorRT 11.0.0.114-cu12、CUDA 12.9、cuDNN 9.22 环境中，7 个已登记的 PP-OCR v4/v5/v6 DET ONNX 工件均完成了：

1. 使用 DeploySharp `TensorRtOnnxEngineBuilder` 从精确 ONNX 构建 Batch profile（`min=1,opt=max=2`）；
2. ORT CPU 与 TensorRT CUDA 均执行 `[2,3,736,736]` 输入；
3. 输出均保持 `[2,1,736,736]`，两个不同 ROI 的概率图行保持可区分；
4. 两个后端都直接进入 `PaddleDbTextDetectionDecoder`，返回 2 个结果，并按 `PreparedVisualInput.BatchFrames` 恢复源几何。

因此，本报告把 7 个精确 DET 工件的“raw Batch + DB Decoder 行/几何合同”记为 `pass-per-artifact`。这不是检测召回率、CER/WER、完整 `det → crop → cls → rec` 质量或正式吞吐结果。

## 测试矩阵

| 模型 | ONNX SHA-256（前 12 位） | 输出 | 最大绝对差 | 平均绝对差 | ORT 区域数 | TensorRT 区域数 | 状态 |
|---|---|---|---:|---:|---|---|---|
| PP-OCRv4 Mobile DET | `63e5e65c450d` | `2×1×736×736` | 0.0176042 | 1.53e-5 | `14,10` | `14,10` | ✓ |
| PP-OCRv4 Server DET | `1d6b24b3038d` | `2×1×736×736` | 0.0153852 | 5.59e-6 | `20,10` | `20,10` | ✓ |
| PP-OCRv5 Mobile DET | `1eb7b4f7ab65` | `2×1×736×736` | 0.0227206 | 2.49e-5 | `14,10` | `14,10` | ✓ |
| PP-OCRv5 Server DET | `0c4ff76f78fe` | `2×1×736×736` | 0.0516257 | 1.91e-5 | `14,10` | `14,10` | ✓ |
| PP-OCRv6 Tiny DET | `193bab7a04fc` | `2×1×736×736` | 0.0085074 | 1.95e-5 | `21,9` | `21,9` | ✓ |
| PP-OCRv6 Small DET | `d73e0058b7a8` | `2×1×736×736` | 0.0069050 | 2.43e-5 | `17,10` | `17,10` | ✓ |
| PP-OCRv6 Medium DET | `eb13b44b25bb` | `2×1×736×736` | 0.0030069 | 9.41e-6 | `14,10` | `14,10` | ✓ |

判据为最大绝对差 ≤ `0.10` 且平均绝对差 ≤ `0.001`；这是图级 provider parity 的保守边界，不是准确率阈值。PP-OCRv5 Server 的最大差约 `0.0516`，但均值差很小，且两行 DB 解码区域数与源几何均一致。

## 复现

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TRT_DET_PARITY_RUN_EXTERNAL = '1'
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'

dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore `
  --filter "FullyQualifiedName~PaddleOcrDetectorTensorRtDynamicBatchAllModelsIntegrationTests"
```

机器可读记录见 [`paddleocr-core-detectors-tensorrt-dynamic-batch-decoder-20261012.json`](paddleocr-core-detectors-tensorrt-dynamic-batch-decoder-20261012.json)。测试输入为 `E:\Data\ocr\demo_1.jpg`，左右两个 ROI 组成 Batch=2；Engine SHA、输出 SHA 和完整模型 SHA 均在 JSON 中记录。

## 边界与后续

- 仅对本报告列出的 7 个 ONNX、Windows RTX 3060 Laptop 和 TensorRT 11.0.0.114/cu12 成立；不外推到其它 TensorRT 版本、GPU 或预构建 Engine。
- 没有人工文字真值，因此不能从该测试宣称检测召回、识别准确率、CER/WER 或端到端 OCR 质量。
- 仍需独立完成 `det → crop → cls → rec` TensorRT 完整流水线、多图质量集、P50/P95 性能协议和跨设备 Engine 可移植性。
