# 设备性能实测

本页汇总 DeploySharp 在具名设备上的可复现观测值，帮助用户估算后端选择和容量。数字只适用于表中模型、输入、运行时和计时范围，不是硬件排名或跨设备性能承诺。模型是否受支持仍以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 测试协议

| 项目 | 规则 |
| --- | --- |
| 标准输入 | 视觉使用 `bus.jpg`，OCR 使用 `ocr-demo.jpg`；报告保存输入 SHA-256 |
| 稳态 | 复用已创建的 Session 和已准备输入；包含后端执行、decoder 与编排 |
| 端到端 | 额外包含读取、解码和预处理；不包含模型下载、TensorRT Engine 构建或进程启动 |
| 重复次数 | 正式记录使用 5 次预热、50 次测量，并从原始样本计算 P50/P95 |
| 身份 | 保存 DeploySharp commit、模型/Engine/输入 SHA-256、runtime、驱动和设备 |
| 并发 | 记录真实 Batch、独立 Session/context 数、stream、预取和动态 shape |
| GPU 状态 | 记录功耗策略、锁频或 Boost 状态；未采集时明确写“未锁频/未知” |
| 正确性 | 测速前检查输出合同；OCR 记录完整 `det -> crop -> cls -> rec -> merge` |
| 缺失数据 | 没有原始样本、commit 或哈希的旧结果只列为“历史参考” |

各阶段的分位数不能相加得到总分位数。跨后端比较必须使用同一模型、输入、精度、shape、结果复制范围和 cold/steady 口径。

## Windows / Ryzen 7 5800H / RTX 3060 Laptop

| 项目 | 配置 |
| --- | --- |
| OS / CPU | Windows x64 `10.0.26200` / Ryzen 7 5800H，16 逻辑处理器 |
| GPU / 驱动 | RTX 3060 Laptop 6 GiB / `576.02` |
| GPU runtime | CUDA 12.9、cuDNN 9.22、TensorRT 10.11/11（按具体记录） |
| 状态 | 未主动锁频；结果不是固定频率硬件极限 |

### PaddleOCR 完整流水线

输入为标准 OCR 图片，返回 16 个文本区域。TensorRT Engine 在该设备用 10.11 重新构建；5 次预热、50 次测量，识别 batch 8、两个独立阶段 Session（v6 medium 为 batch 4、四个 Session）。计时复用准备输入，包含完整流水线，不含 Engine 构建。

| 模型 | 总 P50（ms） | 总 P95（ms） |
| --- | ---: | ---: |
| PP-OCRv4 mobile | 29.775 | 34.304 |
| PP-OCRv4 server | 114.205 | 118.435 |
| PP-OCRv5 mobile | 46.474 | 55.036 |
| PP-OCRv5 server | 90.797 | 93.476 |
| PP-OCRv6 tiny | 16.743 | 18.399 |
| PP-OCRv6 small | 27.311 | 36.788 |
| PP-OCRv6 medium | 64.387 | 77.434 |

Engine 与 GPU 架构、TensorRT 序列化版本和 profile 绑定；换设备或 Runtime 后必须重新构建。其它后端及自动调优方式见 [PaddleOCR Benchmark](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark/README.md)。

### PP-Structure 代表模块

以下为相同设备上的独立模块延迟，不是完整页面总耗时。分类表包含预处理、推理和 decoder；其余行的计时边界写在备注中。

| 模型 / 任务 | 后端 | P50（ms） | P95（ms） | 备注 |
| --- | --- | ---: | ---: | --- |
| 文档方向分类 | ORT CPU | 4.105 | 6.353 | 完整模块 |
| 文档方向分类 | OpenVINO CPU | 2.201 | 3.714 | 完整模块 |
| 文档方向分类 | OpenCV DNN CPU | 5.651 | 6.534 | 完整模块 |
| 文档方向分类 | TensorRT CUDA | 1.318 | 1.921 | 完整模块 |
| 表格分类 | ORT CPU | 3.818 | 5.867 | 完整模块 |
| 表格分类 | OpenVINO CPU | 2.836 | 5.024 | 完整模块 |
| 表格分类 | OpenCV DNN CPU | 5.171 | 6.353 | 完整模块 |
| 表格分类 | TensorRT CUDA | 1.212 | 1.331 | 完整模块 |
| UVDoc | ORT CPU | 148.580 | 157.366 | Session + decoder，batch 1 |
| UVDoc | OpenVINO CPU | 102.519 | 128.084 | Session + decoder，batch 1 |
| UVDoc | TensorRT CUDA | 28.369 | 32.069 | Session + decoder，动态 batch 2 |
| SLANeXt wireless | ORT CPU | 966.413 | 1402.346 | 官方源图 + decoder |
| SLANeXt wireless | OpenVINO CPU | 794.132 | 962.269 | 独立兼容图 + decoder |
| FormulaNet Plus-S | ORT CPU | 390.540 | 617.810 | 自回归 Session + decoder |
| UniMERNet | ORT CPU | 11796.760 | 12938.260 | 自回归 Session + decoder |

不同 Batch 和任务不能仅按表中数字直接排名。公式行证明稳定执行与延迟，不代表数学语义准确率。

### PaddleOCR-VL 1.6

固定官方任务输入、同一 Bundle、单 Session。CPU 行为 5+50；Spotting 因序列较长使用 1+5。TensorRT Table 使用五图 Engine 和设备端 KV；不包含 Engine/Session 创建或调用方图像准备。

| 任务 | 后端 | Wall P50（ms） | Wall P95（ms） | 协议 |
| --- | --- | ---: | ---: | --- |
| Table | ORT CPU | 7268.069 | 7709.583 | 5+50 |
| Table | OpenVINO CPU | 6787.859 | 7318.409 | 5+50 |
| Table | TensorRT CUDA，设备 KV | 9329.231 | 9650.985 | 5+50 |
| OCR | ORT CPU | 19163.912 | 22211.472 | 5+50 |
| OCR | OpenVINO CPU | 20744.661 | 23868.052 | 5+50 |
| Formula | ORT CPU | 8446.139 | 8878.795 | 5+50 |
| Formula | OpenVINO CPU | 8186.035 | 8600.297 | 5+50 |
| Chart | ORT CPU | 22372.818 | 24391.971 | 5+50 |
| Chart | OpenVINO CPU | 22076.120 | 25688.664 | 5+50 |
| Seal | ORT CPU | 20141.041 | 20739.562 | 5+50 |
| Seal | OpenVINO CPU | 12785.056 | 15429.444 | 5+50 |
| Spotting | ORT CPU | 132459.805 | 134999.906 | 1+5 |
| Spotting | OpenVINO CPU | 106346.339 | 112527.683 | 1+5 |

生成长度差异很大，不能用不同任务的总毫秒判断后端效率。CUDA/TensorRT 的未锁频结果也不能解释为该 GPU 的性能极限。

## ROI 与滑动窗口

统一输入为 `bus.jpg`，源图 ROI 为 `100,100,600,800`，模型输入 640x640，batch 1、单 Session/context/stream，5 次预热、20 次测量。`cold` 每次解码并准备 ROI；`steady` 复用准备输入。TensorRT CUDA steady 仍包含 BGR 上传和 CUDA 预处理，因此与普通后端的工作量不完全相同。

### Windows / RTX 3060 Laptop

| 模型 | 后端 | Cold P50/P95（ms） | Steady P50/P95（ms） |
| --- | --- | ---: | ---: |
| YOLOv8n | TensorRT | 30.915 / 34.788 | 9.182 / 10.290 |
| YOLOv8n | TensorRT CUDA preprocessing | 20.489 / 23.298 | 7.447 / 8.814 |
| YOLOv8n | ORT CUDA | 29.990 / 70.457 | 15.546 / 18.769 |
| YOLOv8n-seg | TensorRT | 48.778 / 62.037 | 19.179 / 23.034 |
| YOLOv8n-seg | TensorRT CUDA preprocessing | 21.311 / 23.751 | 7.817 / 9.328 |
| YOLOv8n-seg | ORT CUDA | 44.054 / 49.373 | 20.724 / 23.437 |

### Ubuntu 22.04 / i7-1165G7 / RTX 2060

| 模型 | 后端 | Cold P50/P95（ms） | Steady P50/P95（ms） |
| --- | --- | ---: | ---: |
| YOLOv8n | ORT CPU | 66.723 / 82.942 | 59.414 / 61.073 |
| YOLOv8n | ORT CUDA | 29.360 / 30.559 | 14.875 / 15.551 |
| YOLOv8n | OpenVINO CPU | 42.130 / 46.441 | 26.854 / 27.662 |
| YOLOv8n | OpenCV DNN CPU | 58.348 / 60.986 | 43.620 / 51.674 |
| YOLOv8n | TensorRT | 24.238 / 25.192 | 8.870 / 9.750 |
| YOLOv8n | TensorRT CUDA preprocessing | 15.596 / 16.150 | 9.721 / 10.232 |
| YOLOv8n-seg | ORT CPU | 82.206 / 95.980 | 95.394 / 96.785 |
| YOLOv8n-seg | ORT CUDA | 37.368 / 39.587 | 22.789 / 23.207 |
| YOLOv8n-seg | OpenVINO CPU | 57.677 / 59.521 | 43.209 / 43.909 |
| YOLOv8n-seg | OpenCV DNN CPU | 118.796 / 119.631 | 104.301 / 106.419 |
| YOLOv8n-seg | TensorRT | 33.908 / 35.045 | 20.175 / 21.745 |
| YOLOv8n-seg | TensorRT CUDA preprocessing | 14.899 / 15.942 | 9.078 / 10.053 |

两台设备的 admitted YOLOv8n-seg CUDA 后处理与 managed 参考掩码逐像素一致。该设备路径只覆盖轴对齐矩形及受支持的 YOLO 后处理；旋转框、多边形、Mask 和透视 ROI 使用 OpenCV 精确路径。滑动窗口还会增加切片规划和跨窗口合并，必须用目标尺寸和重叠率单独测量。

## Ubuntu 22.04 / i7-1165G7：PP-OCRv6 Small

固定 OCR 输入、16 个区域、batch 1、两个阶段 Session、最大识别宽度 3200；5 次预热、50 次测量，复用已准备输入，不含模型加载和原图解码。GPU 未参与这些行。

| 后端 / 配置 | P50（ms） | P95（ms） |
| --- | ---: | ---: |
| ONNX Runtime CPU | 309.153 | 345.692 |
| OpenVINO CPU | 239.854 | 284.206 |
| OpenCV DNN，动态 shape 缓存 1 | 1439.253 | 1510.367 |
| OpenCV DNN，动态 shape 缓存 16 | 426.472 | 485.785 |

增加 shape 缓存会占用更多 native 内存，且收益依赖实际宽度分布；默认配置不会据此自动改为 16。

## Windows / i7-14700KF / RTX 5060 Ti：历史参考

便携包使用 3 次预热、10 次测量，只保存均值，未记录 commit、输入 SHA-256 或 P50/P95，因此不能与上面的正式分位数结果直接比较。

| 模型 | ORT CPU | ORT CUDA | OpenVINO CPU | OpenCV DNN CPU | TensorRT |
| --- | ---: | ---: | ---: | ---: | ---: |
| YOLOv8n | 15.281 | 6.682 | 13.342 | - | 5.050 |
| YOLOv8n-seg | 27.082 | 14.819 | 24.921 | 130.889 | 11.867 |
| YOLOv8s pose | 33.200 | 6.857 | 35.262 | 126.708 | 4.007 |
| PaDiM | 15.626 | 5.355 | 13.407 | 31.107 | 3.989 |
| BRIA RMBG 1.4 | 364.413 | 42.940 | 366.734 | 933.514 | 25.141 |
| BRIA RMBG 2.0 | 7864.023 | 538.860 | 7424.923 | - | 201.242 |

单位为毫秒均值；`-` 表示该历史包没有有效记录，不表示不支持。

## 复现与提交结果

通用视觉基准：

```powershell
dotnet run --project tools/DeploySharp.VisualBenchmark -c Release -- `
  --kind yolov8n --model .models/yolov8n.onnx --image data/bus.jpg `
  --backend onnxruntime,openvino,opencv-dnn `
  --mode both --warmup 5 --iterations 50 `
  --output ..\benchmark-results\visual.csv `
  --json-output ..\benchmark-results\visual.json
```

PaddleOCR 使用 `tools/DeploySharp.PaddleOcrBenchmark`，设备测试包使用 `Run-DeviceBenchmark.ps1`。新结果应写到仓库外目录，并同时返回 CSV/JSON、设备信息和控制台日志。更新本页时只加入满足协议的汇总行；逐次样本、TRX、日志和临时 Engine 不提交到公共文档。
