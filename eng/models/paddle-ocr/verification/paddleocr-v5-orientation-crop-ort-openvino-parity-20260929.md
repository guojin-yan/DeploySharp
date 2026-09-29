# PP-OCRv5 Mobile CLS 裁剪 ORT/OpenVINO 对齐验证（2026-09-29）

## 结论

Stage23 使用与 DET/REC 记录相同的 SROIE 10 页选择，每页取前 4 个词级四边形，共 40 个真实 crop。DeploySharp 的 OpenCV crop 预处理结果分别送入 PP-OCRv5 Mobile CLS，在 ONNX Runtime CPU 和 OpenVINO CPU 上完成方向分类解码。测试通过（1/1）：

| 指标 | 结果 |
| --- | ---: |
| 页面 / crop | 10 / 40 |
| 输入张量 SHA 不一致 | 0 |
| 分类类别不一致 | 0 |
| 拒绝状态不一致 | 0 |
| 接受方向不一致 | 0 |
| 最大输出绝对差 | `2.384185791015625e-6` |
| 最大输出平均绝对差 | `2.3543834686279297e-6` |
| 最大置信度差 | `2.3245811e-6` |

两后端的 40 个 CLS 输入 crop digest 完全相同：

`498f1f7c466a01b627d5116e1f598718004c5b24879b4ec0646f28ecaecac670`

这证明同一批真实词框经过当前 OpenCV crop 后，CLS 输入绑定、argmax/拒绝策略和方向结果在两个 CPU 后端上保持一致。它与同日 DET 原始概率图和 REC crop 记录共同覆盖了 PP-OCR 核心三阶段的一轮跨后端语义对齐。

## 固定输入与来源

- 数据集：`jsdnrs/ICDAR2019-SROIE`，revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`。
- manifest：`sroie-train-20260923T072455614249Z.jsonl`（SHA-256 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1`）与 `sroie-train-20260923T072550009854Z.jsonl`（SHA-256 `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`）。
- CLS 模型：`PP-OCRv5_mobile_cls.onnx`，SHA-256 `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2`。
- 每个 crop 使用 `OpenCvOcrImageInput.PrepareRecognitionBatch` 和 `PaddleOcrProfiles.CreateTextLineOrientationClassification`；输出比较包含原始 Float32、类别、拒绝标记、方向和置信度。

机器可读证据由测试通过 `DEPLOYSHARP_STAGE23_EVIDENCE_PATH` 写入忽略目录，例如：

`artifacts/paddleocr-crop-parity/stage23-sroie-v5-mobile-cls-ort-openvino-20260929.json`

## 复现

在 `DeploySharp` 仓库目录执行：

```powershell
$env:DEPLOYSHARP_STAGE23_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_STAGE23_EVIDENCE_PATH = Join-Path (Get-Location) 'artifacts\paddleocr-crop-parity\stage23-sroie-v5-mobile-cls-ort-openvino-20260929.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage23PaddleOcrOrientationCropParityTests' `
  --logger 'console;verbosity=minimal'
```

测试支持 `DEPLOYSHARP_STAGE23_DATASET_ROOT`、`DEPLOYSHARP_STAGE23_MODEL_ROOT`、`DEPLOYSHARP_STAGE23_MANIFEST_A`、`DEPLOYSHARP_STAGE23_MANIFEST_B` 和 `DEPLOYSHARP_STAGE23_CLS_MODEL` 覆盖默认路径。

## 证据边界

- SROIE 标注为词级框，测试没有方向人工真值，不能推出方向分类准确率或完整 OCR CER/WER。
- 只覆盖 PP-OCRv5 Mobile CLS、ORT CPU 和 OpenVINO CPU；没有扩展到 v4/v6、Server、OpenCV DNN、TensorRT 或 GPU。
- `0.01` 输出差和 `0.001` 置信度差是当前合同阈值，不是性能或精度排名；P50/P95 仍需独立性能协议。
