# PaddleOCR backend benchmark

This console tool discovers PaddleOCR v4/v5 mobile and server DET/REC ONNX files plus the v6 tiny/small/medium tiers below <code>E:\\Model\\paddleocr</code> (or <code>DEPLOYSHARP_PADDLEOCR_ROOT</code>) and runs only the complete detection -> crop/batch -> optional orientation -> recognition -> merge pipeline on one real image. PP-OCRv5 uses the matching mobile/server classifier; PP-OCRv4 shares its legacy classifier because no separate server classifier is catalogued. Windows supports the full configured backend matrix; Ubuntu 22.04 x64 supports the native OpenCV adapter and ONNX Runtime CPU directly, while CUDA/TensorRT still require matching consumer-installed NVIDIA runtimes and bridge packages. It does not emit isolated det/cls/rec benchmark rows. Each version/variant/backend produces one final row with the selected batch size, selected independently-created inference-channel count, stage breakdown, and end-to-end latency.

## Recognition crop diagnostics / 识别裁剪诊断

Set `DEPLOYSHARP_PADDLEOCR_CROP_PROCESSING=Report` (default `Disabled`), optionally `DEPLOYSHARP_PADDLEOCR_CROP_SAMPLES=1024` and `DEPLOYSHARP_PADDLEOCR_CROP_LIMIT=1024`. Samples are bounded per stage; the crop limit includes minimum-batch duplicate rows, windows and retries. Report preserves pixels and recognition SHA. Width sidecar schema 8 records `Crop.CropProcessing`, each region's `CropDiagnostics`, and all orientation attempts' diagnostics. `Rectified` is after warp/rotation, `Content` after resize but before padding/normalization; these are not directly comparable quality scores. Actual sampling work is included in recognition preparation and total time. Unsupported input adapters fail explicitly.

通过上述变量显式启用，默认不采集。用 `DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR` 指定 JSON 目录；源区域诊断与裁剪诊断可独立开启。裁剪来源在 ROI/方向映射后不被改写，滑窗角点已包含父行方向。每阶段采样上限 1～65536、每调用物理裁剪上限 1～4096；超限失败而非漏记，诊断不保留图像。Report 不表示增强或准确率提高。

## Opt-in crop enhancement / 显式裁剪增强

`DEPLOYSHARP_PADDLEOCR_CROP_PROCESSING=ContrastNormalize|GrayClahe|GaussianDenoise|AdaptiveThreshold|UnsharpMask|LocalUpscale|ShadowNormalize|JpegArtifactSuppress` selects one bounded pre-resize operation. `Report` collects evidence only; default `Disabled` retains the original path. Sidecar schema 8 includes requested enhancement, gate decision, enhanced-stage statistics and applied linear gain. GrayClahe and AdaptiveThreshold deliberately remove color; their gray pixels are replicated for RGB models before normal model normalization. GaussianDenoise applies OpenCV GaussianBlur only when the sampled Laplacian variance exceeds `ENHANCE_NOISE_THRESHOLD`; this metric is not a calibrated noise classifier. AdaptiveThreshold applies Gaussian local binarization only to a low-contrast crop whose dimensions meet `ADAPTIVE_BLOCK`; it is intentionally opt-in because it can remove grayscale and fine strokes. UnsharpMask applies one Gaussian blur plus `AddWeighted` only when sampled Laplacian variance is below `SHARPNESS_THRESHOLD`; that metric is not a calibrated blur score. ShadowNormalize subtracts a Gaussian illumination background when sampled luminance variation crosses its explicit gate. JpegArtifactSuppress applies a bounded median-filter candidate behind a Laplacian-variance gate; neither gate detects a semantic degradation type. For an unenhanced first pass followed by one candidate, use the separate enhancement-retry switch below instead.

配置变量如下，均以 `DEPLOYSHARP_PADDLEOCR_ENHANCE_` 为前缀：

| 后缀 | 默认值 | 含义 |
|---|---:|---|
| THRESHOLD | 32 | 低对比度排他上限，按校正crop的8位亮度标准差 |
| MIN_CONTRAST | 1 | 包含式下限，低于此值保持原图 |
| TARGET_STD | 64 | 线性增益限制前目标，须≥THRESHOLD且≤128 |
| MAX_GAIN | 3 | 线性增益上限，须>1且≤8 |
| CLAHE_CLIP | 2 | CLAHE直方图截断参数，须>0且≤16 |
| CLAHE_GRID | 8 | 每轴分块数2～16，不是像素尺寸 |
| MAX_PIXELS | 1048576 | 每个待增强裁剪的硬像素上限，最大16777216 |
| NOISE_THRESHOLD | 512 | GaussianDenoise 的拉普拉斯方差门限；不是标定噪声分数 |
| DENOISE_KERNEL | 3 | GaussianDenoise 的奇数核尺寸3～9 |
| DENOISE_SIGMA | 0 | GaussianDenoise 的 sigma，0表示交由 OpenCV 根据核尺寸选择 |
| ADAPTIVE_BLOCK | 15 | AdaptiveThreshold 的奇数邻域3～31；裁剪任一边更小时跳过 |
| ADAPTIVE_C | 5 | AdaptiveThreshold 从局部高斯均值中减去的常数，范围-64～64 |
| SHARPNESS_THRESHOLD | 512 | UnsharpMask 的拉普拉斯方差上限；不是标定模糊分数 |
| SHARPEN_KERNEL | 3 | UnsharpMask 的奇数高斯核尺寸3～9 |
| SHARPEN_AMOUNT | 0.5 | UnsharpMask 细节增益，范围(0,4] |
| SHARPEN_SIGMA | 1 | UnsharpMask 的正高斯 sigma，范围(0,32] |
| UPSCALE_FACTOR | 2 | LocalUpscale 中间放大倍数，范围(1,4] |
| UPSCALE_INTERPOLATION | Cubic | LocalUpscale 插值：Nearest/Linear/Cubic |
| SHADOW_THRESHOLD | 16 | ShadowNormalize 的亮度标准差门限；不是阴影分类器 |
| SHADOW_KERNEL | 31 | ShadowNormalize 的奇数背景高斯核，范围3～127 |
| JPEG_THRESHOLD | 256 | JpegArtifactSuppress 的拉普拉斯方差门限；不是 JPEG 检测器 |
| JPEG_KERNEL | 3 | JpegArtifactSuppress 的奇数中值核，范围3～7 |

启用后不修改原文件、DET/CLS、字典或补边；操作发生在裁剪内部，额外处理计入REC准备和总耗时。应先固定 `Report` 基线，再分别运行需要评估的候选。对照JSON里的逐行文字、门限决策、原始/增强指标以及CSV端到端分位数。增强可能降低准确率，置信度/对比度提升不是CER/WER改善的替代证据；无标注集时只报告行为与一致性，不能发布准确率提升结论。

## Single enhancement retry / 单次增强重试

```powershell
$env:DEPLOYSHARP_PADDLEOCR_CROP_PROCESSING = 'Disabled' # Report also works; no direct first-pass enhancement
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY = 'GrayClahe'
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY_CONFIDENCE = '0.9'
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY_SELECTION = 'PreserveOriginal'
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY_MIN_GAIN = '0.05'
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY_MAX_REGIONS = '16'
$env:DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY_MAX_CROPS = '128'
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = 'artifacts/ocr-enhancement-retry/preserve'
```

Retry is `Disabled` by default; every crop enhancement mode listed above is valid. It reuses `ENHANCE_*` quality/operation parameters and `CROP_SAMPLES/CROP_LIMIT` diagnostics budgets, automatically enabling unenhanced crop diagnostics if necessary. Default confidence threshold is 0.8; the example deliberately uses 0.9. It runs after orientation retries, never repeats DET/CLS, and creates at most one candidate per eligible line. Low confidence alone is insufficient: at least one existing rectified-crop measurement must pass the operation's quality gate. A windowed candidate reruns all windows while only eligible crops are enhanced.

`PreserveOriginal` is default and retains original final text even when candidate confidence is higher. Explicit `ConfidenceGain` can select a nonempty candidate by the minimum-gain heuristic; it can change punctuation or make accuracy worse. Schema 8 records `Crop.EnhancementRetry` and per-row `EnhancementRetry.Options/Original/Candidate/Decision`, keeping all original/candidate text, scores, CTC traces and crop evidence. Null Candidate means no extra REC was run; decisions distinguish quality gate, admission limit, preserved policy, insufficient gain and selection. Original means the orientation-selected result before enhancement, not necessarily the first REC.

区域上限按阅读顺序接收质量合格行；额外crop上限含minimum-batch重复行。候选保持原窗口实际TensorWidth并只按同宽分批，避免补边宽度变化混入增强效果；batch维仍可能变化。初次、方向重试、增强重试还共用CROP_LIMIT与结果字节预算、取消、超时，超限失败而非截断。直接首轮增强与重试不能混用，非法配置明确失败。规划/采样/增强/额外REC均计入总耗时和recognition work/batch计数；导出JSON仍在计时外。请分别跑Disabled、PreserveOriginal和ConfidenceGain并保存原文差异；无标注集时不得把置信度上升称为准确率改善。

## Source-pixel quality diagnostics / 源像素质量诊断

Set `DEPLOYSHARP_PADDLEOCR_PIXEL_QUALITY=Report` to measure the decoded source image and each original detected polygon once. The default is `Disabled`; other values fail explicitly. Pixels, crop policy and OCR results remain unchanged. This is CPU-side sampling of the OpenCV input, even with a GPU inference backend, not GPU preprocessing.

```powershell
$env:DEPLOYSHARP_PADDLEOCR_PIXEL_QUALITY = 'Report'
$env:DEPLOYSHARP_PADDLEOCR_QUALITY_SAMPLES_PER_AREA = '4096'
$env:DEPLOYSHARP_PADDLEOCR_QUALITY_MAX_REGIONS = '128'
$env:DEPLOYSHARP_PADDLEOCR_QUALITY_SAMPLES_PER_CALL = '1048576'
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = 'artifacts/ocr-quality/report'
# Run the normal full-pipeline command; compare with Disabled using identical input/settings.
```

The three numeric values above are defaults. Per-area centers accept 1–1,048,576; region count 1–4096; per-call centers must be at least the per-area budget and no more than 16,777,216. The conservative `(detectedRegions + 1) * samplesPerArea` bound includes the whole source; budget violations fail the complete call with `DS-VISUAL-4102`. Each center reads at most five luminance values. Quality work is included in `crop_ms` and total latency, with the same cancellation/deadline as inference. No per-pixel work occurs when disabled.

Sidecar **schema 8** adds `PixelQualityOptions`, whole-source `PixelQuality`, and per-region `PixelQuality`. Evidence includes mean/stddev luminance, dark/light fractions, native one-pixel Laplacian variance and central gradient, accepted/attempted sample counts, evaluation size/polygon/index and shortest polygon edge. No samples means null pixel metrics; fewer than two valid neighborhoods means null Laplacian variance. Existing text/contract hashes exclude diagnostics.

这里采样的是原 DET 区域内的源像素，不是 resize/padding 后的 REC crop。规则采样可能漏掉小结构；噪声也会提高拉普拉斯方差，黑字白纸也会有大量极暗/极亮像素。当前不输出通用质量总分，不自动增强、拒绝或分类 JPEG/噪声；最短区域边不是字形高度。固定输入、模型、宽度、采样预算和后端后再比较。详见 [OCR quality diagnostics](../../docs/articles/visual-ocr.md#可选源像素质量诊断)。

## Character dictionary audit / 字符集覆盖检查

Build once, then audit a dictionary without loading models, images, CUDA or other native runtimes:

```powershell
dotnet restore tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -p:DeploySharpPaddleOcrCuda12=true --locked-mode
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release --no-restore -p:DeploySharpPaddleOcrCuda12=true
dotnet tools/DeploySharp.PaddleOcrBenchmark/bin/Release/net10.0/DeploySharp.PaddleOcrBenchmark.dll `
  --audit-characters E:\Model\paddleocr\PP-OCRv5\ppocrv5_dict.txt `
  tools/DeploySharp.PaddleOcrBenchmark/character-requirements.example.json artifacts/ocr-characters/v5.json
```

The command appends the model's space class by default; pass `--no-space` only when the model's dictionary contract does not append it. Exit codes: **0** covers every nonempty group; **4** completes the audit and reports missing independent characters; **2** indicates invalid input/encoding/limits or an I/O error. A code 4 is expected for some symbols in the example, and its JSON report remains available.

需求 JSON 是“组名 → 原始需求文本”的对象，例如：

```json
{
  "serial-number": "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_",
  "invoice": "订单金额人民币￥€㎡"
}
```

Use UTF-8 (an initial BOM is accepted), 1–64 uniquely named groups, and 1–65,536 total UTF-16 units of requirement text within a 1 MiB file. Invalid UTF-8, unpaired surrogate escapes, empty strings and duplicate group names are rejected. Newlines inside values are actual requirements, not delimiters. Dictionary audit input is bounded to 16 MiB; the library audit index defaults to 4 Mi UTF-16 units. Reports must have a different path from the input dictionary and requirements.

The example includes 47 Chinese business characters and representative punctuation/units; it is not a complete standard Chinese character list. Replace it with your business repertoire. The audit checks **independent Unicode scalar tokens**, with no normalization: a dictionary token `AB` does not certify standalone `A` or `B`; a missing scalar that occurs in compound tokens is flagged explicitly. This measures dictionary coverage, not model accuracy, language support, or compound-token segmentation.

Report schema 1 records requirement groups/file SHA, dictionary file SHA, append-space behavior, expected CTC class count (blank at zero), repeated dictionary indexes, multi-scalar token count, a length-framed ordered-token hash, and per-group missing scalars (`CodePoint`, text, occurrence count, first UTF-16 offset, compound-token flag). The existing character-set SHA remains for result compatibility. The ordered-token hash distinguishes token boundaries without changing existing result hashing.

To audit the selected v4/v5/v6 recognition dictionaries before a full pipeline benchmark:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT = 'Report' # Disabled (default), Report, Require
$env:DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS = 'tools/DeploySharp.PaddleOcrBenchmark/character-requirements.example.json'
# Run the normal benchmark command with the desired versions/backends.
```

`Report` writes `<output.csv>.characters.json` and proceeds with missing-character evidence. `Require` writes the same report, then exits 4 with `DS-VISUAL-4105` if any selected dictionary misses a requirement, **before image loading or session creation**. Invalid input exits 2 in either mode. Auditing occurs once before all warmups/timed calls and does not enter the timing columns; a fresh report path is recommended per experiment. Standalone audits need no model ONNX files. Startup audits use the same dictionary loader and space policy as the full pipeline.

重复字典项必须保留原类别索引。工具此前给重复项追加控制字符/数字的行为已修复，改为统一调用主库 `PaddleOcrProfiles.LoadCharacterSet`。含重复词条的 v4 字符表哈希会改变；新报告应作为修复后的基线，不能把哈希变化直接当作识别回归。核心 CTC 解码仍按类别折叠重复，并保留显式 unknown 的 trace。

API semantics and local dictionary findings: [OCR character coverage](../../docs/articles/visual-ocr.md#字符集覆盖审计). Command regression can be run with `pwsh -NoProfile -File eng/ocr/Test-CharacterAudit.ps1` after building the tool.

## Recognition width correctness / 识别宽度正确性

The library default is **MaximumWidth=3200**. This benchmark retains **320** as its historical performance setting; it is not a library limitation. Increase it only when the model/backend/engine profile admits the requested shape.

```powershell
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE = 'Clamp' # or Reject / SlidingWindow
$env:DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH = '320'
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = 'artifacts/ocr-widths'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<actual-commit>+dirty'
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- E:\Model\paddleocr artifacts/ocr-widths/full.csv
```

`Clamp` preserves existing resizing and reports compressed rows. `Reject` fails the complete OCR call at `CropAndBatch` with `DS-VISUAL-4103`, before REC crop allocation/inference; DET/CLS may already have run. `SlidingWindow` recognizes bounded overlapping crops and merges them back into each original detected line. These policies apply to recognition, not the fixed-width orientation classifier. Gap-based Split is not implemented.

The tool exits with code 3 when there are no successful rows, including when all calls are intentionally rejected by the width policy. A mixed-backend run with at least one success still requires inspection of every CSV status; exit code 0 is not a claim that all combinations passed.

JSON sidecars are exported **outside measured spans** and contain the last measured result's text/polygons, dictionary SHA, natural/target/tensor widths, compression and batch-padding flags, input/model/assembly hashes and protocol. Existing CSV text/contract hashes are unchanged. A null width diagnostic means unknown, not uncompressed. Successful repeated configurations overwrite their same-named report; use a fresh output directory per experiment and retain failure rows/logs (failed calls have no successful width sidecar).

主库默认上限仍为 3200，基准工具的 320 仅用于保持历史协议。自然宽度、区域受限宽度和实际批张量宽度分别记录，不能把补齐 padding 当成压缩。`SlidingWindow` 保留原检测行和阅读顺序；逐窗口原文、token、宽度和接缝不确定信息导出到 JSON，不能仅凭 `status=pass` 判断文字准确。

Window controls / 滑窗配置（只在 SlidingWindow 时生效）：

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `DEPLOYSHARP_PADDLEOCR_WINDOW_OVERLAP` | `0.2` | Shared window fraction, greater than 0 and at most 0.5 / 重叠比例 |
| `DEPLOYSHARP_PADDLEOCR_WINDOWS_PER_REGION` | `32` | Maximum windows per detected line, 2–256 / 单行窗口限制 |
| `DEPLOYSHARP_PADDLEOCR_WINDOWS_PER_IMAGE` | `1024` | Total including unsliced lines, 1–4096 / 全图窗口限制 |
| `DEPLOYSHARP_PADDLEOCR_WINDOW_MIN_MATCH` | `2` | Minimum exact overlap tokens; 1 enables single-token matching / 最小完全匹配 token 数 |

Unmatched seams retain both sides and set `SeamUncertain`; duplicates may remain. A larger overlap supplies context but costs more inference. Keep model width, batch, sessions, overlap and minimum match fixed for comparisons. CPU/GPU text agreement is reproducibility evidence, not annotated accuracy. Full API details: [OCR windows](../../docs/articles/visual-ocr.md#超长行滑窗识别).

匹配以完整字典 token 和近似 CTC 位置为依据，未匹配接缝保留双侧文本并标记不确定。增大重叠或允许单 token 匹配都应结合真实样本评估；当前工具只记录结果，不自动选择准确率更好的组合。

## Geometry diagnostics / 几何诊断

Set `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MODE=Report` to collect geometry after DET and before CLS/REC; the default is `Disabled`. `Reject` fails the entire call with `DS-VISUAL-4104` if the default rejection mask matches. It does not silently skip a line or change perspective sampling. / 使用 Report 先观察风险；Reject 明确失败，默认不因贴边而拒绝。

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MODE` | `Disabled` | Disabled / Report / Reject |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MIN_AREA` | `4` | Minimum polygon area, pixels² / 最小面积 |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MIN_EDGE` | `1` | Minimum polygon edge, pixels / 最短边 |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MAX_ASPECT` | `200` | Maximum long/short-side ratio / 最大长短边比 |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MAX_OUTSIDE` | `0.25` | Maximum polygon area outside the input / 图外面积比例上限 |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_EDGE_MARGIN` | `1` | Boundary-risk margin, pixels / 贴边风险距离 |
| `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MAX_CONDITION` | `10000` | Maximum normalized homography Frobenius condition / 归一化单应矩阵条件数上限 |

```powershell
$env:DEPLOYSHARP_PADDLEOCR_GEOMETRY_MODE = 'Report'
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = 'artifacts/ocr-geometry/report'
# Run the benchmark with the same model/input/batch settings as the Disabled baseline.
```

Sidecar schema 8 includes `Crop.Geometry` options and per-region `Geometry` evidence, including the evaluated input polygon, angle, exact outside-area fraction, condition number and risk flags. It also records the requested `Crop.TransformMode` and bounded orientation-retry configuration. Export remains outside timed spans. `crop_ms` includes enabled geometry checks; `recognition_ms` includes actual crop materialization. Null diagnostics mean not collected. ROI/global orientation projection preserves the diagnostic input space; use the final `Region.Polygon` for drawing. / JSON 仍在计时外写入；schema 8 同时记录几何、裁剪变换和方向重试配置。几何检查本身计入流水线耗时；null 不表示已证明安全。

These checks are heuristics, not CER/WER or measured text truncation. Small angles do not imply affine equivalence. The default remains four-corner perspective rectification; `OcrCropTransformMode.AffineWhenEquivalent` is an explicit OpenCV-only opt-in with closure and condition checks, and unsafe trapezoids fall back to perspective. Right-angle recognition retries are separately opt-in below. Details and API configuration: [OCR geometry](../../docs/articles/visual-ocr.md#几何质量倾斜角度与边界风险).

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `DEPLOYSHARP_PADDLEOCR_CROP_TRANSFORM` | `Perspective` | `Perspective` keeps the four-corner projective warp; `AffineWhenEquivalent` enables the OpenCV affine fast path only for a numerically safe parallelogram. Other adapters keep their existing sampling path. |

The sidecar schema is now 8 and records the requested `Crop.TransformMode`. The OpenCV preprocessing descriptor also reports the actual batch decision as `cropTransform=Perspective`, `Affine`, or `Mixed`; a requested affine mode can therefore be audited when a batch contains both safe and perspective crops. The affine path is a sampling optimization, not an accuracy change: the default remains perspective and the policy never selects affine from angle alone. / sidecar schema 8 会记录请求模式；OpenCV 描述符还会报告批次实际选择。默认仍为透视，不能仅按角度切换。

## Bounded orientation retries / 有界方向重试

| Environment variable | Default | Meaning |
| --- | --- | --- |
| `DEPLOYSHARP_PADDLEOCR_ORIENTATION_RETRY` | `0` | Explicit enable flag: 0/1/false/true / 显式开关 |
| `DEPLOYSHARP_PADDLEOCR_RETRY_CONFIDENCE` | `0.8` | Retry nonempty REC results below this score; empty text always qualifies / REC 触发阈值 |
| `DEPLOYSHARP_PADDLEOCR_RETRY_MIN_GAIN` | `0.05` | Minimum strict confidence improvement over nonempty incumbent / 最小置信度增量 |
| `DEPLOYSHARP_PADDLEOCR_RETRY_MAX_REGIONS` | `16` | Maximum eligible lines admitted in reading order / 最多重试行数 |
| `DEPLOYSHARP_PADDLEOCR_RETRY_MAX_CROPS` | `1024` | Additional crops across all rounds, excluding minimum-batch duplicates / 所有轮次额外裁剪上限 |
| `DEPLOYSHARP_PADDLEOCR_RETRY_ROTATIONS` | `180` | Distinct comma-separated relative angles, e.g. `180,90,270` / 不重复相对角度 |

Enable with `DEPLOYSHARP_PADDLEOCR_ORIENTATION_RETRY=1`; use the same image, model, width, batch and channel settings for a Disabled baseline. Retry thresholds are REC scores, not CLS scores. Initial DET/CLS and image decode are not repeated. Candidate angles are relative to the initial post-CLS crop and do not accumulate. Ties retain the initial/current winner; high-confidence accepted lines stop early. / 低分或空文本行可重试；不要把置信度提高等同于准确率提高。

Sidecar schema 8 adds `Crop.OrientationRetry`, `Crop.TransformMode` and per-line `OrientationRetry.Attempts`, `SelectedIndex`, `SkippedByRegionLimit`. Each attempt retains original text, CTC trace, effective rotation, width and windows; null means disabled/not triggered. Enable `DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR` to export outside timed spans. Extra planning/crop/REC/merge is included in `recognition_ms`, and detailed work/batch counters include all rounds. / 导出保留初次结果与候选，不只写胜出的文字。

The line admission limit explicitly skips later eligible rows with provenance; crop/result hard limits, backend failures, cancellation and the shared timeout fail the whole call. Retry respects Clamp/Reject/SlidingWindow and model shape limits. With sliding windows, keep overlap/seam parameters fixed as well. Full selection and resource semantics: [OCR retries](../../docs/articles/visual-ocr.md#低置信度方向重试).

## Portable Windows x64 package

`portable/Build-PortableWinX64.ps1` publishes a self-contained Windows x64 benchmark package. The generated folder carries the .NET runtime, native CPU dependencies, the fixed benchmark image, selected ONNX/dictionary assets, a SHA-256 manifest, and a launcher that records hardware/runtime metadata next to every result. The executable first looks for `models` and `images/benchmark/demo_1.jpg` beside itself, so the folder can be copied without rewriting absolute paths. Run the package with `run-benchmark.cmd`; no .NET installation is required on the target host.

The default portable protocol is ORT CPU with one fixed image. Multi-scene images belong to correctness demonstrations, not the cross-device performance table. The package now includes the project-owned Windows TensorRT bridge; the NVIDIA driver, CUDA/cuDNN/TensorRT vendor DLLs and GPU-specific engine sidecars remain target-machine prerequisites and are intentionally not copied into the package.

Detection uses one independently-created backend session. Classification and recognition use independent session pools and batches. ONNX Runtime and OpenVINO use the dynamic model contract; OpenCV DNN specializes symbolic dimensions for each concrete batch shape, keeping recognition width dynamic while padding only within a width group. Batches beyond pool capacity wait for an idle session, and their crop tensors are created only after a slot is available so a long document does not retain every prepared batch at once. The pipeline restores detector order before it writes a result.

When CUDA is selected, ONNX Runtime may report <code>VerifyEachNodeIsAssignedToAnEp</code>. Shape-only and indexing nodes can be intentionally partitioned to the CPU while the main convolution and matrix operators remain on CUDA. The message is emitted during session creation, not inside the measured inference interval; CPU fallback can still add a small synchronization/copy cost, but this warning alone does not indicate a failed CUDA provider. The portable benchmark uses ORT 1.23.2 because its GPU package imports CUDA 12 DLLs required by CUDA 12.8; changing ORT versions may change operator coverage, but the warning is primarily determined by the model graph and execution-provider support rather than by a version mismatch.

~~~powershell
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'E:\\Model\\paddleocr'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '10'
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
~~~

Keep enough warm-up calls when comparing versions. The first supported pipeline in a new process also initializes JIT-compiled preprocessing code, OpenCV native paths, and worker threads; a one-warm-up, low-sample mean can therefore attribute process cold-start cost to that model. The default runner uses three warm-ups and fifteen measured calls; the command above uses five and ten for a quicker focused check.

On the dedicated Win10 host, a five-warm-up/ten-call v5 mobile ORT CPU run measured about 10 ms preprocessing; a previous approximately 72 ms value came from a short noisy sample. Treat preprocessing as workload/host dependent and compare P50/P95.

The default image is <code>E:\\Data\\ocr\\demo\\_1.jpg</code>. On the current workstation that name resolves to the existing file <code>E:\\Data\\ocr\\demo_1.jpg</code>; when it is not present, the runner downloads <code>ocr-demo_1.jpg</code> from the <code>test-assets.1</code> release, verifies SHA-256, and caches it under <code>%LOCALAPPDATA%\\DeploySharp\\TestImages</code>. Set <code>DEPLOYSHARP_TEST_IMAGE_ROOT</code> to reuse another cache or <code>DEPLOYSHARP_PADDLEOCR_IMAGE</code> to use an explicit image. The complete-pipeline CSV is written as <code>paddleocr-full-*.csv</code>. In addition to stage means, each formal row contains <code>total_min_ms</code>, <code>total_max_ms</code>, <code>total_p50_ms</code>, and <code>total_p95_ms</code> calculated from the timed calls. These exclude warm-up, model loading, autotune candidates, and inter-test delays, allowing the same result to show both peak latency and tail behavior.
Use <code>DEPLOYSHARP_PADDLEOCR_BACKENDS</code> with a comma-separated list (for example <code>opencv-dnn</code> or <code>onnxruntime,openvino</code>) to run only selected backends while troubleshooting.
Each full-pipeline call is bounded by <code>DEPLOYSHARP_PADDLEOCR_PIPELINE_TIMEOUT_MS</code> (default 15000 ms). A timeout is recorded as <code>unavailable</code> with a timeout detail instead of blocking the remaining model/backend rows.
For steady-state throughput, set <code>DEPLOYSHARP_PADDLEOCR_REUSE_INPUT=1</code>. The decoded OpenCV image and detector tensor are prepared once and reused for warmup/timed calls; <code>preprocess_ms=0</code> and <code>total_ms</code> then represent warm pipeline latency. Leave it unset for end-to-end latency that includes image decode and preprocessing.

Automatic tuning is enabled by default. For every version/variant/backend, the runner tests the Cartesian product of <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_CONCURRENCY</code> (default <code>1,2,4</code>) and <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_BATCHES</code> (default <code>1,2,4,8,16</code>). Candidate tuning reuses the decoded/prepared input and ranks complete-pipeline wall time; orientation-plus-recognition is used as the tie-breaker because those are the stages most affected by batch and channel counts. The selected combination is then rerun under the requested complete-pipeline cold/steady protocol. Every candidate must return a stable complete result-contract SHA-256 across its timed samples, and the formal rerun must exactly reproduce the selected candidate. Different maximum batch sizes can legitimately produce different dynamic widths and CTC timestep traces, so the report records the number of deterministic contract variants across shapes rather than comparing every shape with batch-one output. The final row records the choice in <code>selected_batch_size</code> and <code>selected_inference_channels</code>. Short tuning samples are controlled by <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_WARMUP</code> and <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_ITERATIONS</code>; increase them when the device is noisy. Set <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE=0</code> only for a fixed-configuration diagnostic run.

The fixed-run and batching controls are:

- <code>DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY</code>: independently-created classifier/recognizer sessions (default <code>1</code>).
- <code>DEPLOYSHARP_OPENCV_NUM_THREADS</code>: optional process-global OpenCV CPU thread count applied before OCR sessions are created. Leave it unset for the native default; test <code>4</code>, <code>8</code>, and <code>16</code> when combining several OCR channels because this trades per-session parallelism against channel-level concurrency.
- <code>DEPLOYSHARP_PADDLEOCR_BATCH_SIZE</code>: maximum classifier/recognizer batch (default <code>4</code>).
- <code>DEPLOYSHARP_PADDLEOCR_MAX_REGIONS</code>: maximum detected regions accepted by the complete-pipeline benchmark (default <code>32</code>). Increase it for dense documents; this is an application safety limit and does not change the DeploySharp pipeline default.
- <code>DEPLOYSHARP_PADDLEOCR_INTRA_OP_THREADS</code>: explicit ONNX Runtime CPU threads per classifier/recognizer session. When unset during automatic tuning, CPU threads are divided by the candidate inference-channel count.
- <code>DEPLOYSHARP_PADDLEOCR_DETECTION_INTRA_OP_THREADS</code>: ONNX Runtime CPU threads for the single detector session (default <code>0</code>, the runtime default); it is independent of the recognition pool size.
- <code>DEPLOYSHARP_PADDLEOCR_MAX_PADDING_RATIO</code>: maximum padded-width work divided by natural-width work (default <code>2.0</code>). This permits useful batches for mixed-width text while bounding wasted padded computation.
- <code>DEPLOYSHARP_PADDLEOCR_VERSIONS</code>: comma-separated filter such as <code>v5,v6-tiny</code>.
- <code>DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE</code>: explicit TensorRT classification/recognition batch (default <code>1</code>; requires dynamic-batch engines).
- <code>DEPLOYSHARP_PADDLEOCR_INTER_TEST_DELAY_MS</code>: pause after each disposed autotune candidate and formal version/backend case (default <code>1000</code>; outside measured timings). The portable PowerShell wrapper exposes the same setting as <code>-InterTestDelayMs</code>.

Tune CPU thread counts and session count together. Multiplying full-core sessions usually oversubscribes the processor and can make a larger pool slower.

If the repository contains `.onnx.engine` sidecars built by a different TensorRT minor release, rebuild them on the target machine before measuring TensorRT. The portable package includes `Build-TensorRtEngines.ps1`; use the installed TensorRT `trtexec.exe` and write the sidecars directly into `models` so no vendor DLLs have to be copied into the package:

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-TensorRtEngines.ps1 -ModelRoot .\models -OutputRoot .\models -TensorRtRoot 'C:\TensorRT-10.10.0.31'
~~~

The script uses the v4/v5/v6 input profiles documented below and requires <code>trtexec.exe</code> from the selected TensorRT installation. Use <code>-ModelNames</code> with exact ONNX basenames to build only a selected subset (for example the server detector/recognizer/classifier set); omitted names retain the all-model behavior. It defaults to <code>BuilderOptimizationLevel=3</code>; pass <code>-BuilderOptimizationLevel 0</code> only when engine build time matters more than steady-state latency. Set <code>JYPPX_CUDA_ROOT</code> and <code>JYPPX_CUDNN_ROOT</code> to the target CUDA 12.8/cuDNN directories before building. The optional <code>-Fp16</code> switch is capability checked before any model is copied or built.

To measure the TensorRT sidecars, configure the consumer-owned vendor runtimes in the same PowerShell process. The runner automatically points <code>JYPPX_NATIVE_BRIDGE_PATH</code> at the bundled Windows bridge and defaults to API line <code>10</code>; pass <code>-TensorRtApiVersion 8|10|11</code> when using another engine line. NuGet does not currently publish an exact `TRT 10.10 + CUDA 12.8` bridge; the bundled bridge is the published Windows TensorRT 10 bridge (`trt10.11.cuda12.9.cudnn9.22`, v4.0.0). Because the bridge uses the TensorRT 10 major ABI, validate the target's 10.10 engine load before collecting formal data and record the exact vendor versions in `environment.json`.

On Windows the benchmark selects Microsoft.ML.OnnxRuntime.Gpu.Windows <code>1.23.2</code> for the CUDA 12 provider, so <code>onnxruntime-cuda</code> resolves <code>cublasLt64_12.dll</code> on CUDA 12.x systems. The repository default remains ORT <code>1.28.0</code>; restore or publish this CUDA 12 application with <code>-p:DeploySharpPaddleOcrCuda12=true</code> so the managed adapter and GPU package both resolve to 1.23.2. On Ubuntu 22.04 x64 the same property selects <code>Microsoft.ML.OnnxRuntime.Gpu.Linux</code> 1.23.2. The demo and benchmark reference the project-owned bridge package <code>JYPPX.TensorRT.CSharp.API.Runtime.linux-x64.ubuntu22.04.trt11.0.cuda12.9.cudnn9.22.Bridge</code> (version <code>4.0.0</code>). The package contains only <code>libjyppxtrtbridge.so</code>; CUDA, cuDNN and TensorRT remain NVIDIA user-space prerequisites. Install CUDA 12.9, cuDNN 9.22 and TensorRT 11.0.0.114 from the matching NVIDIA repository, then restore/publish with <code>-p:RuntimeIdentifier=ubuntu.22.04-x64 -p:DeploySharpPaddleOcrCuda12=true</code>. The bridge is copied into the publish directory by the normal .NET asset flow.

The repository also includes <code>build-tensorrt-engines.sh</code> for Linux. It copies ONNX files and OCR text assets to an isolated output, builds one static TensorRT 11 engine per ONNX file with <code>trtexec</code>, checks the v4/v5/v6 stage shapes, and fails on an empty engine. For the RTX 2060 host used by the case study:

~~~bash
export JYPPX_TENSORRT_ROOT=/usr
export DEPLOYSHARP_PADDLEOCR_ROOT=/home/ygj/models/paddleocr
export DEPLOYSHARP_PADDLEOCR_TENSORRT_ROOT=/home/ygj/models/paddleocr-trt11-cuda12.9-sm75
TRTEXEC=/usr/bin/trtexec tools/DeploySharp.PaddleOcrBenchmark/build-tensorrt-engines.sh
~~~

The resulting <code>.onnx.engine</code> files are bound to the build GPU, TensorRT serialization version and declared profiles (detection <code>1x3x736x736</code>, recognition batch-dynamic with <code>48..320</code> width and an optimization width of <code>160</code>, v4 classification <code>1x3x48x192</code>, v5/v6 classification <code>1x3x80x160</code>). Rebuild them when moving to another GPU or TensorRT minor release.

The Linux validation host completed the full DeploySharp pipeline with that isolated output. With one warm-up, two measured calls, batch one, and stage concurrency one, TensorRT 11 + CUDA 12.9 produced: PP-OCRv4 mobile <code>100.415 ms</code>, PP-OCRv5 mobile <code>171.841 ms</code>, PP-OCRv6 tiny <code>53.554 ms</code>, PP-OCRv6 small <code>93.051 ms</code>, and PP-OCRv6 medium <code>183.059 ms</code>. Every row returned 16 regions. The raw report is <code>paddleocr投稿/实测数据/远端Linux-20260908/linux-tensorrt-report.csv</code>; the combined CPU/GPU matrix is <code>linux-backend-matrix.csv</code> in the same directory.

Example Linux run after publishing the benchmark and installing the matching NVIDIA user-space libraries:

~~~bash
app_root=/home/ygj/DeploySharp-linux/tools/DeploySharp.PaddleOcrBenchmark/bin/Release/net10.0/ubuntu.22.04-x64
export LD_LIBRARY_PATH="$app_root:/usr/local/cuda/targets/x86_64-linux/lib:/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH"
export JYPPX_NATIVE_BRIDGE_PATH="$app_root/libjyppxtrtbridge.so"
export JYPPX_CUDA_ROOT=/usr/local/cuda
export JYPPX_CUDNN_ROOT=/usr
export JYPPX_TENSORRT_ROOT=/usr
export DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1
export DEPLOYSHARP_TENSORRT_API_VERSION=11
export DEPLOYSHARP_CUDA_ARCHITECTURE=compute_75
export DEPLOYSHARP_PADDLEOCR_ROOT=/home/ygj/models/paddleocr-trt11-cuda12.9-sm75
export DEPLOYSHARP_PADDLEOCR_IMAGE=/home/ygj/demo_1.jpg
export DEPLOYSHARP_PADDLEOCR_BACKENDS=tensorrt
dotnet "$app_root/DeploySharp.PaddleOcrBenchmark.dll" "$DEPLOYSHARP_PADDLEOCR_ROOT" /tmp/linux-tensorrt-report.csv
~~~

~~~powershell
$env:JYPPX_TENSORRT_ROOT = 'C:\TensorRT-10.10.0.31'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8'
$env:JYPPX_CUDNN_ROOT = 'C:\Program Files\NVIDIA\CUDNN\v9'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_TENSORRT_ROOT\lib;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-TensorRtEngines.ps1 -ModelRoot .\models -OutputRoot .\models -TensorRtRoot $env:JYPPX_TENSORRT_ROOT
.\Run-PaddleOcrBenchmark.ps1 -Backends tensorrt -Versions v4,v5,v6 -TensorRtApiVersion 10 -Warmup 5 -Iterations 20
~~~

在多台设备上复测时可给运行命令增加 `-DeviceLabel RTX2060-Win10`。便携运行器会在每个 `results/<时间戳>/environment.json` 中记录设备标签、机器名、操作系统、CPU/内存、GPU/驱动、`nvidia-smi`、电源计划、输入与程序 SHA256，以及 CUDA/cuDNN/TensorRT 路径；同目录的 `summary.md` 会显示设备标签和 Run ID，便于后续合并报告。

The bridge DLL must match the selected TensorRT API/CUDA line, and a serialized engine must also be built by the same TensorRT serialization version. A missing or incompatible bridge, or an engine deserialization mismatch, is recorded as <code>unavailable</code>; it is never reported as a timing pass. For example, an engine serialized by TensorRT 10.10 cannot be loaded by TensorRT 10.11 or 11.0.

The TensorRT backend also runs the complete OCR pipeline when all three stage engines are present. Historical sidecars retained under the original model directory use a fixed <code>1x3x48x320</code> recognition contract and are kept only as an earlier correctness/latency baseline; do not use those engines for the current portable benchmark. Rebuild the package's ONNX files with <code>Build-TensorRtEngines.ps1 -StageOptBatch 4 -StageMaxBatch 8</code>. The current recognition profile accepts batch-dynamic widths from <code>48</code> through <code>320</code> (optimization width <code>160</code>), so each width-grouped batch is padded only to that batch's widest crop. Detection remains one image per call, while classification/recognition accept batches from 1 through 8. Set <code>DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE=4</code> when using that rebuilt output; the runner will not claim a larger batch for historical static sidecars.

The complete-pipeline run uses the real image <code>E:\Data\ocr\demo_1.jpg</code> and reports separate preprocessing, detection, crop, orientation, recognition, merge, and total columns. On the dedicated Windows 10 / RTX 2060 machine, TensorRT 11.0 + CUDA 12.9 completed all five mobile pipelines (two warm-ups, five measured calls, stage concurrency one): the latest steady totals were v4 mobile <code>146.838 ms</code>, v5 mobile <code>190.556 ms</code>, v6 tiny <code>83.764 ms</code>, v6 small <code>167.552 ms</code>, and v6 medium <code>339.484 ms</code>. These runs reuse the decoded image and prepared detector input; model loading, engine building, and CUDA initialization are outside the timed region. Report: <code>artifacts/remote-test/paddleocr-full-tensorrt-all-rerun-20260829.csv</code>. A cold rerun (decode and preprocessing included) measured v4 mobile <code>189.896 ms</code>, v5 mobile <code>184.215 ms</code>, v6 tiny <code>92.890 ms</code>, v6 small <code>171.562 ms</code>, and v6 medium <code>343.556 ms</code>; report: <code>artifacts/remote-test/paddleocr-full-tensorrt-all-cold-rerun-20260829.csv</code>.

For the same full TensorRT run, point the root at the rebuilt engine directory and choose the TensorRT backend explicitly. This command still produces only complete pipeline rows; no stage-only benchmark mode is available:

~~~powershell
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'artifacts\local-model-benchmarks\paddleocr-trt11-rebuilt'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\DeploySharp-Remote\data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '2'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '5'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '1'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '4'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '4' # dynamic cls/rec engines only
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1' # omit for cold/decode timing
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_TENSORRT_API_VERSION = '11'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_75' # RTX 2060; choose the target for the actual GPU
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
~~~

On the dedicated Windows 10 / RTX 2060 host, the current code and dynamic TensorRT 11 engines with BuilderOptimizationLevel 3 were measured sequentially with ten warm-ups, fifty timed calls, stage concurrency one, and batch eight. With the CPU CTC fallback, the steady totals were v4 mobile <code>67.158 ms</code>, v5 mobile <code>104.018 ms</code>, v6 tiny <code>45.816 ms</code>, v6 small <code>88.648 ms</code>, and v6 medium <code>135.365 ms</code>. Baseline: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-singlepassctc-20260829.csv</code>.

Setting <code>DEPLOYSHARP_CUDA_ARCHITECTURE=compute_75</code> enables the TensorRT session's compact GPU sequence-argmax path. The recognizer output remains on its TensorRT stream, a lazily compiled CUDA kernel produces only per-timestep class/confidence traces, and the full logits tensor is not copied to the CPU. Under the same ten-warm-up/fifty-call protocol, all five pipelines returned 16 regions and measured v4 mobile <code>53.045 ms</code>, v5 mobile <code>60.489 ms</code>, v6 tiny <code>29.972 ms</code>, v6 small <code>42.703 ms</code>, and v6 medium <code>92.142 ms</code>. Relative to the CPU CTC baseline, these totals are approximately 21%, 42%, 35%, 52%, and 32% lower. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-20260829.csv</code>.

The CUDA sequence-argmax path is optional. With `DEPLOYSHARP_CUDA_ARCHITECTURE` unset, TensorRT still executes the OCR network on the GPU and the recognizer copies logits to the host for CPU CTC decoding. For `DS-TRT-5006`, repeat the same TensorRT case once with the variable cleared. Current diagnostics identify the OCR stage, TensorRT operation, internal phase, batch/time/class dimensions, CUDA architecture, and native exception message; `environment.json` records whether CUDA sequence argmax was enabled. An error that disappears only with the variable cleared is isolated to the optional CUDA CTC path, while a persistent error points to TensorRT enqueue, the engine optimization profile, or the installed runtime combination.

A separate three-warm-up/ten-call A/B run compared the complete public OCR result contract, not only recognized text. Region indices/scores/polygons, recognized confidence/charset, and every token timestep/class/confidence/blank/repeat/unknown/emitted flag are included in <code>result_contract_sha256</code>. The CPU and GPU rows matched for all five models. In that controlled sample, GPU CTC reduced recognition from 41.128 to 24.586 ms (v4), 77.202 to 32.138 ms (v5), 97.492 to 50.916 ms (v6 medium), 66.893 to 22.501 ms (v6 small), and 28.875 to 12.607 ms (v6 tiny). Managed pipeline allocation fell from 56-120 MB to approximately 21-26 MB per call. Reports: <code>artifacts/remote-test/paddleocr-all-trt-b8-cpuctc-contract-20260829.csv</code> and <code>artifacts/remote-test/paddleocr-all-trt-b8-gpuctc-contract-20260829.csv</code>. DB contour/box decoding and region crop preparation still run on the CPU; this is not yet a fully device-resident OCR pipeline.

The subsequent host hot-path pass reuses exact-size recognition tensor buffers and pooled DB workspaces, and computes convex hulls from connected-component boundary pixels instead of sorting every interior pixel. With ten warm-ups, fifty calls, batch eight, and one stage session, the sustained totals became v4 <code>44.652 ms</code>, v5 <code>54.210 ms</code>, v6 tiny <code>24.798 ms</code>, v6 small <code>37.477 ms</code>, and v6 medium <code>86.997 ms</code>. Managed allocation was approximately 14.1-16.8 MB per call and all five complete contract hashes remained unchanged. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-telemetry-run-20260829.csv</code>.

For compact GPU CTC, asynchronous calls now dispatch synchronous reductions across independent pooled sessions when <code>DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY</code> is greater than one. On the same host with two stage sessions, the final telemetry run measured v4 <code>41.249 ms</code>, v5 <code>50.797 ms</code>, v6 tiny <code>22.917 ms</code>, v6 small <code>35.328 ms</code>, and v6 medium <code>85.353 ms</code>. Recognition improved by roughly 4-11% versus the previously serialized compact path; it does not double because both execution contexts share one GPU. Two sessions also consume more engine/device memory, so benchmark one and two sessions on the deployment GPU rather than making two the universal default. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-c2-asyncfix-telemetry-run-20260829.csv</code>.

The previous contract-preserving host pass writes OpenCV crop pixels directly from native `Mat` rows into the pooled Float32 tensor, initializes only actual right-side padding, fuses DB probability validation with the connected-component mask, and computes convex hulls from per-column extrema without sorting every boundary point. Under the same batch-eight/two-session/10+50 protocol, that historical pass measured v4 <code>36.060 ms</code>, v5 <code>48.711 ms</code>, v6 tiny <code>21.652 ms</code>, v6 small <code>34.375 ms</code>, and v6 medium <code>83.683 ms</code>. DB postprocessing measured <code>2.568-3.200 ms</code>, recognition preparation work <code>2.016-2.649 ms</code>, and compact CTC host postprocessing <code>0.023-0.124 ms</code>. Every row still returned 16 regions with the same complete contract hash. Report: <code>artifacts/remote-test/paddleocr-all-trt-c2-paddingonly-20260829.csv</code>.

The current best complete-pipeline result on the dedicated host is the device-cache reuse run: v4 <code>32.319 ms</code>, v5 <code>44.655 ms</code>, v6 tiny <code>17.541 ms</code>, v6 small <code>30.017 ms</code>, and v6 medium <code>80.024 ms</code>. It uses 10 warm-ups, 50 timed calls, two stage sessions, recognition batch 8, prepared-input reuse, and CUDA CTC argmax. See <code>artifacts/remote-test/paddleocr-all-trt-c2-devicecache-20260829.csv</code> and the device-grouped [performance record](../../docs/articles/device-performance-benchmarks.md).

The no-softmax CTC decoder now validates probabilities during its required argmax scan instead of scanning the whole tensor twice, and it no longer allocates an unused class workspace. In a controlled v6-tiny batch-16 comparison, this reduced recognition from <code>32.778 ms</code> to <code>28.250 ms</code> and total latency from <code>53.990 ms</code> to <code>49.332 ms</code>, with the recognized-text SHA-256 unchanged. Report: <code>artifacts/remote-test/paddleocr-v6tiny-trt-opt3-b16-singlepassctc-20260829.csv</code>. A pure recognition-engine test increased image throughput from approximately <code>4,659 images/s</code> at batch eight to <code>5,768 images/s</code> at batch sixteen, but complete-pipeline latency improves less because single-image detection and host-side DB region decoding remain on the critical path.

BuilderOptimizationLevel 5 was also tested against level 3 with otherwise identical v6-tiny batch-16 profiles. Recognition was approximately <code>0.14%</code> slower and detection approximately <code>0.37%</code> faster, while engine construction took about 200 seconds. The difference is within run-to-run noise, so level 3 remains the default.

The command writes a CSV under <code>artifacts/local-model-benchmarks</code> by default and emits one <code>PADDLEOCR_BENCHMARK</code> line per model/backend. Full-pipeline rows include detector inference/postprocessing, recognition preparation/inference/postprocessing work, recognition batch count, <code>result_text_sha256</code>, and <code>result_contract_sha256</code>; the latter covers geometry, region metadata, recognized text metadata, and the complete CTC token trace. Work timings are summed across concurrently executing batches and therefore may exceed recognition wall time. The detail column states whether TensorRT CUDA sequence argmax was enabled and records its architecture target. <code>pass</code> includes mean, minimum, maximum, P50, and P95 milliseconds; <code>unavailable</code> means runtime/device initialization or TensorRT engine deserialization failed; <code>unsupported</code> means the backend importer rejected the graph; <code>skip</code> means the backend gate was not enabled. TensorRT measurements require the consumer-owned bridge/runtime and <code>DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1</code>.

The pipeline benchmark keeps model loading and OpenVINO compilation outside the timed region and measures OCR postprocessing in the stage columns. The crop_ms column is the bounded batch-planning/grouping cost; actual crop tensor materialization is performed just before each recognizer call and is included in recognition_ms so the reported stage sum remains an end-to-end wall-time accounting when batches overlap. The shared OpenCV Pillow-compatible resamplers use parallel row passes for large images (BLIP/Donut/SAM); small OCR crops stay single-threaded to avoid scheduler overhead. On Ubuntu 22.04 the benchmark project selects the Ubuntu OpenCV and OpenVINO native packages; ORT CPU, OpenVINO CPU, and OpenCV DNN CPU have been exercised across PP-OCRv4/v5/v6 in the submission matrix. Results are local evidence and are not cross-machine performance guarantees.

The OCR adapter reuses thread-local perspective-warp and resize Mats plus point buffers. This keeps the returned crop Mat independently owned while removing temporary native allocations from the per-region hot path. The visual pipeline also caches the immutable Core input collection for prepared-frame reuse. / OCR 适配器会复用线程本地透视变换/缩放 Mat 及角点缓冲；返回的 crop Mat 仍独立拥有，同时移除每个区域热路径中的临时 native 分配。视觉 Pipeline 还会缓存已准备帧对应的不可变 Core 输入集合。

CTC token lists are transferred through a trusted internal read-only path and are not copied again while restoring source-region indices; public result constructors remain defensive. / CTC token 列表通过受信任的内部只读路径转移，在恢复源区域索引时不再重复复制；公共结果构造函数仍保持防御性复制。

Do not compare pool sizes while unrelated workloads are active. For a publishable run, pin the same image, runtime versions, CPU/GPU power mode, warm-up and iteration counts, batch size, padding ratio, session count, and per-session thread count. The dedicated Win10 rerun on 2026-08-28 confirmed ORT CPU and OpenCV DNN CPU full-pipeline execution for v5 mobile and v6 tiny/small/medium; ORT CUDA was attempted explicitly and recorded as <code>unavailable</code> with <code>DS-ORT-5008</code> (CUDA 801) when the device context could not initialize.

GPU clocks must be sampled during the timed workload, not inferred from an idle <code>nvidia-smi</code> snapshot. Use <code>Invoke-WithGpuTelemetry.ps1</code> to run a benchmark while recording P-state, utilization, graphics/SM/memory clocks, power, temperature, and NVIDIA clock-event reasons. The summary is calculated only from samples at or above the selected utilization threshold. A low clock with low utilization indicates an input/feed gap rather than a locked GPU; power-cap regulation, thermal slowdown, and hardware slowdown are counted separately.

During the final two-session five-model run, 63 active samples averaged <code>58.2%</code> GPU utilization and <code>1,828 MHz</code> graphics clock, reached <code>1,875 MHz</code>, averaged <code>78.4 W</code>, and peaked at <code>60 C</code>. Thermal and hardware-slowdown samples were both zero. NVIDIA reported software power-cap regulation in 20 samples; this is normal boost control near the configured power limit, not fixed-frequency or thermal throttling. A pure recognizer load independently reached 99% utilization, 1,800-1,845 MHz, and 113-117 W. The device therefore was not locked at the approximately 1,005 MHz clock observed during idle or short CPU-fed gaps. Raw telemetry: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-c2-asyncfix-telemetry-20260829.csv</code>.

~~~powershell
pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/Invoke-WithGpuTelemetry.ps1 `
    -Executable dotnet `
    -ArgumentList @('run', '--project', 'tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj', '-c', 'Release') `
    -OutputPath artifacts/local-model-benchmarks/paddleocr-gpu-telemetry.csv
~~~
