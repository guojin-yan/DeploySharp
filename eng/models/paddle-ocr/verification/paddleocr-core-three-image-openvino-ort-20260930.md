# PP-OCR v4/v5/v6 三图跨后端流水线证据

> 生成时间：2026-09-30T07:24:49.1538467+00:00；源码：3a76f56927fe5b95f5deeecad2b30b681f5f3e23+working-tree。本报告只证明执行覆盖与跨后端输出摘要，不是准确率报告。

## 范围

- 输入：demo_1.jpg、demo_2.jpg、demo_3.jpg。
- 模型：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组。
- 后端：ONNX Runtime CPU 与 OpenVINO CPU。
- 每组记录区域数、已识别区域数、端到端耗时、整结果 SHA 和按区域纯文本 SHA。

## 汇总

| 项目 | 结果 |
| --- | ---: |
| 组合数 | 21 |
| OpenVINO 完整执行 | 21/21 |
| ORT 完整执行 | 21/21 |
| 区域数一致 | 21/21 |
| 已识别数一致 | 21/21 |
| 纯文本 SHA 一致 | 21/21 |

## 逐组合结果

| 图片 | 模型 | ORT 区域/识别 | OpenVINO 区域/识别 | 纯文本 SHA | ORT ms | OpenVINO ms |
| --- | --- | ---: | ---: | :---: | ---: | ---: |
| demo_1.jpg | v4-mobile | 16/16 | 16/16 | yes | 608.974 | 759.504 |
| demo_1.jpg | v4-server | 16/16 | 16/16 | yes | 2371.277 | 2925.645 |
| demo_1.jpg | v5-mobile | 16/16 | 16/16 | yes | 807.37 | 1062.694 |
| demo_1.jpg | v5-server | 16/16 | 16/16 | yes | 1766.877 | 2737.719 |
| demo_1.jpg | v6-tiny | 16/16 | 16/16 | yes | 314.783 | 554.512 |
| demo_1.jpg | v6-small | 16/16 | 16/16 | yes | 859.986 | 868.128 |
| demo_1.jpg | v6-medium | 16/16 | 16/16 | yes | 1796.302 | 2199.068 |
| demo_2.jpg | v4-mobile | 14/14 | 14/14 | yes | 443.071 | 508.479 |
| demo_2.jpg | v4-server | 13/13 | 13/13 | yes | 1252.977 | 1452.092 |
| demo_2.jpg | v5-mobile | 14/14 | 14/14 | yes | 402.1 | 536.674 |
| demo_2.jpg | v5-server | 14/14 | 14/14 | yes | 909.336 | 1118.292 |
| demo_2.jpg | v6-tiny | 14/14 | 14/14 | yes | 178.583 | 283.297 |
| demo_2.jpg | v6-small | 13/13 | 13/13 | yes | 360.064 | 439.904 |
| demo_2.jpg | v6-medium | 13/13 | 13/13 | yes | 782.644 | 901.713 |
| demo_3.jpg | v4-mobile | 10/10 | 10/10 | yes | 598.646 | 440.791 |
| demo_3.jpg | v4-server | 10/10 | 10/10 | yes | 2782.619 | 2255.436 |
| demo_3.jpg | v5-mobile | 10/10 | 10/10 | yes | 645.261 | 635.275 |
| demo_3.jpg | v5-server | 10/10 | 10/10 | yes | 2082.779 | 1849.121 |
| demo_3.jpg | v6-tiny | 10/10 | 10/10 | yes | 268.653 | 312.003 |
| demo_3.jpg | v6-small | 10/10 | 10/10 | yes | 589.975 | 600.542 |
| demo_3.jpg | v6-medium | 10/10 | 10/10 | yes | 1533.958 | 1367.784 |

## 边界

输入没有人工逐字真值，因此不能从本报告计算 CER/WER、检测召回率或识别准确率；单次 elapsedMs 也不替代正式 5 次预热 + 50 次测量。v6 明确复用 PP-OCRv5 mobile CLS。机器可读完整记录见 [paddleocr-core-three-image-openvino-ort-20260930.json](paddleocr-core-three-image-openvino-ort-20260930.json)。

复现脚本：eng/models/paddle-ocr/scripts/Invoke-PaddleOcrCoreThreeImageEvidence.ps1。
