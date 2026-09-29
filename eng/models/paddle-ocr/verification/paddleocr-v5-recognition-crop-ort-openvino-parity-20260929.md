# PP-OCRv5 Mobile 识别裁剪 ORT/OpenVINO 对齐验证（2026-09-29）

## 结论

Stage21 使用 `F:\OCRBenchmarkTesting` 中固定的 SROIE 公开分发数据，从 10 张收据页各取前 4 个词级四边形，经过 DeploySharp 的 OpenCV 真实裁剪与识别输入准备，分别送入 PP-OCRv5 Mobile REC 的 ONNX Runtime CPU 和 OpenVINO CPU。共完成 40 个 crop，测试通过（1/1）。

| 指标 | 结果 |
| --- | ---: |
| 页面 / crop | 10 / 40 |
| 输入张量 SHA 不一致 | 0 |
| 输出 shape 不一致 | 0 |
| CTC 解码文本不一致 | 0 |
| 最大输出绝对差 | `4.696846008300781e-4` |
| 最大输出平均绝对差 | `1.1983426722676186e-9` |
| 最大识别置信度差 | `1.3113022e-6` |

两后端的 40 个输入 crop digest 完全相同：

`67ea18e1aac5681d512ab708517a4315a64f11063d453f730b957020f8213ef9`

这证明了当前 OpenCV 四边形裁剪、REC 输入预处理、ORT/OpenVINO 输入绑定和 CTC 解码在这组真实样本上具有一致语义。输出浮点 digest 因后端数值实现不同而不同，但逐元素差异均在上述界限内。

## 固定输入与来源

- 数据集：`jsdnrs/ICDAR2019-SROIE`，revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`。
- 两份固定 manifest：
  - `sroie-train-20260923T072455614249Z.jsonl`，SHA-256 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1`；
  - `sroie-train-20260923T072550009854Z.jsonl`，SHA-256 `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`。
- REC 模型：`PP-OCRv5_mobile_rec.onnx`，SHA-256 `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`。
- 字典：`ppocrv5_dict.txt`，SHA-256 `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b`。
- 流程：每个 manifest page 读取前 4 个词级 polygon；`OpenCvOcrImageInput.PrepareRecognitionBatch` 为每个四边形生成 REC 输入；同一输入分别运行 ORT CPU 和 OpenVINO CPU，再比较 output shape、浮点输出、CTC 文本和 confidence。

原始机器可读证据（包含 40 个 crop 的输入/输出 hash、文本和置信度）由测试通过 `DEPLOYSHARP_STAGE21_EVIDENCE_PATH` 写入忽略目录，例如：

`artifacts/paddleocr-crop-parity/stage21-sroie-v5-mobile-ort-openvino-20260929.json`

该 JSON 不提交到仓库，也不包含数据集图像。

## 复现

在 `DeploySharp` 仓库目录执行：

```powershell
$env:DEPLOYSHARP_STAGE21_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_STAGE21_EVIDENCE_PATH = Join-Path (Get-Location) 'artifacts\paddleocr-crop-parity\stage21-sroie-v5-mobile-ort-openvino-20260929.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage21PaddleOcrRecognitionCropParityTests' `
  --logger 'console;verbosity=minimal'
```

测试支持 `DEPLOYSHARP_STAGE21_DATASET_ROOT`、`DEPLOYSHARP_STAGE21_MODEL_ROOT`、`DEPLOYSHARP_STAGE21_MANIFEST_A`、`DEPLOYSHARP_STAGE21_MANIFEST_B`、`DEPLOYSHARP_STAGE21_REC_MODEL` 和 `DEPLOYSHARP_STAGE21_DICT` 覆盖默认路径。

## 证据边界

- SROIE 这里使用的是词级框，测试验证的是 crop/REC 跨后端语义和数值对齐，不是文本行检测召回、CER/WER、官方 SROIE 排名或整体 OCR 准确率。
- 只覆盖 PP-OCRv5 Mobile REC、ORT CPU 与 OpenVINO CPU；没有扩展为 v4/v6、Server、OpenCV DNN、TensorRT 或 GPU 结论。
- 只取每页前四个词框，不能替代全页 DET→crop→CLS→REC 的多图质量评测；SROIE 数据来源、原始图像权利和官方划分一致性仍按既有 smoke 记录的限制处理。
- 浮点差异阈值是当前测试合同，不代表任何后端的性能优势。正式 P50/P95 和设备矩阵仍需使用统一性能协议单独采集。
