# PP-OCRv5 Mobile SROIE 收据页 Smoke 验证（2026-09-28）

## 范围与结论

使用 `F:\OCRBenchmarkTesting` 中固定的 `jsdnrs/ICDAR2019-SROIE` 分发版本，对 10 张收据页完整运行 PP-OCRv5 Mobile 的 DET → crop → CLS → REC → merge。ONNX Runtime CPU、OpenVINO CPU、OpenCV DNN CPU 均完成全部页面，三后端结果逐区一致。

| 项目 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU |
| --- | ---: | ---: | ---: |
| 完成页面 | 10/10 | 10/10 | 10/10 |
| 运行失败 / 空输出 | 0 / 0 | 0 / 0 | 0 / 0 |
| 对比区域 | 538 | 538 | 538 |
| 与 ORT 文本不一致的区域 | 基准 | 0 | 0 |
| 与 ORT 区域数不一致页面 | 基准 | 0 | 0 |
| 与 ORT 坐标最大绝对差 | 基准 | 0 px | 0 px |
| 与 ORT 置信度最大绝对差 | 基准 | 0.0000225 | 0.00001643 |

该数据是分发版标注的 `train` 子集，数据审计状态为 `smoke_only`。SROIE 提供词级轴对齐矩形，而 DeploySharp 的检测器输出文本行四边形；因此下面的 IoU/CER/WER 只是固定评测器对异粒度标注的诊断值，**不是有效的行检测召回或正式 SROIE 精度成绩**。上游划分、图像权利和分发版与官方 RRC 划分的一致性尚未完成审计。不得把结果作为官方排行榜或准确率达标声明。

## 数据与固定输入

- 数据源：`jsdnrs/ICDAR2019-SROIE`，revision `bffe40c26759f3376ec2b3ae9031dbba54cd587c`；分发方标记 CC BY 4.0，但原始图像权利/划分仍待核对。来源审计见本机 `F:\OCRBenchmarkTesting\docs\datasets\SROIE.md`。
- 两份互不重叠 manifest：`sroie-train-20260923T072455614249Z.jsonl`（5 页）与 `sroie-train-20260923T072550009854Z.jsonl`（5 页）；分别有 267 和 275 个词级实例，共 542 个标注词。
- Manifest SHA-256：第一份 `b0a5aa819acee08ed6601f1018391265af063a01c49f02c452d247fd6e3a2dd1`；第二份 `8e419b812cbaffb9bc40f0047ac11bace2b0e392cec742ec056dff2d3aaa4358`。
- PP-OCRv5 Mobile 模型 SHA-256：DET `1eb7b4f7ab657ebd1c66d5f79bca7497f29768a2e3c15e52daecbba1a8e4a039`；CLS `dd8b2b61983d76ab230a58da9e0e0e84956b71c3877f2ce6e438fe22d74d2cf2`；REC `f2fb81dc0cf6bf07736e7422bab38c6636e776bc8b5bc8c8d3c7d7322cd8f3a9`；字典 `d1979e9f794c464c0d2e0b70a7fe14dd978e9dc644c0e71f14158cdf8342af1b`。
- 设备：`JYPPX`，Windows 10.0.26200 x64，16 logical processors。三个后端都使用 CPU。
- 参数：warmup 1、每页测量 1 次、REC batch 16、1 inference channel、最多 1,024 regions/page；SlidingWindow overlap `0.2`、每 region 最多 32 windows、page timeout 60 s。单次计时不构成性能协议。
- runner commit `08a865d463d4ade7cac788b668361fa2736db0b6`；benchmark assembly SHA-256 `61dc419e21c4202ac636c1330de0b07b1cae2043ca9aad85082c9c86f4bf1893`。采集时工作树有其他未提交修改，source status SHA-256 为 `28cd9b74edf5eeec829e31416b00302f7a94ce45af2e18f6188127061d6573eb`。

## 诊断指标

评测器在每个 5 页子集上对词级框和检测文本行使用 IoU 0.5 匹配。指标按所有样本编辑距离与参考字符加权汇总，不是逐页平均。

| 分发 manifest | 后端 | TP / FP / FN | IoU F1 | 匹配 CER | 端到端 CER |
| --- | --- | ---: | ---: | ---: | ---: |
| `...072455614249Z.jsonl` | ORT / OpenVINO / OpenCV | 227 / 36 / 40 | 85.66% | 31.30% | 44.97% |
| `...072550009854Z.jsonl` | ORT / OpenVINO / OpenCV | 265 / 10 / 10 | 96.36% | 44.20% | 45.25% |
| 合并 10 页 | ORT / OpenVINO / OpenCV | 492 / 46 / 50 | 91.11% | 38.09% | 45.11% |

类别逐项汇总一致不等于准确率已通过；高检测 F1 也受词框与文本行框粒度错配影响。合并样本的识别精确区域为 188/492；CLS 无人工方向标签，不能计算分类准确率。

三后端逐图比较覆盖 10 页的所有 538 个预测区域：文本完全一致、区域数完全一致、坐标完全一致；最大置信度差分别如上表。该 parity 只适用于指定 ONNX、字典、预处理、CPU runtime 和这 10 张输入，不代表 GPU、其他版本或其他数据域。

## 复现

在 `DeploySharp` 仓库根目录执行；每次必须使用全新输出目录，保留全部原始证据：

```powershell
$runner = '.\eng\models\paddle-ocr\scripts\Invoke-PaddleOcrPublicDataset.ps1'
$manifests = @(
  'F:\OCRBenchmarkTesting\data\annotations\manifests\sroie-train-20260923T072455614249Z.jsonl',
  'F:\OCRBenchmarkTesting\data\annotations\manifests\sroie-train-20260923T072550009854Z.jsonl'
)
foreach ($backend in @('onnxruntime', 'openvino', 'opencv-dnn')) {
  for ($index = 0; $index -lt $manifests.Count; $index++) {
    $set = if ($index -eq 0) { 'set-a' } else { 'set-b' }
    $run = "artifacts/public-ocr-evaluation/sroie-v5-mobile-$backend-sliding-$set-20260928"
    & $runner -ManifestPath $manifests[$index] `
      -DatasetRoot 'F:\OCRBenchmarkTesting' -ModelRoot 'E:\Model\paddleocr' `
      -Version v5 -Variant mobile -Backend $backend `
      -Warmup 1 -Iterations 1 -BatchSize 16 -InferenceChannels 1 `
      -MaximumRegions 1024 -OverflowMode SlidingWindow `
      -WindowOverlap 0.2 -MaximumWindowsPerRegion 32 `
      -PipelineTimeoutMs 60000 -ContinueOnFailure -OutputDirectory $run
    if ($LASTEXITCODE -ne 0) { throw "Runner failed: $backend $set" }
    uv run python .\eng\models\paddle-ocr\scripts\Evaluate-DeploySharpPublicOcrDataset.py `
      --manifest "$run\selected-manifest.jsonl" `
      --dataset-root 'F:\OCRBenchmarkTesting' --run-directory $run `
      --ocrbench-root 'F:\OCRBenchmarkTesting' `
      --predictions "$run\predictions.json" --report "$run\evaluation.json"
    if ($LASTEXITCODE -ne 0) { throw "Evaluator failed: $backend $set" }
  }
}
```

实际运行输出位于忽略目录 `artifacts/public-ocr-evaluation/sroie-v5-mobile-{onnxruntime,openvino,opencv-dnn}-sliding-{set-a,set-b}-20260928/`；没有将图像、标注、预测或逐页文本加入 Git/Release。该记录可作为 CPU 端到端执行和限定范围 parity 的 smoke 证据，不能替代 P2 行级公开精度集、P3 5/50 正式性能协议或 P6 退化/长文本验收。
