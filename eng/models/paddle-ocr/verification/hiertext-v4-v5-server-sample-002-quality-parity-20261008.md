# PP-OCRv4/v5 Server HierText sample-002 质量复核（2026-10-08）

本记录使用独立的 HierText `sample-002` 十页选集复核 PP-OCRv4 Server 和 PP-OCRv5 Server。六个组合都执行完整 `det -> crop -> optional cls -> rec -> merge` 流程：ONNX Runtime CPU、OpenVINO CPU 与 OpenCV DNN CPU 各跑两个模型。

它是对 [sample-003 24 页报告](hiertext-v4-v5-server-quality-parity-20261008.md) 的独立选集复核，不是把两次结果拼成发布准确率。数据、图像和预测仍保存在本机 `F:\OCRBenchmarkTesting`，来源审查完成前保持 smoke-only。

## 复现范围

- 数据源 revision：`70b6620b2b112597d8219e11eee9773a1403827c`，验证集 10 张图、537 个文本区域、17 条 `vertical=true` 行。
- 源 manifest SHA-256：`e74145b9a86ceca9bb386ab91eb8c07fdb8c1eb6e1f052580d2a2494167207b7`。
- 本轮 selected manifest SHA-256：`5f334b20afbdc9303c3bb9426867fa3087c19bb45f78c889d612b4d2ae10512c`。
- SlidingWindow overlap `0.2`、每区域最多 32 个窗口、最多 1,024 个区域、recognition 最大宽度 `320`、batch `16`、inference channels `1`。
- 每页 `1` 次 warm-up、`1` 次测量；计时不含模型加载。
- Windows x64、`.NET 10.0.12`、16 个逻辑处理器、ORT `1.23.2`、OpenVINO `2026.2.1`、OpenCV `5.0.0`，均使用 CPU provider。
- 本轮 benchmark assembly SHA-256：`a2a0b20f6a794a90f2f9349dd47865994160a3c1eca90cd62220da74c42f9320`；OpenCV 运行使用源 revision `bdaa063`，工作树仍包含其他未提交改动。

模型 SHA 与上一份 24 页报告相同，完整机器清单见 [JSON](hiertext-v4-v5-server-sample-002-quality-parity-20261008.json)。

## 质量结果

| 模型 / 后端 | 页 / 失败 / 空结果 | TP / FP / FN | 检测 F1 | matched CER / WER | end-to-end CER / WER | 总耗时 P50 / P95 (ms) |
|---|---:|---:|---:|---:|---:|---:|
| v4 Server / ORT CPU | 10 / 0 / 0 | 178 / 167 / 359 | 40.36% | 39.86% / 66.50% | 109.45% / 117.51% | 3430.371 / 5759.845 |
| v4 Server / OpenVINO CPU | 10 / 0 / 0 | 178 / 167 / 359 | 40.36% | 39.86% / 66.50% | 109.45% / 117.51% | 2386.694 / 3583.851 |
| v4 Server / OpenCV DNN CPU | 10 / 0 / 0 | 178 / 167 / 359 | 40.36% | 39.86% / 66.50% | 109.45% / 117.51% | 7304.825 / 11203.927 |
| v5 Server / ORT CPU | 10 / 0 / 0 | 195 / 209 / 342 | 41.45% | 46.15% / 64.58% | 110.79% / 117.82% | 2581.407 / 5230.704 |
| v5 Server / OpenVINO CPU | 10 / 0 / 0 | 196 / 208 / 341 | 41.66% | 46.12% / 64.73% | 110.37% / 117.67% | 2110.429 / 3841.165 |
| v5 Server / OpenCV DNN CPU | 10 / 0 / 0 | 195 / 209 / 342 | 41.45% | 46.15% / 64.58% | 110.79% / 117.82% | 6919.035 / 12336.641 |

v4 三个 CPU 后端的检测、文本和端到端指标完全相同；v4 OpenCV DNN 的总耗时明显高于另外两个后端。v5 的 OpenCV DNN 与 ORT 在该选集上逐页结果一致，而 OpenVINO 多匹配一个检测、少一个 FP/FN；这些都是十页 smoke 的局部观察，不能外推为某个后端更准确。

## 跨后端逐页对照

对每页按流水线输出顺序比较区域文本、区域数和对应 polygon：

| 模型 / 对照后端 | 页面文本序列一致 | 区域数一致 | 对应区域文本差异 | 区域总数 | 最大 polygon 坐标差 | 最大 confidence 差 |
|---|---:|---:|---:|---:|---:|---:|
| v4 / OpenVINO vs ORT | 10/10 | 10/10 | 0 | 345 / 345 | 0 px | `2.527e-5` |
| v4 / OpenCV DNN vs ORT | 10/10 | 10/10 | 0 | 345 / 345 | 0 px | `1.592e-5` |
| v5 / OpenVINO vs ORT | 9/10 | 10/10 | 1 | 404 / 404 | `1.47354 px` | `0.06779492` |
| v5 / OpenCV DNN vs ORT | 10/10 | 10/10 | 0 | 404 / 404 | 0 px | `1.210e-5` |

v5 唯一文本差异在 `hiertext-validation-ab6fb759938e462b` 的第 10 个对应区域（零基索引 `9`）：ORT 为 `cYanalowb`，OpenVINO 为 `400o`。两边区域数量仍相同；该页对应 polygon 也有较大的局部坐标漂移，因此本记录不把 v5 称为 bitwise 或几何完全 parity。

## 长文本与边界

这个十页选集中没有进入 scorer 的 `>=32` 字符、`>=128` 字符或 `>=3200` 字符自然行桶。因此它不能关闭 A2，也不能替代 sample-003 报告中的长文本边界。两份选集都不是连续超长文本验收集。

本记录覆盖的是两个 Server 模型在三种 CPU 后端的精确组合；没有覆盖 ONNX Runtime CUDA 或 TensorRT，也不能外推到其他 PP-OCR 版本、GPU 或设备。计时仅有一次测量，不是正式 5/50 性能排名。

## 复现

```powershell
$run = 'artifacts/public-ocr-evaluation/hiertext-validation-sample-002-v4-v5-server-20261008/v4-server-ort'
& .\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1 `
  -ManifestPath 'F:\OCRBenchmarkTesting\data\annotations\manifests\hiertext-validation-sample-002.jsonl' `
  -DatasetRoot 'F:\OCRBenchmarkTesting' `
  -ModelRoot 'E:\Model\paddleocr' `
  -Version v4 -Variant server -Backend onnxruntime `
  -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
  -MaximumRegions 1024 -OverflowMode SlidingWindow -WindowOverlap 0.2 `
  -MaximumWindowsPerRegion 32 -OutputDirectory $run -ContinueOnFailure

uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest "$run/selected-manifest.jsonl" `
  --dataset-root 'F:\OCRBenchmarkTesting' `
  --run-directory $run --ocrbench-root 'F:\OCRBenchmarkTesting' `
  --predictions "$run/predictions-corrected.json" `
  --report "$run/evaluation-corrected.json"
```

把 `-Version`、`-Backend` 和输出目录替换为另外五个组合即可（OpenVINO 使用 `openvino`，OpenCV 使用 `opencv-dnn`）。原始输出、逐页 predictions 和 evaluator JSON 留在本机 artifacts，不随仓库提交。
