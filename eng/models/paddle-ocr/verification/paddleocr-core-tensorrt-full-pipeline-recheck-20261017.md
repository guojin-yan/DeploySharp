# PP-OCR v4/v5/v6 TensorRT 10.11 full pipeline recheck (2026-10-17)

本次复核在同一 Windows 主机上，对当前本机可用的 7 组核心 PP-OCR 组合完整执行 `det → crop → cls/orientation → rec → merge`。每组 ONNX 由 DeploySharp `TensorRtOnnxEngineBuilder` 重新构建 TensorRT 10.11 Engine，再由 TensorRT provider 执行；同一输入同时运行 ORT CPU 作为有界文本参考。

## 环境与输入

- Windows 10.0.26200 x64，.NET 10.0.12。
- NVIDIA GeForce RTX 3060 Laptop GPU，driver 576.02；测试进程记录为 P0、graphics clock 2002 MHz、memory clock 7000 MHz。该单次运行没有锁频控制，不能作为设备极限或正式性能排名。
- TensorRT `10.11.0.33-cu12`、CUDA `12.9`、cuDNN `9.22`，使用匹配的 TensorRT 10.11 DeploySharp bridge。
- 输入图片：`E:\Data\ocr\demo_1.jpg`，SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`。
- 测试入口：`PaddleOcrAllCorePipelineTensorRtIntegrationTests.EveryCorePpOcrVariantRunsCompleteDetClsRecMergeOnTensorRt`。

复现前需设置 `DEPLOYSHARP_PADDLEOCR_TRT_ALL_FULL_RUN_EXTERNAL=1`，并为当前进程注入与 Engine Builder 相同的 `JYPPX_NATIVE_BRIDGE_PATH`、`JYPPX_TENSORRT_ROOT`、`JYPPX_CUDA_ROOT`、`JYPPX_CUDNN_ROOT`。报告命令和每个 ONNX/Engine SHA 保存在同目录 JSON 中。

## 结果

| 组合 | 区域 ORT/TRT | 识别 ORT/TRT | 有序文本 SHA | ORT 单次 ms | TensorRT 单次 ms |
| --- | ---: | ---: | --- | ---: | ---: |
| PP-OCRv4 mobile | 16/16 | 16/16 | 一致 | 1331.49 | 1624.42 |
| PP-OCRv4 server | 16/16 | 16/16 | 一致 | 5484.24 | 1568.23 |
| PP-OCRv5 mobile | 16/16 | 16/16 | 一致 | 705.33 | 1074.00 |
| PP-OCRv5 server | 16/16 | 16/16 | 一致 | 2576.71 | 2534.32 |
| PP-OCRv6 tiny | 16/16 | 16/16 | 一致 | 334.85 | 489.42 |
| PP-OCRv6 small | 16/16 | 16/16 | 一致 | 558.39 | 804.37 |
| PP-OCRv6 medium | 16/16 | 16/16 | 一致 | 2043.20 | 1754.24 |

测试汇总为 `7/7 passed`、`0 blocked`；每组 TensorRT 结果均与对应 ORT 结果返回相同区域数、识别数和有序文本 SHA。所有三阶段 Engine 的 ONNX/Engine SHA、完整输入和 runtime 信息见机器可读 [JSON](paddleocr-core-tensorrt-full-pipeline-recheck-20261017.json)。

## 边界

这是单台 Windows RTX 3060 主机、单张演示图片、单次计时的完整流水线执行与文本 parity 证据，不是 CER/WER、检测召回率、正式 `5 warm-up + 50 measured` 性能、跨设备 Engine 可移植性或所有 TensorRT/CUDA 版本支持。ORT 与 TensorRT 的单次耗时不能替代稳定性能矩阵；正式性能应在锁频、固定通道/Batch 和重复协议下单独记录。
