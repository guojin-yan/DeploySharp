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
