# PaddleOCR backend benchmark / PaddleOCR 后端基准

该工具只测量完整的 `det -> crop -> cls/orientation -> rec -> merge` 流水线，不输出容易误解的单阶段“模型速度”。它自动发现 PP-OCRv4 Mobile/Server、PP-OCRv5 Mobile/Server 和 PP-OCRv6 Tiny/Small/Medium，按所选后端执行同一张图片，并输出阶段耗时、端到端 P50/P95、模型与输入身份以及结果合同指纹。

支持的后端选择值：

| 值 | 运行路径 | 额外要求 |
| --- | --- | --- |
| `onnxruntime` | ONNX Runtime CPU | 默认构建即可 |
| `openvino` | OpenVINO CPU | 匹配的 OpenVINO managed/native runtime |
| `opencv-dnn` | OpenCV DNN CPU | 匹配的 OpenCV managed/native runtime |
| `onnxruntime-cuda` | ONNX Runtime CUDA | CUDA 构建和匹配的 CUDA/cuDNN/provider |
| `tensorrt` | TensorRT CUDA | bridge、TensorRT/CUDA/cuDNN 和设备本地 Engine |

具体模型与后端是否通过以[验证矩阵](../../docs/model-backend-verification-matrix.md)为准。公开设备数字集中维护在[设备性能实测](../../docs/articles/device-performance-benchmarks.md)，本页不保存历次调优日志。

## 准备模型

模型根目录必须包含匹配的 DET、可选 CLS、REC ONNX 和识别字典。可以使用 ModelFactory/Release 客户端按需下载，也可以显式运行采集脚本：

```powershell
pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Acquire-PaddleOcrModels.ps1 `
  -ModelRoot .\.cache\paddleocr -All -Python <python-with-paddle2onnx>
```

`<python-with-paddle2onnx>` 应替换为已经安装 Paddle/Paddle2ONNX 的 Python 可执行文件。模型目录、字典和测试图片不应提交到 Git。

## 构建模式

默认锁文件用于 ONNX Runtime CPU、OpenVINO 和 OpenCV DNN：

```powershell
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --artifacts-path .\.build\paddleocr-default `
  -p:DeploySharpPaddleOcrCuda12=false -p:RestoreLockedMode=true
```

CUDA 12 使用独立的依赖图和输出目录，避免覆盖默认锁文件或混用程序集：

```powershell
dotnet build tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release --artifacts-path .\.build\paddleocr-cuda12 `
  -p:DeploySharpPaddleOcrCuda12=true -p:RestoreLockedMode=true
```

不要从两个 artifacts 根目录混合复制 DLL。输出的 `.environment.json` 会记录实际加载的 ORT/OpenVINO/OpenCV 程序集版本、路径和 SHA-256；managed/native 不匹配属于运行时部署错误，不是模型不支持。

## 基本运行

应用参数依次为模型根目录和 CSV 输出路径。未设置图片时，工具会下载并校验默认 OCR 测试图，缓存到用户目录；也可以用环境变量指定自己的图片。

```powershell
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = '.\images\ocr-demo.jpg'
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'onnxruntime,openvino,opencv-dnn'
$env:DEPLOYSHARP_PADDLEOCR_VERSIONS = 'v4,v5,v6'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '50'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = '<git-commit>'

dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release -- .\.cache\paddleocr .\results\paddleocr.csv
```

`DEPLOYSHARP_PADDLEOCR_VERSIONS` 也可使用 `v4-mobile`、`v4-server`、`v5-mobile`、`v5-server`、`v6-tiny`、`v6-small` 或 `v6-medium` 做定向测试。

## 正式 5/50 协议

可纳入设备性能文档的报告必须使用 5 次预热、50 次测量，并固定输入、模型、源码 revision、后端版本、Batch、独立 Session 数、电源策略和宽度策略。正式报告至少包含：

- 输入、ONNX/Engine、程序集和结果合同 SHA-256；
- 操作系统、CPU/GPU、驱动、CUDA/cuDNN/TensorRT 或 CPU runtime 版本；
- `selected_batch_size`、`selected_inference_channels` 和是否复用准备输入；
- 预处理、检测推理/后处理、裁剪、方向、识别准备/推理/后处理、合并和总耗时；
- mean、min、max、P50、P95、区域数和结果稳定性；
- GPU 时钟、功耗、P-state 与降频原因，或明确说明未采集。

运行后用校验脚本拒绝缺失元数据或协议不一致的报告：

```powershell
pwsh -NoProfile -File eng/models/paddle-ocr/scripts/Test-PaddleOcrBenchmarkReport.ps1 `
  -ReportPath .\results\paddleocr.csv `
  -RequireSourceRevision `
  -OutputPath .\results\paddleocr.validation.json
```

TensorRT 报告增加 `-RequireTensorRtRuntime`。校验器只把 `pass` 计为性能证据；`unavailable`、`unsupported` 和 `skip` 保留为诊断，不能换算成通过。

## 自动调优与固定配置

自动调优默认开启。工具会扫描：

- `DEPLOYSHARP_PADDLEOCR_AUTOTUNE_CONCURRENCY`，默认 `1,2,4`；
- `DEPLOYSHARP_PADDLEOCR_AUTOTUNE_BATCHES`，默认 `1,2,4,8,16`；
- `DEPLOYSHARP_PADDLEOCR_AUTOTUNE_WARMUP`，默认 `1`；
- `DEPLOYSHARP_PADDLEOCR_AUTOTUNE_ITERATIONS`，默认 `3`。

候选按完整流水线墙钟排序，正式行再使用选中的 Batch/通道数重跑。每个候选必须保持结果合同稳定。要比较指定配置，设置：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_AUTOTUNE = '0'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '2'
$env:DEPLOYSHARP_PADDLEOCR_MAX_PADDING_RATIO = '2.0'
```

Batch 是一次模型调用中的真实行数；Stage concurrency 是从头创建的独立 Session/执行上下文数量。一个 Session 上排队的多个任务不是多个推理通道。显存不足时先减少 Session 数，再减小 Batch。

`DEPLOYSHARP_PADDLEOCR_REUSE_INPUT=1` 复用已解码图片和准备好的检测输入，用于稳态推理；未设置时包含每次图片解码/准备。两种口径必须分开报告。

## 宽度策略

工具默认最大识别宽度为 320。它是基准配置，不是主库的通用限制。模型和后端允许时可以调整：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_MAXIMUM_WIDTH = '640'
$env:DEPLOYSHARP_PADDLEOCR_OVERFLOW_MODE = 'Clamp' # Clamp, Reject, SlidingWindow
$env:DEPLOYSHARP_PADDLEOCR_FIXED_RECOGNITION_WIDTH = '320' # 可选
$env:DEPLOYSHARP_PADDLEOCR_RECOGNITION_WIDTH_BUCKETS = '48,64,96,128,192,256,320,448,640' # 可选
$env:DEPLOYSHARP_PADDLEOCR_WIDTH_REPORT_DIR = '.\results\width-details'
```

- `Clamp` 保留兼容行为，但超宽行可能被压缩；
- `Reject` 在 REC 分配前以明确错误拒绝超限行；
- `SlidingWindow` 生成有界重叠窗口并保留接缝不确定性；
- 固定宽度和宽度分桶可以减少 OpenCV DNN 形状专门化，但可能改变质量与尾延迟。

宽度策略必须同时比较 CER/WER、P50/P95、分配量和不确定接缝。更完整的合同见 [OCR 教程](../../docs/articles/visual-ocr.md#超长行滑窗识别)。

## 质量与几何诊断

以下功能默认关闭，启用后都会增加 CPU 工作或额外 REC 调用：

| 功能 | 主要变量 | 用途 |
| --- | --- | --- |
| 源像素采样 | `DEPLOYSHARP_PADDLEOCR_PIXEL_QUALITY=Report` | 记录亮度、对比度和边缘统计，不改像素 |
| 裁剪诊断 | `DEPLOYSHARP_PADDLEOCR_CROP_PROCESSING=Report` | 比较校正后、resize 后与张量尺寸 |
| 单一增强 | `DEPLOYSHARP_PADDLEOCR_CROP_PROCESSING=<mode>` | 显式测试一种增强配方 |
| 增强重试 | `DEPLOYSHARP_PADDLEOCR_ENHANCEMENT_RETRY=<mode>` | 低置信度行上保留原文并生成候选 |
| 几何检查 | `DEPLOYSHARP_PADDLEOCR_GEOMETRY_MODE=Report|Reject` | 记录倾斜、透视和边界风险 |
| 方向重试 | `DEPLOYSHARP_PADDLEOCR_ORIENTATION_RETRY=1` | 对低置信度行做有界旋转候选 |
| 裁剪路径 | `DEPLOYSHARP_PADDLEOCR_CROP_TRANSFORM=Perspective|AffineWhenEquivalent` | 比较透视与安全仿射快路径 |

增强 mode 包括 `ContrastNormalize`、`GrayClahe`、`GaussianDenoise`、`AdaptiveThreshold`、`UnsharpMask`、`LocalUpscale`、`ShadowNormalize` 和 `JpegArtifactSuppress`。所有门限都是启发式；置信度上升、跨后端一致或图像指标变好都不能替代人工真值。先使用 `Report`/`PreserveOriginal` 收集证据，再按业务 CER/WER、字段误纠率和延迟预算决定是否启用。

诊断 sidecar 在计时范围之外写入，但诊断采样、增强、重试和额外推理计入阶段与总耗时。详细字段、资源上限和选择策略见 [OCR 检测与识别](../../docs/articles/visual-ocr.md)。

## 字符集覆盖审计

无需加载模型或 native runtime 即可检查字典是否覆盖业务字符：

```powershell
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj `
  -c Release -- --audit-characters `
  .\.cache\paddleocr\PP-OCRv5\ppocrv5_dict.txt `
  tools/DeploySharp.PaddleOcrBenchmark/character-requirements.example.json `
  .\results\paddleocr-v5-characters.json
```

退出码 `0` 表示所有非空组均覆盖，`4` 表示审计完成但存在缺失字符，`2` 表示参数、编码、资源边界或 I/O 错误。审计按独立 Unicode 标量和字典 token 边界执行，不做字符规范化，也不代表模型准确率。

完整基准前也可启用启动审计：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT = 'Require' # Disabled, Report, Require
$env:DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS = `
  'tools/DeploySharp.PaddleOcrBenchmark/character-requirements.example.json'
```

`Require` 会在加载图片或创建 Session 前拒绝缺字字典。

## TensorRT

TensorRT Engine 与 GPU 架构、TensorRT/CUDA 版本和 shape profile 绑定，不能随 ONNX 一起作为通用资产复制。准备运行时：

```powershell
$env:JYPPX_TENSORRT_ROOT = '<TensorRT-root>'
$env:JYPPX_CUDA_ROOT = '<CUDA-root>'
$env:JYPPX_CUDNN_ROOT = '<cuDNN-root>'
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_TENSORRT_API_VERSION = '10' # 与 bridge/runtime 匹配
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_86' # 按目标 GPU 修改
```

可以让基准通过 DeploySharp Builder 在调用方目录创建 Engine：

```powershell
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_REBUILD_ENGINES = '1'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_ENGINE_ROOT = '.\.cache\paddleocr-engines'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '8'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BUILD_OPTIMIZATION_LEVEL = '3'
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
```

首次成功后关闭 `DEPLOYSHARP_PADDLEOCR_TENSORRT_REBUILD_ENGINES`，继续指向同一 Engine 根目录。换 GPU、TensorRT minor、bridge API 或 profile 后必须重建。

也可显式运行 `Build-TensorRtEngines.ps1` 或 Linux 的 `build-tensorrt-engines.sh`。两者都会复制必要字典、检查生成文件非空并记录 profile；不要在源模型目录中混放来自不同设备/runtime 的同名 Engine。

设置 `DEPLOYSHARP_CUDA_ARCHITECTURE` 后，TensorRT 识别器可使用紧凑的 GPU CTC argmax 路径，只回传类别/置信度轨迹。出现该可选路径错误时，先清除此变量复跑同一组合：只有清除后恢复表示问题位于 GPU CTC；仍失败则继续检查 Engine profile、enqueue 或 runtime。

## GPU 遥测

正式 GPU 结果必须在计时负载期间采样，不能用空闲时的单次 `nvidia-smi` 快照判断锁频：

```powershell
pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/Invoke-WithGpuTelemetry.ps1 `
  -Executable dotnet `
  -ArgumentList @('run', '--project', `
    'tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj', `
    '-c', 'Release', '--', '.\.cache\paddleocr', '.\results\paddleocr.csv') `
  -OutputPath .\results\paddleocr-gpu-telemetry.csv
```

同时检查利用率、graphics/SM/memory clock、功耗、温度、P-state、power cap、thermal slowdown 和 hardware slowdown。低利用率下的低时钟通常表示输入或 CPU feed 空隙，不等于 GPU 被锁频。

## 便携 Windows 测试包

需要在其它 Windows x64 设备采集统一报告时，使用便携包脚本并显式提供模型、图片和输出目录：

```powershell
pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/portable/Build-PortableWinX64.ps1 `
  -ModelRoot .\.cache\paddleocr `
  -BenchmarkImage .\images\ocr-demo.jpg `
  -OutputRoot .\dist
```

在目标设备解压后运行：

```powershell
.\Run-PaddleOcrBenchmark.ps1 `
  -Backends onnxruntime,openvino,opencv-dnn `
  -Versions v4,v5,v6 -Warmup 5 -Iterations 50 `
  -DeviceLabel '<device-name>'
```

运行器在 `results/<timestamp>` 写入 CSV、日志、设备/运行时信息和摘要，并校验包 manifest。CUDA/TensorRT 的第三方 DLL 仍必须与目标驱动和 bridge 匹配；便携包不会让设备不兼容的 Engine 变得可移植。完整说明见 [portable/README.md](portable/README.md)。

## 输出与状态

CSV 每个模型/后端一行，包含选择的 Batch/通道、阶段平均值、端到端 P50/P95、区域数、文本指纹、严格结果合同指纹和离散语义合同指纹。并发阶段的 work time 是各 worker 工作量之和，可能大于墙钟时间。

| 状态 | 含义 |
| --- | --- |
| `pass` | 完成正式调用并保持所需结果合同 |
| `unavailable` | runtime、设备、bridge 或 Engine 不能初始化/反序列化 |
| `unsupported` | 后端 importer 或精确图合同明确不支持 |
| `skip` | 该后端门未开启或未选择 |

严格合同包含浮点坐标、分数和 token 置信度。CUDA 低位浮点漂移调查可显式设置 `DEPLOYSHARP_PADDLEOCR_ALLOW_NUMERIC_CONTRACT_DRIFT=1`；只有文本和离散语义合同稳定时才会继续，并记录最大数值漂移。该开关不是准确率容差，不能用于把发布门禁改成通过。

排查失败时按顺序区分：模型发现、native runtime、Session/Engine 创建、输入绑定、推理、输出复制、Decoder、结果合同和性能协议。不要通过降低阈值或关闭完整性校验掩盖 importer、shape 或运行时错误。
