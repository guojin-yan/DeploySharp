# PaddleOCR / PP-Structure 文档智能模块

DeploySharp 现在把 PaddleOCR 的文档智能能力按模块建模，而不是把“版面分析”和“表格识别”误写成普通 OCR 检测。PP-StructureV3 官方产线包含文档方向分类、文本图像矫正、版面区域检测、表格分类/单元格检测/结构识别、公式识别、印章文本检测和图表解析；DeploySharp 对这些模块保留独立的任务 ID、模型来源、转换状态和结果合同。

官方 PP-StructureV3 的组合方式和模型说明见 [PaddleOCR 官方 PP-StructureV3 文档](https://paddlepaddle.github.io/PaddleOCR/main/version3.x/pipeline_usage/PP-StructureV3.html)。本项目不会把 Paddle Inference 的 `.tar` 工件自动当作 ONNX，也不会在没有转换和运行时证据时标记为“多后端已支持”。

## 当前模块边界

| 模块 | 官方模型族 | DeploySharp 任务/结果合同 | 当前状态 |
| --- | --- | --- | --- |
| 文档方向 | `PP-LCNet_x1_0_doc_ori` | `VisualTaskId.DocumentOrientation`、四分类结果 | 已纳入 Profile 合同；需转换工件后运行验证 |
| 文本图像矫正 | `UVDoc` | `VisualTaskId.DocumentUnwarping`、输出尺寸/变换元数据 | 已接入动态 NCHW 矫正图解码器（保留官方 0–255 像素范围）；真实图片视觉质量仍需验收 |
| 版面区域检测 | `PP-DocLayout*`、`PP-DocBlockLayout`、PicoDet/RT-DETR layout | `VisualTaskId.LayoutDetection`、区域检测结果 | 已接入 Paddle 后置 NMS `[class,score,x1,y1,x2,y2]` 和 `bbox_num` 解码；需真实图片语义验收 |
| 表格分类 | `PP-LCNet_x1_0_table_cls` | `VisualTaskId.TableClassification` | 可复用分类 Profile |
| 表格单元格检测 | `RT-DETR-L_*_table_cell_det` | `VisualTaskId.TableCellDetection` | 已接入 Paddle 后置 NMS 解码；需真实图片语义验收 |
| 表格结构识别 | `SLANeXt_wired/wireless` | `VisualTaskId.TableStructureRecognition`、表格标记和单元格结果 | 已接入双输出序列/八点框解码器；仍需真实图片语义验收 |
| 公式识别 | `PP-FormulaNet_plus-*`、`UniMERNet` | `VisualTaskId.FormulaRecognition`、LaTeX/序列结果 | 已接入整数 token 序列解码器；ByteLevel/BPE 词表需由模型包显式提供 |
| 印章文本检测 | `PP-OCRv4_*_seal_det` | `VisualTaskId.SealTextDetection`、区域结果 | 已接入概率图连通区域解码器；弧形文本 OCR 组合仍待补 |
| 图表解析 | `PP-Chart2Table` | `VisualTaskId.ChartParsing`、结构化输出结果 | 结果合同和目录已具备；图表序列解析器待接入 |

## 获取官方模型并转换为 ONNX

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

本轮已在 `E:\Model\PaddleDocument` 使用 Python 3.11、PaddlePaddle 3.0 dev 和 Paddle2ONNX 2.0.2rc3 下载并转换 28 个标准推理归档；其中 `PP-Chart2Table` 是生成式权重归档，不是 `inference.json + inference.pdiparams`，因此仍记录为 `conversion-blocked`。转换结果已使用 ONNX Runtime CPU 做图级 smoke，不能据此宣称已经完成 OpenVINO、TensorRT 或 OpenCV DNN 的运行验证。

## 在代码中创建 Profile

文档模型使用 `PaddleDocumentProfiles` 创建后端无关 Profile。分类可以直接复用分类 Profile；通用 `CreateRegionDetection` 只适用于调用方确认了 `[batch,candidates,fields]` 原始候选张量的导出。当前官方 layout/cell 导出已经将 NMS 写入图中，应使用 `CreatePaddleNmsRegions`；模型输出名、标签、预处理和输出坐标必须以实际 ONNX 图为准：

```csharp
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document;

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

// FormulaNet/UniMERNet exports return integer token IDs. The tokenizer vocabulary
// is explicit so a model package cannot silently use a wrong tokenizer.
var formulaSchema = new PaddleDocumentFormulaSchema(
    new[] { "<s>", "x", "^2", "</s>" }, endTokenId: 3, startTokenId: 0);
PaddleDocumentProfile formula = PaddleDocumentProfiles.CreateFormula(
    PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s"), formulaSchema);
```

`CreateCustom` 仍可用于尚未纳入专用解码器的图表或自定义导出；SLANeXt、UVDoc、FormulaNet/UniMERNet 已分别提供专用 Profile。它们不会用普通分类/检测解码器伪装序列或几何结果。

## 状态和验证规则

模型目录状态只表示资产生命周期：`Catalogued` → `Downloaded` → `OnnxConverted` → `RuntimeVerified`。只有写入实际 ONNX 路径、SHA-256、输出合同并完成后端运行测试后，才能升级到 `RuntimeVerified`。当前新加入的 PP-Structure 条目仍为官方来源目录项；本机没有 Python/Paddle2ONNX 时，脚本会先报告转换阻断，不会声称这些模块已经完成 ONNX Runtime、OpenVINO、OpenCV DNN 或 TensorRT 验证。

已有 PP-OCR 检测/方向/识别完整流水线仍见 [PaddleOCR 三模型流水线](visual-paddle-ocr3.md)。PP-Structure 是文档级模块集合，后续会在转换工件落地后分别补齐表格结构、公式序列、图表解析、UVDoc 几何恢复和真实多后端证据。
