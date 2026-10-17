# PP-OCR v4/v5/v6 TensorRT 10.11 正式稳态性能复核（2026-10-17）

本报告把同一台 Windows RTX 3060 Laptop 上的七组核心 PP-OCR 完整流水线重新按正式协议执行：`det → crop → cls/orientation → rec → merge`。本次使用当前源码 revision `93771bc`（工作树有其它未提交文件，但运行器本身没有未记录的改动）、DeploySharp TensorRT 10.11 bridge 和本机已由 Builder 生成的精确 Engine。

## 协议与设备

- Windows 10.0.26200 x64，.NET 10.0.12，NVIDIA GeForce RTX 3060 Laptop GPU，driver 576.02。
- TensorRT `10.11.0.33-cu12`、CUDA `12.9`、cuDNN `9.22`，`compute_86` CUDA CTC argmax 路径启用。
- 输入：`E:\Data\ocr\demo_1.jpg`，SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`。
- 固定 `5` 次 warm-up、`50` 次 measured；prepared input reuse；识别 Batch `8`；阶段独立推理通道 `2`；每组 16 个检测区域，识别分两批。
- GPU 时钟没有锁定，因此只记录为该设备/runtime/profile 的稳态观察，不作硬件极限或跨设备排名。

## 结果

| 模型 | 状态 | P50 总耗时 (ms) | P95 总耗时 (ms) | 最快/最慢 (ms) | DET (ms) | REC (ms) |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| PP-OCRv4 Mobile | ✓ | 153.249 | 167.140 | 142.099 / 180.715 | 45.862 | 86.835 |
| PP-OCRv4 Server | ✓ | 199.291 | 230.132 | 184.568 / 250.029 | 91.397 | 86.417 |
| PP-OCRv5 Mobile | ✓ | 173.940 | 191.950 | 160.517 / 211.105 | 52.719 | 102.763 |
| PP-OCRv5 Server | ✓ | 427.819 | 533.568 | 368.915 / 578.751 | 154.919 | 215.413 |
| PP-OCRv6 Tiny | ✓ | 68.461 | 79.338 | 58.711 / 83.031 | 35.391 | 33.924 |
| PP-OCRv6 Small | ✓ | 130.262 | 156.516 | 109.326 / 168.143 | 52.816 | 80.132 |
| PP-OCRv6 Medium | ✓ | 276.124 | 293.220 | 255.090 / 300.721 | 106.398 | 171.317 |

七组均返回 16 个区域和 16 个识别结果；每组结果合同在 50 次测量内保持单一变体，文本 SHA 与同一运行中的参考合同一致。完整逐次 CSV、运行时程序集/模型/Engine SHA 和协议字段保存在本机 benchmark artifacts；汇总 JSON 见[机器可读报告](paddleocr-core-tensorrt10.11-steady-formal-current-20261017.json)，协议校验见[validation JSON](paddleocr-core-tensorrt10.11-steady-formal-current-20261017.validation.json)。

## 复现

使用 `tools/DeploySharp.PaddleOcrBenchmark/portable/Run-PaddleOcrBenchmark.ps1` 或直接运行 benchmark，并设置 TensorRT 10.11 bridge、CUDA/cuDNN 根目录、`DEPLOYSHARP_PADDLEOCR_TENSORRT_ENGINE_ROOT`，然后固定：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1'
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
```

引擎必须由当前目标 GPU、TensorRT 主版本和 CUDA 组合重新生成或明确提供匹配的 Engine；不能把本报告的 Engine 当作跨设备可移植资产。

## 边界

该报告关闭了七组核心 PP-OCR 在本机精确 TRT10.11 profile 下的正式稳态计时缺口，但不关闭 CER/WER、检测召回、冷启动、吞吐、锁频排名、跨设备 Engine 可移植性、PP-Structure 其它模型或所有 TensorRT/CUDA 版本支持。OpenCV DNN、OpenVINO、ORT CUDA 的性能结果必须引用各自协议报告，不能混合比较不同 Batch/输入口径。
