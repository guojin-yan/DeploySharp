# PaddleOCR / PP-Structure 文档智能模块

公式结果结束状态合同：`PaddleDocumentFormulaResult.FinishReason` 为 `EndOfSequence` 时表示模型实际输出了声明的 EOS；为 `MaxTokens` 时表示输出张量在安全上限结束但未见 EOS，并会同时保留 `missing-eos:sequence-may-be-truncated` warning。调用方不得因为有非空 LaTeX 就假定结果完整，Decoder 也不会合成 EOS。

DeploySharp 现在把 PaddleOCR 的文档智能能力按模块建模，而不是把“版面分析”和“表格识别”误写成普通 OCR 检测。PP-StructureV3 官方产线包含文档方向分类、文本图像矫正、版面区域检测、表格分类/单元格检测/结构识别、公式识别、印章文本检测和图表解析；DeploySharp 对这些模块保留独立的任务 ID、模型来源、转换状态和结果合同。

官方 PP-StructureV3 的组合方式和模型说明见 [PaddleOCR 官方 PP-StructureV3 文档](https://paddlepaddle.github.io/PaddleOCR/main/version3.x/pipeline_usage/PP-StructureV3.html)。本项目不会把 Paddle Inference 的 `.tar` 工件自动当作 ONNX，也不会在没有转换和运行时证据时标记为“多后端已支持”。

## 支持的文档任务

| 任务 | 官方模型族 | DeploySharp 入口 |
| --- | --- | --- |
| 文档方向 | PP-LCNet doc orientation | `PaddleDocumentProfiles.CreateClassification` |
| 文本图像矫正 | UVDoc | `CreateUnwarping` / `PaddleDocumentUnwarpingResult` |
| 版面区域 | PP-DocLayout、PP-DocBlockLayout、PicoDet、RT-DETR | `CreatePaddleNmsRegions` |
| 表格分类 | PP-LCNet table classification | 分类 Profile |
| 表格单元格 | RT-DETR-L table cell | Paddle NMS 区域 Profile |
| 表格结构 | SLANeXt wired/wireless | `CreateTableStructure` / HTML 标记 |
| 公式识别 | PP-FormulaNet、UniMERNet | `CreateFormula` / LaTeX 结果 |
| 印章文本检测 | PP-OCRv4 seal detection | 印章掩码与区域结果 |
| 图表解析 | PP-Chart2Table | 四图生成 Bundle |
| 视觉文档生成 | PaddleOCR-VL 1.6 | 六任务生成、区域与页面编排 |

表中的 API 表示主库已实现任务合同。具体模型是否能在某个后端运行，以[模型与后端验证矩阵](../model-backend-verification-matrix.md)为准。

## 获取模型

PP-Structure ONNX 以单模型资产发布在 [models-paddleocr Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr)，无需下载整包。使用 `PaddleOcrReleaseClient.GetModelAsync` 按模型 ID 下载，例如：

```csharp
PaddleOcrReleaseModelMaterialization layout =
    await releaseClient.GetModelAsync("paddle-doc/pp-doclayout-s");

PaddleOcrReleaseBundleMaterialization chart =
    await releaseClient.GetBundleAsync("paddle-chart/pp-chart2table");
```

Chart2Table 和 PaddleOCR-VL 是多图 Bundle；模型图、tokenizer 和 manifest 必须来自同一发布批次。模型 ID、资产名、SHA-256、opset 和 sidecar 见[官方模型目录](model-catalog.md)。

需要从 Paddle 官方推理包自行转换时，仓库提供[模型清单](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/eng/models/paddle-document/paddle-document-models.json)和转换脚本。输出放在仓库外或被忽略的本地模型目录：

```powershell
$modelRoot = Join-Path $PWD '.models\paddle-document'
python -m pip install paddlepaddle paddle2onnx

& .\eng\models\paddle-document\scripts\Acquire-PaddleDocumentModels.ps1 `
  -ModelId pp-lcnet-x1-0-doc-ori,pp-doclayout-plus-l `
  -ModelRoot $modelRoot
```

`-SkipConversion` 只下载和解压，不会把结果标记为 ONNX。转换失败会保留明确的 `conversion-blocked` 状态；不要用空文件或 Paddle Inference 原始目录冒充可运行 ONNX。

## Batch 边界

不要从模型名称推断 Batch。以实际图的输入轴、辅助输入和输出布局为准：

- 动态第一轴的分类、部分版面、单元格、印章、公式、SLANeXt 和 UVDoc 可以配置真 Batch，但每个精确后端仍需验证；
- 静态 `batch=1` 的 PicoDet、部分 PP-DocLayout 和生成式 Bundle 使用有界 Session 池或页面并发；
- Paddle NMS 的扁平 `[rows,6]` 输出必须用 `bbox_num` 按 batch 行切分；真正的 `[batch,rows,6]` 输出才可省略计数张量；
- Chart2Table/PaddleOCR-VL 的动态序列或 KV 轴不等于图像请求 Batch，自回归状态仍绑定单个请求；
- 动态轴只表示图允许绑定，不能替代解码行隔离、内存上限、结果一致性和目标后端实测。

需要批量处理完整文档时，优先使用页面级有界并发；需要同模型吞吐时，先比较真 Batch 与独立 Session 池的 P95 和内存，再选择部署配置。
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
string modelRoot = Path.GetFullPath(Path.Combine(".models", "paddle-document"));
PaddleDocumentFormulaTokenizer tokenizer =
    PaddleDocumentFormulaTokenizer.FromPaddleInferenceYaml(
        Path.Combine(modelRoot, "unimernet", "inference.yml"));
var formulaSchema = new PaddleDocumentFormulaSchema(
    tokenizer, endTokenId: 2, startTokenId: 0, padTokenId: 1, unknownTokenId: 3);
PaddleDocumentProfile formula = PaddleDocumentProfiles.CreateFormula(
    PaddleDocumentModelCatalog.Get("paddle-formula/pp-formulanet-plus-s"), formulaSchema);

// Chart2Table is an autoregressive four-graph bundle rather than a single
// integer-token decoder. Use the official qwen.tiktoken sidecars and the
// dedicated generation session.
string chartRoot = Path.Combine(modelRoot, "pp-chart2table");
var chartTokenizer = new PaddleChart2TableTokenizer(chartRoot);
var chartBundle = new PaddleChart2TableOnnxBundle(
    Path.Combine(chartRoot, "vision.onnx"),
    Path.Combine(chartRoot, "embedding.onnx"),
    Path.Combine(chartRoot, "prefill.onnx"),
    Path.Combine(chartRoot, "decoder.onnx"),
    OnnxRuntimeBackendProvider.BackendId);
using var chartSession = new PaddleChart2TableOnnxSession(
    registry, chartBundle,
    new BackendRequest(BackendCapabilities.TensorInference,
        OnnxRuntimeBackendProvider.BackendId));
var chartInput = new OpenCvPaddleChart2TableInputFactory()
    .CreateFromFile(Path.GetFullPath(Path.Combine("data", "chart.png")));
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

仓库中的 `PaddleDocumentPipelineSemanticIntegrationTests` 演示从标准 `bus.jpg` 准备页面，先执行文档方向分类，再把同一页交给版面区域检测。两个阶段使用 ONNX Runtime CPU，版面阶段读取前序方向结果，最终输出页码、区域列表和分段计时。

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore --verbosity quiet `
  --filter FullyQualifiedName~PaddleDocumentPipelineSemanticIntegrationTests
```

该案例验证的是可运行的方向→版面任务链和结果来源，不代表版面区域已经与人工标注集完成精度评测，也不自动包含版面区域内的 OCR、表格或公式任务。后续阶段应从 `context.GetRegions()` 显式接收区域并创建局部输入，再将局部坐标映射回页面坐标。

要扩展到公开数据集，可设置测试入口声明的页数和数据根目录。FUNSD 等词/实体标注并不等同于 PP-Structure 的版面类别真值；这类运行只能证明执行和结果合同，不能直接作为版面准确率。

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

如果已经确认阶段适配器及其底层 Session 支持并发调用，也可以使用显式有界的 `RunManyConcurrentAsync`。该方法只控制页面级并发，不会把多个页面拼成模型 Batch，并且仍按输入顺序返回结果。内部使用固定 worker 数，不会对所有页面预先创建等待 Task；取消或任一阶段失败后停止分配新页面，并等待已开始的阶段退出。页面集合会在启动任何阶段前完整校验，避免无效页导致部分页面已经进入推理：

```csharp
IReadOnlyList<PaddleDocumentPipelineResult> pages =
    await documentPipeline.RunManyConcurrentAsync(
        decodedPages,
        maxDegreeOfParallelism: 2,
        cancellationToken);
```

`maxDegreeOfParallelism` 是硬上限；调用方必须确认 stage 自身及其 native Session 支持并发。若不能确认，应在该方法外创建多个独立 Pipeline 并自行分片调度。默认串行 `RunManyAsync` 仍适合复用有状态或非线程安全的阶段。

### PaddleOCR-VL 原始任务接入页面和多页编排

PaddleOCR-VL-1.6 的六类任务可以作为页面阶段接入同一个编排器。`PaddleOcrVlDocumentStage` 负责准备输入、调用 `PaddleOcrVlOnnxSession`、保留 Token/EOS 或截断状态，并把页码、输入 SHA、模型和后端写入 `PaddleDocumentVisionLanguageResult`。已有四图 Session 的调用方式如下：

固定 1.6 五图 ONNX Bundle 可用 `PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` 从 [`models-paddleocr`](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) 按需下载并校验；十个资产已完成全新缓存与离线回读验证。该下载结果不包含设备专用 TensorRT Engine，且没有替代下述任务生成和页面质量验证。

四图 Session 已在 Linux 的 ONNX Runtime CPU 和 OpenVINO CPU 上分别完成六任务单图生成对照。[OpenVINO 配置](visual-ocr.md#paddleocr-vl)复用同一 API 和图文件；下方使用 `onnxruntime-cpu` 的页面/版面联合案例仍是 ORT 的实测范围，不据此宣称 OpenVINO 多页联合质量已通过。

在带人工公式标注的 realFormula 数据上，当前有 121 图 ORT CUDA 字符质量诊断、同构建 CPU/CUDA 全 121 图对照和 ORT CPU/OpenVINO CPU 全 121 图逐 Token 对照。默认 CUDA 的生成文本与 CPU 为 120/121 一致，显式关闭 CUDA TF32 后为 121/121 一致；Linux ORT CPU/OpenVINO CPU 的 Token ID 为 121/121 一致。官方原始权重全 121 图对照又表明 DeploySharp ORT CPU 与官方的 6699 个生成 Token、121 份文本全部一致；严格标注匹配仍为 0/121，且至少一张图双方都把 `+1` 错放到根号外。保守语义诊断复评将同数据的抽样结果修正为 5 相符、1 不同、115 不可判定；它仍不是数学语义准确率。这些结果说明固定导出的生成一致性，不代表公式数学语义正确，也不是正式性能结果；调用方仍应针对自己的文档数据验收。

```csharp
var vlModel = new PaddleDocumentModelDescriptor(
    "application/paddleocr-vl/1.6", "PaddleOCR-VL 1.6",
    PaddleDocumentModule.TableStructureRecognition,
    "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
var vlStage = PaddleOcrVlDocumentStage.FromSession(
    vlModel,
    backend: "onnxruntime-cpu",
    task: PaddleOcrVlTask.TableRecognition,
    session: vlSession,
    tokenizer: vlTokenizer,
    prepare: (context, token) =>
        Task.FromResult(new OpenCvPaddleOcrVlInputFactory().CreateFromFile(
            (string)context.Page.Source, PaddleOcrVlTask.TableRecognition)),
    inputIdentity: (context, prepared) => prepared.SourceSha256!);

PaddleDocumentPipelineResult page = await new PaddleDocumentPipeline(new[] { vlStage })
    .RunAsync(new PaddleDocumentPage(imagePath, new VisualSize(width, height), pageIndex: 0), cancellationToken);
PaddleDocumentVisionLanguageResult raw =
    page.GetRequired<PaddleDocumentVisionLanguageResult>(PaddleDocumentModule.TableStructureRecognition);
```

`PaddleDocumentVisionLanguageResult` 的 `Text` 是模型原始输出，`TokenIds` 和 `FinishReason` 可用于回放和截断诊断。表格任务在 `Table` 中附带经检查的逻辑单元格与 HTML，不提供单元格像素框；Spotting 在有原始输入几何时通过 `Spotting` 返回文字四边形。`CoordinatesAvailable` 表示完整的源图坐标是否可用，`PageCoordinatesAvailable` 另表示是否有精确页面映射。公式、图表和印章的任务级语义归一不由上述两种解析结果外推。

#### VL 表格：OTSL、合并单元格与 HTML

VL 的表格生成使用 OTSL 标记，而不是直接返回 HTML。`PaddleOcrVlTableParser` 识别六种标记：`<fcel>` 为带文本的单元格锚点，`<ecel>` 为空单元格锚点，`<lcel>` 为同一单元格向右的延续，`<ucel>` 为向下的延续，`<xcel>` 为同时跨行跨列的内部槽位，`<nl>` 为行分隔。输出 `Row`/`Column` 从零开始，`RowSpan`/`ColumnSpan` 表示逻辑跨度；没有像素坐标、推测的表头角色或虚构的单元格置信度。

对于上面的 `raw`，可直接使用自动解析结果：

```csharp
PaddleOcrVlTableParseResult table = raw.Table!;
if (table.Succeeded)
{
    File.WriteAllText("table.html", table.Html!);
    foreach (PaddleOcrVlTableCell cell in table.Cells)
        Console.WriteLine($"({cell.Row},{cell.Column}) " +
            $"span={cell.RowSpan}x{cell.ColumnSpan}: {cell.Text}");
}
else
{
    Console.WriteLine($"{table.Status}: {string.Join(";", table.Diagnostics)}");
}
```

直接调用四图 Session、尚未进入文档阶段时，使用 `PaddleOcrVlTableParser.Parse(generation.Text, generation.FinishReason)` 即可。`PaddleDocumentVisionLanguageResult.Table` 仅对 `TableRecognition` 非空；其它任务保持原有输出。

`FinishReason=EndOfSequence` 是完整表格输出的必要条件。即使网格看起来完整，`MaxTokens`、取消、错误、停止字符串或未知结束原因都会返回 `IncompleteGeneration`，不输出 HTML 或部分单元格。EOS 后仍会检查行宽、悬空合并标记、矩形覆盖和非法文本位置：生成成功并不等于 `Table.Succeeded`。

默认严格检查不会自动改行宽。若应用明确接受官方行宽修复，可单独请求：

```csharp
PaddleOcrVlTableParseResult repaired = raw.ParseTable(
    new PaddleOcrVlTableParseOptions(
        repairMode: PaddleOcrVlTableRepairMode.OfficialRowWidth,
        maximumInputCharacters: 1024 * 1024,
        maximumRows: 4096,
        maximumColumns: 1024,
        maximumGridCells: 65536));
```

该模式按固定 PaddleX 转换器的最小代价宽度补 `<ecel>`、去掉多余的非文本尾槽和空行，保留所有带文本的锚点。发生改动时状态为 `Repaired`，`Diagnostics` 分别记录补位、裁剪及空行删除数量；修复后仍有非法合并则失败。它可能改变合并跨度或表格结构，不应作为准确率修复。原始 `Text`、Token、来源和默认 `Table` 均不改变，可同时保留严格与修复结果供应用选择。

上述四个上限也是默认值，限制的是 UTF-16 原文字符数、原始行数、每行原始槽数以及原始/修复后网格大小。超过上限返回 `LimitExceeded`，取消请求传播 `OperationCanceledException`。最终行可以在 EOS 处结束，末尾 `<nl>` 可省略。HTML 仅生成固定的 `table/tr/td` 和跨度属性，单元格内容按官方规则去掉两端空白并做 HTML 转义；原有实体也作为文本保留，不执行模型生成的标签。JSON 导出保留 `table.status/diagnostics/cells/html`；Markdown 同时保留原始 OTSL 代码围栏和转义后的表格。

官方 OTSL 对照与真实页面联测覆盖合并、空格/特殊字符和显式行宽修复，也核对已生成的两张真实表格。它验证转换与接入正确性，不评估模型识别内容是否正确。

#### VL Spotting：文字四边形与页面坐标

选择 `PaddleOcrVlTask.TextSpotting` 后，Session 仍返回原始生成结果；页面、区域及混合阶段还会自动解析定位标记。支持 `<|TEXT_START|>…<|TEXT_END|>` / `<|LOC_BEGIN|>…<|LOC_END|>` 块式输出，以及“文本 + 八个 `<|LOC_n|>`”的平铺输出。八个值按模型顺序组成四个点，范围为 `[0,1000]`。坐标按 `x=n/1000.0*原始宽`、`y=n/1000.0*原始高` 还原，使用双精度保留小数，不重排顶点、不裁切预测、不猜测置信度。

Spotting 对较小图像可能先执行 2 倍 Lanczos，再执行 Bicubic。解析使用的是 `ImagePlan.SourceSize`，不是放大后的 `PreparedSize` 或最终 `ResizedSize`。直接使用 Session 时可以显式解析：

```csharp
PaddleOcrVlSpottingParseResult spotting = PaddleOcrVlSpottingParser.Parse(
    generation.Text, generation.FinishReason, prepared.ImagePlan.SourceSize,
    prepared.SourceFrame, cancellationToken: cancellationToken);
if (spotting.Succeeded)
{
    foreach (PaddleOcrVlSpottingRegion line in spotting.Regions)
    {
        Console.WriteLine(line.Text);
        var cropPoints = line.SourcePolygon;
        var pagePoints = line.PagePolygon; // 没有精确页面映射时为 null。
    }
}
```

OpenCV 的文件、整页和矩形区域准备器会自动附带 `PaddleOcrVlSourceFrame`。区域框经裁边和 floor/ceil 后，框架保存实际整数裁剪尺寸与页面原点；`SourcePolygon` 是裁剪图局部像素，`PagePolygon` 才是加上该原点的整页像素。原有 `SourceRegion` 保留请求框，不能拿它的浮点原点替代实际裁剪原点。区域内容 SHA 相同也不代表页面位置相同，映射随每次请求独立保存。

自定义准备器若只提供 RGB、Patch 和图像规划，仍可得到源图局部坐标，但默认不推测页面位置。对于未经旋转、缩放或透视变换的整数矩形 crop，可显式绑定映射，且不会复制 Patch Tensor：

```csharp
prepared = prepared.WithSourceFrame(new PaddleOcrVlSourceFrame(
    pageSize, prepared.ImagePlan.SourceSize, actualCropLeft, actualCropTop));
```

透视矫正、旋转、额外缩放后的自定义 crop 不能套用此平移映射；调用方需使用自己的逆变换。没有原始几何的手动原文结果不会伪造定位坐标，`ParseSpotting()` 会明确拒绝该情况。

只有 EOS 且所有配对、坐标和四边形合法时 `Spotting.Succeeded=true`。少于八值、额外块内值、未配对文本、越界或错误标记、重复点、退化及自交四边形均返回 `InvalidStructure`；任何失败都不发布部分成功区域。非 EOS 为 `IncompleteGeneration`，不是“完整识别”。默认最多解析 1,048,576 个 UTF-16 字符和 4096 个区域，超限返回 `LimitExceeded`，取消直接传播。用 `result.ParseSpotting(new PaddleOcrVlSpottingParseOptions(...))` 可以独立调整上限，不改变原文或默认分析。

JSON 的 `spotting` 保存状态、诊断、原始量化值、源图/页面多边形和映射元数据，Markdown 显示独立的坐标系与文字代码围栏。页面聚合的坐标可用标记表示“至少一个区域可用”，并不保证所有任务都有模型坐标，必须检查逐区域状态。官方解析对照与实际 ROI 联测记录了当前验证范围；它不等于人工标注的文字或定位准确率。

如果版面检测已经产出页面区域，可以使用 `PaddleOcrVlRegionStage` 逐区域路由同一个 VL 任务。它按版面结果顺序准备每个 crop，保留原区域的页面坐标、区域索引和输入 SHA，并返回 `PaddleDocumentVisionLanguagePageResult`。输入任务必须与阶段一致；当输入和生成结果都携带 SHA 时，两者必须相同，防止把另一张 crop 的结果关联到当前区域。`continueOnRegionFailure: true` 时，单个区域的裁剪、生成或身份校验失败会变成带异常类型和消息的隔离记录，后续区域仍会运行；取消请求会向上传播，也不会启动备用推理。`fallback` 只用于调用方提供的另一条 VL 生成路径，不能把普通 CTC OCR 的文本伪装成 VL 结果。

```csharp
var vlRegions = new PaddleOcrVlRegionStage(
    vlModel,
    backend: "onnxruntime-cpu",
    task: PaddleOcrVlTask.TableRecognition,
    pageInputIdentity: context => pageSha256,
    prepare: (context, region, regionIndex, token) =>
        Task.FromResult(PrepareVlCrop(context.Page.Source, region, token)),
    generate: (context, region, regionIndex, prepared, token) =>
        vlSession.GenerateAsync(prepared, vlTokenizer, generationOptions, token),
    inputIdentity: (context, region, regionIndex, prepared) => prepared.SourceSha256!,
    continueOnRegionFailure: true);

PaddleDocumentPipelineResult routedPage = await new PaddleDocumentPipeline(
    new IPaddleDocumentPipelineStage[] { layoutStage, vlRegions })
    .RunAsync(new PaddleDocumentPage(pageSource, pageSize, pageIndex: 0), cancellationToken);
PaddleDocumentVisionLanguagePageResult routed =
    routedPage.GetRequired<PaddleDocumentVisionLanguagePageResult>(
        PaddleDocumentModule.TableStructureRecognition);
foreach (var execution in routed.Executions)
    Console.WriteLine($"region {execution.RegionIndex}: {execution.Result?.Text ?? execution.FailureMessage}");
```

使用 OpenCV 时，不必自行实现上例的 `PrepareVlCrop`。`OpenCvPaddleOcrVlRegionStage` 每页调用源工厂一次、解码一次，对全部布局矩形复用同一底图，再由官方合同的托管 Processor 准备 RGB Patch：

```csharp
var decodedRegions = OpenCvPaddleOcrVlRegionStage.FromSession(
    vlModel, "onnxruntime-cpu", PaddleOcrVlTask.TableRecognition,
    vlSession, vlTokenizer,
    page => (OpenCvImageSource)page.Source,
    generationOptions: generationOptions,
    continueOnRegionFailure: true);
var pipeline = new PaddleDocumentPipeline(
    new IPaddleDocumentPipelineStage[] { layoutStage, decodedRegions });
var pageInput = new PaddleDocumentPage(
    OpenCvImageSource.FromFile(imagePath), pageSize, pageIndex: 0);
PaddleDocumentPipelineResult actualPage = await pipeline.RunAsync(pageInput, cancellationToken);
```

该阶段校验页面尺寸与解码尺寸一致，拒绝无交集或无效矩形，支持非连续 stride 的区域视图。裁剪按左上 floor、右下 ceil、源图 clipping 执行；不会重新编码区域、克隆整页或提前使用 OpenCV 缩放。页面 metadata 使用编码文件 SHA，区域 metadata 使用包含裁剪尺寸与紧密 RGB 的内容 SHA。[完整多页案例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddleocr-vl-document/README.md)给出输入 manifest、命令、JSON/Markdown 和非零失败退出码。

Linux CPU 区域验证从同一官方表格 RGB 拼成两张 PNG 页面，运行三个手工区域。Patch 及 `213` Token/文本全部一致、全部 EOS，并检查页码 7→3 及页面内的调用方区域顺序。该记录验证实际裁剪与页面编排，不等于独立多页质量、自动版面检测或阅读顺序验证。

区域阶段的 `SourceRegion` 是版面检测或调用方提供的页面坐标来源，区别于 VL 模型通过 `Spotting` 返回的文字多边形。表格的逻辑单元格/HTML 和 Spotting 坐标各自检查解析状态，不会自动补出公式语义、图表结构或单元格像素框。`PaddleOcrVlRegionStage` 保留“所有区域运行同一任务”的语义；混合页面使用 `PaddleOcrVlLayoutStage`，或它的 OpenCV 单次解码适配器 `OpenCvPaddleOcrVlLayoutStage`：

```csharp
var mixedModel = new PaddleDocumentModelDescriptor(
    "paddleocr-vl/1.6", "PaddleOCR-VL-1.6",
    PaddleDocumentModule.VisionLanguageParsing,
    "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
var router = PaddleOcrVlTaskRouter.CreatePpStructure(
    new Dictionary<string, PaddleOcrVlTask?> {
        ["spotting"] = PaddleOcrVlTask.TextSpotting,
        ["header"] = null // 应用决定忽略页眉。
    });
var mixedStage = OpenCvPaddleOcrVlLayoutStage.FromSession(
    mixedModel, "onnxruntime-cpu", router, vlSession, vlTokenizer,
    page => (OpenCvImageSource)page.Source,
    generationOptions, continueOnRegionFailure: true);
var mixedPage = await new PaddleDocumentPipeline(
    new IPaddleDocumentPipelineStage[] { layoutStage, mixedStage })
    .RunAsync(pageInput, cancellationToken);
var mixed = mixedPage.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(
    PaddleDocumentModule.VisionLanguageParsing);
foreach (var region in mixed.Executions)
    Console.WriteLine($"{region.RegionIndex}: {region.Task} / {region.Status} / " +
        (region.Result?.Text ?? region.SkipReason ?? region.FailureMessage));
```

`CreatePpStructure` 按现有版面标签选择任务：文字/标题类 → OCR，`table` → 表格，`formula` → 公式，`chart` → 图表，`seal` → 印章。类别匹配忽略大小写和两端空白。图片类及未知类默认跳过；可覆盖映射，也可通过 `unmatchedCategoryTask` 为未知类配置任务。未开启图片回退时，显式 `null` 映射始终跳过该类别；Spotting 需应用明确配置，不根据图片类别猜测。路由器复制配置后只读，可供并发页面共享。

版面模型有时把正文误判为 `image`。应用可显式开启 `imageFallback: new PaddleOcrVlImageFallbackOptions(PaddleOcrVlTask.TextRecognition, minimumUncoveredAreaRatio: .25, maximumRegionsPerPage: 1)`，传给 `CreatePpStructure`。路由器只对本来跳过的 `image` 区域计算其它已规划路由区域在其内部的矩形**并集**覆盖；达到未覆盖比例阈值的候选按未覆盖绝对面积排序，每页取前 N 个，同面积保留原索引先后。选中区域的页面警告为 `vl-image-fallback:<index>`。该计算每页执行一次，不启动额外检测器；默认关闭。显式非空的 `image` 类别任务仍优先于此回退，`image: null` 加启用回退表示按几何规则挑选而不是无条件跳过。若要保持全部图片跳过，应不配置回退。筛选发生在生成前，正常区域后来失败不会重新触发回退；回退也不判断图片有无文字或消除其它区域的重复解析，预算、阈值和质量须由应用数据确定。真实检测框与单次图片推理记录展示了边界；完整 manifest 见[示例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddleocr-vl-document/README.md#自动检测版面再执行-vl)。

Linux 混合页面实测验证两张无损拼接 PNG 上的六个任务区域和两个跳过项：全部 Patch、765 Token/文本与固定官方参考一致且 EOS，页码/区域索引不变。该集成测试使用手工版面输入，不是自动版面模型、独立多页质量或正式性能结论。

混合结果保留全部原版面区域和索引，分别统计 `SucceededCount`、`FailedCount`、`SkippedCount`；跳过项不准备 Patch、不执行生成、不伪造空文本。单区域失败保留已选任务，成功结果的模型模块按实际任务设置，页面聚合模块为 `VisionLanguageParsing`。`Succeeded` 表示生成返回结果，仍须检查 `FinishReason`，`MaxTokens` 可能意味着截断。JSON/Markdown 导出保留任务、状态、源框和原文。它沿用输入顺序，不重新排序，也不会识别没有检测出的区域。[多页示例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddleocr-vl-document/README.md#混合页面按类别选择任务)提供 `task=Auto` 的完整 manifest。

同一个 stage 可以按页调用 `RunManyAsync`，或对线程安全适配器调用 `RunManyConcurrentAsync`。OpenCV VL 阶段为每页持有独立底图并在结束、失败或取消时释放；Session 和 Tokenizer 仍由调用方拥有。页面并发不是真正的 VL 模型 Batch，单个 `PaddleOcrVlOnnxSession` 会串行生成。当前精确后端范围以验证矩阵为准；TensorRT Engine 绑定目标设备，OpenCV DNN 不支持该生成式 Bundle。

### 真实版面检测与 VL 共享解码底图

在 .NET 8/9/10 中，`OpenCvPaddleOcrVlDocumentPipeline` 将一个已选择正确 Profile 的布局 `VisualPipeline` 接到混合 VL 阶段。它在每页开始时解码一次，检测器与所有区域裁剪共用该底图，页面结束/异常/取消后释放。返回页面的 `Source` 仍是调用方的 `OpenCvImageSource`，不会返回已经释放的 native 图像句柄。

```csharp
// layoutPipeline 是已经配置模型、后端及布局 Decoder 的 VisualPipeline。
// session/tokenizer 是同一固定 1.6 Bundle 的 VL Session 与 Tokenizer。
var detectorModel = PaddleDocumentModelCatalog.Get("paddle-doc/pp-doclayout-l");
var parsingModel = new PaddleDocumentModelDescriptor(
    "paddleocr-vl/1.6", "PaddleOCR-VL-1.6",
    PaddleDocumentModule.VisionLanguageParsing,
    "https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6", "onnx");
var document = OpenCvPaddleOcrVlDocumentPipeline.FromSession(
    layoutPipeline, detectorModel, parsingModel, "onnxruntime-cpu",
    session, tokenizer,
    new PaddleOcrVlGenerationOptions(maximumNewTokens: 2048),
    maximumRegions: 64, continueOnRegionFailure: true);
PaddleDocumentPipelineResult parsed = await document.RunAsync(
    OpenCvImageSource.FromFile(imagePath), pageIndex: 0,
    cancellationToken: cancellationToken);
Console.WriteLine(PaddleDocumentPipelineExport.ToMarkdown(parsed));
```

该入口使用检测器**实际选择的 Profile**，包括应用调整过的缩放、归一化及 Paddle 辅助几何输入；不会另外创建一份默认前处理覆盖它。模型 ID、LayoutDetection 任务及可用的公共图像前处理必须匹配。需要自行编排阶段时，`OpenCvPaddleDocumentPreprocessing.CreateFromPage(decodedPage, documentProfile)` 可直接从已有 `OpenCvDecodedRoiImage` 生成检测张量及准确的 `im_shape/scale_factor`，不再次读取/解码源文件；底图仍由调用方拥有。

`RunAsync(source)` 自动取得实际尺寸；传入 `PaddleDocumentPage` 时，源必须是 `OpenCvImageSource` 且声明尺寸与解码结果一致。`RunManyAsync(pages, maximumConcurrency: 2)` 使用有界页面任务，保持调用方顺序，并在一页失败时取消、等待其他任务结束后才返回异常；单个 VL Session 仍排队生成。包装器不拥有检测器、VL Session 或 Tokenizer，应用负责其生命周期及自定义委托的并发安全。

`maximumPagePixels`（默认 128 Mi 像素）在解码后、检测前检查；它不能防止解码器自身分配。`maximumRegions`（默认 64）包括将跳过的所有候选，超限在任何 VL 生成前失败，不静默丢弃区域。VL 最小/最大像素、上下文、KV、Token 和超时仍独立生效。返回的 `Elapsed` 覆盖文档阶段，不含包装器外层首次解码；完整调用耗时请在 `RunAsync` 外计时。

区域保留检测器顺序和 `layoutModelRegionIndex`，另可选择下述几何阅读顺序视图。默认图片类跳过，版面模型的漏检/错分类会直接影响解析覆盖；允许覆盖类别映射，但不会自动消除重叠文本或补回漏检。完整配置见[自动版面示例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddleocr-vl-document/README.md#自动检测版面再执行-vl)。

#### 多栏阅读顺序与稳定区域索引

混合任务阶段 `PaddleOcrVlLayoutStage`、`OpenCvPaddleOcrVlLayoutStage` 和整页入口 `OpenCvPaddleOcrVlDocumentPipeline` 接受 `readingOrderOptions`。省略时仍使用检测器或调用方的原始区域顺序；开启后新增阅读视图，生成委托仍按原索引调用，不重新推理、不复制图片或 Token 张量。

```csharp
var order = new PaddleDocumentReadingOrderOptions(
    mode: PaddleDocumentReadingOrderMode.RecursiveXyCut,
    rightToLeft: false,
    minimumColumnGapRatio: 0.02,
    minimumRowGapRatio: 0.005,
    maximumDepth: 32);
var document = OpenCvPaddleOcrVlDocumentPipeline.FromSession(
    layoutPipeline, detectorModel, parsingModel, "onnxruntime-cpu",
    session, tokenizer, readingOrderOptions: order);
var parsed = await document.RunAsync(OpenCvImageSource.FromFile(imagePath));
var page = parsed.GetRequired<PaddleDocumentVisionLanguageRoutedPageResult>(
    PaddleDocumentModule.VisionLanguageParsing);
foreach (var region in page.ReadingExecutions)
    Console.WriteLine($"sourceIndex={region.RegionIndex}; status={region.Status}; text={region.Result?.Text}");
```

| 策略 | 适用场景与行为 |
|---|---|
| `SourceOrder` / 空配置 | 上游已提供可信顺序，直接保持来源顺序 |
| `TopToBottom` | 单栏页面，按上边界、阅读方向起始边、原索引排序 |
| `RecursiveXyCut` | 正向多栏文档，优先沿贯通的垂直空白分栏；跨栏标题/表格阻挡栏间隙时，沿水平空白分区后递归 |

两个间隙参数始终相对**整页**宽/高，默认分别为 2% 和 0.5%；不是相对当前子区，也不是字符间距阈值。`rightToLeft` 反转栏顺序和水平并列项，上下方向仍保持从上到下。没有可用空白、区域重叠或达到最大递归深度时，剩余区域按 `TopToBottom` 排列。几何模式拒绝非有限、空、完全位于页外的框；部分越界框只对排序几何裁边，原 `SourceRegion.Bounds` 不变。

`Executions` 和 JSON `payload.regions` 始终保持原索引，`ReadingOrder` / JSON `payload.readingOrder` 是包含所有区域的索引排列，`ReadingExecutions` 是相同执行对象的阅读视图。失败与跳过项同样保留，因此不能将阅读位置当成原 `RegionIndex`。Markdown 按阅读视图导出，仍标注原索引；整页顺序与多页输入顺序分别管理。

应用有自己的排序模型时，可直接调用 `PaddleDocumentReadingOrder.GetIndices(layout.Regions, pageSize, order)`，或给 `PaddleDocumentVisionLanguageRoutedPageResult` 的 `readingOrder` 参数传入自定义排列。构造时会复制并检查索引必须完整、唯一且在范围内；不接受静默丢弃跳过项或重复区域。

该功能是可配置的几何启发式，已有多栏/跨栏/方向/导出与原生适配器合同测试，**不是官方阅读顺序模型或独立标注集准确率结论**。任意重叠布局、竖排、复杂浮动插图和表格单元格内部顺序需另行处理；排序不会修复检测漏检、去重、改变 VL 文本或调整模型精度。

页面并发和单个视觉 Session 的内部并发是两个独立参数，必须联合调优。更多并发不一定更快：CPU 线程争用、OpenVINO request 数量、GPU 显存和页面任务分布都会影响 P95。保持输入顺序时使用 `RunManyConcurrentAsync` 的有界并发；固定 batch-one 模型通过独立 Session 池处理，不要并发写入同一个 request 或自回归 Session。部署前在目标设备比较串行、页面并发和真 Batch，并记录峰值内存。
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

SLANeXt Decoder 在结构序列没有选出 `<EOS>` 时会保留已解码的 markup，同时追加 `missing-eos:sequence-may-be-truncated` warning。调用方应把该 warning 视为截断风险，而不是把“有 HTML 输出”误判为完整表格；完整序列仍需要结合模型的 EOS、最大长度和任务级质量指标检查。

面向使用者的 [PP-Structure 表格案例](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/samples/02-visual/paddle-structure-table/README.md) 演示分类 → 单元格检测 → SLANeXt 结构 → PP-OCR 逐格识别。分类结果负责选择 wired/wireless 模型；只有结构槽位和检测框能够可靠对齐时才执行逐格 OCR，歧义、跨行单元格或越界几何会保留结构结果并记录跳过原因。示例支持 ORT CPU 与 OpenVINO CPU，并导出页面 JSON、HTML、对齐和单元格 OCR 结果。

Chart2Table、公式与印章使用相同的 `PaddleDocumentDependentStage` 接入页面 Pipeline，分别写入 `PaddleDocumentChartResult.StructuredData`、`PaddleDocumentFormulaResult.Latex` 和 `PaddleDocumentSealResult`。这些结果类型保留页码、区域来源和输入身份；是否通过准确率门禁仍由应用自己的标注集决定。

公式评估可复用内置比较器，而不是在每个项目重复实现字符串指标：

```csharp
PaddleDocumentFormulaQualityResult quality =
    PaddleDocumentFormulaQualityEvaluator.Compare(referenceLatex, formula.Latex);

Console.WriteLine(
    $"CER={quality.NormalizedTextAccuracy.CharacterErrorRate:P2}, " +
    $"token-error={quality.TokenErrorRate:P2}, command-F1={quality.CommandF1:P2}");

if (!quality.ActualBalancedDelimiters)
    Console.WriteLine("prediction has unbalanced delimiters");
```

该 API 提供原文/空白规范化比较、Unicode CER、有序词法 token error、LaTeX 命令 Precision/Recall/F1 以及分组和环境闭合诊断。它不是 TeX 渲染器，也不能证明数学语义等价；需要视觉或语义质量时，在独立评测工具中加入渲染和人工真值。
## 模型与后端边界

模型目录和 Release 下载按单个工件组织，不要求用户下载整个 PaddleOCR 集合。使用 `PaddleOcrReleaseClient` 或 [ModelFactory](modelfactory-getting-started.md) 获取模型、字典、tokenizer 和 manifest；创建 Session 前校验全部文件。PP-OCR 核心模型见 [OCR 教程](visual-ocr.md)，本页任务对应的精确 ID 见[官方模型目录](model-catalog.md)。

后端状态必须按“精确模型工件 + 任务 + 后端”判断：

| 后端 | 当前使用原则 |
| --- | --- |
| ONNX Runtime | 主要兼容基线；CPU/CUDA 仍需分别验证 |
| OpenVINO | 多数 CPU 路径可用；SLANeXt 原始 `Loop` 图需选择 Release 中独立的 `*-openvino-compat.onnx` 工件 |
| OpenCV DNN | 适合已验证的分类、检测和部分 OCR 图；Importer 不支持的动态/生成式图明确标记为不支持 |
| TensorRT | 在目标设备从 ONNX 构建 Engine；Engine 不可跨 GPU、TensorRT 版本或 profile 假定可移植 |

当前已提供方向分类、版面区域、表格分类/单元格/结构、UVDoc、公式、印章、Chart2Table 与 PaddleOCR-VL 的任务级 Profile、decoder 或编排入口。某个 Profile 存在只说明合同已实现，不等于所有后端都已通过；权威状态见[模型与后端验证矩阵](../model-backend-verification-matrix.md)。

### SLANeXt

原始 wired/wireless ONNX 在当前 OpenVINO `Loop` importer 中不能直接加载。Release 提供独立 SHA-256 的兼容图，保持原始资产不变；应用必须显式选择兼容模型 ID。TensorRT 当前精确图在 recurrence shape 处无法创建 Engine，矩阵标为不支持，不能用 OpenVINO 兼容图的结果外推。

### Chart2Table 与公式

Chart2Table 使用视觉编码、token embedding、自回归 decoder 和 lm head 组成 Bundle，必须来自同一导出批次并共同校验。ORT、OpenVINO 与已验证 TensorRT 组合按矩阵选择；OpenCV DNN 不作为生成式 Bundle 的默认路径。

公式模型的 tokenizer、EOS、最大生成长度和 LaTeX 后处理属于模型合同。结果可渲染不等于数学语义正确；质量评估需要人工真值或明确的结构/语义指标。部分公式图包含 TensorRT/OpenCV 当前不支持的控制流算子，应使用已验证后端，不要改写 decoder 掩盖 importer 失败。

## 性能与质量复现

完整页面性能应分别记录页面预处理、版面、区域裁剪、OCR/表格/公式/印章子任务和结果合并。单个模块的 P50/P95 不能冒充整页耗时。推荐协议：

1. 固定源码 commit、模型和输入 SHA-256；
2. 记录后端/runtime、设备、精度、动态 profile、batch 与独立 Session 数；
3. 先做结果合同或人工真值检查，再执行 5 次预热和 50 次测量；
4. 生成式任务同时记录 Token 数、EOS/截断和最大长度；
5. 报告 P50/P95、峰值内存以及 CPU/GPU 电源和锁频状态。

公开设备数据见[设备性能实测](device-performance-benchmarks.md)。功能复现优先运行对应 `samples/02-visual` 示例；PaddleOCR 批量基准使用 [DeploySharp.PaddleOcrBenchmark](https://github.com/guojin-yan/DeploySharp/blob/DeploySharpV2.0/tools/DeploySharp.PaddleOcrBenchmark/README.md)。测试输出写到调用方指定的仓库外目录，不把逐次 JSON、TRX 或本机绝对路径提交到文档。

## 常见问题

### 为什么下载成功仍不能运行

下载只证明工件完整。还要确认 Profile 的输入输出名称、shape、字典/tokenizer、目标后端 runtime 和设备插件。TensorRT 还需要在目标设备构建与当前 profile 匹配的 Engine。

### 为什么表格只有 HTML，没有像素单元格

SLANeXt/OTSL 结构 token 描述逻辑行列与合并关系，不自动提供每个单元格的像素框。需要几何时组合表格区域、单元格检测器和结构结果，并保留各自坐标空间。

### 多页任务如何并发

页面之间可以使用有界并发；同一页面内先完成版面，再按区域路由。自回归 Session、SAM/VLM 状态和非线程安全 request 不应被多个区域并发共享；需要吞吐时创建有限的独立 Session 池。

### 如何处理某个后端不支持

先用 ORT 基线确认工件与前后处理，再定位到 importer、输入绑定、执行或 decoder。确认是第三方 importer/算子限制后，在矩阵标为 `✗` 并选择其他后端；没有实测证据时标 `△`，不要从相近模型推断。
