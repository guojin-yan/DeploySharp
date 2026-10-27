# 模型支持指南

本页回答两个问题：DeploySharp 当前有哪些可获取的模型，以及这些模型实际验证到了哪一步。模型文件可下载、代码中存在 Profile、合同测试通过和真实后端推理通过是四种不同证据，不能用一个“支持”概括。

精确的逐后端结果以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准；本页不复制每个后端符号。模型大小、SHA-256 和下载地址以[官方 ModelFactory 目录](model-catalog.md)及 [`models-paddleocr` Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) 中的 `paddleocr-release-catalog.json` 为准。

## 支持状态

| 状态 | 本页含义 |
| --- | --- |
| **已验证** | 精确模型工件已完成至少一个后端的真实加载、推理和结果解码；其它后端仍须读取矩阵。 |
| **实验性** | 已有可执行 Bundle 和局部真实证据，但任务质量、输入范围或后端覆盖尚未达到常规模型级别。 |
| **可下载 Preview** | 固定 Release 资产可下载并校验，但当前仓库没有可升级为“已验证”的端到端运行证据。 |
| **合同/外部资产** | 公共 API、Profile 或局部组件存在，完整模型 Bundle 需要应用提供，不能视为官方可下载模型。 |
| **规划中** | 只有模型方向或部分原型，当前版本不应向用户声明可用。 |

“已验证”不自动包含准确率、性能、长时间稳定性或所有平台。TensorRT Engine 还与 GPU 架构、CUDA/TensorRT 版本和 Shape Profile 绑定，必须在目标设备重新构建和验证。

## 当前模型总览

| 类型 | 完整模型名称 | 当前状态 |
| --- | --- | --- |
| 目标检测 | YOLOv5n Detect、YOLOv6s Detect、YOLOv7 Detect、YOLOv8n Detect、YOLOv9s Detect、YOLOv10n Detect、YOLO11n Detect、YOLO12n Detect、YOLOv13n Detect、YOLO26n Detect、DEIMv2 Detect、PP-YOLOE Plus CRN-L、RF-DETR Detect、Paddle RT-DETR R50vd | 已验证 |
| 图像分类 | YOLOv8s Classification | 已验证 |
| 实例分割 | YOLOv5s Segmentation、YOLOv8n Segmentation、YOLOv9c Segmentation、YOLO11s Segmentation、YOLO26s Segmentation、RF-DETR Segment | 已验证 |
| 姿态估计 | YOLOv8s Pose、YOLO11s Pose、YOLO26s Pose | 已验证 |
| 旋转框检测 | YOLOv8s Oriented Detection、YOLO11s Oriented Detection、YOLO26s Oriented Detection | 已验证 |
| 核心 OCR | PP-OCRv4、PP-OCRv5、PP-OCRv6 的下表精确 DET/REC/CLS 模型 | 已验证 |
| PP-Structure | 文档方向、矫正、版面、表格、公式、印章和图表解析的下表精确模型 | 已验证，但后端差异较大 |
| 文档视觉语言 | PaddleOCR-VL 1.6 | 实验性 |
| 异常检测与背景移除 | Anomalib PaDiM MVTec AD Bottle、BRIA RMBG 1.4、BRIA RMBG 2.0 | 已验证 |
| 可提示分割 | Meta SAM ViT-B ONNX prompt segmentation bundle | 已验证 |
| 视觉语言 | OpenAI CLIP ViT-B/32 ONNX dual encoder、Salesforce BLIP Caption Base ONNX bundle | 已验证 |
| 本地语言模型 | Qwen2.5 0.5B Instruct Q4_K_M | 已验证 |

## 已验证的通用视觉模型

这些模型均有公开资产，并在验证矩阵中至少存在一个 `✓`。同一行中的模型仍可能拥有不同的后端范围。

| 任务 | 完整模型名称 | 公开模型 ID |
| --- | --- | --- |
| YOLO 检测 | YOLOv5n Detect；YOLOv6s Detect；YOLOv7 Detect；YOLOv8n Detect；YOLOv9s Detect；YOLOv10n Detect；YOLO11n Detect；YOLO12n Detect；YOLOv13n Detect；YOLO26n Detect | `yolo/v5/detect/n`；`yolo/v6/detect/s`；`yolo/v7/detect/base`；`yolo/v8/detect/n`；`yolo/v9/detect/s`；`yolo/v10/detect/n`；`yolo/v11/detect/n`；`yolo/v12/detect/n`；`yolo/v13/detect/n`；`yolo/v26/detect/n` |
| 其它检测器 | DEIMv2 Detect；PP-YOLOE Plus CRN-L；RF-DETR Detect；Paddle RT-DETR R50vd decoded-vector ONNX、decoded-vector OpenVINO IR 和 raw-query ONNX | `deim/v2/detect`；`pp-yoloe/plus-crn-l`；`rf-detr/detect`；`rt-detr/r50vd-decoded-vector-onnx`；`rt-detr/r50vd-decoded-vector-ir`；`rt-detr/r50vd-raw-query` |
| 图像分类 | YOLOv8s Classification | `yolo/v8/classify/s` |
| 实例分割 | YOLOv5s Segmentation；YOLOv8n Segmentation；YOLOv9c Segmentation；YOLO11s Segmentation；YOLO26s Segmentation；RF-DETR Segment | `yolo/v5/segment/s`；`yolo/v8/segment/n`；`yolo/v9/segment/c`；`yolo/v11/segment/s`；`yolo/v26/segment/s`；`rf-detr/segment` |
| 姿态估计 | YOLOv8s Pose；YOLO11s Pose；YOLO26s Pose | `yolo/v8/pose/s`；`yolo/v11/pose/s`；`yolo/v26/pose/s` |
| 旋转框检测 | YOLOv8s Oriented Detection；YOLO11s Oriented Detection；YOLO26s Oriented Detection | `yolo/v8/obb/s`；`yolo/v11/obb/s`；`yolo/v26/obb/s` |
| 异常检测 | Anomalib PaDiM MVTec AD Bottle | `anomalib/padim/mvtec-bottle` |
| 背景移除 | BRIA RMBG 1.4；BRIA RMBG 2.0 FP32；BRIA RMBG 2.0 Dynamic INT8 | `bria/rmbg-1.4`；`bria/rmbg-2.0` |

ModelFactory 主目录目前包含 42 条 `preview` 记录。`preview` 是发布通道状态，不表示模型缺少运行证据；目录字段 `compatibleBackends` 也只表示工件声明的候选适配器。是否完成真实推理必须读取本页和验证矩阵。

## PP-OCR v4、v5 与 v6

`PaddleOcrModelCatalog` 登记 17 个核心模型合同，并由 `PaddleOcrReleaseArtifacts` 映射到 `models-paddleocr` 中可单独下载的 ONNX 与字典资产。下列精确工件已在矩阵记录真实后端证据。

| 版本/模块 | 完整模型名称 | DeploySharp 模型 ID |
| --- | --- | --- |
| v4 检测 | PP-OCRv4_mobile_det；PP-OCRv4_server_det | `paddleocr/ppocrv4/mobile-det`；`paddleocr/ppocrv4/server-det` |
| v4 识别 | PP-OCRv4_mobile_rec；PP-OCRv4_server_rec | `paddleocr/ppocrv4/mobile-rec`；`paddleocr/ppocrv4/server-rec` |
| v4 方向分类 | ch_ppocr_mobile_v2.0_cls | `paddleocr/ppocrv4/legacy-cls` |
| v5 检测 | PP-OCRv5_mobile_det；PP-OCRv5_server_det | `paddleocr/ppocrv5/mobile-det`；`paddleocr/ppocrv5/server-det` |
| v5 识别 | PP-OCRv5_mobile_rec；PP-OCRv5_server_rec | `paddleocr/ppocrv5/mobile-rec`；`paddleocr/ppocrv5/server-rec` |
| v5 文本行方向 | PP-LCNet_x0_25_textline_ori；PP-LCNet_x1_0_textline_ori | `paddleocr/ppocrv5/mobile-cls`；`paddleocr/ppocrv5/server-cls` |
| v6 检测 | PP-OCRv6_tiny_det；PP-OCRv6_small_det；PP-OCRv6_medium_det | `paddleocr/ppocrv6/tiny-det`；`paddleocr/ppocrv6/small-det`；`paddleocr/ppocrv6/medium-det` |
| v6 识别 | PP-OCRv6_tiny_rec；PP-OCRv6_small_rec；PP-OCRv6_medium_rec | `paddleocr/ppocrv6/tiny-rec`；`paddleocr/ppocrv6/small-rec`；`paddleocr/ppocrv6/medium-rec` |

完整流水线是 `det -> crop -> cls/orientation -> rec -> merge`。PP-OCRv6 没有单独登记新的方向分类器，流水线会显式组合 PP-OCRv5 Mobile CLS，不能从模型名称推断出不存在的 v6 CLS。模型级 `✓` 也不能替代 CER/WER、数据集准确率和完整流水线性能结论。

ModelFactory 主目录中另有 6 条 PP-OCRv5 Preview，使用 `models-visual.1` 已发布且匹配的 ModelPack、ONNX、字典和许可证文件；完整 v4/v5/v6 集合则使用 `models-paddleocr`。两套目录的三个 Mobile 模型字节相同，三个 Server 模型的 SHA-256 不同。主目录 Server 工件保留 ORT/OpenVINO Golden 证据，其 OpenCV/TensorRT 状态仍未验证。必须使用同一目录返回的清单和模型字节，不能交叉组合；验证矩阵已将这两组分表记录。

## PP-Structure 与文档模型

`PaddleDocumentModelCatalog` 登记官方模型身份，`PaddleDocumentReleaseArtifacts` 登记已转换的单图 ONNX 资产。后端差异必须读取验证矩阵，特别是 OpenCV DNN、OpenVINO `Loop` 和 TensorRT 动态控制流限制。

| 模块 | 完整模型名称 | 当前交付边界 |
| --- | --- | --- |
| 文档方向 | PP-LCNet_x1_0_doc_ori | 单个 ONNX 资产，已验证 |
| 文本图像矫正 | UVDoc | 单个 ONNX 资产，已验证；不把张量合同通过等同于视觉质量等价 |
| 版面检测 | PP-DocLayout_plus-L；PP-DocLayout-L；PP-DocLayout-M；PP-DocLayout-S；PP-DocBlockLayout | 单个 ONNX 资产，已验证 |
| 轻量版面检测 | PicoDet_layout_1x；PicoDet_layout_1x_table；PicoDet-S_layout_3cls；PicoDet-L_layout_3cls；PicoDet-S_layout_17cls；PicoDet-L_layout_17cls | 单个 ONNX 资产，已验证；部分后端不支持 |
| RT-DETR 版面检测 | RT-DETR-H_layout_3cls；RT-DETR-H_layout_17cls | 单个 ONNX 资产，已验证 |
| 表格结构 | SLANeXt_wired；SLANeXt_wireless | 原始 ONNX 已验证；OpenVINO 使用独立的 `slanext-wired-openvino-compat` / `slanext-wireless-openvino-compat` 派生图，TensorRT 当前不支持 |
| 表格分类 | PP-LCNet_x1_0_table_cls | 单个 ONNX 资产，已验证 |
| 表格单元格检测 | RT-DETR-L_wired_table_cell_det；RT-DETR-L_wireless_table_cell_det | 单个 ONNX 资产，已验证 |
| 公式识别 | PP-FormulaNet_plus-S；PP-FormulaNet_plus-M；PP-FormulaNet_plus-L；PP-FormulaNet-S；PP-FormulaNet-L；UniMERNet | 单个 ONNX 资产；当前真实执行集中在 ONNX Runtime，不能外推到其它后端或数据集准确率 |
| 印章文本检测 | PP-OCRv4_mobile_seal_det；PP-OCRv4_server_seal_det | 单个 ONNX 资产，已验证 |
| 区域文字识别 | PP-OCRv5_mobile_rec | 复用核心 PP-OCR 资产和合同 |
| 图表转表格 | PP-Chart2Table | 7 文件 ONNX/tokenizer Bundle，ORT CPU、OpenVINO CPU 和 TensorRT CUDA 有固定输入生成证据；OpenCV DNN 不支持 |

PP-Chart2Table 的官方 Paddle 归档不是标准 `inference.json`/`inference.pdiparams` 导出，因此 `PaddleDocumentModelCatalog` 中的上游归档行保持 `ConversionBlocked`。这不表示派生 Bundle 不可用：`PaddleOcrReleaseClient.GetBundleAsync("paddle-chart/pp-chart2table")` 会下载已发布的四张 ONNX 图和三个 tokenizer 文件。两种状态描述的是不同工件，不能相互覆盖。

公式和 Chart2Table 的“已验证”表示生成能够到达 EOS 并完成 tokenizer/结果解析合同，不表示公式语义准确率或 ChartQA 全量准确率已经达标。

## PaddleOCR-VL 1.6 实验性支持

`PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` 可下载五张 ONNX 图、四个 tokenizer/template 文件和一个 Bundle manifest，共 10 个独立资产。下载器执行大小与 SHA-256 校验，并支持离线缓存复用。

固定任务已在 ONNX Runtime CPU/CUDA、OpenVINO CPU 和设备专用 TensorRT Engine 上取得局部生成证据；原始动态图无法由 OpenCV DNN 5.0 导入。当前证据只覆盖固定 OCR、表格、公式、图表、Spotting 和印章任务，不表示任意页面质量、任意 TensorRT Profile、多页准确率或全设备端 KV 路径已经验证。因此该 Bundle 保持“实验性”，不与常规 PP-OCR 模型合并声明。

## 其它已验证的公开模型

下列公共 Release 资产与本地实测使用相同的模型 SHA-256，并已完成真实推理。`✓` 仍只覆盖表中列出的精确后端、固定合同和测试输入，不代表其它设备或后端自动可用。

| 完整模型名称 | 模型 ID | 已验证路径 | 仍需注意 |
| --- | --- | --- | --- |
| Meta SAM ViT-B ONNX prompt segmentation bundle | `segmentation/sam-v1-vit-b` | ONNX Runtime CPU、OpenVINO CPU；点/框/Mask feedback 与官方 Predictor 像素对照 | TensorRT 与视频记忆不在该 SAM v1 Bundle 范围内 |
| OpenAI CLIP ViT-B/32 ONNX dual encoder | `vision-language/clip-vit-b-32` | ONNX Runtime CPU、OpenVINO CPU；图像/文本 Embedding 与评分 Golden 对照 | Tokenizer、预处理和候选集评分语义必须保持一致 |
| Salesforce BLIP Caption Base ONNX bundle | `generative-vision-language/blip-caption-base` | ONNX Runtime CPU、OpenVINO CPU；固定图像 Caption、EOS、Token 与 Logit 对照 | 这里只支持 Caption Base，不包含 BLIP VQA、BLIP-2 或 InstructBLIP |
| Qwen2.5 0.5B Instruct Q4_K_M | `llm/qwen2.5-0.5b-instruct-q4-k-m` | LlamaSharp CPU；生成、流式、取消、重复、并发冲突、释放与 Embedding | 不代表 CUDA/Vulkan、任意 Prompt 质量或其它 GGUF 模型已验证 |

## 代码合同与外部资产路径

下列能力可以用于继续集成或外部实验，但不是当前官方可下载、完整验证的模型清单：

- LLaVA-OneVision Qwen2 0.5B：存在原生多模态 Bundle 合同和外部局部证据，完整公共 Bundle 仍由应用提供；
- Whisper tiny.en、Wav2Vec2：存在音频预处理、tokenizer 或三图会话路径，尚无完整公共 Release Bundle；
- Donut CORD-v2：存在 Encoder、Prefill、KV 与 tokenizer 合同，多页和 TensorRT 边界尚未闭合；
- SigLIP base patch16-224：存在双编码器合同，但当前公共 ModelFactory 目录只发布 CLIP；
- SAM2/SAM3 视频 ROI：存在外部 Predictor 计划器和案例，不存在 DeploySharp 可创建的官方原生视频 Bundle；
- YOLO-World、Grounding DINO、YOLOE：存在开放词汇 Profile 或阻断记录，词表、提示和可再分发完整工件尚未形成统一发布包；
- 通用语义分割、PatchCore 等任务已有解码或 Profile 扩展点，但没有对应的官方公共模型条目。

这些接口可用于用户自有模型，不应出现在“官方模型已验证”统计中。

## 未来计划与暂不支持

以下方向只有在完整工件、tokenizer/processor、输入输出合同、真实后端结果和发布资产齐备后，才会升级到 Preview 或已验证状态；本表不承诺发布时间。

| 模型族 | 当前缺口 |
| --- | --- |
| SAM2 / SAM3 | 原生图像/视频记忆 Bundle、跟踪状态和多后端证据 |
| Whisper | 完整公共三图 Bundle、多个后端和音频质量证据 |
| Donut | 原生多页编排、公共 Bundle 和 TensorRT 验证 |
| BLIP VQA、BLIP-2、InstructBLIP | 完整 Q-Former、投影、语言模型与 tokenizer Bundle |
| Qwen2.5-VL、Phi Vision | 完整视觉处理、占位符、KV 推理和公开 Bundle |
| SigLIP 2 | 官方 tokenizer、双编码器导出和真实后端验证 |
| LayoutLMv3、Pix2Struct | 可执行任务头、processor 与完整 DocVQA Bundle |
| HuBERT、pyannote | 下游任务头、VAD/分段和完整说话人处理 Bundle |

## 如何选择和声明一个模型

模型族名称只用于导航。一次可复现执行的身份由 `modelId + artifact + revision + SHA-256 + Profile` 共同组成，还要核对输入尺寸、动态维度、颜色空间、归一化、Resize 模式、输出名称、布局、标签或词表版本。

新模型进入公开清单至少需要：

1. 不可变模型和全部 sidecar，带大小与 SHA-256；
2. 与工件绑定的 Profile、预处理和 decoder 合同；
3. 一份固定测试输入及预期结果；
4. 至少一个后端的真实加载、推理和结果解码；
5. 失败、取消、并发和释放行为测试；
6. 可运行示例和明确的下载边界。

希望声明多个后端时，每个后端都必须单独保留证据。从 ONNX Runtime 成功不能推断 OpenVINO、OpenCV DNN 或 TensorRT 成功；模型下载成功也不能推断推理成功。

后端选择和安装见[平台与后端支持](platform-support.md)与[安装指南](installation.md)，批量和并发见[Batch、Session 池与并发](batch-session-concurrency.md)，设备延迟见[设备性能实测](device-performance-benchmarks.md)。
