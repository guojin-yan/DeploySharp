# DeploySharp PaddleOCR Windows x64 自包含性能测试包

该包用于在另一台 Windows x64 设备上复测 DeploySharp 的 PaddleOCR 完整流水线。包内包含应用程序、.NET 运行时、Windows 原生依赖、PP-OCRv4/v5/v6 ONNX 模型与字典，以及固定的 500×500 基准图片；目标电脑不需要预装 .NET SDK 或 Runtime。

双击 `run-benchmark.cmd` 即可运行默认测试：ONNX Runtime CPU、PP-OCRv4/v5/v6 全规格、cold 口径、预热 5 次、正式测量 20 次。每次运行会新建 `results/<时间戳>`，其中包含：

- `paddleocr-benchmark.csv`：检测、裁剪、方向、识别、合并、平均值、最快/最慢值、P50/P95、区域数及结果哈希。
- `environment.json`：Windows 版本、主机型号、CPU、内存、GPU、驱动、活动电源计划、测试参数及输入/程序 SHA256。
- `benchmark.log`：完整控制台日志和自动调优过程。
- `summary.md`：适合直接汇总进专题材料的结果表。
- `paddleocr-benchmark.csv.sha256`：CSV 校验值。

命令行示例：

```powershell
.\Run-PaddleOcrBenchmark.ps1 -Backends onnxruntime -Warmup 5 -Iterations 20
.\Run-PaddleOcrBenchmark.ps1 -Backends onnxruntime,openvino -Versions v5,v6 -Steady
.\Run-PaddleOcrBenchmark.ps1 -Backends onnxruntime -Versions v6-tiny -FixedConfiguration -BatchSize 4 -StageConcurrency 1
.\Run-PaddleOcrBenchmark.ps1 -Backends onnxruntime-cuda -Versions v4,v5,v6 -TensorRtBatchSize 8 -InterTestDelayMs 2000
```

默认 cold 口径会在每次调用中包含图片解码和检测输入预处理，但不包含模型加载；`-Steady` 复用已解码图片和 detector tensor。比较不同电脑时必须使用相同图片、版本过滤、后端、warm-up、iterations、输入口径和调优设置。

正式测试会保留每次 timed iteration 的端到端耗时，并输出 `total_min_ms`、`total_ms`、`total_max_ms`、`total_p50_ms` 和 `total_p95_ms`。最快/最慢值只来自正式迭代，不包含 warm-up、模型加载、自动调优候选和测试间隔。`summary.md` 直接展示 Fastest/Average/P50/P95/Slowest，便于比较不同后端的峰值性能与尾延迟；发布时应同时给出迭代次数，避免只引用单次最快值。

每个候选测试和每个正式版本/后端测试在 pipeline/session 释放后默认间隔 1000ms，给 CUDA/TensorRT 原生 allocator 一个稳定窗口；间隔不计入 CSV 的 `total_ms`。可用 `-InterTestDelayMs 0` 关闭，或在多任务/高显存设备上设为 2000~5000。自动调优候选也遵守该间隔，因此完整矩阵的墙钟时间会相应增加。

包内的“全部依赖”指可随应用复制的 CPU 应用依赖；项目自有的 Windows TensorRT bridge 已随包提供（`jyppxtrtbridge.dll`），不需要再单独拷贝。GPU 驱动、CUDA 12.8、cuDNN、TensorRT 10.10 及与 GPU 架构匹配的 engine 仍必须在目标设备安装或生成。选择 `onnxruntime-cuda` 或 `tensorrt` 前必须补齐并记录对应运行时；后端初始化失败会记录为 `unavailable`，不能当作 GPU 成绩。

当前 NuGet 没有发布名称精确为 TensorRT 10.10 + CUDA 12.8 的 bridge，包内采用已发布的 Windows TensorRT 10 bridge `JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge` 4.0.0。它按 TensorRT 10 主版本 ABI 工作，但正式结果仍要在目标机用 TensorRT 10.10 生成并验证 engine，并在 `environment.json` 记录真实版本。Windows benchmark 的 `onnxruntime-cuda` 使用 ORT 1.23.2 CUDA 12 provider（依赖 `cublasLt64_12.dll`），与 CUDA 12.8 对应；它和 TensorRT 后端是两套独立的 GPU runtime。

本包同时带有针对动态 OCR 输入的 TensorRT 适配器修复。识别阶段会按 batch 内最大 crop 宽度设置运行时 shape；当下一次 batch 的 host payload 大于上一次运行时，旧适配器可能复用过小的 CUDA 输入缓冲区并报 `Existing CUDA buffer is smaller than the requested host payload`。新版 `JYPPX.TensorRtSharp.dll` 会自动释放并重新分配由适配器拥有的输入缓冲区，因而支持不同 batch、不同文字宽度连续运行；通过 `UseDeviceBuffer` 传入的外部缓冲区仍不会被库隐式替换，容量不足时会给出明确的现有/所需字节数。

`DEPLOYSHARP_CUDA_ARCHITECTURE` 是可选配置：设置后启用 TensorRT 识别输出的 CUDA CTC trace，未设置时 TensorRT 仍在 GPU 上完成模型推理，只把识别 logits 复制回 CPU 做 CTC 解码。若出现 `DS-TRT-5006`，先在同一模型与配置下把该变量清空做一次对照。新版失败行会记录 `ocrStage`、`trtOperation`、内部 `phase`、实际 batch/time/classes、CUDA 架构及原生异常消息；`environment.json` 同时记录该变量和 GPU CTC 是否启用。只有清空后错误消失，才能判定问题位于可选 CUDA CTC 路径；清空后仍失败则继续检查 TensorRT enqueue、engine profile 和运行库兼容性。

CUDA 运行时有时会打印 `VerifyEachNodeIsAssignedToAnEp`：ORT 会把 `Shape`、尺寸索引等不适合 GPU 的小算子放到 CPU，再把主要卷积/矩阵算子放到 CUDA。这个提示出现在 session 创建阶段，不属于 CSV 的推理计时；这些小算子仍可能产生少量同步和数据搬运开销，但不能据此判断 CUDA 推理失败。当前 benchmark 将预期 warning 过滤为 error 级别，真正的 provider 加载或推理错误仍会记录。该现象主要由模型图和 CUDA EP 的算子支持决定，不是 1.23.2 的版本错误；1.23.2 的选择原因是 CUDA 12.x DLL 兼容，升级到 CUDA 13 provider 不能直接用于 CUDA 12.8。

在目标机安装好 CUDA/cuDNN/TensorRT 后，可以直接在包根目录生成 sidecar，不需要复制 NVIDIA DLL。Windows PowerShell 5.1 用户可以使用 `build-tensorrt-engines.cmd`，或直接调用 `powershell.exe`：

```powershell
$env:JYPPX_TENSORRT_ROOT = 'D:\ProgramData\TensorRT\TensorRT-10.10.0.31-cuda12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.8'
# 如果 cuDNN DLL 已经放在 CUDA v12.8\bin，可以保持为 CUDA 根目录；否则改为 cuDNN 根目录。
$env:JYPPX_CUDNN_ROOT = $env:JYPPX_CUDA_ROOT

.\build-tensorrt-engines.cmd -ModelRoot .\models -OutputRoot .\models -TensorRtRoot $env:JYPPX_TENSORRT_ROOT
.\Run-PaddleOcrBenchmark.ps1 -Backends tensorrt -TensorRtApiVersion 10 -Warmup 5 -Iterations 20
```

如需给多台设备做区分，可在运行时增加 `-DeviceLabel RTX2060-Win10`。每次运行都会在 `results/<时间戳>/environment.json` 保存设备信息（设备标签、机器名、Windows 版本/构建号、CPU/核心数/内存、GPU/驱动、`nvidia-smi`、电源计划、输入图和程序 SHA256，以及 CUDA/cuDNN/TensorRT 路径）；同目录的 `summary.md` 也会显示设备标签和 Run ID。建议把整个时间戳目录连同 CSV 一起归档。

也可以把转换命令写成一行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-TensorRtEngines.ps1 -ModelRoot .\models -OutputRoot .\models -TensorRtRoot $env:JYPPX_TENSORRT_ROOT
```

脚本会为每个识别模型生成 batch 动态、宽度 `48..320` 的 TensorRT profile（优化宽度默认 `160`），为分类模型生成固定尺寸 profile，为检测模型生成 batch-one profile。转换完成后，`models` 下应出现与 ONNX 同名的 `.onnx.engine` 文件；如果没有这些 sidecar，TensorRT 后端会明确返回 `unavailable`。如需调整识别宽度，可传 `-RecognitionMinWidth`、`-RecognitionOptWidth`、`-RecognitionMaxWidth`；本应用默认上限为 320。

模型文件的来源、许可证、转换方式和再分发授权必须由发布者在对外提供压缩包前复核。`manifest.sha256` 可用于检查复制或传输后的包完整性。
