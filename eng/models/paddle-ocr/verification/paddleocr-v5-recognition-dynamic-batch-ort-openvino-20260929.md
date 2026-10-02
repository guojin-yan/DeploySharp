# PP-OCRv5 Mobile REC 动态 Batch ORT/OpenVINO 验证（2026-09-29）

## 结论

Stage24 使用 SROIE 10 页的前 4 个词框，每页将 4 个不同原始宽度的四边形 crop 统一填充到该页最大目标宽度后，一次提交给 PP-OCRv5 Mobile REC。ONNX Runtime CPU 和 OpenVINO CPU 均保留真实 batch 维度 `4`，测试通过（1/1）：

| 指标 | 结果 |
| --- | ---: |
| 页面 / batch / crop | 10 / 4 / 40 |
| 输入张量 SHA 不一致 | 0 |
| 输出 shape 不一致 | 0 |
| CTC 文本不一致 | 0 |
| 最大输出绝对差 | `1.48773193359375e-4` |
| 最大输出平均绝对差 | `6.204175646671456e-10` |
| 最大置信度差 | `7.867813e-6` |

两后端的 10 个 batch 输入 digest 完全相同：

`6536d57b33eb7fb52515fcbeec7ea634fc468de85d0474c47b7e6c38c05e16cc`

为使不同原始 crop 宽度能安全进入同一 batch，本轮为公开 `TextCropRequest` 增加 `WithTargetWidth(int)`。它复用已有 profile 宽度与最大像素校验，只允许在该 crop 的合法目标宽度到 `MaximumWidth` 范围内进行显式 padding，不改变透视几何或识别文本。

## 固定输入与来源

- 数据集：`jsdnrs/ICDAR2019-SROIE`，revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`。
- 两份固定 manifest：`sroie-train-20260923T072455614249Z.jsonl`（SHA-256 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1`）与 `sroie-train-20260923T072550009854Z.jsonl`（SHA-256 `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`）。
- REC 模型：`PP-OCRv5_mobile_rec.onnx`，SHA-256 `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`。
- 字典：`ppocrv5_dict.txt`，SHA-256 `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b`。
- 每页以 batch=4 调用 `OpenCvOcrImageInput.PrepareRecognitionBatch`；输出用 `TextRecognitionBatchResult` 逐项比较文本和 confidence。

机器可读证据由测试通过 `DEPLOYSHARP_STAGE24_EVIDENCE_PATH` 写入忽略目录，例如：

`artifacts/paddleocr-crop-parity/stage24-sroie-v5-mobile-rec-batch-ort-openvino-20260929.json`

2026-10-02 使用当前工作树重跑同一测试：`1/1` 通过，10 个 batch/40 个 crop 保留真实 batch=4，输入、shape 和文本不一致数均为 `0`；最大输出差 `1.4877319e-4`，最大置信度差 `7.867813e-6`。

## 复现

在 `DeploySharp` 仓库目录执行：

```powershell
$env:DEPLOYSHARP_STAGE24_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_STAGE24_EVIDENCE_PATH = Join-Path (Get-Location) 'artifacts\paddleocr-crop-parity\stage24-sroie-v5-mobile-rec-batch-ort-openvino-20260929.json'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~Stage24PaddleOcrRecognitionBatchParityTests' `
  --logger 'console;verbosity=minimal'
```

测试支持 `DEPLOYSHARP_STAGE24_DATASET_ROOT`、`DEPLOYSHARP_STAGE24_MODEL_ROOT`、`DEPLOYSHARP_STAGE24_MANIFEST_A`、`DEPLOYSHARP_STAGE24_MANIFEST_B`、`DEPLOYSHARP_STAGE24_REC_MODEL` 和 `DEPLOYSHARP_STAGE24_DICT` 覆盖默认路径。

## 证据边界

- 这是单 Session、batch=4 的动态输入和后端语义合同，不是多 Session 池、并发队列、吞吐 P50/P95 或 GPU 性能结果。
- 只覆盖 PP-OCRv5 Mobile REC、ORT CPU 和 OpenVINO CPU；v4/v6、Server、OpenCV DNN、TensorRT 和 GPU 需要独立验证。
- SROIE 词级框没有作为识别真值使用；文本一致性只说明两后端对同一输入的行为一致，不代表识别准确率。
