# ROI 多后端性能矩阵记录规范

本文是 ROI 性能证据的固定记录格式，不填理论值，也不把合同测试结果当成设备性能。每个设备单独建立一个小节；同一模型、同一输入、同一提交号和同一运行时组合才能横向比较。

## 记录原则

- `pass`：模型真实执行、结果合同通过，并且保存了原始 CSV/JSON 和结果指纹。
- `unsupported`：已进入该后端路径，但模型、算子、形状或 ROI 几何被明确拒绝；不是耗时为零。
- `unavailable`：运行时、驱动、设备、模型文件或 native DLL 不可用，未进行有效计时。
- `—`：没有测量，不得用其他后端或平均值填充。
- 视觉模型至少记录 `preprocess`、`inference`、`postprocess`、`merge`、`total` 的 mean/P50/P95；设备端 CUDA 路径还要记录 host-to-device、kernel/stream 和 device-to-host（若有）。
- OCR 只记录完整 `det -> crop -> cls/orientation -> rec -> merge` 流水线，以及实际选择的识别 batch 和独立 Session/stream 数量。

## 设备信息模板

每个设备小节必须先写设备条件，再写矩阵：

| 字段 | 内容 |
| --- | --- |
| 设备 ID / 测试日期 | 例如 `win10-rtx2060-01` / `2026-09-16` |
| OS / 架构 | Windows 10 x64、进程位数、构建号 |
| CPU / 内存 | 型号、物理/逻辑核心、内存容量 |
| GPU / 驱动 | GPU 型号、显存、驱动版本、功耗上限 |
| CUDA/cuDNN/TensorRT | 精确运行时版本和 API 行 |
| OpenVINO / OpenCV / ORT | 精确包与 native 版本、Execution Provider |
| .NET / DeploySharp | .NET 运行时、DeploySharp commit SHA |
| 时钟状态 | 默认 Boost、锁频或 `nvidia-smi` 记录；失败也要记录 |
| 输入 / 模型 | 输入分辨率和 SHA-256；模型文件 SHA-256 |
| 协议 | warmup、iterations、cold/steady、Batch、Session/stream、预取 |

## 表格模板

| 模型 / 任务 | ROI 模式 | 后端 / 设备 | 状态 | ROI 数 / 几何 | Batch / Session | Pre P50/P95 (ms) | Infer P50/P95 (ms) | Post P50/P95 (ms) | Merge P50/P95 (ms) | Total P50/P95 (ms) | 分配 / 显存 | 结果证据 |
| --- | --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| YOLOv8n / Detection | CropAndInfer | ONNX Runtime CPU | — | — | — | — | — | — | — | — | — | CSV/JSON 链接 |
| YOLOv8n / Detection | FilterResults | OpenVINO CPU | — | — | — | — | — | — | — | — | — | CSV/JSON 链接 |
| YOLOv8n / Detection | SlidingWindow | TensorRT CUDA | — | — | — | — | — | — | — | — | — | CSV/JSON 链接 |
| YOLOv8n-seg / Instance | CropAndInfer | OpenCV DNN | — | — | — | — | — | — | — | — | — | CSV/JSON 链接 |
| PP-OCRv6 tiny / OCR | 完整流水线 | TensorRT | — | — | det=1；rec=batch/Session | — | — | — | — | — | — | CSV/JSON 链接 |
| SAM2/SAM3 / Video | 外部 Predictor | — | unsupported | — | — | — | — | — | — | — | — | blocker/contract |
| Qwen/BLIP / VQA | CropAndInfer | 真实后端 | — | — | Batch=1；Session=1 | — | — | — | — | — | — | CSV/JSON 链接 |

## 结果文件和复现

`DeploySharp.VisualBenchmark` 已输出 backend、mode、status、预处理/推理/后处理/编排/总耗时的 mean/P50/P95，以及 benchmark 线程托管分配；TensorRT CUDA 还可以启用 CUDA 后处理一致性校验。便携设备测试包应原样回传同一时间戳下的 `visual-*.csv`、`visual-*.json`、`device-*.json`、`gpu-*.csv` 和 `console-*.log`，不要手工改 CSV。

轴对齐 ROI 使用源图像素参数 `--roi x,y,width,height`。同一次运行会把该 ROI 应用于全部选中后端，并在 JSON `Configuration.Roi` 中记录；普通后端通过 OpenCV ROI 输入准备，`tensorrt-cuda` 通过设备端 `RunRoi`。例如：

```powershell
dotnet run --project tools/DeploySharp.VisualBenchmark/DeploySharp.VisualBenchmark.csproj -c Release -- `
  --kind yolov8n --model E:\models\yolov8n.onnx --image E:\data\bus.jpg `
  --backend onnxruntime,onnxruntime-cuda,openvino,opencv-dnn,tensorrt,tensorrt-cuda `
  --roi 160,90,960,540 --mode both --warmup 10 --iterations 100 `
  --output artifacts\roi-benchmarks\visual.csv --json-output artifacts\roi-benchmarks\visual.json
```

这个入口当前只接收轴对齐矩形。需要任务专用辅助输入的模型会返回 `unsupported`；旋转、多边形、Mask 和滑窗应由对应任务 Runner 另行测量，不能把矩形结果替代它们。

提交新证据时，建议使用以下目录命名：

```text
artifacts/roi-benchmarks/<device-id>/<yyyyMMdd-HHmmss>/
  device.json
  visual.csv
  visual.json
  roi-matrix.md
  console.log
```

原始报告缺少输入 SHA、commit、P50/P95 或 GPU 时钟时，可以保留历史均值，但必须在表格中标为“历史/未记录”，不能并入标准分位数比较。OpenCV 的 `unsupported` 也必须保留 importer/算子错误信息；不要把它改成零耗时或“通过”。

## 当前证据边界

当前仓库已有 OpenCV ROI 的 stride、Gray/BGR/BGRA/RGBA、SubMat 生命周期和投影合同测试；本轮 OpenCV 测试结果为 81 通过、24 条因外部模型/运行时证据未配置而跳过。已有 TensorRT CUDA 矩形直接采样、YOLO 候选/Mask 后处理合同测试，Backend.TensorRT 为 58 通过、9 条环境跳过，Visual.TensorRT 为 7/7 通过。上述数字只证明合同和当前测试门禁，不是设备性能。

### windows-rtx3060-laptop-20260916：YOLOv8n 轴对齐 ROI

这是当前工作站的一次有效真实模型基线，不代表所有模型或所有后端均已完成验证。设备为 Windows `10.0.26200` x64、`.NET 10.0.12`、16 个逻辑处理器、可用内存约 16 GiB、`NVIDIA GeForce RTX 3060 Laptop GPU` 6144 MiB、驱动 `576.02`；报告记录的 GPU 状态为 `P0`，采样时钟为 `1425/7000 MHz`。这部分为补测前的 CPU 基线；同日后续 TensorRT/CUDA 有效实测见下方两设备补充章节。

协议：`yolov8n.onnx`、`E:\Data\image\bus.jpg`、ROI `100,100,600,800`（600×800 源像素）、warmup 5、iterations 20、`cold` 和 `steady`；结果指纹在 JSON 中保存。模型和输入路径属于本机证据，提交工作树为 dirty，未填写不存在的 commit SHA。

| 后端 | 模式 | Pre P50/P95 (ms) | Infer P50/P95 (ms) | Post P50/P95 (ms) | Total P50/P95 (ms) | 结果 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | cold | 21.647 / 29.184 | 40.205 / 44.258 | 4.190 / 5.078 | 66.417 / 74.269 | pass |
| ONNX Runtime CPU | steady | 0 / 0 | 38.088 / 42.012 | 4.220 / 5.295 | 42.324 / 48.135 | pass |
| OpenVINO CPU | cold | 16.345 / 17.393 | 27.794 / 33.718 | 2.818 / 3.754 | 47.483 / 52.514 | pass |
| OpenVINO CPU | steady | 0 / 0 | 27.437 / 29.314 | 2.820 / 3.486 | 30.158 / 32.309 | pass |
| OpenCV DNN CPU | cold | 15.634 / 17.215 | 81.994 / 95.352 | 2.717 / 4.945 | 101.155 / 113.437 | pass |
| OpenCV DNN CPU | steady | 0 / 0 | 78.282 / 81.954 | 2.566 / 3.125 | 80.771 / 84.746 | pass |

原始文件：`artifacts/roi-benchmarks/local-20260916/visual-cpu.csv` 和 `artifacts/roi-benchmarks/local-20260916/visual-cpu.json`。该证据同时证明本机 OpenCV 依赖闭包已经修复到 `JYPPX.OpenCV.CSharp.API 5.0.0`，并不证明 OpenCV 在所有模型上的 importer、算子或 GPU 路径都可用。

同一协议下的整图 steady 对照如下。它只用于说明该 ROI 输入在这次基线中的变化，不能解释为“裁剪必然更快”；裁剪后的预处理和模型工作量、后处理结果数量以及后端实现都会影响最终结果。

| 后端 | Full Total P50/P95 (ms) | ROI Total P50/P95 (ms) | P50 变化 |
| --- | ---: | ---: | ---: |
| ONNX Runtime CPU | 45.474 / 55.262 | 42.324 / 48.135 | -3.150 ms |
| OpenVINO CPU | 32.352 / 37.769 | 30.158 / 32.309 | -2.194 ms |
| OpenCV DNN CPU | 88.188 / 97.963 | 80.771 / 84.746 | -7.417 ms |

整图原始文件：`artifacts/roi-benchmarks/local-20260916/visual-full.csv` 和 `artifacts/roi-benchmarks/local-20260916/visual-full.json`。这次早期探测缺 engine 且孤立加载 CUDA provider 失败，原始行保留为历史不可用记录；同日后续已通过主库转换 engine 并修复 ORT 插件预检，不能再据此认定当前设备不支持 TensorRT/CUDA。

### windows-rtx3060-laptop-20260916：YOLOv8s 分类 ROI

同一设备再测一条分类链路，模型为 `yolov8s-cls.onnx`（SHA-256 `6d7265a72c1a9006e4faaf8ada744fbf72c32d53e6def3be05c125407adfdcee`），输入为 `E:\Data\image\demo_7.jpg`（`640x512`，SHA-256 `839511d6f6e7688d319b01d6977fc2cc642cb91e8fae4a2e0562c1322d894bf2`），ROI 为 `20,20,400,400`。协议仍为 warmup 5、iterations 20、cold/steady；本地工作树对应 HEAD `7a3ff4981c4b311937cee16e18359393ac26c8b1`，工作树有未提交修改。

| 后端 | 模式 | Pre P50/P95 (ms) | Infer P50/P95 (ms) | Post P50/P95 (ms) | Total P50/P95 (ms) | 结果 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| ONNX Runtime CPU | cold | 11.993 / 19.887 | 5.874 / 6.580 | 0.285 / 1.043 | 18.654 / 27.098 | pass |
| ONNX Runtime CPU | steady | 0 / 0 | 5.789 / 6.460 | 0.221 / 0.240 | 6.051 / 6.663 | pass |
| OpenVINO CPU | cold | 7.074 / 8.723 | 6.239 / 8.647 | 0.188 / 0.261 | 13.626 / 18.432 | pass |
| OpenVINO CPU | steady | 0 / 0 | 5.484 / 7.028 | 0.226 / 0.612 | 5.714 / 7.597 | pass |
| OpenCV DNN CPU | cold | 7.479 / 8.000 | 11.374 / 12.251 | 0.287 / 0.466 | 18.942 / 20.379 | pass |
| OpenCV DNN CPU | steady | 0 / 0 | 12.057 / 13.755 | 0.221 / 0.518 | 12.300 / 13.888 | pass |

分类整图 steady 对照为 ORT `6.394/7.481 ms`、OpenVINO `6.512/8.739 ms`、OpenCV DNN `12.937/14.245 ms`；对应 ROI P50 变化分别为 `-0.343 ms`、`-0.798 ms`、`-0.637 ms`。原始文件位于 `artifacts/roi-benchmarks/local-20260916/classification/visual-cpu.csv`、`visual-cpu.json`、`visual-full.csv` 和 `visual-full.json`。ROI 分类 top-1 结果指纹与整图不同是预期行为，因为输入区域不同；这不是后端一致性失败。


## 2026-09-16 补充实测：Windows 与 Linux GPU

下面的记录替代早期“缺 engine / CUDA provider 不可用”的结论。TensorRT engine 由主库 `TensorRtOnnxEngineBuilder` 在各目标设备上从 ONNX 转换；没有跨设备复用 engine。ORT CUDA 使用 CUDA 12 对应的 1.23.2 包；修正预检后，由 ORT 正确初始化 provider host 再加载 CUDA 插件，不能用孤立加载插件的失败判断 CUDA 不可用。

### 统一输入与计时边界

- HEAD：`7a3ff4981c4b311937cee16e18359393ac26c8b1` 加本轮未提交修改（不是该提交的纯净版本）；两台机器运行相同修正版托管程序集。
- 输入：`bus.jpg`，810×1080，SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`。
- YOLOv8n：SHA-256 `50e299e848bb2586ca7fc5bfebd42eda43d43566cbb9a3ed7a3375243b0dbdf4`。
- YOLOv8n-seg：SHA-256 `986ba70310322ad2d5aec429c4a07d27d3a1c1f5a4eb8f9127ae7c2d358be5c2`。
- ROI：源图矩形 `100,100,600,800`；模型输入 640×640，Batch=1、单 Session/context/stream，warmup=5、iterations=20，单位均为 ms。
- **Cold** 每次读取、解码、准备图像后推理、后处理；不含进程启动、Session 构造和 engine 构建。这里的 cold 不是首次模型加载延迟。
- **Steady** 普通后端复用已准备的 Tensor，预处理为 0；普通 TensorRT 还复用已上传 Tensor。**TensorRT CUDA 的 steady 只复用已解码 BGR，每次仍上传并进行 CUDA 预处理**。它们不是完全相同的端到端工作量，不能直接将 steady 差值解释为 GPU 前处理的全部收益。
- 单 ROI 没有跨 ROI merge，此表不填虚构的 merge 或独立 H2D/D2H kernel 时间。阶段为公开 Pipeline 墙钟计时；各阶段 P50 相加不一定等于 Total P50。
- 未锁频；记录为短测期间的 Boost 状态快照，不是全程频率采样或最优硬件性能保证。20 次样本可复现功能基线，不是高置信度长时 P95。

### Windows / RTX 3060 Laptop（6 GiB）

Windows x64，OS 内核 `10.0.26200`、.NET 10.0.12、16 逻辑处理器、约 16 GiB 内存；驱动 576.02、CUDA 12.9、cuDNN 9.22、TensorRT 11、compute_86。GPU 后采样 P0，核心 1425–1950 MHz、显存 7000 MHz；无外部锁频设置。下列 12 条 cold/steady 记录全部 pass。

| 模型 | 后端 | Cold Pre P50/P95 | Cold Infer P50/P95 | Cold Post P50/P95 | Cold Total P50/P95 | Steady Total P50/P95 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| yolov8n | tensorrt | 18.932 / 22.970 | 8.935 / 9.895 | 3.088 / 3.574 | 30.915 / 34.788 | 9.182 / 10.290 |
| yolov8n | tensorrt-cuda | 12.308 / 14.857 | 4.903 / 7.750 | 2.216 / 2.886 | 20.489 / 23.298 | 7.447 / 8.814 |
| yolov8n | onnxruntime-cuda | 14.174 / 55.406 | 12.641 / 13.672 | 2.858 / 3.318 | 29.990 / 70.457 | 15.546 / 18.769 |
| yolov8n-seg | tensorrt | 24.813 / 34.871 | 13.264 / 18.113 | 10.712 / 13.001 | 48.778 / 62.037 | 19.179 / 23.034 |
| yolov8n-seg | onnxruntime-cuda | 18.370 / 20.827 | 17.166 / 19.277 | 9.358 / 11.711 | 44.054 / 49.373 | 20.724 / 23.437 |
| yolov8n-seg | tensorrt-cuda | 12.556 / 13.796 | 5.420 / 6.359 | 2.735 / 4.687 | 21.311 / 23.751 | 7.817 / 9.328 |

原始报告：`artifacts/roi-benchmarks/local-20260916/gpu/` 中的 `visual`（只采用 TensorRT 成功行）、`ort-fixed`、`segmentation`（只采用普通 TensorRT/ORT 成功行）、`seg-fixed`，均有 CSV/JSON。失败探测记录保留用于追溯，不纳入上表成功结果。

### Ubuntu 22.04 / RTX 2060（6 GiB）

设备 `NUC11PHi7`，Ubuntu 22.04.5 x64、kernel 6.8.0-138、Intel i7-1165G7（4 核 8 线程）、约 16 GiB 内存、.NET 10.0.9；驱动 580.178.04、CUDA 12.9、cuDNN 9.22、TensorRT 11、compute_75。ORT CUDA 1.23.2、OpenCV API/native 5.0；OpenVINO runtime 2026.3.1、C# API 3.3.1。GPU 初始 P8 300/405 MHz，测试后 P0 1005/5500 MHz。下列 24 条 cold/steady 记录全部 pass。

| 模型 | 后端 | Cold Pre P50/P95 | Cold Infer P50/P95 | Cold Post P50/P95 | Cold Total P50/P95 | Steady Total P50/P95 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| yolov8n | onnxruntime | 15.722 / 22.539 | 42.151 / 62.331 | 3.576 / 4.326 | 66.723 / 82.942 | 59.414 / 61.073 |
| yolov8n | onnxruntime-cuda | 14.438 / 15.719 | 12.476 / 12.619 | 2.348 / 2.464 | 29.360 / 30.559 | 14.875 / 15.551 |
| yolov8n | openvino | 14.475 / 16.561 | 24.923 / 27.393 | 2.515 / 3.184 | 42.130 / 46.441 | 26.854 / 27.662 |
| yolov8n | opencv-dnn | 14.418 / 15.189 | 41.512 / 43.583 | 2.455 / 2.896 | 58.348 / 60.986 | 43.620 / 51.674 |
| yolov8n | tensorrt | 10.957 / 11.583 | 10.802 / 11.516 | 2.388 / 2.545 | 24.238 / 25.192 | 8.870 / 9.750 |
| yolov8n | tensorrt-cuda | 6.879 / 7.356 | 5.836 / 5.884 | 2.355 / 2.457 | 15.596 / 16.150 | 9.721 / 10.232 |
| yolov8n-seg | onnxruntime | 14.541 / 23.939 | 50.734 / 66.566 | 12.110 / 20.204 | 82.206 / 95.980 | 95.394 / 96.785 |
| yolov8n-seg | onnxruntime-cuda | 13.653 / 15.680 | 14.628 / 15.060 | 9.300 / 10.111 | 37.368 / 39.587 | 22.789 / 23.207 |
| yolov8n-seg | openvino | 14.480 / 14.940 | 33.779 / 34.986 | 9.645 / 10.246 | 57.677 / 59.521 | 43.209 / 43.909 |
| yolov8n-seg | opencv-dnn | 13.539 / 14.632 | 95.477 / 96.353 | 9.512 / 9.971 | 118.796 / 119.631 | 104.301 / 106.419 |
| yolov8n-seg | tensorrt | 10.378 / 10.892 | 14.341 / 14.657 | 9.171 / 9.520 | 33.908 / 35.045 | 20.175 / 21.745 |
| yolov8n-seg | tensorrt-cuda | 6.276 / 6.840 | 5.775 / 5.782 | 2.090 / 2.592 | 14.899 / 15.942 | 9.078 / 10.053 |

原始报告：`artifacts/roi-benchmarks/linux-20260916/yolov8n.{csv,json,log}`、`yolov8n-seg.{csv,json,log}`。远端保留在 `/home/ygj/DeploySharp-ROI/results/`。原始产物属于运行证据，不随 NuGet 包分发。

### 设备端结果一致性与适用范围

两台设备都启用了 `DEPLOYSHARP_TENSORRT_CUDA_VALIDATE_POSTPROCESSING=1`。YOLOv8n-seg 在同一次 TensorRT 输出上的 CPU/GPU 结果元数据一致，源图掩码逐像素差异 **0**。修复了 CropBeforeResize 原型裁剪后恢复阶段重复裁剪的问题，并加入分数边界与投影参考路径的回归测试。CUDA 确定性 NMS 候选上限为 32768，8400 候选的实测模型可进入设备路径；超界、不兼容模型仍 fallback。

这不是所有任务/几何的全后端验证：旋转、透视、多边形、Mask ROI 使用已有 OpenCV 精确准备路径；TensorRT 设备路径仍限定静态 Batch=1 轴对齐输入。多 context 池不等于真模型 Batch，最佳并发数需要应用负载实测。VLM/VQA 真模型、SAM3 CUDA 和数万帧真实 GPU soak 没有被此表覆盖。

### 官方 SAM 短视频证据

同一 Linux 设备使用 Meta SAM2 源码 `2b90b9f5ceec907a1c18123530e92e794ad901a4`、`sam2.1_hiera_tiny.pt`、Python 3.12.14、PyTorch 2.10.0+cpu，在官方 `bedroom.mp4` 的前 6 帧执行 ROI 初始化、传播、修正和重置。每帧得到源图掩码，重置后首帧掩码 SHA-256 一致，`ResetConsistent=true`。这是官方 PyTorch CPU Predictor 的真实短测，不是原生 ONNX/TensorRT SAM Bundle，也不报告 6 帧为稳态性能分位数。结果保存为 `artifacts/roi-benchmarks/linux-20260916/sam2-report.json`；操作见[双语官方适配案例](https://github.com/guojin-yan/DeploySharp/tree/DeploySharpV2.0/samples/02-visual/roi-official-sam)。

SAM3 适配入口已提供；该固定官方版本 CPU 构造路径在 `PositionEmbeddingSine` 缓存初始化中直接分配 CUDA Tensor，CPU 实测失败，不能宣称支持。必须使用匹配 CUDA 的 PyTorch 和足够显存完成后续验证；本轮没有修改第三方实现来伪装通过。VLM/VQA 真模型和长期 soak 按当前范围暂缓。

补充 CUDA 实测：独立 Python 3.12.14 / PyTorch 2.10.0+cu128 已成功识别 RTX2060。SAM3 首帧 FP16 autocast 推理失败于显存不足：申请 1.60 GiB 时仅余 1.53 GiB；额外 FP16 权重存储实验仍 OOM（申请 412 MiB，仅余 116.94 MiB）。低精度权重实验未作为默认行为保留。该 6GB 设备不能记为 SAM3 通过；原始日志为 `sam3-cuda-01.log`、`sam3-cuda-fp16-01.log`。这是官方模型在当前配置的显存约束，不再是 CUDA provider 未安装。
