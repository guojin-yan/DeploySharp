# PP-OCRv6 Small：SROIE 10 页三后端质量对照（2026-10-09）

本记录使用同一份本地 SROIE train 10 页选集，对 PP-OCRv6 Small 完整执行 `det → crop → rec → merge`，并在 ONNX Runtime CPU、OpenVINO CPU、OpenCV DNN CPU 上对照。它是可追溯的质量 smoke，不是官方 SROIE 排名、发布准确率或正式性能基准。

## 固定输入与协议

- 数据源：`jsdnrs/ICDAR2019-SROIE` train；源合并 manifest SHA-256 `2975c4e967b485f2b064980fe62b1df5e2e43acb954c44f144d87b020f8f8e41`，三后端 runner 生成的 selected manifest SHA-256 `946fb167a92c9646ee28d938f622114136dc97728b52aea0bdf183da522f9d83`。
- 设备：`JYPPX`，Windows 10 `10.0.26200`，x64，16 个逻辑处理器；源码 revision `cbaae7a6b8353a9fd0fe84131df8feec5126389e`；benchmark assembly SHA-256 `9305cb0e6d50cb7e6a98c1045d4306a5583dfe52119a69f3dbe9309cc3761724`。
- 模型：v6 Small；detector `d73e0058b7a8086bbd57f3d10b8bcd4ff95363f67e06e2762b5e814fe9c9410e`，recognizer `5435fd747c9e0efe15a96d0b378d5bd157e9492ed8fd80edf08f30d02fa24634`，dictionary `c89959f09a966e3401cec55aa6d3b95ecb1d802910264182766ee6a88f3d0e35`。
- 每页 1 次预热、1 次测量，batch=1、推理通道=1，最大区域 1024；SlidingWindow overlap `0.2`、每区域最多 32 个窗口，超时 60 秒。batch=1 是为了与当前 OpenCV DNN 静态 batch 合同公平对照，不是生产吞吐推荐值。
- 三个 run 都记录为同一源码 revision、同一 dirty-worktree 状态摘要、同一 benchmark assembly 和 selected manifest；本机 run 的原始 CSV、environment JSON 和 predictions 留在 ignored `artifacts/`，不作为仓库证据文件提交。

## 结果

| 后端 | 页成功 | Det TP/FP/FN（IoU 0.5） | Det F1 | 匹配 CER/WER | 端到端 CER/WER | 总耗时 P50/P95（ms） |
|---|---:|---:|---:|---:|---:|---:|
| ONNX Runtime CPU | 10/10 | 489 / 42 / 53 | 91.15% | 30.10% / 45.44% | 37.39% / 55.37% | 1302.87 / 2534.95 |
| OpenVINO CPU | 10/10 | 489 / 42 / 53 | 91.15% | 30.10% / 45.44% | 37.39% / 55.37% | 779.76 / 1054.15 |
| OpenCV DNN CPU | 10/10 | 489 / 42 / 53 | 91.15% | 30.10% / 45.44% | 37.39% / 55.37% | 7722.31 / 10131.55 |

三种后端均无失败页或空输出。相对 ORT CPU，OpenVINO 和 OpenCV DNN 都是 10/10 页有序文本一致、区域数 10/10 一致，共 531 个区域索引文本差异为 0；polygon 最大索引差均为 `0 px`，confidence 最大差分别为 `2.909e-5` 和 `1.639e-5`。这证明的是本模型、这 10 页、当前 Windows 运行时下的行为对照，不是所有模型或设备的通用 parity。

OpenCV DNN 的 one-shot P50 约为 ORT 的 5.9 倍，OpenVINO 在本次协议下较快；这些是不同页面各一次计时的观察值，不能替代正式 `5 warmup + 50 measured` 性能协议，也不能作为跨设备排名。

## 边界

SROIE 标注为词级轴对齐框，而 PP-OCR 输出文本行四边形，IoU/CER/WER 仅作可追溯诊断。10 页中有 21 个长度至少 32 字符的真值区域，但没有 ≥128 或 ≥3200 字符的自然连续行，因此自然长文本 A2、正式行级精度门和增强收益门仍未关闭。数据图像、标注、预测和原始运行目录均保留在本机 `F:\OCRBenchmarkTesting`/ignored artifacts，不随仓库或 Release 分发。

## 复现

先按 [SROIE 10 页 ORT/OpenVINO 记录](sroie-v6-small-10page-quality-parity-20261009.md) 的 runner 生成三个本地 run；OpenCV 运行时需设置 `JYPPX_OPEN_CV_RUNTIME_PATH`。三份 run 必须使用同一 selected manifest、源码 revision、assembly、模型 SHA、batch 和滑窗协议，然后执行：

```powershell
$py = 'C:\Users\guoji\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $py eng/models/paddle-ocr/scripts/Compare-PaddleOcrBackendQualityEvidence.py `
  --reference-root artifacts/public-ocr-evaluation/sroie-v6-small-ort-batch1-20261009 `
  --candidate-root artifacts/public-ocr-evaluation/sroie-v6-small-openvino-batch1-20261009 `
  --candidate-root artifacts/public-ocr-evaluation/sroie-v6-small-opencv-20261009 `
  --output-json artifacts/public-ocr-evaluation/sroie-v6-small-backend-compare-20261009/compare-3backend-batch1.json `
  --output-markdown artifacts/public-ocr-evaluation/sroie-v6-small-backend-compare-20261009/compare-3backend-batch1.md
```

机器可读摘要见 [JSON](sroie-v6-small-10page-three-backend-quality-20261009.json)。比较器会拒绝模型、manifest、源码、assembly 或协议不一致的 run。
