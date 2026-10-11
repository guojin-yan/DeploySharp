# 模型 × 后端验证矩阵

本页是当前公开模型工件的后端验证总表。验证范围为 Windows x64；`✓` 表示该精确工件在对应后端完成加载、推理和结果解码，`△` 表示已有代码合同和已发布资产但尚无该后端的真实模型证据，`✗` 表示已尝试但当前后端不能完成该工件，`—` 表示没有适用工件或该格式不属于该后端。表格不把“可构建”或“存在适配器”当作推理通过。

模型目录中的 `External` 条目不纳入本表的通过统计。SAM2/SAM3 视频、Whisper 完整发布 Bundle、Donut 原生多页/TensorRT、BLIP VQA/BLIP-2/InstructBLIP、Qwen2.5-VL、Phi Vision、SigLIP 2，以及部分 LayoutLMv3/Pix2Struct 任务头仍处于合同或局部实验阶段，不能宣称全部后端可用。模型目录、下载边界和状态语义见[模型支持指南](articles/model-support.md)；可复现性能结果见[设备性能实测](articles/device-performance-benchmarks.md)。

## 当前工件矩阵

| 模型工件 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LLamaSharp |
|---|:---:|:---:|:---:|:---:|:---:|
| `yolo/v5/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
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
| `paddleocr/ppocrv4/legacy-cls` | ✓ | ✓ | △ | △ | — |
| `paddleocr/ppocrv4/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/server-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/server-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/server-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `anomalib/padim/mvtec-bottle` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-1.4` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-2.0 (onnx.fp32)` | ✓ | — | ✗ | ✓ | — |
| `bria/rmbg-2.0 (onnx.dynamic-int8)` | ✓ | — | ✗ | ✗ | — |
| `llm/qwen2.5-0.5b-instruct-q4-k-m` | — | — | — | — | — |
| `vision-language/clip-vit-b-32` | — | — | — | — | — |
| `segmentation/sam-v1-vit-b` | — | — | — | — | — |
| `generative-vision-language/blip-caption-base` | — | — | — | — | — |

### PP-OCR 核心模型质量证据索引

上方 PP-OCR 的后端勾号只表示对应精确工件已完成加载、推理和结果解码，不代表数据集准确率或跨后端质量等价。当前七组 PP-OCR v4/v5/v6 DET+REC 流水线（v4/v5 Mobile/Server、v6 Tiny/Small/Medium）已有同一 SROIE 十页选择上的 ORT CPU 与 OpenVINO CPU 质量 smoke 汇总：14 个模型/后端组合均完成 `10/10` 页，详细列出检测 F1、匹配与端到端 CER/WER、区域文本 parity 及一次性延迟观察。SROIE 是词框标注、模型输出为文本行，因此这些数值只用于可复现诊断，不是官方准确率或正式性能排名；该证据也不扩展 OpenCV DNN、CUDA 或 TensorRT 的质量状态。见[七模型十页汇总](../eng/models/paddle-ocr/verification/sroie-core-seven-models-10page-quality-summary-20261010.md)及[机器可读 JSON](../eng/models/paddle-ocr/verification/sroie-core-seven-models-10page-quality-summary-20261010.json)。
文档相对链接的最新审计为[2026-10-10 结果](../eng/models/paddle-document/verification/document-link-audit-20261010-matrix-quality.md)，共检查 `353` 条本地 Markdown 链接、断链 `0`；这是链接完整性检查，不是模型支持或质量通过证明。

## PP-Structure 模型状态

PP-Structure 共收录 29 个官方模型合同，其中 28 个标准模型有独立 ONNX Release 资产；`PP-Chart2Table` 另以 `paddle-chart/pp-chart2table` 四图 ONNX + tokenizer Bundle 发布在同一个 `models-paddleocr` Release。Chart2Table 官方源包仍不是标准 `inference.json + inference.pdiparams`，因此单文件 Paddle archive 转换目录仍保留 `conversion-blocked`，这不影响已发布派生 Bundle 的下载和运行。下表按模型族分组，普通模型组内只要仍有未逐一执行的工件就保持 `△`；Chart2Table 则以真实完整流水线证据单独标记。PP-Structure 其它模型尚未全部逐一验证每个后端，不能把 Chart2Table 的结果外推到其它模型。

本机已有 `E:\Model\PaddleDocument\onnx-smoke.json` 和 `semantic-smoke-selected.json`：28 个已转换 ONNX 均完成 ONNX Runtime CPU 图级 smoke（加载、输入绑定和原始输出形状）。`PaddleDocumentSemanticIntegrationTests` 使用真实 `E:\Data\image\bus.jpg` 对已接入专用 Decoder 的工件执行语义 smoke；版面导出当前按官方 23 类标签解码，阈值 0 时 `pp-doclayout-l` 返回 300 个候选。`PaddleDocumentPipelineSemanticIntegrationTests` 还验证了真实 ORT CPU 方向→版面统一编排。2026-09-30 又在同一 `bus.jpg`、各模型注册的输入/Decoder 合同下，逐项运行其余 12 个版面 ONNX 工件（PP-DocLayout/PicoDet/RT-DETR），OpenVINO CPU 为 12/12 pass；逐模型 SHA、输入尺寸和检测数量见 [`paddle-document-openvino-layout-matrix-20260930.md`](../eng/models/paddle-document/verification/paddle-document-openvino-layout-matrix-20260930.md)。这组证据只表示精确工件能够在声明的 OpenVINO CPU 运行时完成推理并返回有限结果，不表示版面召回、mAP、标签准确率或性能排名。UVDoc 另有同一 `bus.jpg`、同一 `[1,3,640,640]` 准备张量的 ORT/OpenVINO CPU 对照：两边均返回有限 `640x640x3` 张量，均值 `115.8434269/115.8434069`，最大/平均绝对差 `0.0730591/0.00176066`。这是可运行性和数值范围证据；当前输出存在有界数值差异，不能写成像素等价或视觉质量通过。完整记录见 [`uvdoc-ort-openvino-parity-20260924.json`](../eng/models/paddle-document/verification/uvdoc-ort-openvino-parity-20260924.json)。Paddle NMS Profile 已明确接受 `Int32` 和 `Int64` 计数，并按每张图的累计计数处理不等长 batch，避免把合法的 Paddle2ONNX 类型差异误判为后端失败。TensorRT 现已有 `PaddleDocumentTensorRtExternalIntegrationTests` 外部入口；本机已确认 `D:\Program Files\TensorRT-11.0.0.114-cu12`、CUDA 12.9、cuDNN 9.22 和 TRT 11 bridge 可以共同加载，并完成 PP-LCNet 文档方向、PP-LCNet 表格分类以及 `pp-doclayout-l` 的 ONNX→Engine→推理 smoke。此前的失败来自 TensorRT 根目录、bridge API 和动态 profile 未显式配置，不是缺少 vendor DLL。

UVDoc OpenCV DNN 精确组合已尝试但失败：OpenCV 5.0 importer 在 `PaddingLayerImpl::forward` 抛出 `inputs[0].dims == 4`，矩阵标记为 `✗`。TensorRT 11 + DeploySharp Provider/decoder 已从同一 ONNX 构建并加载 engine，DisableTf32 对照输出 max/mean abs diff `0.0778809/0.00172731`，输出 `640x640x3`；该记录证明 TensorRT engine 可执行且存在有界数值差异，不代表像素等价或视觉质量通过。GPU trtexec mean/P95 `8.55564/9.9389 ms`。

2026-10-05 对本地真实模型根目录完成了 [ONNX Batch 轴审计](../eng/models/paddle-document/verification/paddle-document-onnx-batch-axis-audit-20261005.md)：53/54 图成功解析，1 个零字节 Chart2Table 实验文件保留为 `load-failed`；32 个图有动态输入第一轴，8 个标准图静态为 `batch=1`。动态图包括 PP-DocLayout-L/Plus-L、PP-DocBlockLayout、RT-DETR 版面/单元格、公式、分类、印章、SLANeXt 和 UVDoc；PicoDet、PP-DocLayout-M/S 需使用独立 Session 或页面并发。Chart2Table 所有生成图仍是请求 batch=1，动态的是序列/KV 轴。该审计只证明图级形状合同，不代表任何后端的真实 Batch 性能或 Decoder 通过；逐文件输入/输出轴和 SHA-256 见[机器可读记录](../eng/models/paddle-document/verification/paddle-document-onnx-batch-axis-audit-20261005.json)。

随后对两个官方动态 PP-LCNet 分类图做了真实 Batch 运行：ORT CPU/OpenVINO CPU 均以两个相同 `bus.jpg` 行绑定 `[2,3,224,224]`，方向和表格分类各返回两行，标签、分数和顺序保持一致。该结果只覆盖这两个精确模型/后端/主机，不改变静态 layout 或 Chart2Table 的边界；机器可读记录见 [`paddle-document-dynamic-batch-ort-openvino-20261005.json`](../eng/models/paddle-document/verification/paddle-document-dynamic-batch-ort-openvino-20261005.json)，复现协议见对应 [Markdown 报告](../eng/models/paddle-document/verification/paddle-document-dynamic-batch-ort-openvino-20261005.md)。

同一协议随后覆盖官方 `PP-DocLayout-L` 动态 NMS 图：ORT CPU/OpenVINO CPU 均以两个相同输入绑定图像和几何辅助张量，按 `bbox_num` 正确拆分扁平输出，两行均返回 300 个候选且结果保持一致。机器可读记录见 [`paddle-document-layout-dynamic-batch-ort-openvino-20261005.json`](../eng/models/paddle-document/verification/paddle-document-layout-dynamic-batch-ort-openvino-20261005.json)，复现协议见对应 [Markdown 报告](../eng/models/paddle-document/verification/paddle-document-layout-dynamic-batch-ort-openvino-20261005.md)。该结果不扩展为 layout 准确率、TensorRT/OpenCV 或静态图 Batch 支持。

跨架构复核还覆盖官方 `RT-DETR-H_layout_3cls` 动态 NMS 图：两个相同输入在 ORT CPU/OpenVINO CPU 上均完成 `[2,3,640,640]` 与几何辅助绑定，`bbox_num` 切分后两行各 300 个候选且标签/几何一致。机器可读记录见 [`paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.json`](../eng/models/paddle-document/verification/paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.json)，复现协议见对应 [Markdown 报告](../eng/models/paddle-document/verification/paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.md)。

随后对 `paddle-table/slanext-wired` 完成了真正动态 Batch 表格解码：ORT CPU 使用官方图，OpenVINO CPU 使用独立 SHA-256 的 Loop 兼容图；两行相同输入均为 `[2,3,512,512]`，两后端各返回 24 个 token、13 个 cell，HTML 哈希一致。该结果只覆盖精确工件的输入绑定和 Decoder 行隔离，不改变原始 SLANeXt/OpenVINO 的 `✗` 与派生兼容图的 `✓**` 语义，也不构成表格准确率或性能结论。详见 [`paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md`](../eng/models/paddle-document/verification/paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md) 和 [JSON](../eng/models/paddle-document/verification/paddle-document-slanext-dynamic-batch-ort-openvino.json)。

后续对 `paddle-table/slanext-wireless` 完成相同双行合同：ORT CPU 官方无线图与 OpenVINO 独立 SHA-256 兼容图均返回 24 个 token、13 个 cell，HTML 哈希一致。该证据覆盖 wireless 两个精确图与 Decoder 的动态 Batch 行隔离，不改变原始 OpenVINO 图的 importer 阻断状态。详见 [`paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md`](../eng/models/paddle-document/verification/paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md) 和 [JSON](../eng/models/paddle-document/verification/paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json)。

2026-10-07 又对 `paddle-doc/uvdoc` 官方动态导出执行双行 ORT CPU / OpenVINO CPU 解码，输入为 `bus.jpg` 左/右两个不重叠区域。两后端均绑定 `[2,3,640,640]` 并输出 `[2,3,640,640]`，每行生成独立的 `640×640×3` 图像结果且 page index 为 `[0,1]`；行间输出 mean absolute difference `72.4329`，证明两条结果没有互相覆盖。对应行的 ORT/OpenVINO mean absolute difference 为 `0.002078/0.002263`，全量 max 为 `0.103020`。这只证明动态 Batch 绑定、结果行隔离和有界数值对照，不表示视觉质量或吞吐通过。详见 [`paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md`](../eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md) 及 [JSON](../eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.json)；OpenCV DNN 的 importer 阻断和 TensorRT 未验证状态不变。

2026-10-07 又补充 wired/wireless 两个 RT-DETR-L 表格单元格官方导出：ORT CPU、OpenVINO CPU 均以 `[2,3,640,640]` 和 `im_shape=[2,2]`、`scale_factor=[2,2]` 运行，四个精确模型/后端组合均解码为两行、每行 300 个后置 NMS 候选。输入来自同一 `table_recognition.jpg` 的上下两个不重叠 `551×66` 区域；每个组合的两行输入张量 SHA 和解码结果 SHA 均不同，未出现行结果复用。该 300 是图输出候选数，不是单元格真值；本测试不评价准确率、不做 ORT/OpenVINO 数值 parity，也不采集吞吐。详见[动态 Batch 验证报告](../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.md)及[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.json)。

将两种 RT-DETR-L 单元格图扩展到 OpenCV DNN 后，wired 与 wireless Session 均可创建，但 OpenCV 5.0 `forward_many` 均返回 `DS-OCV-8004 / Requested blob not found`。因此矩阵中模型级 OpenCV 单图状态不应误读为动态 Batch 通过；仅 batch=2 精确组合记为不支持，ORT/OpenVINO 四个动态 Batch 结果不变。依照项目约定不继续排查 importer。详见[OpenCV 动态 Batch 边界报告](../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.md)及[JSON](../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.json)。

2026-10-07 又对官方 `PP-DocLayout_plus-L` 和 `PP-DocBlockLayout` 完成 batch=2 全解码运行：ORT CPU、OpenVINO CPU 共四个精确组合均成功绑定动态图像轴与 `im_shape`/`scale_factor` 辅助轴。每行来自 `bus.jpg` 的不同半幅裁剪，输入张量 SHA 与解码结果 SHA 均按行不同；每行返回 300 个图导出候选。半幅只是行隔离执行探针，不用于版面质量评价；候选数不是准确率指标，也不代表数值 parity、性能、TensorRT/OpenCV Batch 或所有版面模型支持。详见[动态 Batch 验证报告](../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.md)及[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.json)。

之后将这两个动态图扩展到 OpenCV DNN CPU batch=2 探针：PP-DocLayout_plus-L 与 PP-DocBlockLayout 均在 Session 成功创建后，于 native `forward_many` 遇到 `DS-OCV-8004 / Requested blob not found`。因此只将这两个精确动态 batch 组合记为不支持；精确资产表已有 OpenCV `✓` 仍表示单图证据，不应解释成动态 Batch 支持。按项目约定未继续调查 importer。ORT/OpenVINO 的四个动态 Batch 全解码组合保持通过。详见[OpenCV 动态 Batch 报告](../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.md)及[JSON](../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.json)。

2026-10-11 的一次完整外部回归把上述动态 Batch 证据汇总为 `22/22` 测试用例通过、`59` 个精确模型×后端检查，其中 `46` 个执行通过、`13` 个明确不支持；不支持项全部是 OpenCV DNN 动态 Batch 的已知边界。ORT CPU 为 `24` 个通过，OpenVINO CPU 为 `18` 个通过，OpenCV DNN CPU 为 `4` 个通过和 `13` 个不支持。TensorRT 本轮未运行，不能从该汇总推断 TensorRT Batch 支持。逐项来源、模型 ID、后端和边界见[动态 Batch 外部回归汇总](../eng/models/paddle-document/verification/paddle-document-dynamic-batch-external-regression-20261011.md)及[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-dynamic-batch-external-regression-20261011.json)。该汇总只表示执行/行隔离合同，不是准确率、吞吐、P50/P95 或跨设备性能结论。

2026-10-07 对 PP-OCRv4 mobile/server seal probability-map 两个动态图完成 batch=2 ORT CPU、OpenVINO CPU 和 OpenCV DNN CPU 实测，六个精确模型/后端组合均通过：每个组合都用 `demo_4.jpg`、`demo_5.jpg` 两张不同图片绑定 `[2,3,224,224]`，输出 `[2,1,224,224]`，解码成两条结果并保留 page index `0,1` 和对应输入 SHA；每个组合的输入行及各自 raw mask 行摘要均不同。OpenCV mask SHA 与 ORT/OpenVINO 不同，本轮不声明数值 parity。mobile 两行均为 0 个区域、server 为 `1,0`，但这两张图没有印章真值，因此区域数只作为输出诊断，不能用于召回率/准确率判断。详见[三后端印章动态 Batch 报告](../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md)及[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json)；这不表示 TensorRT batch、印章质量或性能通过。

2026-10-07 又以 `table_recognition.jpg` 顶/底两个不重叠区域，验证 PP-LCNet 文档方向与表格分类在 ORT CPU、OpenVINO CPU、OpenCV DNN CPU 的六个真实 batch=2 组合。每个模型/后端均绑定 `[2,3,224,224]`，两行 input SHA 和 raw logits SHA 不同；六个组合均返回两条 Decoder 结果，且同一输入区域在三个后端观察到的 top label 相同。测试逐行将 OpenVINO/OpenCV 原始输出与 ORT 基准比较，强制最大绝对差不超过 `1e-4`；本机实测最大值低于 `1.2e-7`。raw logits SHA 不同代表未逐位相同，不表示超过容差；无标注真值，数值 parity 不作为精度结论。详见[分类模型跨后端动态 Batch 报告](../eng/models/paddle-document/verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.md)及[JSON](../eng/models/paddle-document/verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.json)。

PP-LCNet 表格分类的 TensorRT batch=2 仍未验证。既有 `✓‡` 只对应静态 batch=1 Engine 的单图推理，不能据此推断动态 profile 支持。2026-10-07 显式配置匹配 TRT11 bridge 后，vendor `trtexec` 能构建/反序列化 min/opt/max=`1/2/2` Engine，但跳过了推理；DeploySharp Builder 和加载该 Engine 的 Runtime 创建都在模型推理前以 `0xC06D007E` 失败。TRT10.11 对照也没有进入模型执行。故精确动态 Batch 维持 `△`，不改变静态 batch=1 的既有证据；详见[TensorRT Batch 探针及复测记录](../eng/models/paddle-document/verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md)。

公式模型的 ORT CPU 动态 Batch 行隔离已覆盖全部六个官方动态输入导出：PP-FormulaNet Plus-S/M/L、FormulaNet-S/L 和 UniMERNet。每个模型均以官方示例的上下不同区域构成 batch=2，各行到达 EOS，并与各自 batch=1 推理的 token IDs/LaTeX 完全一致。区域只是执行探针，不是独立标注公式；该结果不代表公式准确率、吞吐、OpenVINO/OpenCV/TensorRT 或跨设备支持。各模型输入/输出 shape、token 数、SHA 和复现命令见[六模型报告](../eng/models/paddle-document/verification/formula-dynamic-batch-six-models-ort-20261007.md)。

公式数据集质量证据已在 2026-10-07 增补：六个模型各自对 MathNet realFormula v1 的 121 条人工标注图像完成 ORT CPU 解码。所有模型至少 120/121 条到 EOS、空输出为 0，但 exact match 仅 `9.09%–14.88%`，字符 CER `51.59%–89.86%`。这是明确的低质量诊断，模型训练数据重叠未知，不能泛化为其他数据；LaTeX 编辑距离也不验证数学语义等价。详见[六模型真实数据集报告](../eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007.md)和[机器可读汇总](../eng/models/paddle-document/verification/formula-realformula-six-models-ort-20261007-summary.json)。此结果不影响后端可执行矩阵，但公式质量门仍开放。

同一结果的[结构诊断索引](../eng/models/paddle-document/verification/formula-realformula-structure-quality-20261011.md)按参考长度分层记录生成括号平衡和命令数量；新增[命令 token 质量诊断](../eng/models/paddle-document/verification/formula-realformula-command-quality-20261011.md)进一步使用外部 manifest 的真实参考标签计算词法 micro-F1 `45.15%–63.01%`，但两者都只补充字符串级退化定位，不构成渲染/数学语义准确率。

对其中唯一未到 EOS 的 FormulaNet-L 样本 `211110912-2.png` 又做了输入敏感性对照：DeploySharp 与 PaddleX 官方预处理张量虽有 `3,433/589,824` 个值不同，ORT 两次 raw token 序列仍完全一致；官方 Paddle predictor 与 ORT 在每个相同张量上也逐 token 一致，均输出 BOS + 1,024 个位置且没有 EOS。随后静态核验匹配的 Paddle IR 与 ONNX Loop，二者都将生成循环限制为 1,024 次；将派生 ONNX 上限调至 1,025/2,048 后，均在位置嵌入表索引 1,026 处越界（表 shape `[1026,512]`）。追加三个定向样本后，一张在 220 个 raw token 处 EOS，两张均在 raw 序列末位 1,023 才输出 EOS；后两张预测 CER 分别为 971.43% 和 1,047.93%，表明 EOS 完成不代表公式正确。已确定的是导出图硬上限和这些样本的停止位置，而不是原异常样本为何未输出 EOS；不把它归因于 checkpoint，也不修改正式模型。Decoder `4096` 是安全上限。保留截断 warning，精确边界和复现方式见[敏感性报告](../eng/models/paddle-document/verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md)、[生成上限探针](../eng/models/paddle-document/verification/formula-formulanet-l-generation-limit-20261007.md)及[JSON](../eng/models/paddle-document/verification/formula-formulanet-l-generation-limit-20261007.json)。

六个公式动态图随后均尝试 OpenCV DNN CPU batch=2，且在图导入/输入特化阶段以 `DS-OCV-8002` 失败；PP-FormulaNet Plus-S/M/L、FormulaNet-S/L 对应 `ConstantOfShape` 零维度 importer 错误，UniMERNet 对应 `GatherND` importer 错误。只将这六个精确 artifact/runtime/batch 组合记为不支持，不改变 ORT 的通过状态，不外推 batch=1 或其他 OpenCV 版本，也不继续追查 importer。详见[公式 OpenCV 动态 Batch 边界报告](../eng/models/paddle-document/verification/paddle-document-formula-dynamic-batch-opencv-20261007.md)及[JSON](../eng/models/paddle-document/verification/paddle-document-formula-dynamic-batch-opencv-20261007.json)。

六个公式模型的当前后端状态另有一份集中索引：ORT CPU 的 121 条 realFormula 结果是质量诊断而非发布通过，OpenVINO 六项保持精确阻断，OpenCV 只记录上述动态 Batch 不支持，TensorRT 尚未建立六模型质量/性能矩阵；其中 Plus-S 已在匹配 TRT10.11/CUDA12.9 上做过 Builder 探针，但在 parser startup 以 `0xC0000005` 终止、未生成 Engine。见[公式后端质量/执行状态索引](../eng/models/paddle-document/verification/formula-backend-quality-status-20261011.md)及[机器可读 JSON](../eng/models/paddle-document/verification/formula-backend-quality-status-20261011.json)，以及[TensorRT Builder 探针](../eng/models/paddle-document/verification/formula-tensorrt-builder-probe-20261011.md)。

六个公式图的[静态 TensorRT 兼容性审计](../eng/models/paddle-document/verification/formula-tensorrt-static-compatibility-20261011.md)显示它们都包含动态输入/输出和自回归 `Loop`，循环体还包含 `If`、动态 shape 与 bitwise 算子。该审计用于定位派生兼容图的投入边界，不改变任何后端勾号，也不把 Plus-S 的 parser 崩溃外推到所有 TensorRT 版本。

2026-10-07 又补齐官方 RT-DETR-H 17-class layout 导出的真实 batch=2：不同的 `bus.jpg` 上/下区域在 ORT CPU 与 OpenVINO CPU 均绑定 `[2,3,640,640]`，并传入 `[2,2]` 的 `im_shape` 与 `scale_factor`；两后端各自完成两行解码、每行 300 个候选且输入/结果行不同。OpenCV DNN session 创建成功，但动态 batch=2 forward 返回 `DS-OCV-8004` / OpenCV 5.0 `Requested blob not found`，故这个精确动态 Batch 组合不支持；不推翻该模型已有单 Batch OpenCV 证据。区域不是版面标注真值，因此此项不代表版面精度或吞吐；TensorRT Batch 仍未验证。详见[验证报告](../eng/models/paddle-document/verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-20261007.md)及[三后端 JSON](../eng/models/paddle-document/verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json)。

同日补测官方 RT-DETR-H **3-class** layout 导出：`bus.jpg` 两个不同区域以 batch=2 输入 `[2,3,640,640]`，并绑定 `[2,2]` 的 `im_shape/scale_factor`。ORT CPU 与 OpenVINO CPU 都完成全 NMS 解码，各返回两行、每行 300 个候选，且两行输入/结果摘要不同；OpenCV DNN 成功创建 Session，但 `forward_many` 报 `DS-OCV-8004 / Requested blob not found`，因此仅此模型的动态 Batch=2 OpenCV 合同标为不支持，原有单图状态不变。无版面真值，不作精度、跨后端 parity 或性能结论，TensorRT Batch 未测。见[验证报告](../eng/models/paddle-document/verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.md)和[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json)。

2026-10-07 对 23 类官方 `PP-DocLayout-L` 再做不同输入 batch=2 三后端验证：同一 `bus.jpg` 的上下两个 `810×540` 区域在 ORT CPU/OpenVINO CPU 以 `[2,3,640,640]` 运行，并绑定 `im_shape/scale_factor=[2,2]`；两边均完成 NMS Decoder、每行返回 300 个导出候选且输入与解码结果行 SHA 均不同。OpenCV DNN 建立 Session 后 forward 报 `DS-OCV-8004` / OpenCV 5.0 `Requested blob not found`，仅此模型的动态 Batch=2 组合记为不支持，不改变既有单图状态。该图像没有 layout 标注，本次不作准确率、数值 parity 或吞吐结论；TensorRT 动态 Batch 仍未验证。详见[验证报告](../eng/models/paddle-document/verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.md)及[机器可读记录](../eng/models/paddle-document/verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.json)。

2026-10-07 对 PP-LCNet 表格分类器另做 TensorRT batch=2 探针：TensorRT 11 `trtexec` 可以从官方 ONNX 构建 min/opt/max batch `1/2/2` Engine，并以 `[2,3,224,224]` 绑定执行至 `[2,2]`。但这只是 vendor 工具、随机输入、无 H2D/D2H 的 Engine 可执行性探针，不代表 DeploySharp 后端通过。DeploySharp 使用外部预构建 TRT11 Engine 时在 Runtime 创建阶段抛出结构化异常 `3228369022 (0xC06D007E)`；用匹配 TRT10.11 NuGet bridge 重试时，今天连已有 batch=1 基线也在 Builder 创建阶段遇到相同异常并被标记 inconclusive。历史 2026-10-04 单 Batch 成绩作为历史精确运行保留；当前 batch=2 的 DeploySharp parity 仍是 `△`，不增加矩阵勾号。trtexec GPU-only P50/P95 `0.417847/0.518143 ms`，GPU 计算时间 CV `13.7767%` 且工具提示不稳定，不作库性能结果。详见[TensorRT 动态 Batch 探针报告](../eng/models/paddle-document/verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md)和[机器可读 JSON](../eng/models/paddle-document/verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.json)。

| 模块 | 代码合同 | 独立 ONNX Release | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| 文档方向：`paddle-doc/pp-lcnet-x1-0-doc-ori` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| 文档矫正：`paddle-doc/uvdoc` | ✓ | ✓ | ✓ | ✓ | ✗ | △ |
| 版面分析：`paddle-doc/pp-doclayout-l` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓† |
| 版面分析：`paddle-doc/pp-doclayout-plus-l`、`pp-doclayout-m`、`pp-doclayout-s`、`pp-docblocklayout` | ✓ | ✓ | ✓ | ✓ | △ | △ |
| 版面分析：`paddle-doc/picodet-layout-1x`、`picodet-layout-1x-table`、`picodet-s-layout-3cls`、`picodet-l-layout-3cls`、`rt-detr-h-layout-3cls` | ✓ | ✓ | ✓ | ✓ | △ | △ |
| 版面分析：`paddle-doc/picodet-s-layout-17cls`、`picodet-l-layout-17cls`、`rt-detr-h-layout-17cls` | ✓ | ✓ | ✓ | ✓ | △ | △ |
| 表格结构（原始 Release ONNX）：`paddle-table/slanext-wired`、`paddle-table/slanext-wireless` | ✓ | ✓ | ✓ | ✗ | △ | △ |
| 表格结构（本地 alpha-renamed 兼容图）：`slanext-wired`、`slanext-wireless` | ✓ | — | ✓ | ✓** | △ | △ |
| 表格分类：`paddle-table/pp-lcnet-x1-0-table-cls` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓‡ |
| 表格单元格：`paddle-table/rt-detr-l-wired-cell-det`、`rt-detr-l-wireless-cell-det` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓‖ |
| 公式：`paddle-formula/pp-formulanet-plus-s/m/l`、`pp-formulanet-s/l`、`unimernet` | ✓ | ✓ | ✓ | ✗¹ | △ | △ |
| 印章：`paddle-seal/ppocrv4-mobile` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓§ |
| 印章：`paddle-seal/ppocrv4-server` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓§ |
| 图表（四图生成 Bundle）：`paddle-chart/pp-chart2table` | ✓ | ✓ | ✓ | ✓ | △ | ✓¶ |

2026-10-07 补充的印章 batch=2 验证现已覆盖 ORT CPU、OpenVINO CPU 与 OpenCV DNN CPU：PP-OCRv4 mobile/server 的六个精确组合均完成两张不同图像的真实 Batch 绑定、概率图输出与 Decoder 行/来源映射。OpenCV mask SHA 与另外两个后端不同，因此只登记为执行/行隔离通过，不登记数值 parity。相关矩阵中的 OpenCV 状态仅对这两个印章 ONNX 工件成立；其它 PP-Structure 模型的未验证状态不变。详见[三后端印章动态 Batch 报告](../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md)。

### PP-Structure 精确资产状态（29 个目录项 + 2 个派生兼容图）

上面的模型族表用于快速浏览，下面按目录中的每个精确资产列出当前证据。`△` 表示尚未完成该精确组合的真实执行，`✗` 表示已经尝试并确认当前组合阻断；派生 SLANeXt 兼容图使用独立的 compatibility ID，不等同于原始 Release 图。

同一状态的机器可读版本见 [`paddle-document-backend-matrix-20260929.json`](../eng/models/paddle-document/verification/paddle-document-backend-matrix-20260929.json)。它固定了本机设备、运行时版本、状态语义和每个精确资产的四个后端状态，新增设备时按相同 schema 追加独立设备记录。OpenCV DNN 的 14 个 Paddle NMS 精确组合执行报告见 [`paddle-document-opencv-nms-matrix-20260930.md`](../eng/models/paddle-document/verification/paddle-document-opencv-nms-matrix-20260930.md)；TensorRT 两个 RT-DETR 表格单元格工件的正式 5/50 证据见 [`paddle-document-tensorrt-table-cell-matrix-20260930.md`](../eng/models/paddle-document/verification/paddle-document-tensorrt-table-cell-matrix-20260930.md)。

| 精确资产 | ORT CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
|---|:---:|:---:|:---:|:---:|
| `paddle-doc/pp-lcnet-x1-0-doc-ori` | ✓ | ✓ | ✓ | ✓ |
| `paddle-doc/uvdoc` | ✓ | ✓ | ✗ | ✓ |
| `paddle-doc/pp-doclayout-plus-l` | ✓ | ✓ | ✓ | △ |
| `paddle-doc/pp-doclayout-l` | ✓ | ✓ | ✓ | ✓ |
| `paddle-doc/pp-doclayout-m` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/pp-doclayout-s` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/pp-docblocklayout` | ✓ | ✓ | ✓ | △ |
| `paddle-doc/picodet-layout-1x` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/picodet-layout-1x-table` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/picodet-s-layout-3cls` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/picodet-l-layout-3cls` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/rt-detr-h-layout-3cls` | ✓ | ✓ | ✓ | △ |
| `paddle-doc/picodet-s-layout-17cls` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/picodet-l-layout-17cls` | ✓ | ✓ | ✗ | △ |
| `paddle-doc/rt-detr-h-layout-17cls` | ✓ | ✓ | ✓ | △ |
| `paddle-table/slanext-wired` (原始图) | ✓ | ✗ | △ | △ |
| `paddle-table/slanext-wireless` (原始图) | ✓ | ✗ | △ | △ |
| `paddle-table/slanext-wired-openvino-compat` (派生图) | ✓ | ✓ | △ | △ |
| `paddle-table/slanext-wireless-openvino-compat` (派生图) | ✓ | ✓ | △ | △ |
| `paddle-table/pp-lcnet-x1-0-table-cls` | ✓ | ✓ | ✓ | ✓ |
| `paddle-table/rt-detr-l-wired-cell-det` | ✓ | ✓ | ✓ | ✓‖ |
| `paddle-table/rt-detr-l-wireless-cell-det` | ✓ | ✓ | ✓ | ✓‖ |
| `paddle-formula/pp-formulanet-plus-s` | ✓ | ✗¹ | △ | △ |
| `paddle-formula/pp-formulanet-plus-m` | ✓ | ✗¹ | △ | △ |
| `paddle-formula/pp-formulanet-plus-l` | ✓ | ✗¹ | △ | △ |
| `paddle-formula/pp-formulanet-s` | ✓ | ✗¹ | △ | △ |
| `paddle-formula/pp-formulanet-l` | ✓ | ✗¹ | △ | △ |
| `paddle-formula/unimernet` | ✓ | ✗¹ | △ | △ |
| `paddle-seal/ppocrv4-mobile` | ✓ | ✓ | ✓ | ✓ |
| `paddle-seal/ppocrv4-server` | ✓ | ✓ | ✓ | ✓ |
| `paddle-chart/pp-chart2table` (四图 Bundle) | ✓ | ✓ | △ | ✓ |

¹ 六个公式工件已经通过隔离进程逐项尝试，当前 OpenVINO 组合全部不支持：历史 alpha-renaming 派生图没有通过 token parity/Loop reshape/native 稳定性；2026-10-08 在当前 OpenVINO `2026.2.1` runtime 上对六个精确导出复核时，五个 FormulaNet 仍在 `Loop-18` canonical-input 校验失败，UniMERNet 使隔离 reader 进程中止。逐项错误摘要见 [`formula-openvino-isolated-20261008.json`](../eng/models/paddle-document/verification/formula-openvino-isolated-20261008.json)，历史派生图实验见 [`formula-openvino-loop-compat-20261004.json`](../eng/models/paddle-document/verification/formula-openvino-loop-compat-20261004.json)。这表示当前精确后端准入失败，不代表公式识别准确率，也不是缺少 DLL 的结论。

Chart2Table 的 OpenCV DNN 隔离探针已加载 Vision/Projector 和 Token Embedding 两张图；Prefill 的三维 `inputs_embeds` 与 Decode 的四维动态 KV 辅助输入被当前 `OpenCvDnnModelContract` 的 rank≤2 辅助输入合同拒绝。该边界对应当前 JYPPX OpenCV C# `Mat` bridge 只暴露二维辅助分配/reshape，已由 fail-closed 合同测试固定。因此完整 OpenCV 自回归仍保持 `△`，不是四图 Bundle 的完整支持；详细记录见 [`chart2table-opencv-isolated-20260929.json`](../eng/models/paddle-document/verification/chart2table-opencv-isolated-20260929.json)。

Chart2Table 的统一后端/质量索引见 [`chart2table-backend-quality-status-20261011.md`](../eng/models/paddle-document/verification/chart2table-backend-quality-status-20261011.md) 和 [机器可读 JSON](../eng/models/paddle-document/verification/chart2table-backend-quality-status-20261011.json)。该索引集中记录四图完整生成、ChartQA 有界质量选择和 OpenCV rank-3/rank-4 辅助输入阻断；不改变矩阵语义，也不把小样本质量结果写成 split accuracy。

2026-10-04 的 ChartQA 扩展完成了 ORT CPU 12 图有界任务质量运行：`12/12` EOS、`9/12` 结构维度一致、`140/293` 单元格匹配；同一固定选择随后在 TensorRT CUDA 上完成 `12/12` EOS、`9/12` 结构维度一致和 `140/293` 单元格匹配。两份结果均为任务质量诊断，不改变后端运行矩阵。ORT 报告见 [`chart2table-extended-quality-ort-20261004.json`](../eng/models/paddle-document/verification/chart2table-extended-quality-ort-20261004.json)，TensorRT 报告见 [`chart2table-extended-quality-tensorrt-20261005.json`](../eng/models/paddle-document/verification/chart2table-extended-quality-tensorrt-20261005.json)。同 token 上限的 OpenVINO 12 图扩展未在限定时间内完成，矩阵继续以已有六图完整证据为准。

两份既有 JSON 还通过 [ORT/TensorRT 对齐报告](../eng/models/paddle-document/verification/chart2table-extended-quality-backend-alignment-20261005.md)进行同源核对：12/12 输入图片 SHA、结束原因、结构标志和期望单元格数量一致，两边精确单元格合计均为 `140`。该对齐只证明固定样本的后端一致性，不是重新推理、ChartQA split accuracy 或受控性能基准。

### PP-Chart2Table 四图生成 Bundle 验证

验证复用了现有官方 checkpoint 和示例图片，没有重复下载。四张图分别是 Vision/Projector、动态 Token Embedding、固定 286-token 官方 Prompt Prefill（含 `lm_head` 和 KV）、单 token 动态 past Decode（含 `lm_head` 和 KV）。Tokenizer 直接读取官方 `qwen.tiktoken`、`tokenizer_config.json`、`added_tokens.json`；Prompt 为 286 token，含 256 个连续 `<imgpad>`。

ORT CPU、OpenVINO CPU 与 TensorRT CUDA 在同一官方图片上均运行到 EOS，返回 141 个 token（含 EOS `151645`）及相同的 2018–2023 六行表格。此前三 token ORT 参考相对 Paddle 的逐步最大 logit 误差为 `4.29e-5`、`4.82e-5`、`6.77e-5`；三个后端的完整表格文本哈希相同。OpenCV/Pillow-compatible 输入与官方 PaddleX 图像处理器最大绝对差为 `0.01501`、平均绝对差 `2.31e-7`。原始 TensorRT 通用 host-KV 路径耗时 `83.42 s`；加入几何缓冲区复用、显存驻留 KV ping-pong、精简动态 shape/binding 调用，并使用 FP32+TF32 语言 plans 后，TensorRT device 专用路径观测为 `12.64/12.12/10.12 s`，最好一次 140 个 Decode 步骤 P50/P95 `66.47/73.48 ms`。ORT CPU 为 `39.76 s`、Decode P50/P95 `94.69/123.92 ms`；OpenVINO CPU 为 `46.40 s`、`146.63/197.52 ms`。测试设备为 Ryzen 7 5800H / Windows 11 / .NET 10 + RTX 3060 Laptop / TensorRT 10.11 / CUDA 12.9 / cuDNN 9.22。未锁定 GPU 时钟，也没有完整负载下时钟轨迹；结果是单样例观测，不是受控多轮基准或数据集精度结论。严格 FP32 host-cache TensorRT 路径仍可通过原 `PaddleChart2TableOnnxSession` 使用；优化路径用 `PaddleChart2TableTensorRtDeviceSession`。详见验证 JSON。

TensorRT 设备为 RTX 3060 Laptop 6GB、TensorRT 10.11.0、CUDA 12.9、cuDNN 9.22。四图使用 FP16 Vision/Embedding 与 FP32 Prefill/Decode；FP16 文本图曾产生全零 `lm_head` logits，改用 FP32 后正确生成。之前的空 Engine 是回归测试使用了错误 Decoder 图和不匹配的 attention-mask profile（past KV optimum 为 512 时 mask 应为 513），不是 `TensorRtOnnxEngineBuilder` 的通用限制。选用与 Release 一致的 epsilon 图、修正 mask profile 后，库 Builder 成功创建动态 KV Decode Engine；库 Builder 创建的四张 plan 通过完整 EOS 生成回归。除官方样例外，四张 ChartQA human 图也在 TensorRT 上完成 4/4 EOS、期望表格 SHA 和结构行/单元格 1.00 回归；逐图阶段耗时、输入/Engine SHA 见 [`chart2table-tensorrt-multi-image-20260930.md`](../eng/models/paddle-document/verification/chart2table-tensorrt-multi-image-20260930.md)。四图总耗时约 `26.995–99.866 s`，Decode P50/P95 约 `573–790/975–1164 ms`，未锁定 GPU 时钟且每图仅运行一次，仍不可视为受控性能基准。OpenCV DNN 自回归流水线和数据集级准确率仍未验证。精确合同、plan 哈希与完整官方样例阶段数值见 [`chart2table-component-validation.json`](../eng/models/paddle-document/verification/chart2table-component-validation.json)。
同一固定 ChartQA `val` 六图选择又完成了 TensorRT 扩展记录：`6/6` EOS、`5/6` 结构维度一致、单元格匹配 `44/116`，总耗时 `1.18–13.25 s`，Decode P50/P95 `33.32–40.39 / 34.32–48.60 ms`。这与 ORT/OpenVINO 选择暴露相同的多列结构边界；因样本数量、分组方式和未锁频条件限制，只作为任务质量/阶段计时证据，不改变 `✓` 的精确 Bundle 运行语义，也不宣称 ChartQA split accuracy。详见 [`chart2table-tensorrt-extended-quality-20261004.md`](../eng/models/paddle-document/verification/chart2table-tensorrt-extended-quality-20261004.md) 及 [JSON](../eng/models/paddle-document/verification/chart2table-tensorrt-extended-quality-20261004.json)。12 图 TensorRT 记录和 ORT/TensorRT 对齐报告见上一节。

### 已完成的精确 PP-Structure Decoder 语义 smoke

| 精确工件 | DeploySharp Decoder 结果 | ORT CPU | OpenVINO CPU |
|---|---|:---:|:---:|
| `paddle-doc/pp-lcnet-x1-0-doc-ori` | 官方 `img_rot180_demo.jpg`：180°，score 约 `0.89236`，不重复 Softmax | ✓ | ✓ |
| `paddle-doc/pp-doclayout-l` | Paddle NMS 区域结果：300 个候选（阈值 0），阈值 0.5 时 1 个区域 | ✓ | ✓ |
| `paddle-doc/uvdoc` | 矫正张量：`640x640x3`，全部有限值 | ✓ | ✓ |
| `paddle-table/pp-lcnet-x1-0-table-cls` | 官方 `table_recognition.jpg`：`wired_table`，score 约 `0.844209` | ✓ | ✓ |
| `paddle-table/rt-detr-l-wired-cell-det` | Paddle NMS 单元格结果：300 个区域 | ✓ | ✓ |
| `paddle-table/rt-detr-l-wireless-cell-det` | Paddle NMS 单元格结果：300 个区域 | ✓ | ✓ |
| `paddle-table/slanext-wired` | 结构序列：专用表格 Decoder；派生兼容图通过 OpenVINO | ✓ | ✓** |
| `paddle-table/slanext-wireless` | 结构序列：专用表格 Decoder；派生兼容图通过 OpenVINO | ✓ | ✓** |
| `paddle-seal/ppocrv4-mobile` | 概率图掩码：`224x224`，专用印章 Decoder | ✓ | ✓ |
| `paddle-seal/ppocrv4-server` | 概率图掩码：`224x224`，专用印章 Decoder | ✓ | ✓ |

印章模型的上述 ORT/OpenVINO 解码证据现另有真实 batch=2 OpenCV DNN 运行补充；该批结果只扩展到 mobile/server 两张精确图，不构成其它图或其它 PP-Structure 模型的 OpenCV 支持声明。机器可读的逐行输入/输出 hash 见[报告 JSON](../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json)。

### TensorRT 精确工件证据

| 精确工件 | Engine / 运行时 | 一致性结果 | 稳态耗时 |
|---|---|---|---:|
| `paddle-table/pp-lcnet-x1-0-table-cls` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；静态 `x=[1,3,224,224]`；Engine SHA `c9464c22f5d1d11ac4e2f35fb3d437a782278d9a85f697bdd3f8ef050ad065f2`（7,350,700 bytes） | ORT `wired_table` / `0.844208`，TensorRT `wired_table` / `0.851693`，绝对分数差 `0.007485` ≤ `0.01` | P50 `1.212 ms` / P95 `1.331 ms` |
| `paddle-doc/pp-doclayout-l` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态 `image`、`im_shape`、`scale_factor` profile；Engine SHA `c6acaedc9f3ed2996e012d1fd6b45c5eb061877d2ac3aa0f77bde34f65e9a0ba` | 300 个候选中 9 个 score ≥ 0.05；与 ORT CPU 的高置信度 score 误差 ≤ 0.01、源坐标误差 ≤ 1.5 px | P50 `19.414 ms` / P95 `25.876 ms` |
| `paddle-table/rt-detr-l-wired-cell-det` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态 `im_shape=[1,2]`、`image=[1,3,640,640]`、`scale_factor=[1,2]`；Engine SHA `71fb2ee40a53b66c5e61d8bf2679235f5eb53fc2f01dc335dc3bb828b0a2132b`（155,294,724 bytes） | ORT/TensorRT 均返回 300 个候选；高置信候选全量比较最大 score 差 `0.00895`、最小 IoU `0.9285` | P50 `21.605 ms` / P95 `24.613 ms` |
| `paddle-table/rt-detr-l-wireless-cell-det` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态 `im_shape=[1,2]`、`image=[1,3,640,640]`、`scale_factor=[1,2]`；Engine SHA `cb7df85c136e0b45e673a37995ed5c58f2a587f385cca15f674a6cf8f29c6c2a`（144,357,020 bytes） | ORT/TensorRT 均返回 300 个候选；score ≥ 0.05 统计为 `294/295`，共同比较 294 个，最大 score 差 `0.00701`、最小 IoU `0.9179` | P50 `21.689 ms` / P95 `25.661 ms` |
| `paddle-seal/ppocrv4-mobile` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态输入 profile 固定为 `image=[1,3,224,224]`；Engine SHA `13772da4a34bd9fb3ea1e1c3cfedabf6a2fb1aace0762c6e5253904e374c9953`（7,045,964 bytes） | `demo_1.jpg` 上 ORT/TensorRT 均返回 `224x224` 掩码、区域数 `0/0`；原始掩码最大/平均绝对误差 `9.42e-8`/`1.36e-8`；该图片无印章，仅作执行和输出一致性 | P50 `3.710 ms` / P95 `4.558 ms` |
| `paddle-seal/ppocrv4-server` | TensorRT 11 API；CUDA 12.9；cuDNN 9.22；动态输入 profile 固定为 `image=[1,3,224,224]`；显式 `DisableTf32=true`；Engine SHA `d89b4c5cbfde1707bd486007bdf65afe481e3fcdb85c1cdf11d7c98f88332d03`（140,877,596 bytes） | `demo_1.jpg` 上 ORT/TensorRT 均返回 1 个区域；掩码平均绝对误差 `2.4136e-6`，最大绝对误差 `8.6451e-4`，逐元素误差通过当前合同 | P50 `12.221 ms` / P95 `24.648 ms` |

上述 TensorRT 证据均使用 5 次预热、50 次测量；表格分类由 TensorRT CUDA 预处理、推理和分类 Decoder 完成，`pp-doclayout-l` 使用 OpenCV 在 CPU 上准备输入并由 TensorRT 负责模型执行和图内 Paddle NMS，两个 RT-DETR 表格单元格模型使用 OpenCV 准备三输入几何张量、TensorRT 执行图内 Paddle NMS，再由同一 Decoder 完成 ORT 对照，印章 mobile/server 使用 OpenCV 在 CPU 上准备输入并由 TensorRT 执行概率图和专用 Decoder。server 印章在 TRT11 强类型网络下显式关闭 TF32，当前样本的原始概率图逐元素误差通过测试合同；这仍不替代数据集级精度评估。所有结果只对表中精确工件成立，不外推到同组其它模型。Engine 与 TensorRT/CUDA/cuDNN 版本及 GPU 架构绑定，换设备或运行时后必须重新构建并验证。

公式语义解码的精确证据：`FormulaExportsDecodeWithOfficialTokenizerOnRealOrtCpu` 对 6 个公式工件逐一使用官方 `general_formula_rec_001.png`、专用去白边/缩放/归一化和各自官方 BPE tokenizer 运行。最新结果为：Plus-S `197/262`、Plus-M `197/262`、Plus-L `197/262`、FormulaNet-S `213/276`、FormulaNet-L `197/262`、UniMERNet `208/270`（分别为 token 数/LaTeX 字符数），全部 warnings=0 并遇到 EOS。Plus-S/M/L 的 LaTeX 忽略排版空白后与官方示例一致；FormulaNet-S/L、UniMERNet 仍有符号或格式差异。原先 `bus.jpg` 的输出长度只算旧 smoke，不能视为公式精度。当前 ORT `✓` 表示精确工件可运行，不代表数据集准确率通过。2026-10-04 的派生 OpenVINO 兼容图实验仍逐项失败准入：Plus-S 仅返回 3 tokens、FormulaNet-S 返回 1023 tokens 且缺少 EOS，Plus-M/Plus-L/FormulaNet-L 在 Loop reshape 处失败，UniMERNet native crash，六个精确组合均标记 `✗`。详细 SHA 和错误摘要见 [`formula-openvino-loop-compat-20261004.json`](../eng/models/paddle-document/verification/formula-openvino-loop-compat-20261004.json)。

ORT 的 PP-Structure 语义 smoke 由 `tests/DeploySharp.Visual.OpenCV.Tests/PaddleDocumentSemanticIntegrationTests.cs` 完成，方向→版面真实统一编排由 `PaddleDocumentPipelineSemanticIntegrationTests.cs` 完成；OpenVINO 的代表性模块和 SLANeXt 派生兼容图由 `PaddleDocumentOpenVinoSemanticIntegrationTests.cs` 完成；OpenCV DNN 的 `pp-doclayout-l` 由 `PaddleDocumentOpenCvSemanticIntegrationTests.cs` 完成，采用单 batch、无 `bbox_num` 输出合同并返回有效版面区域；TensorRT 的方向、表格分类、版面和 mobile/server 印章精确工件由 `PaddleDocumentTensorRtExternalIntegrationTests.cs` 与 `PaddleDocumentTensorRtServerSealExternalIntegrationTests.cs` 完成 ORT 对照和 P50/P95 测量。测试在 Windows x64 执行：版面/矫正沿用 `bus.jpg`，印章使用 `E:\Data\ocr\demo_1.jpg`，分类、表格结构和公式已切换到官方对应任务示例；SLANeXt 两个模型均返回 24 tokens、13 个单元格。原始 SLANeXt Release 图仍会触发 OpenVINO `Loop` importer 阻断；`Build-PaddleDocumentOpenVinoCompatibility.ps1` 对循环体形式参数做 alpha-renaming，生成的 wired/wireless 派生图已经与原始 ORT 输出逐元素（容差 0.001）及表格 Decoder 结果对齐。公式六个 OpenVINO 精确组合均已在隔离进程中标记为 importer/native 阻断。上述结果是可复现的精确工件语义证据，不代表其余 PP-Structure 工件或 OpenCV DNN/TensorRT 已全部通过。

## 阅读规则

补充说明：上一版记录中的 OpenCV/TensorRT 阻断描述已经过复核。当前 OpenCV DNN 的 `pp-doclayout-l` 单 batch 测试已通过（不请求被 importer 裁掉的整型 `bbox_num` 输出）；TensorRT 已在本机 TRT 11 + CUDA 12.9 + cuDNN 9.22 组合下完成 PP-LCNet 文档方向、PP-LCNet 表格分类、`pp-doclayout-l` 和 mobile/server 印章的 ONNX→Engine→推理 smoke。其余工件仍按表格中的精确证据状态处理。

- 通过只对表中精确工件成立；更换导出文件、输入尺寸、引擎或运行时版本后需要重新验证。
- TensorRT 列仅表示 CUDA 引擎路径通过；引擎必须与本机 TensorRT/CUDA 版本和输入 profile 匹配。
- PaddleOCR 的单图完整流水线、batch 和并发通道组合单独记录在[设备性能实测](articles/device-performance-benchmarks.md)；这里的单元格只表达阶段工件是否可执行。
- OpenCV DNN 的 `✗` 是当前 importer、动态 shape 或辅助输入限制，不代表其他后端的结果。
- `✓**` 表示使用 `eng/models/paddle-document/scripts/Build-PaddleDocumentOpenVinoCompatibility.ps1` 生成的派生 ONNX；原始 Release 文件保持未修改，派生文件使用独立 compatibility ID、文件名和 SHA-256 注册在 [`slanext-openvino-compatibility.json`](../eng/models/paddle-document/verification/slanext-openvino-compatibility.json)。原始 SLANeXt Release 图在当前 OpenVINO `Loop` importer 下标记为 `✗`，不能直接部署；兼容图的独立 Release 资产名已确定为 `slanext-wired-openvino-compat.onnx` 和 `slanext-wireless-openvino-compat.onnx`，上传前状态为 `planned-separate-assets`。
- `✓†` 表示精确工件 `pp-doclayout-l` 已完成 TensorRT 11 动态输入和图内 Paddle NMS 推理，并与 ORT CPU 结果比较；输入预处理仍在 CPU，不能解释为 CUDA 端到端流水线。
- `✓‡` 表示精确工件 `pp-lcnet-x1-0-table-cls` 已完成 TensorRT 11 静态输入、CUDA 预处理、分类 Decoder 和 ORT 标签/置信度一致性比较；置信度允许明确记录的绝对误差 `0.01`，不能外推为数据集精度结论。
- `✓‖` 表示两个 RT-DETR 表格单元格精确工件已完成 TensorRT 11 动态三输入 profile、图内 Paddle NMS、全候选几何 IoU/score 对照和 5/50 P50/P95 测量；wireless 的一个 score≥0.05 候选落在数值边界，按共同匹配候选记录，不能外推为 cell 召回率或表格结构准确率。
- `✓§` 表示精确工件 `ppocrv4-mobile/server-seal-det` 已完成 TensorRT 11 概率图推理、印章 Decoder 和 ORT 尺寸/区域数一致性比较；server 构建显式关闭 TF32，当前样本的原始掩码逐元素误差通过合同，但不能外推为数据集召回率。
- `✓¶` 表示 Chart2Table 四图 Bundle 在 ORT CPU、OpenVINO CPU 和 TensorRT CUDA 均有完整 EOS 文本证据；TensorRT 四张 plan 已由 DeploySharp `TensorRtOnnxEngineBuilder` 构建并完成完整生成回归。此符号仍不表示数据集级准确率验证。
- `△` 表示存在适用的后端执行合同，但本机尚无该精确组合的真实运行证据；部署时应先在目标设备完成 smoke test。
- `✗` 表示已尝试并确认当前精确组合不支持或无法运行；原始 SLANeXt 图在当前 OpenVINO `Loop` importer 下即属此类。只有尚无真实执行证据（包括 Chart2Table/OpenCV DNN 自回归）时应标 `△`，不能将“未验证”写成“已证实失败”。此状态不会被相近模型或派生图的结果覆盖。
- 未验证的模型不再使用 `—` 混淆“未验证”和“不适用”；不会用相近模型、脚本退出码或合同测试代替真实推理证据。

## PaddleOCR 完整流水线证据

当前已完成真实 `det → crop → cls/orientation → rec → merge` 的核心组合包括：PP-OCRv4 mobile/server、PP-OCRv5 mobile/server、PP-OCRv6 tiny/small/medium，共 7 组。`PaddleOcrAllCorePipelineOrtIntegrationTests` 在 Windows x64、`E:\Data\ocr\demo_1.jpg`、ONNX Runtime CPU 上逐组返回 16 个区域且全部产生识别结果；单次冷启动端到端耗时为 v4 mobile `676.431 ms`、v4 server `2840.955 ms`、v5 mobile `911.419 ms`、v5 server `1890.864 ms`、v6 tiny `356.106 ms`、v6 small `1044.636 ms`、v6 medium `2286.589 ms`。v6 没有独立 CLS 归档，测试明确复用 PP-OCRv5 mobile text-line CLS。OpenCV DNN 七组短诊断的机器可读结果见 [`paddleocr-core-opencv-20260924.json`](../eng/models/paddle-ocr/verification/paddleocr-core-opencv-20260924.json)。

PP-OCRv5 mobile 的真实 OpenCV DNN 全流程已与 ORT 在同一准备张量上复核：原先记录的 8/16 差异来自外部测试合同把全分辨率概率图错误声明为 `[1,1,128,-1]`，OpenCV 因而把相同元素数重解释成 `[1,1,128,2048]`；不是 OpenCV importer 或 DB 解码器漏检。合同改为从输入张量绑定输出高度后，两边均返回 16 个区域，逐区域识别文本一致，坐标误差 ≤0.5 px、分数误差 ≤0.001；原始概率图最大/平均绝对差 `4.2915344e-5` / `8.6187186e-8`。输入张量 SHA-256 为 `dbbdb3938fa7a880aec0403f74d064e125826238abf16b12c6d5d58b0612f553`，最终输出 SHA-256 为 OpenCV `dd55ffdab9b595f016083c32ac53b61f964d378958409825473d65b6b4d798c1`、ORT `bcd67a98e08ee65c6fd51dd49f50a1bb3828cbc4037a57d73af64ddba416a2eb`。两次单次 OpenCV `pipeline.Run` 观察分别为 `5655.803 ms` 和 `4117.808 ms`，包含裁剪、分类、识别等流水线开销且不是稳定性能基准；此结果不替代其它后端的逐工件性能基线，也不能外推到其它 OCR 模型。

上述 7 组覆盖当前本机全部 DET/REC 核心工件；v4/v5 的每个 CLS 工件也已分别完成阶段级验证，v6 的 CLS 复用策略在测试和目录中显式记录。新增的[三图跨后端证据](../eng/models/paddle-ocr/verification/paddleocr-core-three-image-openvino-ort-20260930.md)使用 `demo_1.jpg`、`demo_2.jpg`、`demo_3.jpg`，对 7 组共 21 个组合分别运行 ORT CPU 和 OpenVINO CPU：两边均 `21/21` 完整执行，区域数和已识别区域数 `21/21` 一致，按区域纯文本 SHA `21/21` 一致。该证据仍没有人工逐字真值，不计算 CER/WER 或召回率，单次耗时也不替代 5/50 正式测速。OpenCV DNN 的 v4 server、v5 mobile/server 全流程和 TensorRT 的 v4/v5 server 全流程已有精确工件证据；v4 server 重新构建 mobile CLS engine 后，DeploySharp bridge det/rec/CLS 三个 engine 均能加载并推理。v4 server TensorRT 带遥测 `demo_1.jpg`、batch 8、2 个阶段 Session 的 P50/P95 为 `122.200/127.458 ms`；v5 server 带遥测结果为 `145.284/156.248 ms`。OpenCV 这三组本轮使用同一 `demo_1.jpg`、单 channel、batch 1 的诊断协议，耗时分别为 v4 server P50/P95 `6655.278/7877.100 ms`、v5 server `5087.234/5557.888 ms`；这组 CPU 数值不能与 TensorRT 的 GPU 记录直接排序。`pp-doclayout-l` 已有 TensorRT NMS 级实测，但不能把它自动外推到其它 PP-Structure 模型。PP-Structure 目前已具备模块级合同、专用 Decoder、按需模型资产、统一编排层和 TensorRT 外部验证入口；真实多模型、多后端文档智能端到端证据仍待各运行时完成。
上述三图记录还配套了[检测器中间张量对照](../eng/models/paddle-ocr/verification/paddleocr-core-det-intermediate-parity-20260930.md)：7 个 DET 工件 × 3 张图共 21 组 ORT/OpenVINO 原始 DB 输出，输入和输出形状均 `0` 不一致，最大绝对差 `0.0067548752`，低于 `0.01` 合同阈值。该结果用于定位前处理、importer 或 DB 解码差异，不等同于检测召回率或模型精度。
同一三图集合还完成了[OpenCV DNN/ORT 全流程对照](../eng/models/paddle-ocr/verification/paddleocr-core-three-image-opencv-ort-20260930.md)：7 组模型 × 3 张图 `21/21` 通过，逐区域文本、置信度和坐标均符合合同。测试修正了 v4 mobile 输出名、v4 legacy CLS `[1,3,48,192]` shape 以及 batch=1 的 OpenCV 限制；这不改变 ORT/OpenVINO 的动态 Batch 合同。OpenCV 单次端到端耗时仅作兼容性观察，不能与正式 5/50 或 GPU 性能直接比较。

新增的[v6 Medium HierText 质量记录](../eng/models/paddle-ocr/verification/hiertext-v6-medium-public-ocr-20261002.md)在同一 24 页标注选择上完成 ORT CPU SlidingWindow 全流程，24/24 页面无失败或空输出。IoU 0.5 检测 TP/FP/FN 为 `459/478/561`、F1 `46.91%`，匹配区域 CER/WER 为 `12.12%/32.58%`，端到端 CER/WER 为 `71.76%/98.42%`，总耗时 P50/P95 为 `1928.53/4687.44 ms`（单次测量）。该数据仍为 smoke-only，不是发布精度或正式 5/50 性能证据，且没有 3,200 字符以上自然连续行。

对应的[ORT/OpenVINO v6 Medium 质量 parity 记录](../eng/models/paddle-ocr/verification/hiertext-v6-medium-ort-openvino-quality-parity-20261002.md)在完全相同的 24 页、模型和滑窗合同下复跑 OpenVINO CPU；两后端页面文本、区域数量、TP/FP/FN、CER/WER 全部一致，937 个对应区域的 polygon 坐标最大差为 `0`、置信度最大差为 `2.57e-5`。OpenVINO 总耗时 P50/P95 为 `1325.58/2646.71 ms`，但仍是单次 smoke 观察，不是正式性能排名。

同一精确组合的[OpenCV DNN 质量记录](../eng/models/paddle-ocr/verification/hiertext-v6-medium-opencv-quality-20261002.md)也完成 24/24 页面；检测 TP/FP/FN 和几何覆盖与 ORT 完全一致，但 23/24 页文本序列、937 个对应区域中的 174 个文本与 ORT 不同。OpenCV 匹配 CER/WER `11.89%/30.48%`、端到端 CER/WER `71.14%/96.89%`，总耗时 P50/P95 `11971.02/26997.76 ms`。矩阵应将其理解为“可执行但后端结果/速度边界不同”，不能标为 importer 失败或正式性能通过。

本轮补充的本机证据：`E:\Model\paddleocr\paddle-ocr-onnx-smoke.json` 对 17 个核心 ONNX 图均记录了 ONNX Runtime CPU 图级 smoke；`PaddleOcrAllCorePipelineOrtIntegrationTests` 对 7 组完整流水线完成真实 ORT CPU 运行；新增 `PaddleOcrAllCorePipelineOpenVinoIntegrationTests` 对同样 7 组完成真实 OpenVINO CPU `det → crop → cls → rec → merge` 运行；阶段 19/20 集成测试补测了 PP-OCRv5 mobile/server 的 ORT CPU、OpenVINO CPU 以及 v4 legacy/v5 mobile/server CLS；基准工具在同一 `demo_1.jpg` 上补测了 OpenCV DNN 的 v4 mobile/server、v5 mobile/server、v6 tiny/small/medium 七组完整流水线，均返回 16 个区域并产生识别结果。测试使用的模型路径已经统一为 `E:\Model\paddleocr\PP-OCRv4`、`PP-OCRv5`、`PP-OCRv6`，不再依赖旧的 `E:\Model\ocr` 路径。
