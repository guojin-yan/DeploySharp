# PP-OCR v4/v5/v6 OpenCV DNN 三图全流程对照

> 生成时间：2026-09-30；源码：本测试提交记录见计划执行日志。本报告只覆盖当前本机模型和输入，不代表公开数据集精度或正式性能排名。

## 范围与合同

- 模型：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组 DET+CLS+REC。
- 输入：`demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg`，路径为 `E:\Data\ocr`。
- 后端：OpenCV DNN CPU 与 ONNX Runtime CPU；两边使用同一 DeploySharp 前处理和相同模型/字典文件。
- 对照：真实 `det → crop → cls/orientation → rec → merge`，区域数和逐区域文本必须完全一致，置信度允许 `0.01`，多边形顶点允许 `0.5 px`。
- 运行：每个模型/图片组合一次完整调用，OpenCV 的单次耗时包含所有前后处理和单通道推理；不是 5 次预热 + 50 次的稳定性能协议。

## 汇总

| 项目 | 结果 |
| --- | ---: |
| 组合数 | 21 |
| OpenCV 全流程通过 | 21/21 |
| ORT 对照通过 | 21/21 |
| 文本 SHA/逐区域文本一致 | 21/21 |
| 坐标与置信度合同通过 | 21/21 |
| OpenCV importer 失败 | 0 |

## 模型级结果

| 模型 | 区域数（demo_1/2/3） | OpenCV 单次范围 ms | ORT 单次范围 ms | 文本/几何 |
| --- | --- | ---: | ---: | :---: |
| PP-OCRv4 mobile | 16 / 14 / 10 | 3748.910–3960.392 | 378.502–653.695 | 3/3 |
| PP-OCRv4 server | 16 / 13 / 10 | 10610.746–14888.078 | 1184.505–2744.647 | 3/3 |
| PP-OCRv5 mobile | 16 / 14 / 10 | 2290.994–3403.311 | 375.612–698.780 | 3/3 |
| PP-OCRv5 server | 16 / 14 / 10 | 6666.859–9567.456 | 929.233–2390.928 | 3/3 |
| PP-OCRv6 tiny | 16 / 14 / 10 | 690.159–958.563 | 201.476–298.641 | 3/3 |
| PP-OCRv6 small | 16 / 13 / 10 | 2038.179–2860.484 | 361.876–689.383 | 3/3 |
| PP-OCRv6 medium | 16 / 13 / 10 | 4877.222–7529.372 | 849.196–1655.871 | 3/3 |

完整的模型/图片/字典 SHA、逐组合文本 SHA、结果 SHA、耗时和异常字段见机器可读记录：[paddleocr-core-three-image-opencv-ort-20260930.json](paddleocr-core-three-image-opencv-ort-20260930.json)。

## 兼容性修正记录

本轮测试首先暴露了测试合同中的两个问题，均已在测试侧修正后重新运行：

1. v4 mobile DET 和 legacy CLS/REC 使用的输出 tensor 名称与 v5 不同，不能固定写成 `fetch_name_0`；测试现在从 catalog/profile 绑定精确输入输出名。
2. v4 legacy CLS 的输入是 `[1,3,48,192]`，PP-LCNet text-line CLS 是 `[1,3,80,160]`；测试现在使用 profile 的真实固定 shape，并将 OpenCV CLS/REC 运行限制为 batch=1。该限制只作用于 OpenCV 证据测试，不改变 ORT/OpenVINO 的动态 Batch 能力。

## 结论边界

这项结果证明在三张演示图上，当前七组模型可以由 OpenCV DNN 执行完整流水线，并与 ORT 产生相同的区域文本和几何结果。它没有人工逐字/逐框真值，因此不计算 CER/WER、召回率、IoU 或准确率；图片数量也不足以代表生产质量。OpenCV 的耗时远高于 ORT，主要用于兼容性和结果对照，正式性能需要独立锁频、预热和 5/50 协议。

复现：设置 `DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_RUN_EXTERNAL=1`，可覆盖 `DEPLOYSHARP_PADDLEOCR_ROOT`、`DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_IMAGE_ROOT` 和 `DEPLOYSHARP_PADDLEOCR_OPENCV_MATRIX_EVIDENCE_PATH`，运行 `PaddleOcrCoreOpenCvThreeImageParityTests`。
