# PP-OCR 核心检测器中间张量跨后端证据

> 生成时间：2026-09-30；源码：`3f9d8e2c4eb1b660ef2337018044db20485a05fd`。本报告验证检测器输入合同、输出形状和原始 DB 概率图的一致性，不代表检测召回率、OCR 准确率或 CER/WER。

## 范围

- 输入：`E:\Data\ocr\demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg`，每张图均记录 SHA-256。
- 模型：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 个 DET 工件。
- 后端：ONNX Runtime CPU 与 OpenVINO CPU。
- 前处理：DeploySharp OpenCV 输入工厂和 PP-OCR 官方检测预处理；两后端使用同一输入张量。
- 比较：逐元素比较检测器原始 Float32 输出，阈值为最大绝对差 `0.01`，同时记录最大均值绝对差。

## 汇总

| 项目 | 结果 |
| --- | ---: |
| 检测器 | 7 |
| 图片 | 3 |
| 对照组合 | 21 |
| 输入 SHA 不一致 | 0 |
| 输出形状不一致 | 0 |
| 最大绝对差 | `0.0067548752` |
| 最大均值绝对差 | `0.00000138022` |
| 阈值内通过 | 21/21 |

最大漂移来自 PP-OCRv4 server 的 `demo_3.jpg`，仍低于约定的 `0.01` 阈值。其余模型的最大绝对差均不超过 `0.000124`。

## 模型级结果

| 模型 | 输入/形状不一致 | 最大绝对差 | 最大均值绝对差 | 结果 |
| --- | ---: | ---: | ---: | :---: |
| PP-OCRv4 mobile | 0 / 0 | `0.0001184` | `4.29e-8` | pass |
| PP-OCRv4 server | 0 / 0 | `0.0067549` | `1.38e-6` | pass |
| PP-OCRv5 mobile | 0 / 0 | `0.0000553` | `8.09e-8` | pass |
| PP-OCRv5 server | 0 / 0 | `0.0001236` | `5.73e-8` | pass |
| PP-OCRv6 tiny | 0 / 0 | `0.0000685` | `1.38e-7` | pass |
| PP-OCRv6 small | 0 / 0 | `0.0000279` | `1.49e-7` | pass |
| PP-OCRv6 medium | 0 / 0 | `0.0000208` | `9.51e-8` | pass |

模型和图片 SHA、每张图片的输出形状以及 ORT/OpenVINO 输出 SHA 见机器可读记录：[paddleocr-core-det-intermediate-parity-20260930.json](paddleocr-core-det-intermediate-parity-20260930.json)。

## 结论边界

这项证据说明两后端在当前 3 张图片上使用相同前处理时，七个检测器的原始输出合同一致，可用于排查后续 DB 解码差异。它没有人工框标注，因此不能证明检测框召回率、IoU、端到端识别质量或跨后端文本一致性；正式质量仍需带标注的数据集评测。

复现测试：`PaddleOcrCoreDetectionIntermediateParityTests`。设置 `DEPLOYSHARP_PADDLEOCR_DET_PARITY_RUN_EXTERNAL=1`，必要时覆盖 `DEPLOYSHARP_PADDLEOCR_ROOT` 和 `DEPLOYSHARP_PADDLEOCR_DET_PARITY_EVIDENCE_PATH`，运行 `DeploySharp.Visual.OpenCV.Tests` 的该测试即可重新生成记录。
