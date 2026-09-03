# 设备性能实测

本文只记录具名设备、明确模型制品和固定测试口径下的推理结果。数字是特定设备上的观测值，不构成其他硬件、模型版本或运行时配置的性能承诺。

## 统一口径

- 视觉模型的 `steady` 结果复用已解码、已准备的输入，包含后端执行与 DeploySharp 后处理，不包含模型加载、OpenVINO 编译、TensorRT engine 构建和 CUDA 初始化。`cold` 结果才包含一次输入解码和预处理。
- 标准输入名固定为视觉 `data/bus.jpg`、OCR `data/ocr-demo.jpg`；设备可以把文件复制到其他路径，但报告必须同时记录输入 SHA-256。历史 RTX 2060 OCR 记录使用 `E:\Data\ocr\demo_1.jpg`，视为同一标准输入的路径别名；若没有 SHA-256 不能与新记录做字节级比较。
- 标准协议为 5 次预热、50 次计时；每次计时都记录总耗时和预处理/推理/后处理分段耗时，并从原始样本计算 mean、P50、P95。只有原始报告确实采集了分位数时才填写数值，不能用 mean 推算。
- 每个设备小节必须记录 DeploySharp commit SHA、模型制品 SHA-256、输入 SHA-256、运行时版本、batch/Session、CPU/GPU 功耗与锁频状态。历史便携包缺少这些字段时明确标注“未记录”，不与标准记录混排。
- PaddleOCR 只记录完整 `det -> crop -> cls/orientation -> rec -> merge` 流水线；不会将单独的检测、分类或识别耗时当作完整 OCR 结果。
- `通过` 表示本轮结果合同通过；“历史均值”只表示旧报告曾通过，不代表满足当前分位数协议。
- 模型/后端是否支持、未测试或当前不支持的状态以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准；本页只列出已经得到有效耗时的组合。

## Windows 10 / RTX 2060

### 测试环境

| 项目 | 配置 |
| --- | --- |
| 测试日期 | 2026-08-28 至 2026-09-02 |
| 代码版本 | DeploySharp `2.0.0-alpha.1` 测试构建；历史报告未记录 commit SHA |
| 操作系统 | Windows 10 x64，build `10.0.19045.6466` |
| CPU / 内存 | 本轮结果未记录 |
| GPU | NVIDIA GeForce RTX 2060，6 GB |
| NVIDIA 驱动 | `576.02` |
| CUDA / cuDNN / TensorRT | CUDA `12.9`、cuDNN `9.22`、TensorRT `11.0.0.114` |
| .NET | SDK `10.0.301`；运行时验证使用 .NET `8.0.28` |
| 视觉输入 | 标准 `data/bus.jpg`；历史报告未保存输入 SHA-256 |
| OCR 输入 | 标准 `data/ocr-demo.jpg` 的路径别名 `E:\Data\ocr\demo_1.jpg`；历史报告未保存输入 SHA-256 |

### 视觉模型：CPU 后端历史均值

口径：历史记录每个模型 5 次预热、20 次计时，复用已准备的输入。原始报告没有 P50/P95 和 commit/input SHA，因此只作为方向性均值基线，不满足当前标准协议。

| 模型 | 任务 | 后端 | 精度 | 输入规格 | 平均推理时间（ms） | P50（ms） | P95（ms） | 状态 |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| YOLOv8n | 检测 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 37.660 | 未记录 | 未记录 | 通过 |
| YOLOv8n | 检测 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 26.859 | 未记录 | 未记录 | 通过 |
| YOLOv8n | 检测 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 70.809 | 未记录 | 未记录 | 通过 |
| YOLOv8n-seg | 分割 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 64.977 | 未记录 | 未记录 | 通过 |
| YOLOv8n-seg | 分割 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 48.912 | 未记录 | 未记录 | 通过 |
| YOLOv8n-seg | 分割 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 352.975 | 未记录 | 未记录 | 通过 |
| YOLOv8s-pose | 姿态 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 100.708 | 未记录 | 未记录 | 通过 |
| YOLOv8s-pose | 姿态 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 88.393 | 未记录 | 未记录 | 通过 |
| YOLOv8s-pose | 姿态 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 165.670 | 未记录 | 未记录 | 通过 |
| YOLOv8s-obb | 旋转框 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 313.107 | 未记录 | 未记录 | 通过 |
| YOLOv8s-obb | 旋转框 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 229.729 | 未记录 | 未记录 | 通过 |
| YOLOv8s-obb | 旋转框 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 432.015 | 未记录 | 未记录 | 通过 |
| PaDiM | 异常检测 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 35.699 | 未记录 | 未记录 | 通过 |
| PaDiM | 异常检测 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 33.566 | 未记录 | 未记录 | 通过 |
| PaDiM | 异常检测 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 53.771 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 1.4 | 背景移除 | ONNX Runtime CPU | FP32 | 已准备 `bus.jpg` | 1167.398 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 1.4 | 背景移除 | OpenVINO CPU | FP32 | 已准备 `bus.jpg` | 924.965 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 1.4 | 背景移除 | OpenCV DNN CPU | FP32 | 已准备 `bus.jpg` | 1419.603 | 未记录 | 未记录 | 通过 |

### 视觉模型：TensorRT CUDA 稳态口径

口径：每个模型 10 次预热、50 次计时，TensorRT 11 CUDA 视觉流水线，FP32，复用已准备的 `bus.jpg` 输入。

| 模型 | 任务 | 后端 | 精度 | 输入规格 | 平均推理时间（ms） | P50（ms） | P95（ms） | 状态 |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| YOLOv8n | 检测 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 8.760 | 未记录 | 未记录 | 通过 |
| YOLOv8n-seg | 分割 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 20.628 | 未记录 | 未记录 | 通过 |
| YOLOv8s-pose | 姿态 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 10.023 | 未记录 | 未记录 | 通过 |
| YOLOv8s-obb | 旋转框 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 19.207 | 未记录 | 未记录 | 通过 |
| PaDiM | 异常检测 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 11.642 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 1.4 | 背景移除 | TensorRT CUDA | FP32 | 已准备 `bus.jpg` | 60.995 | 未记录 | 未记录 | 通过 |

### 视觉模型：2026-09-02 标准分位数复测

来源为 `artifacts/remote-test/visual-followup-20260902.csv`（ORT/OpenVINO）和 `artifacts/remote-test/visual-tensorrt-cuda-postopt-final-20260829.csv`（TensorRT CUDA）。两份原始文件均使用已准备的 `bus.jpg`，并采集了 mean/P50/P95，但没有在 CSV 中记录输入 SHA、commit SHA 或 warmup/iteration 参数；因此这里保留设备实测分位数，后续复测必须补齐这些字段。计时包含后端推理、DeploySharp 后处理和编排，不包含模型加载。

| 模型 | 后端 | 总耗时 mean（ms） | P50（ms） | P95（ms） | 预处理 mean（ms） | 推理 mean（ms） | 后处理 mean（ms） |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| YOLOv8n | ONNX Runtime CPU | 75.614 | 70.890 | 102.120 | 0.000 | 70.226 | 5.358 |
| YOLOv8n-seg | ONNX Runtime CPU | 124.587 | 113.310 | 184.715 | 0.000 | 103.680 | 20.876 |
| YOLOv8s-pose | ONNX Runtime CPU | 115.745 | 114.023 | 143.346 | 0.000 | 115.137 | 0.583 |
| YOLOv8s-obb | ONNX Runtime CPU | 315.670 | 300.786 | 374.823 | 0.000 | 314.185 | 1.470 |
| PaDiM | ONNX Runtime CPU | 31.782 | 30.325 | 40.253 | 0.000 | 27.082 | 4.683 |
| BRIA RMBG 1.4 | ONNX Runtime CPU | 1229.172 | 1227.256 | 1349.439 | 0.000 | 1223.891 | 5.267 |
| YOLOv8n | OpenVINO CPU | 30.443 | 29.642 | 36.281 | 0.000 | 28.021 | 2.416 |
| YOLOv8n-seg | OpenVINO CPU | 44.778 | 44.768 | 45.531 | 0.000 | 34.840 | 9.932 |
| YOLOv8s-pose | OpenVINO CPU | 78.919 | 79.005 | 79.546 | 0.000 | 78.409 | 0.505 |
| YOLOv8s-obb | OpenVINO CPU | 209.306 | 208.923 | 211.929 | 0.000 | 207.900 | 1.402 |
| PaDiM | OpenVINO CPU | 32.558 | 31.997 | 35.080 | 0.000 | 28.645 | 3.907 |
| BRIA RMBG 1.4 | OpenVINO CPU | 864.636 | 849.476 | 924.939 | 0.000 | 860.165 | 4.465 |
| YOLOv8n | TensorRT CUDA | 8.695 | 8.584 | 10.178 | 0.760 | 5.172 | 2.135 |
| YOLOv8n-seg | TensorRT CUDA | 20.723 | 20.640 | 21.496 | 0.978 | 10.481 | 8.591 |
| YOLOv8s-pose | TensorRT CUDA | 9.973 | 9.693 | 11.035 | 0.729 | 8.263 | 0.341 |
| YOLOv8s-obb | TensorRT CUDA | 19.201 | 19.155 | 19.546 | 0.836 | 16.512 | 1.210 |
| PaDiM | TensorRT CUDA | 11.548 | 11.280 | 12.663 | 0.826 | 6.311 | 3.711 |
| BRIA RMBG 1.4 | TensorRT CUDA | 61.014 | 60.991 | 61.725 | 0.954 | 54.871 | 4.494 |

这组标准分位数记录没有 GPU 遥测，因此不能判断每次运行是否处于固定 P0/锁频状态；后续 GPU 记录必须同时保存时钟、功耗上限和 thermal/hardware slowdown 字段。

### PaddleOCR：TensorRT 完整流水线稳态口径

口径：动态 TensorRT 11 engine，FP32，输入 `E:\Data\ocr\demo_1.jpg`，10 次预热、50 次计时，复用已准备输入。检测阶段每张图执行一次；识别阶段按下表选择稳定的 batch 和独立 Session 数量。所有记录均返回 16 个文本区域。

| 模型 | 任务 | 后端 | 精度 | 输入规格 | 平均推理时间（ms） | P50（ms） | P95（ms） | 状态 |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| PP-OCRv4 mobile | 完整 OCR 流水线 | TensorRT | FP32 | `demo_1.jpg`；batch 8；2 个阶段 Session | 33.194 | 32.505 | 37.394 | 通过 |
| PP-OCRv5 mobile | 完整 OCR 流水线 | TensorRT | FP32 | `demo_1.jpg`；batch 8；2 个阶段 Session | 46.785 | 46.090 | 50.015 | 通过 |
| PP-OCRv6 tiny | 完整 OCR 流水线 | TensorRT | FP32 | `demo_1.jpg`；batch 8；1 个阶段 Session | 19.548 | 19.527 | 20.339 | 通过 |
| PP-OCRv6 small | 完整 OCR 流水线 | TensorRT | FP32 | `demo_1.jpg`；batch 8；2 个阶段 Session | 32.166 | 31.511 | 36.351 | 通过 |
| PP-OCRv6 medium | 完整 OCR 流水线 | TensorRT | FP32 | `demo_1.jpg`；batch 8；2 个阶段 Session | 81.351 | 80.692 | 85.247 | 通过 |

## Windows 10 / Intel Core i7-14700KF / RTX 5060 Ti

### 测试环境

| 项目 | 配置 |
| --- | --- |
| 测试日期 | 2026-08-30 |
| 代码版本 | 便携测试包运行 `20260830-092323`；历史包未记录 commit SHA，不能与当前标准协议直接比较 |
| 操作系统 | Windows 10 x64，build `10.0.26200` |
| CPU | Intel Core i7-14700KF，20 个物理核心 / 28 个逻辑处理器，报告基准频率 `3.4 GHz` |
| 内存 | `34,127,826,944` bytes（约 32 GB） |
| GPU | NVIDIA GeForce RTX 5060 Ti，`16,311 MiB` |
| NVIDIA 驱动 | `581.57` |
| CUDA / TensorRT / cuDNN | CUDA `12.9`、TensorRT API `11`、`compute_120`；cuDNN 版本未记录 |
| .NET | .NET `10.0.9` x64 |
| 视觉输入 | 便携包 `data/bus.jpg`；输入 SHA-256 未记录 |
| OCR 输入 | 便携包 `data/ocr-demo.jpg`；输入 SHA-256 未记录，不能证明与 `demo_1.jpg` 字节相同 |

### 视觉模型稳态口径

口径：历史便携包每个模型 3 次预热、10 次计时，复用已准备的 `data/bus.jpg` 输入，FP32，模型加载不计入。该轮原始数据未记录 P50/P95、commit SHA 或输入 SHA，因此表中数值仅作历史均值参考；未通过组合见验证矩阵。后续同设备复测使用统一的 5/50 协议。

| 模型 | 任务 | 后端 | 精度 | 输入规格 | 平均推理时间（ms） | P50（ms） | P95（ms） | 状态 |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| DEIMv2 | 检测 | ONNX Runtime CPU | FP32 | 已准备 `data/bus.jpg` | 116.007 | 未记录 | 未记录 | 通过 |
| DEIMv2 | 检测 | OpenVINO CPU | FP32 | 已准备 `data/bus.jpg` | 114.377 | 未记录 | 未记录 | 通过 |
| DEIMv2 | 检测 | TensorRT | FP32 | 已准备 `data/bus.jpg` | 9.351 | 未记录 | 未记录 | 通过 |
| PaDiM | 异常检测 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 15.626 / 5.355 / 13.407 / 31.107 / 3.989 / 4.060 | 未记录 | 未记录 | 通过 |
| PP-YOLOE | 检测 | ONNX Runtime CPU / CUDA | FP32 | 已准备 `data/bus.jpg` | 101.953 / 33.058 | 未记录 | 未记录 | 通过 |
| RF-DETR | 检测 | ONNX Runtime CPU | FP32 | 已准备 `data/bus.jpg` | 90.999 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 1.4 | 背景移除 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 364.413 / 42.940 / 366.734 / 933.514 / 25.141 / 22.756 | 未记录 | 未记录 | 通过 |
| BRIA RMBG 2.0 | 背景移除 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT | FP32 | 已准备 `data/bus.jpg` | 7864.023 / 538.860 / 7424.923 / 201.242 | 未记录 | 未记录 | 通过 |
| RT-DETR decoded ONNX | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT | FP32 | 已准备 `data/bus.jpg` | 223.501 / 17.359 / 168.562 / 10.653 | 未记录 | 未记录 | 通过 |
| RT-DETR raw | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT | FP32 | 已准备 `data/bus.jpg` | 236.975 / 17.456 / 181.107 / 10.843 | 未记录 | 未记录 | 通过 |
| YOLO11n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 15.949 / 6.860 / 12.374 / 5.116 / 4.437 | 未记录 | 未记录 | 通过 |
| YOLO11s-obb | 旋转框 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 83.408 / 11.841 / 90.739 / 264.354 / 6.938 / 7.149 | 未记录 | 未记录 | 通过 |
| YOLO11s-pose | 姿态 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 30.114 / 6.940 / 30.870 / 108.119 / 3.991 / 3.970 | 未记录 | 未记录 | 通过 |
| YOLO11s-seg | 分割 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 50.831 / 15.143 / 47.799 / 238.904 / 12.787 / 5.339 | 未记录 | 未记录 | 通过 |
| YOLO12n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 20.590 / 8.167 / 13.965 / 5.511 / 5.944 | 未记录 | 未记录 | 通过 |
| YOLO13n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 24.885 / 9.641 / 16.565 / 6.488 / 6.379 | 未记录 | 未记录 | 通过 |
| YOLO26n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 12.048 / 5.105 / 9.936 / 2.224 / 2.876 | 未记录 | 未记录 | 通过 |
| YOLO26s-obb | 旋转框 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 77.658 / 11.134 / 76.032 / 291.134 / 5.665 / 5.959 | 未记录 | 未记录 | 通过 |
| YOLO26s-pose | 姿态 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 29.639 / 6.949 / 30.166 / 120.952 / 3.242 / 3.460 | 未记录 | 未记录 | 通过 |
| YOLO26s-seg | 分割 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 50.530 / 13.658 / 46.112 / 239.468 / 9.504 / 5.168 | 未记录 | 未记录 | 通过 |
| YOLOv5n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 11.358 / 7.351 / 11.200 / 5.860 / 5.054 | 未记录 | 未记录 | 通过 |
| YOLOv5s-seg | 分割 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 40.219 / 17.866 / 42.873 / 166.592 / 16.489 / 5.481 | 未记录 | 未记录 | 通过 |
| YOLOv6s | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 36.644 / 7.740 / 42.558 / 7.996 / 6.035 | 未记录 | 未记录 | 通过 |
| YOLOv7 | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT | FP32 | 已准备 `data/bus.jpg` | 101.047 / 13.212 / 117.984 / 9.461 | 未记录 | 未记录 | 通过 |
| YOLOv8n | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 15.281 / 6.682 / 13.342 / 5.050 / 4.004 | 未记录 | 未记录 | 通过 |
| YOLOv8n-seg | 分割 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 27.082 / 14.819 / 24.921 / 130.889 / 11.867 / 4.229 | 未记录 | 未记录 | 通过 |
| YOLOv8s-cls | 分类 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT | FP32 | 已准备 `data/bus.jpg` | 2.324 / 1.738 / 2.608 / 8.486 / 0.766 | 未记录 | 未记录 | 通过 |
| YOLOv8s-obb | 旋转框 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 91.536 / 11.695 / 92.086 / 272.182 / 7.006 / 7.327 | 未记录 | 未记录 | 通过 |
| YOLOv8s-pose | 姿态 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 33.200 / 6.857 / 35.262 / 126.708 / 4.007 / 4.172 | 未记录 | 未记录 | 通过 |
| YOLOv9c-seg | 分割 | ONNX Runtime CPU / CUDA / OpenVINO / OpenCV DNN / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 155.974 / 23.471 / 168.321 / 382.681 / 20.181 / 13.325 | 未记录 | 未记录 | 通过 |
| YOLOv9s | 检测 | ONNX Runtime CPU / CUDA / OpenVINO / TensorRT / TensorRT CUDA | FP32 | 已准备 `data/bus.jpg` | 36.799 / 12.627 / 34.758 / 7.026 / 6.736 | 未记录 | 未记录 | 通过 |

同一行中的后端与耗时按照列出的相同顺序一一对应；该压缩表达只用于保留同一模型、同一口径下已通过的全部后端数据，并不代表这些后端使用同一执行设备。

### PaddleOCR 完整流水线口径

口径：输入 `data/ocr-demo.jpg`，每个组合 5 次预热、10 次计时；自动搜索独立阶段 Session 数量 `1,2,4` 与识别 batch `1,2,4,8,16`，记录最优稳定完整流水线总耗时。`输入规格`列中的 batch 与 Session 是选中的组合；P50/P95 未记录。

| 模型 | 任务 | 后端 | 精度 | 输入规格 | 平均推理时间（ms） | P50（ms） | P95（ms） | 状态 |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| PP-OCRv4 mobile | 完整 OCR 流水线 | ONNX Runtime CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 1113.508 | 未记录 | 未记录 | 通过 |
| PP-OCRv4 mobile | 完整 OCR 流水线 | ONNX Runtime CUDA | FP32 | `ocr-demo.jpg`；batch 8；2 个 Session | 92.502 | 未记录 | 未记录 | 通过 |
| PP-OCRv4 mobile | 完整 OCR 流水线 | OpenCV DNN CPU | FP32 | `ocr-demo.jpg`；batch 1；4 个 Session | 629.645 | 未记录 | 未记录 | 通过 |
| PP-OCRv4 mobile | 完整 OCR 流水线 | TensorRT | FP32 | `ocr-demo.jpg`；batch 8；2 个 Session | 20.872 | 未记录 | 未记录 | 通过 |
| PP-OCRv5 mobile | 完整 OCR 流水线 | ONNX Runtime CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 993.741 | 未记录 | 未记录 | 通过 |
| PP-OCRv5 mobile | 完整 OCR 流水线 | ONNX Runtime CUDA | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 85.914 | 未记录 | 未记录 | 通过 |
| PP-OCRv5 mobile | 完整 OCR 流水线 | OpenCV DNN CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 579.912 | 未记录 | 未记录 | 通过 |
| PP-OCRv5 mobile | 完整 OCR 流水线 | TensorRT | FP32 | `ocr-demo.jpg`；batch 8；2 个 Session | 21.357 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 tiny | 完整 OCR 流水线 | ONNX Runtime CPU | FP32 | `ocr-demo.jpg`；batch 1；2 个 Session | 419.364 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 tiny | 完整 OCR 流水线 | ONNX Runtime CUDA | FP32 | `ocr-demo.jpg`；batch 16；1 个 Session | 50.408 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 tiny | 完整 OCR 流水线 | OpenCV DNN CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 265.286 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 tiny | 完整 OCR 流水线 | TensorRT | FP32 | `ocr-demo.jpg`；batch 16；1 个 Session | 14.851 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 small | 完整 OCR 流水线 | ONNX Runtime CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 658.259 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 small | 完整 OCR 流水线 | ONNX Runtime CUDA | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 81.061 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 small | 完整 OCR 流水线 | OpenCV DNN CPU | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 519.439 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 small | 完整 OCR 流水线 | TensorRT | FP32 | `ocr-demo.jpg`；batch 8；2 个 Session | 18.825 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 medium | 完整 OCR 流水线 | ONNX Runtime CPU | FP32 | `ocr-demo.jpg`；batch 2；4 个 Session | 1397.720 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 medium | 完整 OCR 流水线 | ONNX Runtime CUDA | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 106.863 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 medium | 完整 OCR 流水线 | OpenCV DNN CPU | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 1381.233 | 未记录 | 未记录 | 通过 |
| PP-OCRv6 medium | 完整 OCR 流水线 | TensorRT | FP32 | `ocr-demo.jpg`；batch 4；4 个 Session | 33.983 | 未记录 | 未记录 | 通过 |

## 复现

设备测试包中的 `Run-DeviceBenchmark.ps1` 会记录系统、运行时和结果文件。运行前应先准备与目标设备匹配的模型、ONNX Runtime、OpenVINO、OpenCV、CUDA、TensorRT 和 cuDNN；TensorRT 测试还需要匹配版本的 bridge。每次在新的设备、模型制品、运行时版本、输入图片或 batch/Session 配置上运行后，都应新增独立设备小节，而不是替换其他设备的结果。
