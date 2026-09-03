# 设备性能实测

本文只记录具名设备、明确模型制品和固定测试口径下的推理结果。数字是特定设备上的观测值，不构成其他硬件、模型版本或运行时配置的性能承诺。

## 统一口径

- 视觉模型的 `steady` 结果复用已解码、已准备的输入，包含后端执行与 DeploySharp 后处理，不包含模型加载、OpenVINO 编译、TensorRT engine 构建和 CUDA 初始化。
- PaddleOCR 只记录完整 `det -> crop -> cls/orientation -> rec -> merge` 流水线；不会将单独的检测、分类或识别耗时当作完整 OCR 结果。
- `通过` 表示本轮结果合同通过。`P50` 和 `P95` 未在原始结果中采集时记为“未记录”，不以估算值替代。
- 模型/后端是否支持、未测试或当前不支持的状态以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准；本页只列出已经得到有效耗时的组合。

## Windows 10 / RTX 2060

### 测试环境

| 项目 | 配置 |
| --- | --- |
| 测试日期 | 2026-08-28 至 2026-09-02 |
| 代码版本 | DeploySharp `2.0.0-alpha.1` 测试构建；提交号未记录 |
| 操作系统 | Windows 10 x64，build `10.0.19045.6466` |
| CPU / 内存 | 本轮结果未记录 |
| GPU | NVIDIA GeForce RTX 2060，6 GB |
| NVIDIA 驱动 | `576.02` |
| CUDA / cuDNN / TensorRT | CUDA `12.9`、cuDNN `9.22`、TensorRT `11.0.0.114` |
| .NET | SDK `10.0.301`；运行时验证使用 .NET `8.0.28` |
| 视觉输入 | `bus.jpg` |
| OCR 输入 | `E:\Data\ocr\demo_1.jpg` |

### 视觉模型：CPU 后端稳态口径

口径：每个模型 5 次预热、20 次计时，复用已准备的输入。模型制品为当前视觉案例对应 ONNX 文件，精度为 FP32。

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
| 代码版本 | 便携测试包运行 `20260830-092323`；提交号未记录 |
| 操作系统 | Windows 10 x64，build `10.0.26200` |
| CPU | Intel Core i7-14700KF，20 个物理核心 / 28 个逻辑处理器，报告基准频率 `3.4 GHz` |
| 内存 | `34,127,826,944` bytes（约 32 GB） |
| GPU | NVIDIA GeForce RTX 5060 Ti，`16,311 MiB` |
| NVIDIA 驱动 | `581.57` |
| CUDA / TensorRT / cuDNN | CUDA `12.9`、TensorRT API `11`、`compute_120`；cuDNN 版本未记录 |
| .NET | .NET `10.0.9` x64 |
| 视觉输入 | 便携包 `data/bus.jpg` |
| OCR 输入 | 便携包 `data/ocr-demo.jpg` |

### 视觉模型稳态口径

口径：每个模型 3 次预热、10 次计时，复用已准备的 `data/bus.jpg` 输入，FP32，模型加载不计入。该轮原始数据未记录 P50/P95。表中只保留通过的数值单元格；未通过组合见验证矩阵。

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
