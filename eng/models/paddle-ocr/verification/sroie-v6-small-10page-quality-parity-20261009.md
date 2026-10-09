# PP-OCRv6 Small：SROIE 10 页 ORT/OpenVINO 质量与一致性记录（2026-10-09）

本记录把 `F:\OCRBenchmarkTesting` 中两个固定的 SROIE train manifest 分片合并为同一份 10 页选集，执行 PP-OCRv6 Small 的完整 `det → crop → rec → merge` 流程，并分别在 ONNX Runtime CPU 与 OpenVINO CPU 上复测。结果用于补充真实页面质量和跨后端行为证据，不是官方 SROIE 排名或发布准确率。

## 数据、模型和协议

- 数据源：`jsdnrs/ICDAR2019-SROIE`，分发版 revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`，train 子集 10 页。两个输入 manifest 的 SHA-256 为 `3015917423e64aad275f7e3626187d0061cae2738ba6d780907aa87de06b4cb4` 和 `3b9c8159d88d1da188483ede63c067bcaf8564e73137c5cdf278ce292ad25518`；合并后的 selected manifest SHA-256 为 `2975c4e967b485f2b064980fe62b1df5e2e43acb954c44f144d87b020f8f8e41`。
- 设备：`JYPPX`，Windows 10 `10.0.26200`，x64，16 个逻辑处理器。
- 模型：PP-OCRv6 Small；detector SHA-256 `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`，recognizer SHA-256 `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`，dictionary SHA-256 `c89959f09a966e3401cec55aa6d3b95ecb1d802910264182766ee6a88f3d0e35`。
- 每页 1 次预热、1 次计时，recognition batch `16`，1 个推理通道，最大区域 `1024`，SlidingWindow 重叠 `0.2`、每行最多 32 个窗口，流水线超时 60 秒。延迟只是页面级 one-shot 观察，不属于正式 5/50 性能协议。
- 源码 revision：`f75c82d0f50a39976d12a76def04bfa596094fab`；benchmark assembly SHA-256：`9305cb0e6d50cb7e6a98c1045d4306a5583dfe52119a69f3dbe9309cc3761724`。

## 结果

| 后端 | 页成功 | Det TP/FP/FN（IoU 0.5） | Det F1 | 匹配 CER/WER | 端到端 CER/WER | 总耗时 P50/P95（ms） |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 10/10 | 489 / 42 / 53 | 91.15% | 30.42% / 46.03% | 37.63% / 55.74% | 1645.67 / 2279.64 |
| OpenVINO CPU | 10/10 | 489 / 42 / 53 | 91.15% | 30.42% / 46.03% | 37.63% / 55.74% | 735.74 / 890.24 |

两后端均无失败页或空输出，共解码 531 个预测区域。21 个真值区域长度至少 32 个字符，共 730 个字符，端到端编辑数 233；没有长度至少 128 或 3,200 字符的自然连续行。

## 跨后端对照

- 页面文本序列：`10/10` 一致。
- 区域数量：`10/10` 一致，ORT/OpenVINO 都为 531 个区域。
- 按页面阅读顺序索引对齐的区域文本差异：`0`。
- 索引对齐 polygon 最大绝对坐标差：`0 px`；confidence 最大绝对差：`3.028e-5`。

这说明在该设备、该模型和该 10 页输入上，两种 CPU 后端的完整流水线行为一致；不代表所有模型、GPU、TensorRT 或 OpenCV DNN 都具有相同结果。P50/P95 也只是该 one-shot 质量运行中的观察值，不能用于跨设备性能排名。

## 解释边界

SROIE 分发版提供的是词级轴对齐框，而 PP-OCR 检测器输出文本行四边形。严格 IoU、CER/WER 是评测器在异粒度标注上的可追溯诊断，不能当作官方 SROIE 行检测准确率。数据源仍为本地 smoke-only 资产，未将图像、标注、预测或原始运行目录提交到仓库或 Release。自然超长文本、倾斜/旋转真值、增强收益和正式发布准确率门仍保持开放。

## 复现

先分别运行两个 5 页 manifest（ORT 和 OpenVINO 各一份），再用分片合并工具生成 10 页 run。合并工具会拒绝协议/源码/模型不一致、重复 `image_id` 或非空覆盖目录。

```powershell
$py = 'C:\Users\guoji\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $py eng/models/paddle-ocr/scripts/Merge-PaddleOcrPublicDatasetRuns.py `
  --run-directory artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009 `
  --run-directory artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-b `
  --output-directory artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-merged
& $py eng/models/paddle-ocr/scripts/Evaluate-DeploySharpPublicOcrDataset.py `
  --manifest artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-merged/selected-manifest.jsonl `
  --dataset-root F:\OCRBenchmarkTesting `
  --run-directory artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-merged `
  --ocrbench-root F:\OCRBenchmarkTesting `
  --predictions artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-merged/predictions.json `
  --report artifacts/public-ocr-evaluation/sroie-v6-small-ort-20261009-merged/evaluation.json
```

OpenVINO 使用相同命令替换分片目录；最后用 `Compare-PaddleOcrBackendQualityEvidence.py` 比较两个 merged run。逐页 JSON、输入图片和原始输出只保留在本机 ignored artifacts 中；本记录的完整机器可读摘要见 [JSON 报告](sroie-v6-small-10page-quality-parity-20261009.json)。
