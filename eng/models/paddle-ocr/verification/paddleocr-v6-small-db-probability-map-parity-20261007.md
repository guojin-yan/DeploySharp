# PP-OCRv6 Small DB 概率图跨后端差异

日期：2026-10-07。本次对既有 `sample-003` 24 页 HierText 固定 holdout 做 ORT CPU 与 OpenVINO CPU 原始 DB 概率图数值对照，目的是解释两个后端输出 SHA 不同，不选择/调整解码参数，也不构成精度或性能结论。

## 复现合同

- 模型：`paddleocr/ppocrv6/small-det`，SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`。
- 数据：本地 `F:\OCRBenchmarkTesting` 的 `hiertext-validation-sample-003.jsonl`，SHA-256 `f6f5500856868ee68699c88a8f941eaca8cfb06d9252c4121be75a89a6a7db3e`；图像和标签未复制到仓库。
- 每页仅解码/预处理一次。相同 Float32 tensor 实例顺序提交给两个 CPU Session，并在每次推理后复查其 SHA；24 个输入张量 SHA 均逐图只对应同一份共享输入。
- 输出要求 Float32，shape 和元素数逐页相同；直接对原始概率图逐元素计算绝对差，以及默认 DB 概率阈值 `0.3` 下的跨阈值像素。
- 测试命令：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_DB_SWEEP = "1"
$env:DEPLOYSHARP_PADDLEOCR_DB_SWEEP_OUTPUT = (Join-Path (Get-Location) "eng/models/paddle-ocr/verification")
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release --no-restore `
  --filter "FullyQualifiedName~PpOcrV6SmallDbProbabilityMapsAreComparedOnTheSameHoldoutInputs"
```

## 结果

| 指标 | 结果 |
|---|---:|
| 图像数 | 24 |
| Float32 元素比较数 | 19,398,656 |
| 最大绝对差 | `8.5353851e-5` |
| 平均绝对差 | `9.6955643e-8` |
| 数值不完全相同元素 | 19,186,410 |
| `p >= 0.3` 跨阈值像素 | 3（`1.5465e-7`，约 `0.0000155%`） |
| ORT>=0.3、OpenVINO<0.3 | 3 |
| ORT<0.3、OpenVINO>=0.3 | 0 |
| 输出 SHA 不同的图像 | 24/24 |

两后端概率图不是 bitwise 相同，且多数元素有微小数值漂移；但该固定样本上最大差低于 `8.54e-5`，默认概率阈值 mask 仅有 3 个像素不同。因此此前 raw output SHA 不同不能推导出实质性的 DB 阈值分割差异，也不能声称两后端张量完全相等。

## 边界与机器报告

这是单一模型、单一主机、CPU 设备和 24 页 smoke holdout 的数值诊断。阈值 mask 差异不是文本框 IoU、召回率、CER/WER 或用户感知质量；不覆盖 GPU、其他模型版本或设备，也不为 `unclipRatio=1.30` 的默认化提供额外质量证据。模型阈值敏感性及其独立 holdout 结果见[对应报告](paddleocr-v6-small-db-threshold-sensitivity-20261007.md)。

逐图输入/输出 SHA、shape、max/mean 差、逐阈值计数和运行元数据见[机器报告](paddleocr-v6-small-db-probability-map-parity-20261007T134952439Z.json)。opt-in 集成测试为 `1/1` 通过，用例位于 `tests/DeploySharp.Visual.OpenCV.Tests/PaddleOcrDbThresholdSensitivityIntegrationTests.cs`。
