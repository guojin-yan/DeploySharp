# PaddleOCR / PP-Structure 文档智能模块

DeploySharp 现在把 PaddleOCR 的文档智能能力按模块建模，而不是把“版面分析”和“表格识别”误写成普通 OCR 检测。PP-StructureV3 官方产线包含文档方向分类、文本图像矫正、版面区域检测、表格分类/单元格检测/结构识别、公式识别、印章文本检测和图表解析；DeploySharp 对这些模块保留独立的任务 ID、模型来源、转换状态和结果合同。

官方 PP-StructureV3 的组合方式和模型说明见 [PaddleOCR 官方 PP-StructureV3 文档](https://paddlepaddle.github.io/PaddleOCR/main/version3.x/pipeline_usage/PP-StructureV3.html)。本项目不会把 Paddle Inference 的 `.tar` 工件自动当作 ONNX，也不会在没有转换和运行时证据时标记为“多后端已支持”。

## 当前模块边界

| 模块 | 官方模型族 | DeploySharp 任务/结果合同 | 当前状态 |
| --- | --- | --- | --- |
| 文档方向 | `PP-LCNet_x1_0_doc_ori` | `VisualTaskId.DocumentOrientation`、四分类结果 | ORT CPU、OpenVINO CPU 和 TensorRT 11 smoke 已完成；分类预处理为短边 256 后中心裁剪 224 |
| 文本图像矫正 | `UVDoc` | `VisualTaskId.DocumentUnwarping`、输出尺寸/变换元数据 | ORT CPU/OpenVINO CPU 均完成同一 `bus.jpg`、同一输入张量的真实运行；输出均为 `640x640x3` 且有限值，max/mean 绝对差 `0.0730591/0.00176066`。TensorRT 11 + DeploySharp decoder 已完成 DisableTf32 对照，max/mean 绝对差 `0.0778809/0.00172731`；OpenCV 5.0 importer 在 `PaddingLayerImpl` 处确认阻断。该 TensorRT 结果是有界数值比较，不是像素等价或视觉质量通过 |
| 版面区域检测 | `PP-DocLayout*`、`PP-DocBlockLayout`、PicoDet/RT-DETR layout | `VisualTaskId.LayoutDetection`、区域检测结果 | 本机 13 个已转换 layout 工件已完成 ORT CPU + Paddle 后置 NMS Decoder 逐模型 smoke；12 个此前未逐项验证的工件已在 OpenVINO CPU 上全部执行通过。OpenCV DNN 对 `pp-doclayout-plus-l`、`pp-docblocklayout`、`rt-detr-h-layout-3cls/17cls` 执行通过，PP-DocLayout-M/S 和 PicoDet 相关工件在 OpenCV 5.0 `MatMul` importer 处明确不支持；逐项边界见[OpenVINO 矩阵报告](../../eng/models/paddle-document/verification/paddle-document-openvino-layout-matrix-20260930.md)和[OpenCV NMS 矩阵报告](../../eng/models/paddle-document/verification/paddle-document-opencv-nms-matrix-20260930.md) |
| 表格分类 | `PP-LCNet_x1_0_table_cls` | `VisualTaskId.TableClassification` | wired 模型已完成 ORT CPU/OpenVINO CPU 分类 Decoder smoke，并有 TensorRT 11 CUDA 真实证据；使用官方短边 256、中心裁剪 224 |
| 表格单元格检测 | `RT-DETR-L_*_table_cell_det` | `VisualTaskId.TableCellDetection` | wired/wireless 均已完成 ORT CPU Paddle NMS Decoder smoke；OpenVINO CPU wired/wireless 两个精确工件均已执行通过；TensorRT 11 动态三输入 profile、图内 NMS、ORT 几何/score 对照和 5/50 P50/P95 也已完成，逐项报告见 [`paddle-document-openvino-table-cell-matrix-20260930.md`](../../eng/models/paddle-document/verification/paddle-document-openvino-table-cell-matrix-20260930.md) 与 [`paddle-document-tensorrt-table-cell-matrix-20260930.md`](../../eng/models/paddle-document/verification/paddle-document-tensorrt-table-cell-matrix-20260930.md) |
| 表格结构识别 | `SLANeXt_wired/wireless` | `VisualTaskId.TableStructureRecognition`、表格标记和单元格结果 | wired/wireless 均已完成 ORT CPU 双输出序列/八点框 Decoder smoke；原始 Release 图在 OpenVINO 当前版本不支持（`Loop` importer），仅派生 alpha-renamed 图通过逐元素和 Decoder 对齐 |
| 公式识别 | `PP-FormulaNet_plus-*`、`UniMERNet` | `VisualTaskId.FormulaRecognition`、LaTeX/序列结果 | 6 个可转换公式工件均已在真实 ORT CPU 推理后使用官方 `inference.yml` BPE tokenizer 完成语义 LaTeX 解码；OpenVINO 兼容图实验中没有模型通过 token 级 parity，仍标记为不支持；token-piece fallback 保留给旧目标框架 |
| 印章文本检测 | `PP-OCRv4_*_seal_det` | `VisualTaskId.SealTextDetection`、区域结果 | mobile/server 均已完成 ORT CPU + OpenVINO CPU 概率图连通区域 Decoder smoke；mobile/server 另有 TensorRT 11 输入/掩码/Decoder 一致性实测（server 已显式关闭 TF32）；弧形文本明确不支持，需调用方先展开后再提交识别器-only |
| 图表解析 | `PP-Chart2Table` | `PaddleChart2TableOnnxSession`、`PaddleChart2TableTensorRtDeviceSession`、`PaddleChart2TableGenerationResult` | 四图 greedy 生成已接入；四张 ONNX 图和 tokenizer 作为 `paddle-chart/pp-chart2table` Bundle 发布在 `models-paddleocr`；ORT CPU、OpenVINO CPU、TensorRT CUDA 已完成官方样例 EOS 证据，ChartQA 样本扩展验证见下文 |

## 获取官方模型并转换为 ONNX

标准 PP-Structure ONNX 已经同步发布到 [`models-paddleocr`](https://github.com/guojin-yan/DeploySharp/releases/tag=models-paddleocr) Release，按单模型资产提供，不要求用户下载整包。运行时可用 `PaddleOcrReleaseClient.GetModelAsync("paddle-doc/pp-doclayout-s")`、`paddle-table/slanext-wired` 或 `paddle-formula/unimernet` 按需获取并校验；`PaddleDocumentModelCatalog.GetReleaseArtifact` 可查询对应资产名、大小、SHA-256、opset 和下载地址。Chart2Table 是生成式四图模型，按 `paddle-chart/pp-chart2table` Bundle 用 `PaddleOcrReleaseClient.GetBundleAsync(...)` 单独获取四张 ONNX 和 tokenizer 文件；它不伪装成一个普通单图 ONNX。

仓库提供官方模型目录和获取脚本：

- 模型目录：[paddle-document-models.json](../../eng/models/paddle-document/paddle-document-models.json)
- 获取/转换：[Acquire-PaddleDocumentModels.ps1](../../eng/models/paddle-document/scripts/Acquire-PaddleDocumentModels.ps1)

脚本只把模型保存到外部模型根目录，不把大型模型提交到 Git。它会下载 Paddle 官方 `paddle3.0.0` 推理归档，记录源归档大小和 SHA-256，解压后调用 `paddle2onnx`，最后为每个模型写出 `records/<id>.json` 和根目录 `acquisition-summary.json`。

先安装与当前 Paddle 导出格式匹配的 Python 运行时、`paddlepaddle` 和 `paddle2onnx`，再执行：

```powershell
python -m pip install paddlepaddle paddle2onnx
& .\eng\models\paddle-document\scripts\Acquire-PaddleDocumentModels.ps1 `
  -ModelId pp-lcnet-x1-0-doc-ori,pp-doclayout-plus-l `
  -ModelRoot E:\Model\PaddleDocument
```

确认小规模转换成功后，再获取全部官方目录：

```powershell
& .\eng\models\paddle-document\scripts\Acquire-PaddleDocumentModels.ps1 `
  -All -ModelRoot E:\Model\PaddleDocument
```

`-SkipConversion` 只下载和解压源模型，用于排查网络或转换依赖；它不会把结果标记成 ONNX。转换失败的记录会标记为 `conversion-blocked`，包括具体异常，不会被静默写成可运行模型。公式、图表、矫正等模型的转换可能需要特定 Paddle/Paddle2ONNX 版本；如果官方算子不在 ONNX 导出支持范围内，应保留源模型记录并实现专用导出器，而不是篡改结果合同。

本地标准推理归档转换为 ONNX 后按单模型资产同步到上述 Release；Chart2Table 的上游归档是生成式权重而非 `inference.json + inference.pdiparams`，因此模型目录仍把“单文件源归档转换项”标为 `conversion-blocked`。它的四张派生 ONNX 和三份 tokenizer 资产现已作为可单独按需下载的 Bundle 发布。ORT CPU、OpenVINO CPU 和 TensorRT CUDA 在官方样例上均跑到 EOS 并生成相同完整文本；OpenCV DNN 隔离探针已确认 Vision/Embedding 可加载，但 Prefill/Decode 需要三维/四维辅助张量，当前 OpenCV DNN 适配器合同不支持，因此完整 OpenCV 自回归仍未支持。详见 [`chart2table-opencv-isolated-20260929.json`](../../eng/models/paddle-document/verification/chart2table-opencv-isolated-20260929.json)。

### 真实 ONNX 的 Batch 边界

不要根据模型名称或 Profile 参数猜测官方图是否支持 Batch。使用 [`Audit-PaddleDocumentBatchShapes.py`](../../eng/models/paddle-document/scripts/Audit-PaddleDocumentBatchShapes.py) 对外部模型根目录做 protobuf 级审计后，当前 54 个本地 ONNX 文件中 53 个可解析、1 个零字节实验导出被保留为失败记录；其中 32 个图的输入第一轴是动态的，8 个标准 PP-Structure 图明确是静态 `batch=1`。动态输入/输出图包括 PP-DocLayout-L/Plus-L、PP-DocBlockLayout、RT-DETR 版面/单元格、公式、表格分类、印章、SLANeXt 和 UVDoc；PicoDet 与 PP-DocLayout-M/S 应走 Session 池或多页有界并发。完整逐文件输入/输出轴、SHA-256 和失败记录见[机器可读 JSON](../../eng/models/paddle-document/verification/paddle-document-onnx-batch-axis-audit-20261005.json)与[审计报告](../../eng/models/paddle-document/verification/paddle-document-onnx-batch-axis-audit-20261005.md)。

Chart2Table 的视觉、Embedding、Prefill 和 Decode 图都保持请求 batch 为 `1`，只是序列长度或 KV 轴可以动态；自回归状态绑定到单张图，不能因为 KV 动态就把它标记成模型 Batch。动态轴也只表示可以尝试批量绑定，仍需按后端完成输入绑定、Decoder、内存和结果一致性验证；本审计不替代任何后端运行或精度证据。

目前已有多条真实 Batch 执行证据，分别按精确 ONNX、后端和主机记录；最初覆盖的文档方向 `PP-LCNet_x1_0_doc_ori` 与表格分类 `PP-LCNet_x1_0_table_cls` 在 ORT CPU、OpenVINO CPU 上均以两个相同 `bus.jpg` 行运行 `[2,3,224,224]`，标签、分数和顺序一致，见[分类动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-dynamic-batch-ort-openvino-20261005.md)。以下结果不代表所有 PP-Structure 模型都支持 Batch，也不代表性能或数据集准确率。

随后对官方 `PP-DocLayout-L` 动态 Paddle NMS 图完成了真实双行运行：ORT CPU、OpenVINO CPU 均绑定 `[2,3,640,640]` 图像和 `[2,2]` 的 `im_shape`/`scale_factor`，使用 `bbox_num` 将扁平 `[300*batch,6]` 输出按行切分，两行均保留 300 个导出候选且标签/几何完全一致。详见 [版面动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-layout-dynamic-batch-ort-openvino-20261005.md)。这不是版面准确率或性能基准；`300` 是导出图的后置 NMS 候选数量，不是真值目标数量。

同一协议还在架构不同的官方 `RT-DETR-H_layout_3cls` 图上通过：ORT CPU、OpenVINO CPU 均完成双行输入、几何辅助绑定和 `bbox_num` 扁平输出切分，两行各返回 300 个候选且结果逐项一致。详见 [RT-DETR 动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-rtdetr-layout-dynamic-batch-ort-openvino-20261005.md)。这仍只覆盖精确模型/后端/主机，不扩展为 TensorRT、OpenCV 或所有 RT-DETR 变体支持。

SLANeXt wired 也完成了真实动态 Batch 解码：ORT CPU 使用官方 `slanext-wired.onnx`，OpenVINO CPU 使用独立 SHA-256 的 Loop 兼容图；两边均绑定 `[2,3,512,512]` 的两行相同表格，Decoder 均返回 24 个结构 token、13 个 cell，HTML 哈希完全一致。该证据只覆盖这两个精确工件和单机双行执行，不代表原始 SLANeXt 图可以直接导入 OpenVINO，也不代表表格准确率、吞吐或 TensorRT/OpenCV 支持。详见 [SLANeXt 动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-slanext-dynamic-batch-ort-openvino-20261005.md) 及 [JSON 记录](../../eng/models/paddle-document/verification/paddle-document-slanext-dynamic-batch-ort-openvino.json)。

SLANeXt wireless 现也完成同协议实测：ORT CPU 使用官方 `slanext-wireless.onnx`，OpenVINO CPU 使用独立 SHA 的 Loop 兼容图；两个后端均以 `[2,3,512,512]` 运行两行，返回 24 个 token、13 个 cell，HTML 哈希一致。wired 与 wireless 两个变体因此都具备双行动态 Batch 的模型/Decoder 证据。OpenVINO 仍需显式兼容图；该结果不构成表格准确率或性能结论。见 [wireless 报告](../../eng/models/paddle-document/verification/paddle-document-slanext-wireless-dynamic-batch-ort-openvino-20261007.md) 和 [JSON 记录](../../eng/models/paddle-document/verification/paddle-document-slanext-wireless-dynamic-batch-ort-openvino.json)。

2026-10-07 又补充 wired/wireless 两个 RT-DETR-L 表格单元格模型：ORT CPU 和 OpenVINO CPU 均以 `[2,3,640,640]` 执行，`im_shape`、`scale_factor` 分别绑定为 `[2,2]`；输入是 `table_recognition.jpg` 上下两个不重叠的 `551×66` 区域。四个精确模型/后端组合都返回两行、每行 300 个后置 NMS 候选；两行输入张量 SHA 和解码结果 SHA 均不同，能排除把同一行结果复用到另一行。这里的 300 是图导出候选数，不是单元格真值数量。详见[单元格动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.md)及[JSON](../../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-ort-openvino-20261007.json)；这不是准确率、跨后端数值 parity 或性能结论。

随后对这两种 ONNX 图补做 OpenCV DNN batch=2 探针：两个 Session 均成功建立，但 `forward_many` 都报 `DS-OCV-8004 / Requested blob not found`。因此只记录 wired/wireless 两个精确模型的 OpenCV 动态 Batch 不支持，不改变单图 OpenCV 结果以及 ORT/OpenVINO 动态 Batch 证据；按本项目范围不再排查 importer/native 原因。该输入没有单元格真值，本结果不代表准确率或吞吐。详见[OpenCV 边界报告](../../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.md)和[机器可读 JSON](../../eng/models/paddle-document/verification/paddle-document-table-cell-dynamic-batch-opencv-20261007.json)。

UVDoc 也已完成双行动态 Batch：使用 `bus.jpg` 左/右两个不重叠 ROI，ORT CPU 和 OpenVINO CPU 均解码成两个独立 `640×640×3` 结果；逐行跨后端平均绝对差为 `0.002078/0.002263`，属于有界数值对照，不表示像素等价或视觉质量通过。详见[UVDoc 报告](../../eng/models/paddle-document/verification/paddle-document-uvdoc-dynamic-batch-ort-openvino-20261007.md)。

2026-10-07 另对官方 `PP-DocLayout_plus-L` 与 `PP-DocBlockLayout` 做了 batch=2 真模型验证。ORT CPU 和 OpenVINO CPU 四个模型/后端组合均绑定各自的动态图像输入及 `[2,2]` 几何辅助张量，并经 NMS Decoder 返回两行；行输入和结果摘要 SHA 均不同。两个模型分别使用 `[2,3,800,800]` 与 `[2,3,640,640]`；每行各有 300 个图导出候选。测试切自 `bus.jpg` 上下两个半幅，仅作为 batch 行隔离探针，不是文档质量样本、准确率或性能证据。详见[验证报告](../../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.md)及[机器可读 JSON](../../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-ort-openvino-20261007.json)。

对这两个模型追加 OpenCV DNN 动态 Batch 尝试后，Session 创建成功，但 `forward_many` 均返回 `DS-OCV-8004 / Requested blob not found`；该结论只限定于这两个精确模型、OpenCV 5.0 和 batch=2，不推翻已有单图 OpenCV 证据，也不影响 ORT/OpenVINO 的 batch=2 解码结果。依照项目约定未继续深挖 importer。详情见[OpenCV 动态 Batch 边界报告](../../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.md)及[JSON](../../eng/models/paddle-document/verification/paddle-document-additional-layout-dynamic-batch-opencv-20261007.json)。

PP-OCRv4 mobile/server 印章检测已验证真正的 batch=2：使用不同的 `demo_4.jpg` 与 `demo_5.jpg`，ORT CPU、OpenVINO CPU、OpenCV DNN CPU 均把两图组成 `[2,3,224,224]` 并得到 `[2,1,224,224]` 概率图，六个模型/后端组合都完成 Decoder 行映射。page index 与来源 SHA 保持输入顺序；不同后端 raw mask 摘要不一致，因此只证明执行和行隔离，不作为数值 parity。样本没有印章真值，mobile `0/0`、server `1/0` 区域数不能解释为检测准确性，也不是速度结论。见[三后端报告](../../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.md)及[JSON](../../eng/models/paddle-document/verification/paddle-document-seal-dynamic-batch-ort-openvino-opencv-20261007.json)。

方向分类和表格分类也补上了跨三种 CPU 后端的不同输入 Batch 证据：在官方表格示例的顶部/底部区域上，两个 PP-LCNet 模型都返回两行，逐行 raw-logit SHA 不同；测试逐行比较原始输出与 ORT CPU，并要求最大绝对差不超过 `1e-4`。本机观测最大差小于 `1.2e-7`，三个后端对同一区域的 top label 一致。SHA 不同反映浮点值并非逐位相同；输入区域没有人工标签，因此数值 parity 不代表分类准确率或速度。详情见[跨后端报告](../../eng/models/paddle-document/verification/paddle-document-classifier-distinct-dynamic-batch-ort-openvino-opencv-20261007.md)。

公式动态 Batch 已逐项覆盖六个官方动态输入导出：PP-FormulaNet Plus-S/M/L、FormulaNet-S/L 和 UniMERNet。它们都在 ORT CPU 上以官方公式样例的上下不同区域绑定 batch=2，每行到达 EOS，token IDs 与 LaTeX 均和该行独立 batch=1 推理完全一致。裁剪区域是执行隔离探针，不是独立标注公式；结论不代表数据集准确率、吞吐或其他后端支持。各模型 shape、token 数、SHA 与复现命令见[六模型动态 Batch 报告](../../eng/models/paddle-document/verification/formula-dynamic-batch-six-models-ort-20261007.md)。

随后把六个公式模型的动态 Batch=2 路径扩展到 OpenCV DNN CPU；均在推理前以 `DS-OCV-8002` 失败，错误位于 OpenCV 5.0 ONNX importer/input specialization。PP-FormulaNet 五个图报告 `ConstantOfShape` 零维度错误，UniMERNet 报 `GatherND` importer 错误。此处只登记精确图与动态 Batch 尝试的不支持边界，不推翻 ORT CPU 的真实 Batch 证据，也不外推 batch=1 或其它 OpenCV 版本；按范围不再深挖 importer。详见[公式 OpenCV 动态 Batch 报告](../../eng/models/paddle-document/verification/paddle-document-formula-dynamic-batch-opencv-20261007.md)和[机器可读 JSON](../../eng/models/paddle-document/verification/paddle-document-formula-dynamic-batch-opencv-20261007.json)。

同日还为官方 RT-DETR-H 17-class 版面模型完成 batch=2 验证：ORT CPU/OpenVINO CPU 对 `bus.jpg` 上下不同区域绑定 `[2,3,640,640]` 和两个 `[2,2]` 几何输入，均返回两行、每行 300 个候选；OpenCV DNN session 虽创建成功，但 forward 报 `DS-OCV-8004` / `Requested blob not found`，因此该精确 OpenCV 动态 Batch 组合不支持，不改变其单 Batch 既有结果。半幅图片没有版面标注，此项不是准确率或吞吐证据；TensorRT Batch 仍未验证。详情见[报告](../../eng/models/paddle-document/verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-20261007.md)及[三后端 JSON](../../eng/models/paddle-document/verification/paddle-document-rtdetr-17cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json)。

随后将同一不同区域协议用于官方 RT-DETR-H 3-class 版面模型：ORT CPU 与 OpenVINO CPU 完成 batch=2 全解码、各返回两行且每行 300 个候选；OpenCV DNN 的 Session 创建成功，但 `forward_many` 报 `DS-OCV-8004 / Requested blob not found`。这只标记该精确模型的动态 Batch=2 OpenCV 组合不支持，不改动已有单图结果。裁剪不是版面真值，因此不代表质量或吞吐。详见[3-class 报告](../../eng/models/paddle-document/verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.md)和[机器可读 JSON](../../eng/models/paddle-document/verification/paddle-document-rtdetr-3cls-layout-dynamic-batch-ort-openvino-opencv-20261007.json)。

同日随后对官方 23 类 `PP-DocLayout-L` 做不同输入的 batch=2 三后端验证：`bus.jpg` 上下两个不重叠 `810×540` 区域在 ORT CPU、OpenVINO CPU 均以 `[2,3,640,640]` 和两个 `[2,2]` 几何输入完成 NMS 解码，每行各返回 300 个导出候选，输入和结果摘要均区分两个 batch 行。OpenCV DNN 在 Session 创建后 forward 报 `DS-OCV-8004` / OpenCV 5.0 `Requested blob not found`；仅这一精确动态 Batch 组合不支持，不影响既有单图验证。区域没有版面真值，因此不代表准确率、跨后端数值 parity 或性能；TensorRT Batch 仍未验证。详见[三后端验证报告](../../eng/models/paddle-document/verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.md)和[JSON](../../eng/models/paddle-document/verification/paddle-document-pp-doclayout-l-dynamic-batch-ort-openvino-opencv-20261007.json)。

表格分类器的 TensorRT 动态 Batch 仍未通过 DeploySharp 实测。vendor `trtexec` 可以构建并执行 batch=2 Engine，但该随机输入、GPU-only 运行不包含 DeploySharp Provider、预处理/后处理或数据搬运；DeploySharp 在当前机器则被 TensorRT native bridge 的结构化异常 `3228369022` 阻断。故这只定位为 bridge/runtime 初始化待查，不能宣称动态 Batch 已支持，也不改动既有单 Batch 精确模型状态。详情见[独立探针报告](../../eng/models/paddle-document/verification/paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.md)。

## 在代码中创建 Profile

文档模型使用 `PaddleDocumentProfiles` 创建后端无关 Profile。分类可以直接复用分类 Profile；通用 `CreateRegionDetection` 只适用于调用方确认了 `[batch,candidates,fields]` 原始候选张量的导出。它现在可以通过可选的 `maximumBatch` 暴露真正的动态 Batch（默认仍是 `1`，以保持旧调用兼容），Decoder 会按输入顺序逐行执行坐标还原和 NMS；只有 ONNX 图的输入 batch 轴确实是动态时才应设置大于 `1`。当前官方 layout/cell 导出已经将 NMS 写入图中，应使用 `CreatePaddleNmsRegions`；模型输出名、标签、预处理和输出坐标必须以实际 ONNX 图为准：

```csharp
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

using var registry = new BackendRegistry();
registry.Register(new OnnxRuntimeBackendProvider());

PaddleDocumentModelDescriptor descriptor = PaddleDocumentModelCatalog.Official[0];
PaddleDocumentProfile orientation = PaddleDocumentProfiles.CreateClassification(
    descriptor,
    PaddleDocumentProfiles.DocumentOrientationLabels,
    VisualTaskId.DocumentOrientation);

PaddleDocumentModelDescriptor layout = PaddleDocumentModelCatalog.Official[2];
PaddleDocumentProfile regions = PaddleDocumentProfiles.CreateRegionDetection(
    layout,
    PaddleDocumentProfiles.Layout17Labels,
    VisualTaskId.LayoutDetection);

// Converted PP-DocLayout/PicoDet/RT-DETR exports use Paddle post-NMS rows.
PaddleDocumentProfile exportedLayout = PaddleDocumentProfiles.CreatePaddleNmsRegions(
    PaddleDocumentModelCatalog.Get("paddle-doc/rt-detr-h-layout-17cls"),
    PaddleDocumentProfiles.Layout17Labels,
    new VisualSize(640, 640),
    includeGeometryInputs: true);

PaddleDocumentProfile table = PaddleDocumentProfiles.CreateTableStructure(
    PaddleDocumentModelCatalog.Get("paddle-table/slanext-wired"));
// table.VisualProfile.Decoder returns PaddleDocumentTableResult:
// Markup contains the HTML-like sequence and Regions contains source-space cell bounds.

PaddleDocumentProfile uvdoc = PaddleDocumentProfiles.CreateUnwarping(
    PaddleDocumentModelCatalog.Get("paddle-doc/uvdoc"));

// FormulaNet/UniMERNet exports return integer token IDs. On net8+ load the
// official BPE tokenizer from the downloaded Paddle inference.yml (or tokenizer.json)
// so byte-level merges are applied instead of simply concatenating token pieces.
PaddleDocumentFormulaTokenizer tokenizer =
    PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(
        @"E:\Model\PaddleDocument\source\unimernet\UniMERNet_infer\inference.yml");
var formulaSchema = new PaddleDocumentFormulaSchema(
    tokenizer, endTokenId: 2, startTokenId: 0, padTokenId: 1, unknownTokenId: 3);
PaddleDocumentProfile formula = PaddleDocumentProfiles.CreateFormula(
    PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s"), formulaSchema);

// Chart2Table is an autoregressive four-graph bundle rather than a single
// integer-token decoder. Use the official qwen.tiktoken sidecars and the
// dedicated generation session.
var chartTokenizer = new PaddleChart2TableTokenizer(
    @"E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table");
var chartBundle = new PaddleChart2TableOnnxBundle(
    @"E:\Model\PaddleDocument\chart-export\chart-vision.onnx",
    @"E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923\chart-token-embedding-dynamic.onnx",
    @"E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923\chart-text-prefill-full-epsilon.onnx",
    @"E:\Model\PaddleDocument\chart-export\text-onnx-verify-20260923\chart-text-decoder-dynamic-past-full-epsilon.onnx",
    OnnxRuntimeBackendProvider.BackendId);
using var chartSession = new PaddleChart2TableOnnxSession(
    registry, chartBundle,
    new BackendRequest(BackendCapabilities.TensorInference,
        OnnxRuntimeBackendProvider.BackendId));
var chartInput = new OpenCvPaddleChart2TableInputFactory()
    .CreateFromFile(@"E:\Model\PaddleDocument\validation\chart_parsing_02.png");
using (chartInput)
{
    PaddleChart2TableGenerationResult result =
        await chartSession.GenerateAsync(chartInput, chartTokenizer, maximumNewTokens: 128);
}
```

四图 Session 负责将官方 256 个视觉特征替换到文本 Embedding、固定 Prompt Prefill、greedy token 选择和动态 KV Decode。使用 OpenVINO 或 TensorRT 时，只需将 Bundle 中四个 `ModelArtifact` 的格式/路径换成该后端的工件；TensorRT 当前的验证 plan 采用 FP16 Vision/Embedding、FP32 文本两图。可从 `models-paddleocr` 取到四个 ONNX 文件和 tokenizer sidecars；旧 `CreateChartParsing` 仍用于单图整数 token 导出合同，不代表自回归 Bundle。

布局、表格单元格和印章的 Paddle 后置 NMS Profile 默认要求 `[rows,6] + bbox_num`。对于导出为真正 `[batch,rows,6]`、没有 `bbox_num` 的模型，可以将 `countOutputName` 设为 `null` 并设置 `maximumBatch > 1`；Profile 会切换到三维输出契约，Decoder 为每个 batch 行使用全部候选。若输出是扁平 `[rows,6]`，仍必须提供 `bbox_num`，避免把不同图片的候选混在一起。NMS 现在也会拒绝非有限类别/坐标，防止异常张量污染源图坐标。

## 统一端到端页面编排

PP-Structure 的模型组合依赖具体业务：有的页面只需要方向和 OCR，有的页面还需要矫正、版面、表格、公式或图表。为避免每个应用重复编写阶段顺序，Visual 包现在提供 `PaddleDocumentPipeline`。它是一个后端无关的编排层，负责：

- 按确定顺序运行调用方提供的阶段适配器：方向 → 矫正 → 版面 → 表格分类/单元格/结构 → 公式 → 印章 → 图表；
- 将页面对象、页码、源尺寸和前序结果传给后续阶段；
- 校验阶段返回的模块类型与页码来源，记录每个阶段和整页墙钟时间；
- 支持同步/异步调用、取消、按模块获取结果，并保留阶段结果的独立后端/模型元数据。

### 可复现的方向 → 版面案例

仓库中的 `PaddleDocumentPipelineSemanticIntegrationTests` 是一个完整的真实案例：它从 `E:\Data\image\bus.jpg` 准备页面，使用 `pp-lcnet-x1-0-doc-ori` 进行方向分类，再把同一页交给 `pp-doclayout-l` 做 23 类版面区域检测。两个阶段都通过 ONNX Runtime CPU Session，版面阶段读取前一阶段的方向结果，最终输出页码、区域列表和分段计时。

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --verbosity quiet `
  --filter FullyQualifiedName~PaddleDocumentPipelineSemanticIntegrationTests
```

该案例验证的是可运行的方向→版面任务链和结果来源，不代表版面区域已经与人工标注集完成精度评测，也不自动包含版面区域内的 OCR、表格或公式任务。后续阶段应从 `context.GetRegions()` 显式接收区域并创建局部输入，再将局部坐标映射回页面坐标。

另外，`PaddleDocumentFunsdLayoutEvidenceTests` 默认仍在三个真实 FUNSD 表单页上运行；设置 `DEPLOYSHARP_PADDLE_DOCUMENT_FUNSD_LAYOUT_MAX_PAGES=50` 后，可对本地 FUNSD 测试集的 50 页执行同一合同。2026-10-04 的 ORT/OpenVINO 扩展均完成 `50/50` 页、共 1332 个区域，检测数量和标签逐页一致，坐标/尺寸/score 最大绝对差约 `4.6e-4`；两后端只有 `5/50` 页天然按左上顺序输出，因此仍需显式 reading-order policy。该记录只证明版面模型的跨后端执行和 typed region 合同；FUNSD 是词/实体标注，没有对齐的 23 类版面真值，因此不被解释为版面准确率或阅读顺序通过。详见 [`funsd-doclayout-l-50page-multibackend-20261004.md`](../../eng/models/paddle-document/verification/funsd-doclayout-l-50page-multibackend-20261004.md) 及两份原始 JSON。

如果阶段已经由 `VisualPipeline` 管理，Visual 包还提供
`PaddleDocumentVisualPipelineStage` 适配器。它把“准备 `PreparedVisualInput` → 调用所选后端 →
把 `VisualInferenceResult` 映射为文档结果”固定成一个可复用阶段，同时遵守输入所有权：`Owned`
输入在阶段结束后释放，`Borrowed` 输入仍由调用方持有。这样应用只需要为每个模块提供预处理和结果映射，
不必重新实现页面顺序、取消和页码来源校验。

编排层不猜测模型输入、不自动裁剪、不隐式选择后端，也不把“有模型资产”当成“后端可用”。每个阶段仍应由应用使用对应的 `VisualPipeline`、OpenCV/TensorRT 适配器或其它后端实现；尚未完成实测的模块会自然停留在其现有验证状态：

```csharp
var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[]
{
    new PaddleDocumentPipelineStage(
        PaddleDocumentModule.DocumentOrientation,
        (context, cancellationToken) => RunOrientationAsync(context.Page, cancellationToken)),
    new PaddleDocumentPipelineStage(
        PaddleDocumentModule.LayoutDetection,
        (context, cancellationToken) => RunLayoutAsync(context.Page, context.Results, cancellationToken)),
    new PaddleDocumentPipelineStage(
        PaddleDocumentModule.TableStructureRecognition,
        (context, cancellationToken) => RunTableAsync(context.Page, context.GetRegions(), cancellationToken))
});

PaddleDocumentPipelineResult pageResult = await pipeline.RunAsync(
    new PaddleDocumentPage(pageImage, new VisualSize(width, height), pageIndex: 0), cancellationToken);
PaddleDocumentTableResult table =
    pageResult.GetRequired<PaddleDocumentTableResult>(PaddleDocumentModule.TableStructureRecognition);
```

阶段适配器可以复用同一个已配置的 Session 池，也可以按模块使用不同后端；页面源对象由调用方拥有，Pipeline 不会擅自释放它。这个编排层已经有顺序、取消、页码来源和结果一致性合同测试，但它不替代 PP-Structure 官方的完整产线，也不改变验证矩阵中的后端状态。

对已有 `VisualPipeline` 的模块适配可以直接使用：

```csharp
var orientationStage = PaddleDocumentVisualPipelineStage.FromSynchronousPreparation(
    PaddleDocumentModule.DocumentOrientation,
    orientationVisualPipeline,
    (context, cancellationToken) => PrepareOrientationInput(context.Page, cancellationToken),
    (context, inference) => MapOrientationResult(context.Page, inference));

var documentPipeline = new PaddleDocumentPipeline(new[] { orientationStage, layoutStage, tableStage });
PaddleDocumentPipelineResult result = await documentPipeline.RunAsync(page, cancellationToken);
```

这是真正可执行的多阶段组合入口，但仍不会替调用方猜测模型输出或把尚未实测的模块标记为“已支持”。
`PaddleDocumentPipelineTests.VisualPipelineStageBridgesPreparedInputAndCanonicalDocumentResult` 覆盖了
适配器的输入准备、推理、结果映射、页码来源和所有权路径；真实模型证据仍按后端矩阵单独记录。

### 页面结果导出

Pipeline 结果现在可以直接导出为 JSON 或 Markdown。导出内容保留页码、源尺寸、阶段计时、模型/后端、输入 SHA-256、区域坐标、警告，以及方向、矫正、表格、公式、图表和印章等已知任务载荷；调用方的原始图像对象不会被序列化。

```csharp
PaddleDocumentPipelineResult result = await documentPipeline.RunAsync(page, cancellationToken);
string json = PaddleDocumentPipelineExport.ToJson(result);
string markdown = PaddleDocumentPipelineExport.ToMarkdown(result);
await File.WriteAllTextAsync("page.json", json, cancellationToken);
await File.WriteAllTextAsync("page.md", markdown, cancellationToken);
```

JSON 适合保存机器可读结果和后续多页聚合，Markdown 适合调试报告和人工复核。导出器不会补齐缺失阶段，也不会把通用 stage runner 描述成自动可用的 PP-StructureV3 产品流水线。

多页输入保持调用顺序和页码来源，阶段按页顺序执行：

```csharp
IReadOnlyList<PaddleDocumentPipelineResult> pages = await documentPipeline.RunManyAsync(
    decodedPages, cancellationToken);
string bookJson = PaddleDocumentPipelineExport.ToJson(pages);
string bookMarkdown = PaddleDocumentPipelineExport.ToMarkdown(pages);
```

`RunManyAsync` 不并行复用 stage；需要并发时应由应用创建独立的 pipeline/session 通道，再自行合并结果，以免把非线程安全的视觉会话隐式共享。

如果已经确认阶段适配器及其底层 Session 支持并发调用，也可以使用显式有界的 `RunManyConcurrentAsync`。该方法只控制页面级并发，不会把多个页面拼成模型 Batch，并且仍按输入顺序返回结果：

```csharp
IReadOnlyList<PaddleDocumentPipelineResult> pages =
    await documentPipeline.RunManyConcurrentAsync(
        decodedPages,
        maxDegreeOfParallelism: 2,
        cancellationToken);
```

`maxDegreeOfParallelism` 是硬上限；调用方必须确认 stage 自身及其 native Session 支持并发。若不能确认，应在该方法外创建多个独立 Pipeline 并自行分片调度。默认串行 `RunManyAsync` 仍适合复用有状态或非线程安全的阶段。

外部基准入口将页面并发和每个视觉 Session 的内部并发分别暴露为 `DEPLOYSHARP_PADDLE_DOCUMENT_PAGE_CONCURRENCY` 与 `DEPLOYSHARP_PADDLE_DOCUMENT_SESSION_CONCURRENCY`，默认值都是 `2`，并会原样写入证据 JSON。两者必须联合调优；OpenVINO CPU 的 5/50 记录显示，在当前主机上固定为 `2/2` 时页面并发 P50 反而比串行慢 `12.0%`，不能把页面并发当成通用加速开关。

四组 `session/page` 组合的同协议对照见[OpenVINO 并发调优报告](../../eng/models/paddle-document/verification/paddle-document-multipage-openvino-concurrency-tuning-20261005.md)。本机这次小样本中 `1/2` 同时降低了 P50/P95，但串行基线在不同进程间有波动，因此仅作为当前设备的候选配置；库和示例不会据此自动改变默认值。

真实模型证据见[两页 ORT CPU 有界并发记录](../../eng/models/paddle-document/verification/paddle-document-multipage-concurrent-ort-20261005.md)：方向→版面两阶段在同一 `bus.jpg` 的两个页对象上运行，串行墙钟为 `882.267 ms`，页面并发上限为 `2` 时墙钟为 `791.9943 ms`。进一步的[5 次预热/50 次测量记录](../../eng/models/paddle-document/verification/paddle-document-multipage-concurrent-ort-benchmark-20261005.md)给出串行 P50/P95 `841.0031/918.2798 ms`、并发 P50/P95 `708.4469/873.7787 ms`；[OpenVINO CPU 单次记录](../../eng/models/paddle-document/verification/paddle-document-multipage-concurrent-openvino-20261005.md)给出串行/并发墙钟 `898.9205/644.0613 ms`，[OpenVINO 5/50 记录](../../eng/models/paddle-document/verification/paddle-document-multipage-concurrent-openvino-benchmark-20261005.md)给出串行 P50/P95 `563.6314/647.9142 ms`、并发 P50/P95 `631.2580/664.9269 ms`，说明该主机上页面并发反而变慢。四组 `session/page` 组合的对照见[OpenVINO 并发调优报告](../../eng/models/paddle-document/verification/paddle-document-multipage-openvino-concurrency-tuning-20261005.md)，本机 `1/2` 仅作为候选配置。 [TensorRT CUDA 5/50 记录](../../eng/models/paddle-document/verification/paddle-document-tensorrt-multipage-benchmark-20261005.md)在相同双页合同下记录 P50/P95 `103.2937/109.6768 ms` 串行、`66.7278/69.0992 ms` 并发。结果只证明页面顺序、源 SHA、区域数量和当前主机上的执行行为；它们不是模型 Batch、质量结论或跨设备性能承诺。

### 版面区域 → OCR

版面检测结果可以交给 `PaddleDocumentRegionTextStage`。该适配器要求前序阶段产生 `LayoutDetection` 区域，然后按页面区域顺序调用应用提供的 OCR 委托；委托负责从原图按 `region.Bounds` 裁剪、调用已有 `OcrPipeline`，再返回一条 `PaddleDocumentTextItem`。库会检查区域索引、页码和输入 SHA，并把每条文本与页面坐标绑定。

```csharp
PaddleDocumentModelDescriptor textModel = new PaddleDocumentModelDescriptor(
    "application/ppocr-region-rec", "caller-selected-recognizer",
    PaddleDocumentModule.TextRecognition, "https://github.com/PaddlePaddle/PaddleOCR",
    "caller-owned-onnx");

var textStage = new PaddleDocumentRegionTextStage(
    textModel,
    backend: "onnxruntime-cpu",
    inputSha256: context => sourceSha256,
    recognize: async (context, region, token) =>
    {
        // Prepare a crop from context.Page.Source using region.Bounds,
        // run the caller-owned OcrPipeline, then map its text back to page coordinates.
        return await RecognizeRegionAsync(context.Page.Source, region, token);
    });

var pipeline = new PaddleDocumentPipeline(new IPaddleDocumentPipelineStage[]
{
    layoutStage,
    textStage
});
```

该 stage 不假定裁剪库、颜色顺序、归一化或模型后端，因此不会把四边形裁剪误报为曲线展开，也不会把“区域有文本结果”误报成版面或 OCR 精度通过。图像裁剪和 OCR 结果的真实证据仍应在具体后端测试中记录。

对于 Windows/OpenCV，`OpenCvPaddleDocumentRegionOcrStage` 已提供真实图像裁剪案例：它只解码和准备页面一次，按版面区域创建识别请求，直接调用现有 `OcrPipeline.RecognizeOnlyAsync`，不会在每个区域 crop 内重复执行 DET。输出会保留父区域页面坐标和识别文本。`OcrPipeline` 的识别器-only 合同测试还验证了 detector session 调用次数为 0。

```csharp
var regionOcrStage = new OpenCvPaddleDocumentRegionOcrStage(
    ocrPipeline,
    page => OpenCvImageSource.FromFile((string)page.Source),
    detectionOptions,
    new PaddleDocumentModelDescriptor(
        "application/ppocr-region-rec", "caller-selected-recognizer",
        PaddleDocumentModule.TextRecognition, "https://github.com/PaddlePaddle/PaddleOCR",
        "caller-owned-onnx"));
```

多页综合结果可以把同一组阶段按页顺序运行后一次导出：

```csharp
IReadOnlyList<PaddleDocumentPipelineResult> pages = await pipeline.RunManyAsync(decodedPages, cancellationToken);
await File.WriteAllTextAsync("document.json", PaddleDocumentPipelineExport.ToJson(pages), cancellationToken);
await File.WriteAllTextAsync("document.md", PaddleDocumentPipelineExport.ToMarkdown(pages), cancellationToken);
```

`PaddleDocumentPipelineTests.OrderedMultiPagePipelineExportsEachPageWithModuleProvenance` 覆盖方向→公式依赖、多页页码顺序和 JSON/Markdown 导出；`ConcurrentMultiPagePipelinePreservesOrderAndBoundsStageConcurrency` 覆盖有界并发的输入顺序、并发上限和页码来源，取消合同另有测试覆盖。真实模型综合案例仍需在目标设备上按模型资产和后端重新运行。

### 表格、公式、印章和图表任务链

复杂任务使用 `PaddleDocumentDependentStage` 声明前置结果。下面的结构表达了推荐依赖关系；每个 handler 内部可以使用 ORT、OpenVINO、OpenCV 或 TensorRT 的 `VisualPipeline`：

```csharp
var tableClassify = new PaddleDocumentDependentStage(
    PaddleDocumentModule.TableClassification,
    new[] { PaddleDocumentModule.LayoutDetection },
    (context, token) => RunTableClassificationAsync(context, token));

var cellDetect = new PaddleDocumentDependentStage(
    PaddleDocumentModule.TableCellDetection,
    new[] { PaddleDocumentModule.TableClassification },
    (context, token) => RunCellDetectionAsync(context, token));

var tableStructure = new PaddleDocumentDependentStage(
    PaddleDocumentModule.TableStructureRecognition,
    new[] { PaddleDocumentModule.TableCellDetection },
    (context, token) => RunSlaNextAndBuildHtmlAsync(context, token));

var formula = new PaddleDocumentDependentStage(
    PaddleDocumentModule.FormulaRecognition,
    new[] { PaddleDocumentModule.LayoutDetection },
    (context, token) => RunFormulaOnFormulaRegionsAsync(context, token));

var seal = new PaddleDocumentDependentStage(
    PaddleDocumentModule.SealTextDetection,
    new[] { PaddleDocumentModule.LayoutDetection },
    (context, token) => RunSealDetectionAsync(context, token));

var chart = new PaddleDocumentDependentStage(
    PaddleDocumentModule.ChartParsing,
    new[] { PaddleDocumentModule.LayoutDetection },
    (context, token) => RunChart2TableAsync(context, token));
```

`PaddleDocumentTableResult.Markup` 是表格 HTML/结构化标记承载字段；公式使用 `PaddleDocumentFormulaResult.Latex`，印章使用 `PaddleDocumentSealResult` 的掩码和区域，Chart2Table 使用 `PaddleDocumentChartResult.StructuredData`。这些阶段只负责合同和依赖检查，尚未验证的精确模型/后端组合仍必须在矩阵中保持 `△`。

仓库中的 `PaddleDocumentTablePipelineIntegrationTests` 已把表格链跑成一个真实 ORT CPU 案例：`table_recognition.jpg` → `pp-lcnet-x1-0-table-cls`（`wired_table`）→ `rt-detr-l-wired-cell-det`（300 个候选，阈值 0）→ `slanext-wired`（24 tokens、13 个单元格、HTML markup）。复现命令：

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --verbosity quiet `
  --filter FullyQualifiedName~PaddleDocumentTablePipelineIntegrationTests
```

这条案例证明了阶段依赖、真实模型执行和 HTML 结果传递；300 个候选是阈值 0 的执行观察，不是单元格检测准确率，HTML 结构也不代表数据集级表格识别指标。

Chart2Table 也可挂接到同一 Pipeline。`PaddleDocumentChartPipelineIntegrationTests` 使用四图 ONNX Bundle、官方 tokenizer 和 `chart_parsing_02.png`，在 ORT CPU 上把生成结果封装为 `PaddleDocumentChartResult.StructuredData`。测试要求显式提供 Bundle、模型源目录和导出目录：

```powershell
$env:DEPLOYSHARP_CHART2TABLE_PIPELINE_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_CHART2TABLE_MODEL_ROOT = 'E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table'
$env:DEPLOYSHARP_CHART2TABLE_EXPORT_ROOT = 'E:\Model\PaddleDocument\chart2table-export-repro-20260923'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --verbosity quiet `
  --filter FullyQualifiedName~PaddleDocumentChartPipelineIntegrationTests
```

公式和印章已有独立 ORT/Decoder 真实案例；它们接入页面 Pipeline 时分别使用 `PaddleDocumentDependentStage` 声明版面区域依赖，并将 `Latex` 或掩码区域写入页面导出。当前这些组合仍是代表样本验证，不是数据集级精度结论。2026-10-04 又对六个公式 ONNX 做了 Loop-body 参数重命名兼容实验：六个派生图均通过 `onnx.checker`，但 Plus-S/FormulaNet-S 虽进入 OpenVINO 执行却没有通过 token parity，Plus-M/Plus-L/FormulaNet-L 在 Loop reshape 处失败，UniMERNet 仍 native crash；六个精确组合继续标记为当前 OpenVINO 不支持。详见 [`formula-openvino-loop-compat-20261004.json`](../../eng/models/paddle-document/verification/formula-openvino-loop-compat-20261004.json) 与 [`formula-openvino-loop-compat-20261004.md`](../../eng/models/paddle-document/verification/formula-openvino-loop-compat-20261004.md)。

`PaddleDocumentFormulaSealPipelineIntegrationTests` 已验证这两类结果可以挂入页面 Pipeline：公式使用 `PP-FormulaNet_plus-S` 和官方公式图片，输出带页码/输入 SHA 的 LaTeX；印章使用 PP-OCRv4 mobile seal 模型和 `demo_1.jpg`，输出带页码/输入 SHA 的掩码尺寸及区域。测试入口：

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --verbosity quiet `
  --filter FullyQualifiedName~PaddleDocumentFormulaSealPipelineIntegrationTests
```

公式案例校验 `\\frac` 和 token 输出，印章案例校验 mask 尺寸；两者仍属于官方代表样本执行合同，不等同于公式识别或印章检测数据集精度。另有 Plus-S 五种受控公式图变体回归：原图/白边/对比度/轻模糊归一化 LaTeX 精确一致，JPEG q45 出现两个符号替换；证据见 [`formula-plus-s-variants-20260929.md`](../../eng/models/paddle-document/verification/formula-plus-s-variants-20260929.md)。

印章 Decoder 另有 `SealDecoderRunsAcrossThreeLocalImagesOnOrtCpu` 多图片执行入口，覆盖 `demo_1/2/3.jpg`，只记录掩码尺寸、区域数和分数范围；由于这些图片没有印章人工标注，该报告不被解释为召回率或精度评测。`FormulaModelsAndSealModelsProduceMachineReadableOrtEvidence` 进一步将六个公式模型和 mobile/server 两个印章模型的结果写入测试 JSON，记录模型/图片 SHA、token/LaTeX、mask 尺寸和区域数。公式当前有六个模型在官方公式样本上完成 token/LaTeX 语义回归，仍缺多公式人工真值集。当前本机数据根目录的可用性审计见 [`formula-data-availability-audit-20261005.md`](../../eng/models/paddle-document/verification/formula-data-availability-audit-20261005.md) 和 [JSON 清单](../../eng/models/paddle-document/verification/formula-data-availability-audit-20261005.json)：只有一张官方公式图片，没有可准入的多公式标注文件，因此不能计算数据集级 CER/WER。

## 状态和验证规则

28 个独立 ONNX Release 资产均有 ORT CPU 图执行及 Decoder 冒烟证据。PP-LCNet、SLANeXt、公式模型另有下方官方示例回归。PP-Chart2Table 的四图 Bundle 和 tokenizer 已放入 `models-paddleocr` Release。ORT CPU、OpenVINO CPU 和 TensorRT CUDA 在官方图表样例均生成完整 2018–2023 表格并正常到 EOS；优化 TensorRT device path 在该样例最好 10.12 秒，原 host-KV 严格 FP32 通用路径为 83.42 秒。额外四个 ChartQA human 图表均生成完整表格并核对通过 8 个关联 QA 标签。该小样本不是数据集准确率指标；Chart2Table 的 OpenCV DNN 自回归仍未验证，PP-OCR 核心 OpenCV DNN 证据不代表 PP-Structure 生成模型。

| 后端/模块 | 当前真实证据 | 边界 |
| --- | --- | --- |
| ORT CPU | 22 个非公式工件、6 个公式 token/BPE 解码，方向→版面阶段编排 | 公式结果长度与 tokenizer 无告警不是公式准确率 |
| OpenVINO CPU | 方向、版面、表格分类、wired/wireless 单元格、UVDoc、mobile/server 印章 | 按精确模型记录；未外推其它工件 |
| OpenVINO SLANeXt | wired/wireless 派生 ONNX 均通过，与原始 ORT 输出逐元素对比 | 原始 Release ONNX 仍受循环变量同名问题影响，需先执行下方兼容转换 |
| OpenCV DNN | PP-DocLayout-L、PP-DocLayout-Plus-L、PP-DocBlockLayout、RT-DETR 3/17 类版面、wired/wireless 单元格、两个 PP-LCNet 官方示例以及 PP-OCR v4/v5/v6 七组核心完整流水线 | PP-DocLayout-M/S 与 PicoDet layout 在 OpenCV 5.0 `MatMul` importer 处 `unsupported`；PP-DocLayout-L 不请求被 importer 忽略的常量计数输出；Chart2Table 自回归仍未验证 |
| TensorRT CUDA | PP-LCNet 文档方向、PP-LCNet 表格分类、`pp-doclayout-l` Paddle NMS、mobile/server 印章概率图；均有 ORT 对照和预热后 P50/P95 | TRT 11 bridge + TRT 11.0.0.114-cu12 + CUDA 12.9 + cuDNN 9.22；server 印章已显式关闭 TF32，当前样本逐元素误差通过合同，不外推其它模型 |
| Chart2Table 四图 Bundle | ORT CPU/OpenVINO CPU/TensorRT CUDA：官方样例生成完整 2018–2023 表格；4 张 ChartQA human 图在 ORT/OpenVINO/TensorRT 均到 EOS、精确匹配 golden table，关联 QA 8/8 匹配。ORT/OpenVINO 四图机器可读证据见 `chart2table-ort-multi-image-20260929.json` / `chart2table-openvino-multi-image-20260929.json`；TensorRT device path 的官方样例最好 10.12 s，Decode P50/P95 66.47/73.48 ms | 库 Builder 已构建四图并通过完整生成回归；严格 FP32 Builder 四图运行耗时异常且不是性能基准；OpenCV DNN、跨数据集准确率仍未验证 |

完整状态见[模型后端验证矩阵](../model-backend-verification-matrix.md)。这些测试使用本机模型和真实图片，但尚未完成 PP-Structure 的任务级精度基准或全模型 P50/P95。UVDoc TensorRT 运行记录见 [`uvdoc-tensorrt11-20260924.json`](../../eng/models/paddle-document/verification/uvdoc-tensorrt11-20260924.json)。Chart2Table 四图合同、tokenizer 哈希、端到端结果和每后端阶段耗时见 [Bundle 验证记录](../../eng/models/paddle-document/verification/chart2table-component-validation.json)。

当前 PP-Structure 31 个精确 artifact/backend 单元的 pass/unsupported/unverified 数量见[状态摘要](../../eng/models/paddle-document/verification/paddle-document-status-summary-20260929.md)；其中 OpenVINO CPU 的 12 个版面工件逐项报告见[机器可读矩阵](../../eng/models/paddle-document/verification/paddle-document-openvino-layout-matrix-20260930.json)；这些摘要只汇总逐组合执行证据，不代表数据集级准确率或跨设备兼容性。

### Chart2Table 完整 Bundle 技术验证

本次验证复用了 `E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table\model_state.pdparams` 和官方 `chart_parsing_02.png`，没有重新下载同一官方包。结论分为三个层次：

1. **多后端端到端与额外图表样本**：视觉编码、官方 Tokenizer、图像特征替换、286-token Prefill、`lm_head` greedy 选择和动态 KV Decode 均已覆盖。三 token ORT 阶段与 Paddle 逐 token 一致，最大逐步 logits 误差 `6.77e-5`；ORT CPU、OpenVINO CPU、TensorRT CUDA 完整运行均在 256-token 上限内生成 141 个 token（含 EOS），返回同一张官方六行表。新增四张 ChartQA human 图的 ORT/OpenVINO 回归也均为 4/4 EOS 和精确表格 SHA；详见 [`chart2table-ort-multi-image-20260929.json`](../../eng/models/paddle-document/verification/chart2table-ort-multi-image-20260929.json) 与 [`chart2table-openvino-multi-image-20260929.json`](../../eng/models/paddle-document/verification/chart2table-openvino-multi-image-20260929.json)。运行主机为 Ryzen 7 5800H、Windows 11、.NET 10；官方单图 ORT CPU `39.76 s`，OpenVINO CPU `46.40 s`，优化 TensorRT device path 三次 `12.64/12.12/10.12 s`，最好一次 Decode P50/P95 为 `66.47/73.48 ms`（ORT CPU `94.69/123.92 ms`）；最初 host-KV 严格 FP32 TensorRT 路径为 `83.42 s`。

   随后使用 ChartQA `test_human` 的 4 张独立图表（官方仓库：[vis-nlp/ChartQA](https://github.com/vis-nlp/ChartQA)）进行 ORT、OpenVINO 和 TensorRT 端到端补测。三后端每次均到 EOS，生成表格逐字匹配 golden outputs；TensorRT 结果还从表格值核对了 8 个关联问答标签，8/8 一致。ORT 四图总时延约 `12.65–47.05 s`，OpenVINO 四图约 `11.92–49.08 s`；这些是同一设备单次观察，不是性能基准。样本图像 SHA-256、表格文本和逐项核对记录在 [Bundle 验证 JSON](../../eng/models/paddle-document/verification/chart2table-component-validation.json) 以及新增的 ORT/OpenVINO 多图证据文件中。ChartQA 数据文件带 GPL-3.0 许可，本仓库和 Release 不包含这些图像。TensorRT 严格 FP32 Builder 计划曾出现约 29 分钟的异常耗时，原因仍需 profiling，不能与优化 device path 结果混用。

   ```text
   年份 | 单家五星级旅游饭店年平均营收 (百万元) | 单家五星级旅游饭店年平均利润 (百万元)
   2018 | 104.22 | 9.87
   2019 | 99.11 | 7.47
   2020 | 57.87 | -3.87
   2021 | 68.99 | -2.90
   2022 | 56.29 | -9.48
   2023 | 87.99 | 5.96
   ```
2. **TensorRT 使用边界与优化**：文本 TensorRT plan 保持 FP32 数据类型；FP16 text `lm_head` 图在此 GPU 上返回全零 logits。可选 TF32 tactics 在该官方样例上保持与严格 FP32 完全相同的输出，但尚未进行数据集精度验证。四图 TensorRT plan 已由 DeploySharp `TensorRtOnnxEngineBuilder` 构建；Decode 动态 profile 的空 Engine 问题由测试选择了错误图及 mask 长度不匹配造成，修正后库 Builder 计划通过完整 EOS 回归。`PaddleChart2TableTensorRtDeviceSession` 用两组 CUDA KV 缓冲区轮换，只回读 logits，并减少动态 shape/binding 重复工作。没有锁定 GPU 时钟，计时不是受控基准。
3. **发布与泛化精度边界**：四个派生 ONNX 和 tokenizer sidecars 已作为 `paddle-chart/pp-chart2table` Bundle 放入 `models-paddleocr` Release；OpenCV DNN 仍未验证。官方样例和 4 个 ChartQA human 样本均在 ORT、OpenVINO 和 TensorRT 完成 EOS 与表格输出校验，TensorRT 还记录了四图逐阶段耗时和结构指标，详见 [`chart2table-tensorrt-multi-image-20260930.md`](../../eng/models/paddle-document/verification/chart2table-tensorrt-multi-image-20260930.md)。这些是定性回归，不是全量数据集精度或受控性能基准。该模型目录项保留上游源包的 `conversion-blocked` 状态，不能表示其派生 ONNX Session 不可用。

    12 张 `ChartQA/val` 图像的 ORT 和 TensorRT 结果还可以通过 [对齐报告](../../eng/models/paddle-document/verification/chart2table-extended-quality-backend-alignment-20261005.md) 直接核对。脚本只对已生成的两个 JSON 做同源校验：输入图像 SHA、EOS 原因、结构标志、期望单元格数量均为 `12/12`，两边精确单元格合计均为 `140`；它不重新推理，也不构成全量准确率或受控性能结论。对应的[机器可读 JSON](../../eng/models/paddle-document/verification/chart2table-extended-quality-backend-alignment-20261005.json) 可供 CI 或后续设备结果比较使用。

### Chart2Table 结构化质量指标

生成文本除了整段字符串比较，还可以用 `PaddleChart2TableQualityEvaluator` 记录可解释的结构指标：行数、数据行数、列数、空单元格、Markdown 分隔行、畸形行、列数不一致、逐行匹配数和逐单元格匹配数。比较时会单独保留 `ExactTextMatch`，并对单元格首尾空白和连续空白做确定性归一化；转义的 `\|` 不会被误拆成新列。

```csharp
PaddleChart2TableQualityComparison quality =
    PaddleChart2TableQualityEvaluator.Compare(expectedTable, result.Text);

if (!quality.Actual.IsStructurallyValid)
    throw new InvalidOperationException("Chart2Table output is not a rectangular table.");

Console.WriteLine($"rows={quality.Actual.RowCount};columns={quality.Actual.ColumnCount};" +
    $"rowAccuracy={quality.RowAccuracy:P2};cellAccuracy={quality.CellAccuracy:P2}");
```

这些指标用于区分“生成结束但结构损坏”“行列结构正确但部分单元格错误”和“完全匹配”。它们不替代 ChartQA、PubTables 或业务数据集的标注评测；只有固定数据集、golden 版本和样本来源后，才可以汇总为数据集级质量结果。四图 ORT/OpenVINO/TensorRT 外部回归现在会把同一组结构指标写入 `chart2table-*-multi-image-evidence.json`，仍保持四张精选图的 qualitative boundary。

为扩大任务级证据，新增了[六张官方 ChartQA `val` 图表的有界质量记录](../../eng/models/paddle-document/verification/chart2table-extended-quality-20261002.md)。样本来自固定 upstream revision，CSV 表格转换为期望管道表；ORT CPU 和 OpenVINO CPU 均为 `6/6` EOS，生成文本 SHA `6/6` 一致，复杂多列样本在两个后端暴露相同的列结构边界。该选择累计匹配 `44/116` 个单元格，只用于定位图表类型和结构问题，不能外推为 ChartQA split 准确率；图片和 CSV 保留在本地缓存，不进入仓库或 Release。

2026-10-04 又按同一 revision 扩展了 12 张图的 ORT CPU 任务质量记录：`12/12` EOS、`9/12` 行列维度一致、`3/12` 文本精确匹配，累计单元格匹配 `140/293`（`47.78%`），总耗时 P50/P95 为 `26.866/72.933 s`。这是分组诊断证据，不是 ChartQA split accuracy 或受控性能基准，逐样本 SHA、阶段耗时和结构字段见 [ORT 12 图 JSON](../../eng/models/paddle-document/verification/chart2table-extended-quality-ort-20261004.json)。同一 `1024` token 上限的 OpenVINO 扩展运行超过 13 分钟后停止且未形成报告，六图完整 OpenVINO 记录仍是当前跨后端质量基线。

同一六图选择也完成了 TensorRT CUDA 复测：`6/6` EOS、`5/6` 结构维度一致、单元格匹配 `44/116`，总耗时 `1.18–13.25 s`，Decode P50/P95 `33.32–40.39 / 34.32–48.60 ms`。该结果使用未锁频的本机 RTX 3060 和既有四张 plan，只作为任务质量与阶段计时证据，不是 ChartQA split 准确率或受控性能基准。详见 [TensorRT 六图记录](../../eng/models/paddle-document/verification/chart2table-tensorrt-extended-quality-20261004.md) 及 [机器可读报告](../../eng/models/paddle-document/verification/chart2table-tensorrt-extended-quality-20261004.json)。

同一固定 `ChartQA/val` 选择已扩展为 12 张图并在相同 TensorRT plans 上复测：`12/12` EOS、`9/12` 行列维度一致、单元格匹配 `140/293`（`47.78%`），总耗时 P50/P95 `4,398.95/12,209.76 ms`，Decode P50/P95 范围 `33.37–39.48 / 35.85–48.81 ms`。这仍是有界任务质量和阶段计时诊断，不是 split accuracy、问答分数或受控性能基准。详见 [TensorRT 12 图记录](../../eng/models/paddle-document/verification/chart2table-extended-quality-tensorrt-20261005.md) 及 [机器可读报告](../../eng/models/paddle-document/verification/chart2table-extended-quality-tensorrt-20261005.json)。

文档链接可用性由[最新审计 JSON](../../eng/models/paddle-document/verification/document-link-audit-20261007-classifier-distinct-batch.json)维护；审计脚本检查六份 PP-OCR/PP-Structure 入口文档的相对本地链接，并在 JSON 中记录检查数量与断链数。外部 URL 与运行时生成路径不在此审计范围。

Paddle2ONNX 需要 `--enable_dist_prim_all True`；导出边界还必须使用无状态 rotary 计算并显式恢复 Qwen2 RMSNorm 的 `1e-6` epsilon。它们是转换器兼容性修正，不是对官方权重的修改。关于 Builder 空 Engine，已定位为回归测试选择了 plain Decoder 图（Release 路径使用 epsilon 图），且把 Decoder mask 的 optimum 写为 512（past KV optimum 为 512 时，mask 应为 513）；改用 Release 路径的图并对齐 KV/mask profile 后，`TensorRtOnnxEngineBuilder` 成功构建 51-input Decoder，库 Builder 构建的四张 plan 也通过 EOS 完整表格回归。剩余边界为 OpenCV DNN 自回归流程和更大规模/多风格的数据集精度评测。

### 官方预处理与示例回归

- **文档方向、表格分类**：默认 RGB、短边缩放到 256、中心裁剪 224、ImageNet 归一化。缩放尺寸采用官方 nearest-even 取整。两个登记的 PP-LCNet ONNX 已输出 Softmax 概率，Decoder 直接保留分数；自定义 logits 导出可以显式设置 `scoreMode: ClassificationScoreMode.Logits`。
- **SLANeXt wired/wireless**：默认 BGR，长边缩放到 512，归一化后在右侧/底部补浮点零。`normalizedPaddingValue: 0` 是张量空间的零，不能用黑色像素经 ImageNet 归一化替代。标准字典执行官方 `merge_no_span_structure=true` 规则，删除 `<td>` 并加入 `<td></td>`，避免 token 类别错位。当前 CUDA 图像预处理内核不支持归一化后填充，会明确拒绝；可先用 OpenCV 准备张量，再交给 TensorRT Session。
- **FormulaNet/UniMERNet**：`CreateFormula` 按模型选择 384×384、768×768 或 UniMERNet 的宽 672×高 192。默认 `Preprocessing=null` 表示专用流程，`OpenCvVisualInputFactory.CreateFromFile(path, profile.VisualProfile)` 会自动调用 `PaddleDocumentFormulaInputFactory`，执行去白边、Pillow 双线性短边缩放、bicubic thumbnail、黑色居中填充及 mean `0.7931` / std `0.1738` 的浮点灰度归一化。可向专用 factory 传入已解码 `OpenCvBgrImage` 复用像素；显式传入自定义 `preprocessing` 时采用调用方配置。序列未遇到 EOS 会返回截断警告。

官方示例使用固定 SHA-256 校验：[方向分类](https://github.com/PaddlePaddle/PaddleX/blob/c50f5da858020db473a2285f089bb8c7bbd6afdc/docs/module_usage/tutorials/ocr_modules/doc_img_orientation_classification.en.md)、[表格分类](https://github.com/PaddlePaddle/PaddleX/blob/c50f5da858020db473a2285f089bb8c7bbd6afdc/docs/module_usage/tutorials/ocr_modules/table_classification.en.md)、[公式识别](https://github.com/PaddlePaddle/PaddleX/blob/c50f5da858020db473a2285f089bb8c7bbd6afdc/docs/module_usage/tutorials/ocr_modules/formula_recognition.en.md)。回归结论如下：

| 输入 | 模型和验证范围 | 结果 |
| --- | --- | --- |
| `img_rot180_demo.jpg` | PP-LCNet 方向；ORT/OpenVINO/OpenCV CPU、TensorRT CUDA | 类别 180°；CPU 三后端分数约 0.89236；CUDA 预处理允许 0.005 分数差异 |
| `table_recognition.jpg` | PP-LCNet 表格分类；ORT/OpenVINO/OpenCV CPU、TensorRT CUDA | `wired_table`；CPU 分数约 0.844209，TensorRT 分数约 0.851693，绝对差 0.007485 ≤ 0.01 |
| 同一表格 | SLANeXt wired/wireless；ORT、OpenVINO 派生兼容图 | 24 个结构 token、13 个单元格，含 colspan=4 的标题；原始输出误差 ≤0.001，结构一致 |
| `general_formula_rec_001.png` | 六个公式模型；ORT CPU | token/LaTeX 长度为 Plus-S `197/262`、Plus-M `197/262`、Plus-L `197/262`、FormulaNet-S `213/276`、FormulaNet-L `197/262`、UniMERNet `208/270`；六个模型均到达 EOS 且 warnings=0。Plus-S/M/L 的 LaTeX 忽略排版空白后与官方示例一致；FormulaNet-S/L 和 UniMERNet 在该样例仍有符号或格式差异 |

六模型的模型、Tokenizer、输入、token/LaTeX SHA-256 和完整输出见 [`formula-ort-six-models-20260930.md`](../../eng/models/paddle-document/verification/formula-ort-six-models-20260930.md) 及其 [JSON 证据](../../eng/models/paddle-document/verification/formula-ort-six-models-20260930.json)。该报告是单张官方样例的语义执行合同，不是公式数据集准确率；FormulaNet-S/L 和 UniMERNet 仍需额外人工真值样本。

同一官方公式图还生成了原图、白边、对比度、模糊和 JPEG 五个可追溯变体，并让六个模型全部通过 ORT CPU。Plus-S/M/L 的归一化参考式匹配为各 `4/5`，平均去空白 LaTeX CER 为 `1.18%/0.47%/0.71%`，差异均出现在 JPEG 变体；FormulaNet-S/L、UniMERNet 五个变体均正常结束但与 Plus 参考式不同，平均 CER 为 `7.10%/2.37%/6.51%`。完整模型/变体 SHA、LaTeX、编辑距离和 warning 见 [`formula-six-models-variants-20260930.md`](../../eng/models/paddle-document/verification/formula-six-models-variants-20260930.md) 及其 [JSON](../../eng/models/paddle-document/verification/formula-six-models-variants-20260930.json)。这里的 CER 是单一公式去空白字符串的 Levenshtein 比率，不是自然公式数据集准确率。

2026-10-02 在当前工作树按同一固定输入重跑六模型×五变体，两个测试方法 `2/2` 通过，30 行输出与已提交报告的模型/变体、LaTeX SHA、归一化编辑距离和 EOS 状态逐行一致。这只证明解码器回归可复现，不改变多公式真值集或 OpenVINO 尚未完成的状态。

2026-10-04 的 OpenVINO 兼容图实验进一步确认：结构合法不等于可用后端。Plus-S 的派生图只返回 3 个 token（ORT 参考 197），FormulaNet-S 返回 1023 个 token并带 `missing-eos`（ORT 参考 213），其余四个模型分别在 Loop reshape 或 native importer 阶段失败。因此不能把 alpha-renamed 图上传为 OpenVINO 资产；后续必须先修复循环状态形状并通过 token 级 parity，再重新进入发布矩阵。

公式输入还与固定版本的 PaddleX 原始 processor 逐元素对比。三种尺寸平均绝对误差分别约 `1.54e-7`、`7.70e-8`、`2.10e-6`；最大差异约 `0.022564`（相当于归一化前一个灰度级），来自 Pillow 整数滤波取整。此证据针对当前示例，不代表所有输入逐位相同。

针对 realFormula 中 FormulaNet-L 唯一未到 EOS 的 `211110912-2.png`，又分别使用 DeploySharp 和 PaddleX 官方预处理张量复跑 ONNX，并让官方 PaddleX predictor 在相同张量上对照。两份 Float32 张量有 `3,433/589,824` 个元素不同（最大差 `0.022564`、平均差 `4.01e-8`），但 ORT 在两种张量下产生完全相同的 `1,025` 个 raw token（BOS + 1,024 个生成位置），都没有 EOS；官方 Paddle predictor 与 ORT 在每种相同输入上也逐 token 完全一致。因此，该单样本的 EOS 缺失既不是这处预处理数值差导致，也不是 DeploySharp Decoder 丢弃 EOS。官方 `inference.yml` 的 `max_seq_len=1024` 位于预处理标签编码配置；当前证据不能证明它就是推理图的因果停止上限。Decoder 的 `4,096` 只是安全校验上限，不能据此要求模型生成 4,096 token。保留 `missing-eos:sequence-may-be-truncated`，不人为补 EOS；具体 hashes、形状和复现方式见 [EOS/预处理敏感性报告](../../eng/models/paddle-document/verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.md) 与 [JSON](../../eng/models/paddle-document/verification/formula-formulanet-l-eos-preprocessing-sensitivity-20261007.json)。这只解释一个样本，不能关闭公式准确率或其他后端质量门。

```powershell
& .\eng\models\paddle-document\scripts\Acquire-PaddleDocumentValidation.ps1
python eng/models/paddle-document/scripts/Generate-FormulaReference.py `
  --image E:\Model\PaddleDocument\validation\general_formula_rec_001.png `
  --output E:\Model\PaddleDocument\validation
$env:DEPLOYSHARP_PADDLE_DOCUMENT_ACCURACY = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --filter 'FullyQualifiedName~PaddleDocumentOfficialExampleTests|FullyQualifiedName~PaddleDocumentFormulaInputTests|FullyQualifiedName~FormulaExportsDecodeWithOfficialTokenizerOnRealOrtCpu'
```

参考生成器依赖 NumPy、Pillow 和 opencv-python-headless，并校验上游 processor 的提交及文件哈希。分类测试按 5 次预热、50 次测量输出 JSON（设备、源码基准、模型/图片哈希、原始样本、P50/P95），结果见[设备性能实测](device-performance-benchmarks.md)。这些分类耗时不是完整 PP-Structure 页面耗时。

### 版面模型的标签、归一化与坐标

当前 Release 的 PP-DocLayout-L/M/S 使用 23 类标签；不能传入 PicoDet/RT-DETR 的 17 类标签，否则后六类会被丢弃。PP-DocLayout-plus 使用独立的 20 类顺序。

Paddle NMS Profile 的默认预处理依据已登记模型选择：DETR 家族（PP-DocLayout-L/plus、DocBlockLayout、RT-DETR 版面和单元格）使用 RGB、像素除以 255；PicoDet 和 PP-DocLayout-M/S 使用 ImageNet mean/std。这些官方配置均采用直接 Resize、Cubic 插值。自定义模型或不同导出应显式传入 `preprocessing` 覆盖。

`CreateGeometryInputs()` 生成模型坐标系的 `im_shape=[H,W]` 与 `scale_factor=[1,1]`，由 Decoder 使用每张图的 Transform 统一还原源坐标。它只分配少量几何值，不复制图像张量。不要将模型内的反缩放和 Decoder 的反变换重复执行，也不要把示例图片尺寸写死成所有输入的尺寸。生产代码建议使用 `OpenCvPaddleDocumentPreprocessing`：它先准备图像张量，再按实际模型画布自动绑定几何辅助输入，避免调用方手工拼接不匹配的原图尺寸。

```csharp
var profile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
    PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
    PaddleDocumentProfiles.Layout23Labels,
    new VisualSize(640, 640),
    includeGeometryInputs: true,
    scoreThreshold: 0.5f);

using var input = OpenCvPaddleDocumentPreprocessing.CreateFromFile(
    new OpenCvVisualInputFactory(), imagePath, profile);
// 将 input 交给使用该 Profile 创建的 VisualPipeline。
```

如果已经有 `PreparedVisualInput`，可以调用
`OpenCvPaddleDocumentPreprocessing.RebindWithGeometryInputs(input, profile)`；该操作复用原有图像张量，只新增几何输入。`PaddleDocumentProfile.CreateGeometryInputs(PreparedVisualInput)` 会检查输入是否已释放以及模型画布是否匹配，`CreateGeometryInputs(int)` 仍保留为兼容性的纯值生成器。

NMS Decoder 支持 Int32/Int64 计数、二维紧凑输出和三维带 Batch 维的输出。二维批输出按 `bbox_num` 的累积偏移拆分，因此每张图的检测数量可以不同；无效计数会明确报错。OpenCV DNN 单 batch 可设置 `countOutputName: null`，但须确认工件返回的每一行均为候选或具有无效标记，不能将任意含未标记 padding 的输出当作此形式。

### OpenCV DNN PP-DocLayout-L 复现

兼容层只移除推理模式的 `BatchNormalization.training_mode=0`，保留训练语义；对 Identity shape 边界与空整数 Reshape 掩码补充 Int32 类型，不改动原始 ONNX 文件。

本机 `bus.jpg` 实际尺寸为 810×1080。修正标签和预处理后，在阈值 0 的合同 smoke 中得到 300 个候选；阈值 0.5 时有 1 个区域。OpenCV 与 ORT 使用同一个准备好的输入张量，比较高于阈值的区域数量、类别、分数（误差 ≤0.001）和源坐标（误差 ≤0.5 像素）。这是一张图片的后端一致性证据。

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENCV_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentOpenCvSemanticIntegrationTests
```

需要能够加载 JYPPX OpenCV native runtime。PP-OCR 核心流水线的 OpenCV DNN 诊断已覆盖 v4 mobile/server、v5 mobile/server、v6 tiny/small/medium 七组；同一 `demo_1.jpg` 均返回 16 个区域并产生识别文本。v5 mobile 另有同张量 ORT 对照：输出形状均为 `[1,1,512,512]`，原始概率图 max/mean 绝对差 `4.2915344e-5` / `8.6187186e-8`，逐区域文本、坐标和分数通过容差。七组的模型/输入 SHA、区域数、P50/P95 和结果指纹见 [`paddleocr-core-opencv-20260924.json`](../../eng/models/paddle-ocr/verification/paddleocr-core-opencv-20260924.json)。这些是单图短诊断，不能替代多图准确率或正式锁频性能基准；PP-Structure 生成模型和 Chart2Table 自回归仍按矩阵单独记录。

### OpenVINO SLANeXt 兼容转换

原始图的 Loop 形参与外层常量同名，当前 OpenVINO importer 报出循环体参数数量 1/11 不匹配。该问题可以用局部变量重命名处理，无需展开循环、修改权重或改变停止条件。兼容脚本生成两个新文件，运行 ONNX checker，并输出源文件与派生文件 SHA-256，同时生成 [`slanext-openvino-compatibility.json`](../../eng/models/paddle-document/verification/slanext-openvino-compatibility.json)：

```powershell
# 在仓库根目录执行；Python 环境需要 onnx，建议复用模型转换环境。
& .\eng\models\paddle-document\scripts\Build-PaddleDocumentOpenVinoCompatibility.ps1 `
  -ModelRoot E:\Model\PaddleDocument\onnx `
  -OutputRoot E:\Model\PaddleDocument\onnx-normalized `
  -Python E:\Model\PaddleDocument\paddle3\python.exe `
  -ManifestPath E:\Model\PaddleDocument\onnx-normalized\slanext-openvino-compatibility.json

$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_NORMALIZED_ROOT = `
  (Resolve-Path E:\Model\PaddleDocument\onnx-normalized).Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~AlphaRenamedTableGraphMatchesOriginalOrtOutputs
```

| 派生工件 | SHA-256 |
| --- | --- |
| wired | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` |
| wireless | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` |

两个工件均通过 OpenVINO 对原始 ORT 的输出比较，浮点逐值容差 0.001，并执行表格 Decoder。兼容工件有独立兼容 ID、文件名和 SHA，不能沿用原始 Release 的 SHA；创建 `ModelArtifact` 时使用实际派生路径和哈希。发布策略是为 OpenVINO 单独上传 `slanext-wired-openvino-compat.onnx` 和 `slanext-wireless-openvino-compat.onnx`，保留原始 Release 图并由用户显式选择兼容资产；在这些资产上传前，清单状态为 `planned-separate-assets`，用户需本地生成。该输入不是表格标注集，HTML/单元格准确率仍需真实表格验证。

### 弧形文本的能力边界

当前 DeploySharp OCR 几何合同只接受源图矩形、简单多边形和显式四边形透视裁剪。它没有曲线控制点、基线采样或弧形展开器，因此弧形文本明确不支持，也不会被当作普通四边形静默处理。需要识别弧形文本时，应用层必须先用自己的曲线采样/展开算法生成可追溯的局部图像和多边形来源，再调用识别器-only 接口；未提供该展开结果时应返回/记录不支持。普通四边形仍可使用现有透视裁剪，但不得称为弧形文本矫正。

### TensorRT PP-Structure 复现

本机 TensorRT 已安装在 `D:\Program Files`。DLL 加载失败需检查运行时根目录、bridge 编译的 TensorRT 主版本以及 CUDA/cuDNN 搜索路径，不能仅凭失败推断“没有安装”。

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_API = '11'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_86' # 按实际 GPU 修改
$env:JYPPX_NATIVE_BRIDGE_PATH = 'E:\GitSpace\DeploySharp-V2.0\DeploySharp\artifacts\local-model-benchmarks\tensorrt11-bridge\runtimes\win-x64\native\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_TENSORRT_ROOT\lib;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~PaddleDocumentTensorRtExternalIntegrationTests
```

也可以直接使用仓库脚本自动扫描 `D:\Program Files`、选择 TensorRT 11 安装、匹配已登记的 TRT11 bridge，并在当前 PowerShell 进程内设置 PATH（不会修改系统环境变量）：

```powershell
& .\eng\models\paddle-document\scripts\Invoke-PaddleDocumentTensorRtTest.ps1
```

脚本在找不到 `trtexec.exe`、CUDA、cuDNN 或对应 API bridge 时会明确失败；仅有 `nvinfer.dll` 并不等于存在 DeploySharp bridge。需要 TensorRT 8/10 时必须显式传入与该 API 线匹配的 `-TensorRtRoot` 和 `-BridgePath`。

脚本默认对版面、表格分类和 mobile/server 印章都执行 5 次预热、50 次计时；复现实验时可通过 `-LayoutWarmup/-LayoutIterations`、`-TableWarmup/-TableIterations`、`-SealWarmup/-SealIterations` 和 `-ServerSealWarmup/-ServerSealIterations` 显式调整，文档中的公开数字使用默认的 5/50 协议。

如果把同一 PP-Structure ONNX 合同编译成 TensorRT Engine，使用 `ForArtifactFormat("tensorrt-engine")` 创建后端专用 Profile 副本，再通过该 Profile 创建工件。原始 Profile 不会被修改，因此 ONNX、OpenVINO 或其它调用可以继续复用原对象：

```csharp
PaddleDocumentProfile onnxProfile = PaddleDocumentProfiles.CreatePaddleNmsRegions(
    PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l"),
    PaddleDocumentProfiles.Layout23Labels,
    new VisualSize(640, 640),
    includeGeometryInputs: true);

PaddleDocumentProfile engineProfile = onnxProfile.ForArtifactFormat("tensorrt-engine");
ModelArtifact engine = engineProfile.CreateArtifact(
    @"C:\\models\\pp-doclayout-l.engine",
    TensorRtBackendProvider.BackendId);
```

格式变体会获得稳定的格式后缀 Profile ID，因此 ONNX 与 TensorRT 变体可以同时注册到同一个 `VisualProfileRegistry`；它只改变 Profile 与工件匹配所需的逻辑格式，不会自动转换 ONNX、推断 Engine 的 TensorRT 版本，也不会绕过 `ConversionBlocked` 模型的来源限制。格式发生变化时，`CreateArtifact` 不会把 ONNX SHA 错当成 Engine SHA；如果要把实际 Engine 哈希带入工件，可使用 `CreateArtifactWithSha256(path, sha256, backend)`。Engine 仍必须由目标设备上的匹配 TensorRT/CUDA/cuDNN 组合构建，并按本文的 SHA、输入 profile 和计时协议单独记录。

如果 TRT11 的强类型网络需要严格的 FP32 数值对照，可在 ONNX→Engine 构建时显式关闭 TF32；这与 `Float32` 弱类型精度选项不同，适用于 TRT11：

```csharp
var buildOptions = new TensorRtOnnxEngineBuildOptions(
    apiVersion: TensorRtApiVersion.TensorRt11,
    precision: TensorRtOnnxEnginePrecision.RuntimeDefault,
    disableTf32: true,
    inputProfiles: profiles,
    overwrite: true);
```

`DisableTf32` 会进入 build-input hash、engine cache key 和 manifest。切换该值会得到新的缓存身份，旧 Engine 不会被错误复用；它只控制构建时的 TF32 flag，不会把 TRT11 强类型网络改成弱类型 FP32。

同时需要 OpenCV native runtime 读取图片，并先运行上方官方示例获取脚本。PP-LCNet 导出带动态维度，测试显式配置 `x=[1,3,224,224]` 的 min/opt/max profile、构建优化级别 3，再由库内 builder 转 Engine。测试校验同一个 Engine 的 CPU/CUDA 预处理分类结果，记录 Engine SHA、构建时间、5 次预热后 50 个样本及 P50/P95；构建和图像解码不计入稳态时间，也不代表其它 PP-Structure 模型均已验证。

最近一次按官方分类预处理重新执行的 TRT 11 smoke（`compute_86`，正确匹配的 TRT 11 bridge SHA `a94d1e5fe4454c9402979ae050f7a64d74c7f51bd6bb2d74936531f608a9ef6`）为：PP-LCNet ONNX SHA `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`，Engine SHA `3a6fbfd57fcd41aadd0d5b7353efc0db3babaac0b8410909595b6f77bb0f39ab`，构建 `40719.153 ms`，稳态 P50 `1.318 ms`、P95 `1.921 ms`；同一 Engine 的 CPU/CUDA 预处理分类分数分别约 `0.892226` / `0.894656`。这里的 Engine 与 TensorRT/CUDA/cuDNN 版本绑定，不能复制到其它设备后直接复用。

2026-10-04 又在当前 `JYPPX` Windows 环境使用本机可用且与 bridge 匹配的 TensorRT `10.11.0.33-cu12` 复跑同一方向模型：Engine SHA `cc7e1454489680630a18a1d6c403668b9d7571dc8d4410c9d33d75bb21ef2547`，构建 `63634.811 ms`，稳态 P50/P95 `1.1231/1.8312 ms`，CPU/CUDA 预处理分类分数 `0.8923855/0.89481854`，绝对差 `0.00243304`。机器可读证据见 [`paddle-document-tensorrt-orientation-20261004.json`](../../eng/models/paddle-document/verification/paddle-document-tensorrt-orientation-20261004.json)。这条记录只证明 TRT10.11 bridge、该 ONNX 和该设备的单模型链路；用 TRT11 运行该 bridge 会得到 `DS-TRT-5005`，不能混用版本或外推到全部 PP-Structure。

表格分类也已在同一入口完成 TRT 11 真实运行：ONNX SHA `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`，Engine SHA `c9464c22f5d1d11ac4e2f35fb3d437a782278d9a85f697bdd3f8ef050ad065f2`（7,350,700 bytes），静态 `x=[1,3,224,224]`，5 次预热、50 次测量，构建 `36204.071 ms`，稳态 P50 `1.212 ms`、P95 `1.331 ms`。ORT 返回 `wired_table/0.844208`，TensorRT 返回 `wired_table/0.851693`，绝对置信度差 `0.007485`，在测试合同的 `0.01` 容差内。该 Engine 同样绑定 TensorRT/CUDA/cuDNN/GPU 架构，换设备必须重新构建。

在当前 `JYPPX` 机器的 TRT10.11 匹配 bridge 上，同一表格分类模型又完成了一次独立复跑：Engine SHA `f80df8411b34714cfa77eddefa7a1c2afaca70e83dceeaf85601b76545e6f195`（8,593,404 bytes），构建 `58024.8733 ms`，稳态 P50/P95 `0.9974/1.1903 ms`；TensorRT/ORT 分数 `0.8517079/0.8442081`，差值 `0.0074998`，仍在 `0.01` 合同容差内。详见 [`paddle-document-tensorrt-table-classification-20261004.json`](../../eng/models/paddle-document/verification/paddle-document-tensorrt-table-classification-20261004.json)。这只是当前设备的单模型表格分类证据，不改变 TRT11 与 TRT10 bridge 不能混用的边界。

同一测试入口还完成 `ppocrv4-mobile-seal-det.onnx` 的 TRT 11 真实运行：ONNX SHA `e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199`，Engine SHA `13772da4a34bd9fb3ea1e1c3cfedabf6a2fb1aace0762c6e5253904e374c9953`（7,045,964 bytes），输入 profile 为 `image=[1,3,224,224]`，5 次预热、50 次测量，构建 `86508.609 ms`，稳态 P50 `3.710 ms`、P95 `4.558 ms`。`demo_1.jpg` 上 ORT/TensorRT 都返回 `224x224` 掩码且区域数为 `0/0`，原始掩码最大/平均绝对误差为 `9.42e-8`/`1.36e-8`；因为该图片无印章，这条结果仅是输入/输出合同和数值一致性，不是检测精度或 server 模型的证明。

server 印章也已在同一入口完成 TRT 11 真实运行：ONNX SHA `f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7`，Engine SHA `d89b4c5cbfde1707bd486007bdf65afe481e3fcdb85c1cdf11d7c98f88332d03`（140,877,596 bytes），输入 profile 为 `image=[1,3,224,224]`，显式 `DisableTf32=true`，5 次预热、50 次测量，构建 `93150.467 ms`，稳态 P50 `12.221 ms`、P95 `24.648 ms`。`demo_1.jpg` 上 ORT/TensorRT 都解码出 1 个区域，掩码平均绝对误差 `2.4136e-6`、最大绝对误差 `8.6451e-4`，逐元素误差通过当前测试合同；这仍是单张图的数值一致性证据，不是数据集精度结论。

同一测试入口还对 `pp-doclayout-l` 完成了真正的 TensorRT Paddle NMS 版面推理。输入为 `E:\Data\image\bus.jpg`，使用动态 `image`、`im_shape`、`scale_factor` 三输入 profile 和 TensorRT 11 API；输入张量由 OpenCV 准备，TensorRT 负责模型推理与图内 NMS，ORT CPU 结果用于确定性比较。ONNX SHA 为 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`，Engine SHA 为 `c6acaedc9f3ed2996e012d1fd6b45c5eb061877d2ac3aa0f77bde34f65e9a0ba`（155,392,076 bytes）；5 次预热、50 次测量得到 300 个候选，其中 9 个置信度不低于 0.05，TensorRT 与 ORT 的高置信度结果满足 score 误差 ≤`0.01`、源坐标误差 ≤`1.5 px`，P50 `19.414 ms`、P95 `25.876 ms`。这是 CPU 准备输入的 TensorRT NMS 证据，不是尚未接入辅助输入的 CUDA 端到端预处理指标。

同一入口新增两个 RT-DETR 表格单元格工件的 TensorRT 11 证据：wired Engine SHA `71fb2ee40a53b66c5e61d8bf2679235f5eb53fc2f01dc335dc3bb828b0a2132b`，P50/P95 `21.605/24.613 ms`；wireless Engine SHA `cb7df85c136e0b45e673a37995ed5c58f2a587f385cca15f674a6cf8f29c6c2a`，P50/P95 `21.689/25.661 ms`。两者均为 300 个 Paddle NMS 候选；以 score `>=0.05` 统计时 wireless 为 ORT/TensorRT `294/295`，共同比较候选的最大 score 差 `0.00701`、最小 IoU `0.9179`。完整 Engine/ONNX SHA 和复现命令见 [`paddle-document-tensorrt-table-cell-matrix-20260930.md`](../../eng/models/paddle-document/verification/paddle-document-tensorrt-table-cell-matrix-20260930.md)。这仍是精确工件的执行/数值合同证据，不代表表格单元格召回率或表格结构准确率。
