# PP-OCRv5 Mobile DET 中间输出 ORT/OpenVINO 对齐验证（2026-09-29）

## 结论

Stage22 使用 `F:\OCRBenchmarkTesting` 中固定的 SROIE 分发版本，对 10 张真实收据页执行 PP-OCRv5 Mobile DET 的 OpenCV 官方预处理，然后分别运行 ONNX Runtime CPU 和 OpenVINO CPU，直接比较 detector 原始概率图。测试通过（1/1）：

| 指标 | 结果 |
| --- | ---: |
| 页面 | 10/10 |
| 输入张量 SHA 不一致 | 0 |
| 输出 shape 不一致 | 0 |
| 输出 shape | `[1,1,1024,512]`（10/10） |
| 最大输出绝对差 | `3.491640090942383e-4` |
| 最大输出平均绝对差 | `2.687820332691469e-7` |

两后端 10 页输入张量 digest 完全相同：

`dd7778531a69abd5dc14f145f6638bf18a28605b54956afa2c37c6ef80368aef`

ORT 与 OpenVINO 的原始输出 digest 不同是后端浮点实现差异；逐元素漂移在测试合同的 `0.01` 上限内。该结果与 [REC crop parity 记录](paddleocr-v5-recognition-crop-ort-openvino-parity-20260929.md) 一起覆盖了真实多页样本的 DET 输入/输出和识别 crop 输入/输出对齐链路。

## 固定输入与来源

- 数据集：`jsdnrs/ICDAR2019-SROIE`，revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`。
- manifest：`sroie-train-20260923T072455614249Z.jsonl`（SHA-256 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1`）与 `sroie-train-20260923T072550009854Z.jsonl`（SHA-256 `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`）。
- DET 模型：`PP-OCRv5_mobile_det.onnx`，SHA-256 `1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039`。
- 每页使用 `OpenCvStage19Preprocessing.CreatePaddleOcrOfficialInferenceDetectionOptions`；模型输出不经过 DB contour/NMS 解码，直接比较 `fetch_name_0` 的 Float32 概率图。

机器可读证据由测试通过 `DEPLOYSHARP_STAGE22_EVIDENCE_PATH` 写入忽略目录，例如：

`artifacts/paddleocr-crop-parity/stage22-sroie-v5-mobile-det-ort-openvino-20260929.json`

该 JSON 只包含路径、hash、shape 和摘要，不提交数据集图像或预测文件。

## 复现

在 `DeploySharp` 仓库目录执行：

```powershell
$env:DEPLOYSHARP_STAGE22_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_STAGE22_EVIDENCE_PATH = Join-Path (Get-Location) 'artifacts\paddleocr-crop-parity\stage22-sroie-v5-mobile-det-ort-openvino-20260929.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage22PaddleOcrDetectionIntermediateParityTests' `
  --logger 'console;verbosity=minimal'
```

测试支持 `DEPLOYSHARP_STAGE22_DATASET_ROOT`、`DEPLOYSHARP_STAGE22_MODEL_ROOT`、`DEPLOYSHARP_STAGE22_MANIFEST_A`、`DEPLOYSHARP_STAGE22_MANIFEST_B` 和 `DEPLOYSHARP_STAGE22_DET_MODEL` 覆盖默认路径。

## 证据边界

- 这是 DET 原始概率图的跨后端输入/输出对齐，不是文本检测 precision/recall、IoU、CER/WER 或官方 SROIE 成绩。
- 只覆盖 PP-OCRv5 Mobile、ORT CPU 和 OpenVINO CPU；没有扩展到 v4/v6、Server、OpenCV DNN、TensorRT 或 GPU。
- SROIE manifest 为词级标注，且本测试不使用标注做检测评分；完整 DET→crop→CLS→REC 质量仍受既有 smoke 记录的标注粒度和数据来源限制。
- `0.01` 是当前中间输出合同阈值，不代表某个后端更快或更准；P50/P95 必须由独立统一性能协议采集。
