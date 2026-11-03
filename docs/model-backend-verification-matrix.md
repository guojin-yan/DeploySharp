# 模型 × 后端验证矩阵

本页只记录**精确模型工件**在各后端的当前运行状态。`✓` 表示已完成真实模型加载、推理和结果解码；`△` 表示只有部分合同或组件证据；`✗` 表示已经尝试且当前组合不能完成；`—` 表示尚未验证或工件格式不适用。可下载、可构建或存在适配器都不等于推理通过。

除 PaddleOCR-VL 行注明的 Linux x64 验证外，当前证据主要来自 Windows x64。更换模型文件、输入 shape、运行时、驱动或设备后需要重新验证。性能数据单独见[设备性能实测](articles/device-performance-benchmarks.md)，模型下载与状态语义见[模型支持指南](articles/model-support.md)。

SAM2/SAM3 视频、Whisper 完整发布 Bundle、Donut 原生多页/TensorRT、BLIP VQA/BLIP-2/InstructBLIP、Qwen2.5-VL、Phi Vision、SigLIP 2，以及部分 LayoutLMv3/Pix2Struct 任务头仍处于合同或局部实验阶段，不列为完整后端支持。

## 当前工件矩阵

通用模型行使用 ModelFactory 主目录中的工件；其中 PP-OCRv5 的六条 Preview 绑定 `models-visual.1` 已发布的 ModelPack 和配套 ONNX。后文的 PP-OCR 全版本表使用 `models-paddleocr` 的工件，只有精确 SHA-256 相同的模型才能跨表复用验证状态。

| 模型工件 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LlamaSharp |
|---|:---:|:---:|:---:|:---:|:---:|
| `yolo/v5/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v6/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v7/detect/base` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v10/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v12/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v13/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/classify/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v5/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/segment/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/segment/c` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `deim/v2/detect` | ✓ | — | ✗ | ✓ | — |
| `pp-yoloe/plus-crn-l` | ✓ | — | ✗ | ✓ | — |
| `rf-detr/detect` | ✓ | ✗ | ✗ | ✓ | — |
| `rf-detr/segment` | ✓ | ✗ | ✗ | ✓ | — |
| `rt-detr/r50vd-decoded-vector-ir` | — | ✓ | — | — | — |
| `rt-detr/r50vd-decoded-vector-onnx` | ✓ | — | ✗ | ✓ | — |
| `rt-detr/r50vd-raw-query` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-cls` | ✓ | ✓ | — | — | — |
| `paddleocr/ppocrv5/server-det` | ✓ | ✓ | — | — | — |
| `paddleocr/ppocrv5/server-rec` | ✓ | ✓ | — | — | — |
| `paddleocr-vl/1.6`（实验性 ONNX/TensorRT Bundle） | ✓ | ✓ | ✗ | ✓ | — |
| `anomalib/padim/mvtec-bottle` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-1.4` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-2.0` (`onnx.fp32`) | ✓ | — | ✗ | ✓ | — |
| `bria/rmbg-2.0` (`onnx.dynamic-int8`) | ✓ | — | ✗ | ✗ | — |
| `llm/qwen2.5-0.5b-instruct-q4-k-m` | — | — | — | — | ✓ |
| `vision-language/clip-vit-b-32` | ✓ | ✓ | — | — | — |
| `segmentation/sam-v1-vit-b` | ✓ | ✓ | — | — | — |
| `generative-vision-language/blip-caption-base` | ✓ | ✓ | — | — | — |

主目录 PP-OCRv5 的 ModelPack、ONNX、字典和许可证均绑定 `models-visual.1` 已发布资产，并执行大小与 SHA-256 校验。三个 Mobile 模型与 `models-paddleocr` 字节相同；三个 Server 模型字节不同，其 ORT/OpenVINO 状态来自这些精确工件保留的 Golden，OpenCV/TensorRT 仍标记未验证。不能把另一套 Server 模型的结果套用到本表。

SAM v1、CLIP 和 BLIP Caption Base 的 `✓` 绑定公共 ModelPack 中的精确 ONNX SHA-256，并分别完成官方 Predictor、Embedding/Logit 或 Caption/Token Golden 对照。Qwen2.5 的 `✓` 绑定公共 GGUF SHA-256 与 LLamaSharp CPU 运行证据，覆盖生成、流式、取消、重复、并发冲突、释放和 Embedding；它不是通用 LLM 质量或其它原生后端声明。

PaddleOCR-VL-1.6 使用四图 ONNX Bundle；TensorRT 使用带独立 Prefill 图的五图 Bundle。ORT CPU、OpenVINO CPU 和 TensorRT CUDA 已完成声明任务的生成与 EOS/文本合同，OpenCV DNN 因动态 Shape 和高阶辅助输入导入限制不支持。该行不表示任意页面质量、任意 TensorRT profile 或全设备端 KV 路径均已验证。

**ORT CUDA 补充验证：** `yolo/v26/detect/n` 的同一公开 ONNX 工件已在 Windows 11 x64、RTX 3060 Laptop、驱动 `576.02`、Managed `1.28.0` + 官方 CUDA 12 原生发行包 `1.28.0`、CUDA `12.9` / cuDNN `9.22` 上通过。输入为 `bus.jpg`、640×640 Letterbox，关闭 TF32；5 个检测框的类别和顺序与 CPU 一致，最大分数差小于 `0.000002`，最大坐标差小于 `0.001` 像素，并由 ORT Profiling 核实 CUDA 节点执行。部署命令和计时见 [YOLO26 教程第 8.4 节](blog/2.0.0/2026-10/2026-10-28-02-yolo26-quickstart.md#84-在-windows-上切换后端结果是否一致)。此项独立于上表的 ORT CPU 列，不表示其它模型或 CUDA 13 组合已验证。

## RF-DETR 1.11.2 本地工件

当前源码/未发布版本新增 Keypoint Preview，并补齐 Seg Nano 的实例分割案例。两份 ONNX 均由官方 `rfdetr==1.11.2`（提交 `7433d5919d0f7e72cc6cb78663e0a731afd499e0`）以 opset 17、FP32、固定 Batch=1 导出。官方权重可下载并导出为本地 ModelPack；DeploySharp Release 和 ModelFactory 自动下载尚未发布。此表独立于上面的旧 `rf-detr/detect`、`rf-detr/segment` 公共工件。

| 本地模型 ID | ORT CPU | ORT CUDA | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
| --- | :---: | :---: | :---: | :---: | :---: |
| `rf-detr/keypoint-preview` | ✓ | ✓ | ✓ | ✗ | ✓ |
| `rf-detr/segment/nano` | ✓ | ✓ | ✓ | ✗ | ✓ |

| 工件 | ONNX 字节数 | SHA-256 |
| --- | ---: | --- |
| Keypoint Preview | 145,069,161 | `8597197b492e6b8e73a025bd199bf8e630827ffc11cddf4cf559ea5c15e8a19c` |
| Seg Nano | 122,825,605 | `d94ae1a747c01894a30af7cabc434c5262b6c627172d79708761ac8ad528b15e` |

Keypoint 输入 `input [1,3,576,576]`，输出 `dets [1,100,4]`、`labels [1,100,2]`、`keypoints [1,100,34,8]`；Seg Nano 输入 `[1,3,312,312]`，输出 `dets [1,100,4]`、`labels [1,100,91]`、`masks [1,100,78,78]`。全为 float32。完整合同、标签、权重身份和部署文件清单见 [Keypoint 元数据](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/rf-detr/contracts/keypoint-preview/rfdetr.json)、[Seg Nano 元数据](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/rf-detr/contracts/segment-nano/rfdetr.json) 与[下载/导出工具](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/rf-detr/README.md)。

验证于 2026-11-03，Windows x64、.NET SDK 10.0.401、AMD Ryzen 7 5800H、NVIDIA RTX 3060 Laptop 6 GB（sm_86）、驱动 576.02。ORT Managed/native 为 1.23.2（CPU 与 CUDA provider 分别执行），CUDA 12.9、cuDNN 9.22，CUDA provider 显式关闭 TF32；OpenVINO wrapper 3.3.1/native 2026.2.1 CPU；OpenCV wrapper/native 5.0.0；TensorRT wrapper/bridge 4.0.0、TensorRT 10.11.0.33。TensorRT Engine 在该设备上由库的 Builder 生成，FP32、禁用 TF32、workspace 256 MiB、optimization level 0、静态输入，无动态 Shape Profile。

输入为现有 `bus.jpg`，810×1080、487,438 字节，SHA-256 `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c`。`RFDETRInputFactory` 与官方 RGB→float/255→bilinear Resize（antialias=false）→ImageNet normalize 的输入逐值一致。官方参考由 PyTorch 2.10.0 CPU、torchvision 0.25.0 与 RF-DETR `PostProcess` 生成；ONNX raw 基线为 ORT CPU 1.23.2。关键点启用默认 trace-alpha 0.2，实例阈值均为严格 `>0.4`，不附加 NMS。

每个后端均检查原始输出的精确 shape、float32 和有限值；raw 容差为 `absError <= 0.001 + 0.001 * abs(reference)`，密集 mask logit 的绝对项为 `0.01`。Keypoint 所有后端的最大 raw 关键点误差不超过 `0.000651`；Seg Nano 的最大 raw mask 误差为 OpenVINO `0.002929`、CUDA `0.003928`、TensorRT `0.007186`，未跳过这些数值检查。最终结果还独立与官方参考对照：数量和类别精确匹配，分数绝对误差 ≤0.002、框/关键点坐标误差 ≤1 像素、mask 像素不一致比例 ≤0.0001（0.01%）。

| 模型 / 后端 | 实例数 | 最大分数误差 | 最大坐标误差（像素） | mask 不一致比例 |
| --- | ---: | ---: | ---: | ---: |
| Keypoint / ORT CPU | 4 | 1.20e-6 | 0.000245 | — |
| Keypoint / ORT CUDA | 4 | 1.35e-6 | 0.000733 | — |
| Keypoint / OpenVINO | 4 | 2.57e-6 | 0.000245 | — |
| Keypoint / TensorRT | 4 | 2.15e-6 | 0.000428 | — |
| Seg Nano / ORT CPU | 5 | 5.37e-7 | 0.000123 | 2.29e-7 |
| Seg Nano / ORT CUDA | 5 | 2.39e-7 | 0.000123 | 2.29e-7 |
| Seg Nano / OpenVINO | 5 | 1.79e-7 | 0.000123 | 2.29e-7 |
| Seg Nano / TensorRT | 5 | 4.18e-7 | 0.000138 | 0 |

两个模型在 ORT CPU 上另以两个独立原生 Session、三份输入检查有序 `RunManyAsync` 与预取消；这不是模型 Batch=2。关键点 Letterbox/ROI 回映射与 Include/Exclude 过滤、分割 ROI 框及 mask 的几何合同已有针对性测试。图像案例保存 JSON、骨架或掩码叠加图以及逐实例 mask；此为固定图 parity，不是数据集准确率、吞吐或性能上限。

OpenCV DNN 在创建 Session/导入 `/transformer/Greater` 时失败，原始诊断为 `All inputs should have equal types`，`CV_32SC1` 与 `CV_32FC1` 不一致；未执行到推理或解码。保留此精确 importer 限制，替代路径为上表已验证后端。其它 Seg 尺寸、微调权重、动态 Batch、FP16/INT8 和其它设备未验证。

复现先运行[导出工具](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/rf-detr/README.md)，然后从仓库根目录执行；换 `--backend cuda|openvino|opencv|tensorrt` 验证其它后端，原生环境设置及 Engine 复用见[案例说明](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/06-models/rf-detr/README.md)：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/keypoint/rfdetr.json --backend ort --output ../results/rf-detr/keypoint --validate ../models/rf-detr-1.11.2/keypoint --concurrency 2
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend ort --output ../results/rf-detr/segment-nano --validate ../models/rf-detr-1.11.2/segment-nano --concurrency 2
```

## PP-OCR 全版本发布工件

本表绑定 `PaddleOcrReleaseArtifacts` 登记的 `models-paddleocr` ONNX、字典和 SHA-256，由 `PaddleOcrReleaseClient` 获取。它与主目录中的同名 Preview 是不同的下载路径。

| 模型工件 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
|---|:---:|:---:|:---:|:---:|
| `paddleocr/ppocrv4/legacy-cls` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv4/mobile-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv4/mobile-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv4/server-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv4/server-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/mobile-cls` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/mobile-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/mobile-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/server-cls` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/server-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv5/server-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/tiny-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/tiny-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/small-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/small-rec` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/medium-det` | ✓ | ✓ | ✓ | ✓ |
| `paddleocr/ppocrv6/medium-rec` | ✓ | ✓ | ✓ | ✓ |

符号对应单个 DET、CLS 或 REC 工件。PP-OCRv4 Mobile/Server、PP-OCRv5 Mobile/Server、PP-OCRv6 Tiny/Small/Medium 另有完整 `det -> crop -> cls/orientation -> rec -> merge` 流水线验证；v6 使用明确登记的 v5 Mobile CLS。质量或性能结论仍须按完整流水线、数据集和后端分别读取。

## PP-Structure 精确资产

PP-Structure 的模型族差异较大，因此按目录中的精确资产列出。派生 SLANeXt OpenVINO 兼容图使用独立模型 ID 和 SHA-256，不会改写原始 Release 文件。

| 精确资产 | ORT CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
|---|:---:|:---:|:---:|:---:|
| `paddle-doc/pp-lcnet-x1-0-doc-ori` | ✓ | ✓ | ✓ | ✓ |
| `paddle-doc/uvdoc` | ✓ | ✓ | ✗ | ✓ |
| `paddle-doc/pp-doclayout-plus-l` | ✓ | ✓ | ✓ | ✓ |
| `paddle-doc/pp-doclayout-l` | ✓ | ✓ | ✓ | ✓† |
| `paddle-doc/pp-doclayout-m` | ✓ | ✓ | ✗ | ✓ |
| `paddle-doc/pp-doclayout-s` | ✓ | ✓ | ✗ | ✓ |
| `paddle-doc/pp-docblocklayout` | ✓ | ✓ | ✓ | ✓ |
| `paddle-doc/picodet-layout-1x` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/picodet-layout-1x-table` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/picodet-s-layout-3cls` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/picodet-l-layout-3cls` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/rt-detr-h-layout-3cls` | ✓ | ✓ | ✓ | ✓# |
| `paddle-doc/picodet-s-layout-17cls` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/picodet-l-layout-17cls` | ✓ | ✓ | ✗ | ✓P |
| `paddle-doc/rt-detr-h-layout-17cls` | ✓ | ✓ | ✓ | ✓# |
| `paddle-table/slanext-wired`（原始图） | ✓ | ✗ | ✗ | ✗² |
| `paddle-table/slanext-wireless`（原始图） | ✓ | ✗ | ✗ | ✗² |
| `paddle-table/slanext-wired-openvino-compat`（派生图） | ✓ | ✓** | ✗ | ✗³ |
| `paddle-table/slanext-wireless-openvino-compat`（派生图） | ✓ | ✓** | ✗ | ✗³ |
| `paddle-table/pp-lcnet-x1-0-table-cls` | ✓ | ✓ | ✓ | ✓‡ |
| `paddle-table/rt-detr-l-wired-cell-det` | ✓ | ✓ | ✓ | ✓‖ |
| `paddle-table/rt-detr-l-wireless-cell-det` | ✓ | ✓ | ✓ | ✓‖ |
| `paddle-formula/pp-formulanet-plus-s` | ✓ | ✗¹ | ✗ | ✗⁴ |
| `paddle-formula/pp-formulanet-plus-m` | ✓ | ✗¹ | ✗ | ✗⁴ |
| `paddle-formula/pp-formulanet-plus-l` | ✓ | ✗¹ | ✗ | ✗⁴ |
| `paddle-formula/pp-formulanet-s` | ✓ | ✗¹ | ✗ | ✗⁴ |
| `paddle-formula/pp-formulanet-l` | ✓ | ✗¹ | ✗ | ✗⁴ |
| `paddle-formula/unimernet` | ✓ | ✗¹ | ✗ | ✗⁵ |
| `paddle-seal/ppocrv4-mobile` | ✓ | ✓ | ✓ | ✓§ |
| `paddle-seal/ppocrv4-server` | ✓ | ✓ | ✓ | ✓§ |
| `paddle-chart/pp-chart2table`（四图 Bundle） | ✓ | ✓ | ✗ | ✓¶ |

## 已知边界

- `✓**`：SLANeXt 使用独立发布的 OpenVINO 兼容图；原始 `Loop` 图仍不支持。
- `✓†`：PP-DocLayout-L 的 TensorRT 动态输入和图内 Paddle NMS 已与 ORT 对照。
- `✓#`：RT-DETR layout 的 TensorRT 动态 Batch、三输入和 NMS Decoder 已验证。
- `✓‡`：PP-LCNet 表格分类的 TensorRT 预处理、分类 Decoder 和 ORT 对照已验证。
- `✓‖`：RT-DETR 表格单元格的 TensorRT 动态三输入及几何/score 对照已验证。
- `✓§`：PP-OCRv4 印章的 TensorRT 概率图和专用 Decoder 已验证。
- `✓¶`：Chart2Table Bundle 已运行到 EOS，并完成声明后端的文本合同验证。
- `✓P`：静态 PicoDet layout Engine、Decoder 和 ORT 有界对照已验证。
- `✗¹`：公式图受 OpenVINO `Loop`/动态控制流导入或 native 稳定性限制。
- `✗²`/`✗³`：SLANeXt 原始图或 OpenVINO 派生图在 TensorRT recurrence shape 处不能创建 Engine。
- `✗⁴`：FormulaNet 在 TensorRT `Loop/BitwiseAnd` 导入处阻断。
- `✗⁵`：UniMERNet 在 TensorRT `If/ConcatFromSequence` 导入处阻断。

OpenCV DNN 对 SLANeXt 的 `Loop`、公式生成图的动态控制流、Chart2Table 的高阶动态 KV 辅助输入以及部分 Paddle NMS 动态 Batch 组合存在 importer 限制。`✗` 只限制表中精确 OpenCV 组合，不影响其它后端。

公式模型的 ORT `✓` 表示官方 tokenizer 与 Decoder 能完成生成，不表示数据集准确率通过。Chart2Table 的 `✓` 表示 Bundle 能完整生成和解析，不表示 ChartQA 全量准确率。UVDoc 的 `✓` 表示输出张量有限且 Decoder 合同通过，不表示视觉矫正质量等价。

## 阅读规则

- 状态只对表中精确工件、任务、后端和已验证输入合同成立，不能从相邻模型推断。
- TensorRT Engine 与 GPU 架构、TensorRT/CUDA 版本和 shape profile 绑定，必须在目标设备重建并验证。
- 真动态 Batch 与多个独立 Session 是不同能力；静态 Batch=1 模型只能通过 Session 池并发。
- `✓` 是执行与结果合同，不自动包含准确率、性能、长时间稳定性或跨设备结论。
- 质量评测应使用带人工真值的数据集；只有演示图或跨后端一致性时，应称为 smoke 或 parity。

配置与使用见 [PaddleOCR 教程](articles/visual-ocr.md)、[PP-Structure 教程](articles/visual-paddle-structure.md)、[Batch、Session 池与并发](articles/batch-session-concurrency.md)和[官方模型目录](articles/model-catalog.md)。
