# PaddleOCR / PP-Structure 文档智能模块

DeploySharp 现在把 PaddleOCR 的文档智能能力按模块建模，而不是把“版面分析”和“表格识别”误写成普通 OCR 检测。PP-StructureV3 官方产线包含文档方向分类、文本图像矫正、版面区域检测、表格分类/单元格检测/结构识别、公式识别、印章文本检测和图表解析；DeploySharp 对这些模块保留独立的任务 ID、模型来源、转换状态和结果合同。

官方 PP-StructureV3 的组合方式和模型说明见 [PaddleOCR 官方 PP-StructureV3 文档](https://paddlepaddle.github.io/PaddleOCR/main/version3.x/pipeline_usage/PP-StructureV3.html)。本项目不会把 Paddle Inference 的 `.tar` 工件自动当作 ONNX，也不会在没有转换和运行时证据时标记为“多后端已支持”。

## 当前模块边界

| 模块 | 官方模型族 | DeploySharp 任务/结果合同 | 当前状态 |
| --- | --- | --- | --- |
| 文档方向 | `PP-LCNet_x1_0_doc_ori` | `VisualTaskId.DocumentOrientation`、四分类结果 | ORT CPU、OpenVINO CPU 和 TensorRT 11 smoke 已完成；分类预处理为短边 256 后中心裁剪 224 |
| 文本图像矫正 | `UVDoc` | `VisualTaskId.DocumentUnwarping`、输出尺寸/变换元数据 | ORT CPU 已完成真实图片 + 专用矫正图 Decoder smoke；视觉质量指标和其它后端仍待验收 |
| 版面区域检测 | `PP-DocLayout*`、`PP-DocBlockLayout`、PicoDet/RT-DETR layout | `VisualTaskId.LayoutDetection`、区域检测结果 | 本机 13 个已转换 layout 工件已完成 ORT CPU + Paddle 后置 NMS Decoder 逐模型 smoke；`pp-doclayout-l` 另有 OpenVINO、OpenCV DNN 和 TensorRT 11 NMS 实测，其余后端仍按矩阵逐工件记录 |
| 表格分类 | `PP-LCNet_x1_0_table_cls` | `VisualTaskId.TableClassification` | wired 模型已完成 ORT CPU/OpenVINO CPU 分类 Decoder smoke，并有 TensorRT 11 CUDA 真实证据；使用官方短边 256、中心裁剪 224 |
| 表格单元格检测 | `RT-DETR-L_*_table_cell_det` | `VisualTaskId.TableCellDetection` | wired/wireless 均已完成 ORT CPU Paddle NMS Decoder smoke；OpenVINO 目前仅 wired 有通过证据 |
| 表格结构识别 | `SLANeXt_wired/wireless` | `VisualTaskId.TableStructureRecognition`、表格标记和单元格结果 | wired/wireless 均已完成 ORT CPU 双输出序列/八点框 Decoder smoke；原始 Release 图在 OpenVINO 当前版本不支持（`Loop` importer），仅派生 alpha-renamed 图通过逐元素和 Decoder 对齐 |
| 公式识别 | `PP-FormulaNet_plus-*`、`UniMERNet` | `VisualTaskId.FormulaRecognition`、LaTeX/序列结果 | 6 个可转换公式工件均已在真实 ORT CPU 推理后使用官方 `inference.yml` BPE tokenizer 完成语义 LaTeX 解码；token-piece fallback 保留给旧目标框架 |
| 印章文本检测 | `PP-OCRv4_*_seal_det` | `VisualTaskId.SealTextDetection`、区域结果 | mobile/server 均已完成 ORT CPU + OpenVINO CPU 概率图连通区域 Decoder smoke；mobile/server 另有 TensorRT 11 输入/掩码/Decoder 一致性实测（server 已显式关闭 TF32）；弧形文本 OCR 组合仍待补 |
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

本地标准推理归档转换为 ONNX 后按单模型资产同步到上述 Release；Chart2Table 的上游归档是生成式权重而非 `inference.json + inference.pdiparams`，因此模型目录仍把“单文件源归档转换项”标为 `conversion-blocked`。它的四张派生 ONNX 和三份 tokenizer 资产现已作为可单独按需下载的 Bundle 发布。ORT CPU、OpenVINO CPU 和 TensorRT CUDA 在官方样例上均跑到 EOS 并生成相同完整文本；OpenCV DNN 自回归流程仍无证据，但 PP-OCR 核心流水线的 OpenCV DNN 证据已覆盖 v4/v5/v6 七组，详见模型后端矩阵和核心验证 JSON。

## 在代码中创建 Profile

文档模型使用 `PaddleDocumentProfiles` 创建后端无关 Profile。分类可以直接复用分类 Profile；通用 `CreateRegionDetection` 只适用于调用方确认了 `[batch,candidates,fields]` 原始候选张量的导出。当前官方 layout/cell 导出已经将 NMS 写入图中，应使用 `CreatePaddleNmsRegions`；模型输出名、标签、预处理和输出坐标必须以实际 ONNX 图为准：

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

## 状态和验证规则

28 个独立 ONNX Release 资产均有 ORT CPU 图执行及 Decoder 冒烟证据。PP-LCNet、SLANeXt、公式模型另有下方官方示例回归。PP-Chart2Table 的四图 Bundle 和 tokenizer 已放入 `models-paddleocr` Release。ORT CPU、OpenVINO CPU 和 TensorRT CUDA 在官方图表样例均生成完整 2018–2023 表格并正常到 EOS；优化 TensorRT device path 在该样例最好 10.12 秒，原 host-KV 严格 FP32 通用路径为 83.42 秒。额外四个 ChartQA human 图表均生成完整表格并核对通过 8 个关联 QA 标签。该小样本不是数据集准确率指标；Chart2Table 的 OpenCV DNN 自回归仍未验证，PP-OCR 核心 OpenCV DNN 证据不代表 PP-Structure 生成模型。

| 后端/模块 | 当前真实证据 | 边界 |
| --- | --- | --- |
| ORT CPU | 22 个非公式工件、6 个公式 token/BPE 解码，方向→版面阶段编排 | 公式结果长度与 tokenizer 无告警不是公式准确率 |
| OpenVINO CPU | 方向、版面、表格分类、单元格、UVDoc、mobile/server 印章 | 按精确模型记录；未外推其它工件 |
| OpenVINO SLANeXt | wired/wireless 派生 ONNX 均通过，与原始 ORT 输出逐元素对比 | 原始 Release ONNX 仍受循环变量同名问题影响，需先执行下方兼容转换 |
| OpenCV DNN | PP-DocLayout-L 单 batch 与 ORT 对比；两个 PP-LCNet 官方示例；PP-OCRv5 mobile 完整流水线 | PP-DocLayout-L 不请求被 importer 忽略的常量计数输出 |
| TensorRT CUDA | PP-LCNet 文档方向、PP-LCNet 表格分类、`pp-doclayout-l` Paddle NMS、mobile/server 印章概率图；均有 ORT 对照和预热后 P50/P95 | TRT 11 bridge + TRT 11.0.0.114-cu12 + CUDA 12.9 + cuDNN 9.22；server 印章已显式关闭 TF32，当前样本逐元素误差通过合同，不外推其它模型 |
| Chart2Table 四图 Bundle | ORT CPU/OpenVINO CPU/TensorRT CUDA：官方样例生成完整 2018–2023 表格；额外 4 张 ChartQA human 图到 EOS、精确匹配 golden table，关联 QA 8/8 匹配。TensorRT device path 的官方样例最好 10.12 s，Decode P50/P95 66.47/73.48 ms | 库 Builder 已构建四图并通过完整生成回归；ChartQA 本次严格 FP32 Builder 计划运行 29m19s，耗时异常且原因待 profiling，不是性能基准；OpenCV DNN、跨数据集准确率仍未验证 |

完整状态见[模型后端验证矩阵](../model-backend-verification-matrix.md)。这些测试使用本机模型和真实图片，但尚未完成 PP-Structure 的任务级精度基准或全模型 P50/P95。Chart2Table 四图合同、tokenizer 哈希、端到端结果和每后端阶段耗时见 [Bundle 验证记录](../../eng/models/paddle-document/verification/chart2table-component-validation.json)。

### Chart2Table 完整 Bundle 技术验证

本次验证复用了 `E:\Model\PaddleDocument\source\pp-chart2table\PP-Chart2Table\model_state.pdparams` 和官方 `chart_parsing_02.png`，没有重新下载同一官方包。结论分为三个层次：

1. **多后端端到端与额外图表样本**：视觉编码、官方 Tokenizer、图像特征替换、286-token Prefill、`lm_head` greedy 选择和动态 KV Decode 均已覆盖。三 token ORT 阶段与 Paddle 逐 token 一致，最大逐步 logits 误差 `6.77e-5`；ORT CPU、OpenVINO CPU、TensorRT CUDA 完整运行均在 256-token 上限内生成 141 个 token（含 EOS），返回同一张官方六行表。运行主机为 Ryzen 7 5800H、Windows 11、.NET 10；ORT CPU `39.76 s`，OpenVINO CPU `46.40 s`，优化 TensorRT device path 三次 `12.64/12.12/10.12 s`，最好一次 Decode P50/P95 为 `66.47/73.48 ms`（ORT CPU `94.69/123.92 ms`）；最初 host-KV 严格 FP32 TensorRT 路径为 `83.42 s`。

   随后使用 ChartQA `test_human` 的 4 张独立图表（官方仓库：[vis-nlp/ChartQA](https://github.com/vis-nlp/ChartQA)）进行 TensorRT 端到端补测。四次均到 EOS，生成表格逐字匹配实测 golden outputs，并从表格值核对关联的 8 个问答标签，8/8 一致：食物商品柱状图的类别数为 14、Lamb 与 Corn 差值为 0.57；三类国家图表中的类别数为 3、Madagascar 低于 Fiji；军事角色图表最低值 23、绿色系列差值 6；特朗普特征图中 Dangerous 为 62、39+26 大于 55。样本图像 SHA-256、表格文本和逐项核对记录在 [Bundle 验证 JSON](../../eng/models/paddle-document/verification/chart2table-component-validation.json)。ChartQA 数据文件带 GPL-3.0 许可，本仓库和 Release 不包含这些图像。此次使用 Builder 创建的严格 FP32 文本计划、未锁 GPU 时钟，四张图总耗时约 29 分 19 秒，明显高于另一组优化官方样例观察；原因尚未定位，不能视为速度基准，后续需受控复跑并做逐阶段 profiling。

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
3. **发布与泛化精度边界**：四个派生 ONNX 和 tokenizer sidecars 已作为 `paddle-chart/pp-chart2table` Bundle 放入 `models-paddleocr` Release；OpenCV DNN 仍未验证。官方样例和 4 个 ChartQA human 样本均完成 EOS 与表格输出校验，但这不是全量数据集精度指标。该模型目录项保留上游源包的 `conversion-blocked` 状态，不能表示其派生 ONNX Session 不可用。

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
| `general_formula_rec_001.png` | 六个公式模型；ORT CPU | token/LaTeX 长度依次为 Plus-S `1022/1226`、Plus-M `2/6`、Plus-L `49/62`、FormulaNet-S `1023/1036`、FormulaNet-L `31/37`、UniMERNet `1022/1252`；全部生成 EOS 且 warnings=0。Plus-S/M/L 的 LaTeX 忽略排版空白后与官方示例一致；FormulaNet-S/L 和 UniMERNet 仍有符号或格式差异 |

公式输入还与固定版本的 PaddleX 原始 processor 逐元素对比。三种尺寸平均绝对误差分别约 `1.54e-7`、`7.70e-8`、`2.10e-6`；最大差异约 `0.022564`（相当于归一化前一个灰度级），来自 Pillow 整数滤波取整。此证据针对当前示例，不代表所有输入逐位相同。

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

原始图的 Loop 形参与外层常量同名，当前 OpenVINO importer 报出循环体参数数量 1/11 不匹配。该问题可以用局部变量重命名处理，无需展开循环、修改权重或改变停止条件。转换脚本生成一个新文件，运行 ONNX checker，并输出源文件与派生文件 SHA-256：

```powershell
# 在仓库根目录执行；Python 环境需要 onnx，建议复用模型转换环境。
python eng/models/paddle-document/scripts/normalize_loop_parameters.py `
  E:\Model\PaddleDocument\onnx\slanext-wired.onnx `
  artifacts/model-compatibility/slanext-wired-local-parameters.onnx
python eng/models/paddle-document/scripts/normalize_loop_parameters.py `
  E:\Model\PaddleDocument\onnx\slanext-wireless.onnx `
  artifacts/model-compatibility/slanext-wireless-local-parameters.onnx

$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_NORMALIZED_ROOT = `
  (Resolve-Path artifacts/model-compatibility).Path
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter FullyQualifiedName~AlphaRenamedTableGraphMatchesOriginalOrtOutputs
```

| 派生工件 | SHA-256 |
| --- | --- |
| wired | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` |
| wireless | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` |

两个工件均通过 OpenVINO 对原始 ORT 的输出比较，浮点逐值容差 0.001，并执行表格 Decoder。派生文件有独立哈希，不能沿用原始 Release 的 SHA；创建 `ModelArtifact` 时使用实际派生路径和哈希。Release 下载客户端仍提供原始工件，不会暗中替换模型。该输入不是表格标注集，HTML/单元格准确率仍需真实表格验证。

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

表格分类也已在同一入口完成 TRT 11 真实运行：ONNX SHA `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45`，Engine SHA `c9464c22f5d1d11ac4e2f35fb3d437a782278d9a85f697bdd3f8ef050ad065f2`（7,350,700 bytes），静态 `x=[1,3,224,224]`，5 次预热、50 次测量，构建 `36204.071 ms`，稳态 P50 `1.212 ms`、P95 `1.331 ms`。ORT 返回 `wired_table/0.844208`，TensorRT 返回 `wired_table/0.851693`，绝对置信度差 `0.007485`，在测试合同的 `0.01` 容差内。该 Engine 同样绑定 TensorRT/CUDA/cuDNN/GPU 架构，换设备必须重新构建。

同一测试入口还完成 `ppocrv4-mobile-seal-det.onnx` 的 TRT 11 真实运行：ONNX SHA `e4b20a5c47c70dbe67cebfdad970124e8c43b0c23cfa4eda5e17f77ef51f9199`，Engine SHA `13772da4a34bd9fb3ea1e1c3cfedabf6a2fb1aace0762c6e5253904e374c9953`（7,045,964 bytes），输入 profile 为 `image=[1,3,224,224]`，5 次预热、50 次测量，构建 `86508.609 ms`，稳态 P50 `3.710 ms`、P95 `4.558 ms`。`demo_1.jpg` 上 ORT/TensorRT 都返回 `224x224` 掩码且区域数为 `0/0`，原始掩码最大/平均绝对误差为 `9.42e-8`/`1.36e-8`；因为该图片无印章，这条结果仅是输入/输出合同和数值一致性，不是检测精度或 server 模型的证明。

server 印章也已在同一入口完成 TRT 11 真实运行：ONNX SHA `f9448c3ffd73f778ad312de10d5ae4df03dbd6b162638bf07fa0880a21a634c7`，Engine SHA `d89b4c5cbfde1707bd486007bdf65afe481e3fcdb85c1cdf11d7c98f88332d03`（140,877,596 bytes），输入 profile 为 `image=[1,3,224,224]`，显式 `DisableTf32=true`，5 次预热、50 次测量，构建 `93150.467 ms`，稳态 P50 `12.221 ms`、P95 `24.648 ms`。`demo_1.jpg` 上 ORT/TensorRT 都解码出 1 个区域，掩码平均绝对误差 `2.4136e-6`、最大绝对误差 `8.6451e-4`，逐元素误差通过当前测试合同；这仍是单张图的数值一致性证据，不是数据集精度结论。

同一测试入口还对 `pp-doclayout-l` 完成了真正的 TensorRT Paddle NMS 版面推理。输入为 `E:\Data\image\bus.jpg`，使用动态 `image`、`im_shape`、`scale_factor` 三输入 profile 和 TensorRT 11 API；输入张量由 OpenCV 准备，TensorRT 负责模型推理与图内 NMS，ORT CPU 结果用于确定性比较。ONNX SHA 为 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`，Engine SHA 为 `c6acaedc9f3ed2996e012d1fd6b45c5eb061877d2ac3aa0f77bde34f65e9a0ba`（155,392,076 bytes）；5 次预热、50 次测量得到 300 个候选，其中 9 个置信度不低于 0.05，TensorRT 与 ORT 的高置信度结果满足 score 误差 ≤`0.01`、源坐标误差 ≤`1.5 px`，P50 `19.414 ms`、P95 `25.876 ms`。这是 CPU 准备输入的 TensorRT NMS 证据，不是尚未接入辅助输入的 CUDA 端到端预处理指标。
