# 模型接入位置与交付清单

以下路径相对于 Git 根目录；按当前模型和任务选择，不要求修改每个文件。先复用同类模型，不从历史审计报告猜测代码状态。

## 一、改动通常落在哪里

| 内容 | 优先检查的位置 |
| --- | --- |
| 版本、依赖、TFM 与公共 API 注释规则 | `Directory.Build.props`、`Directory.Packages.props`、受影响 `.csproj` |
| 视觉任务、Profile 和标签合同 | `src/DeploySharp.Visual/Profiles/`、`src/DeploySharp.Visual/Models/` 中的同类实现 |
| 用户扩展入口 | `src/DeploySharp.Visual/Extensions/VisualModelDefinition.cs` |
| 可配置预处理 | `src/DeploySharp.Visual/Preprocessing/VisualPreprocessingOptions.cs` |
| 通用解码与模型专用解码 | `src/DeploySharp.Visual/Decoding/`、对应 `Models/` 子目录 |
| 图像读入、颜色、缩放和裁剪 | `src/DeploySharp.Visual.OpenCV/` |
| CUDA 视觉处理 | `src/DeploySharp.Visual.TensorRT/` |
| 通用张量、结果与调度 | `src/DeploySharp.Core/`；只有缺少通用合同才修改 |
| 通用后端 | `src/DeploySharp.Backend.*/`；不放模型专用阈值或图片业务逻辑 |
| OCR/文档流水线 | `src/DeploySharp.Visual/Ocr/`、`src/DeploySharp.Visual/Models/PaddleOcr/` |
| ROI、投影、合并与滑动窗口 | `src/DeploySharp.Visual/Roi/`、`src/DeploySharp.Visual/Detection/` |
| LLM 与多模态 | `src/DeploySharp.LLM/`、`src/DeploySharp.Multimodal/` 和对应任务的实际实现 |
| 官方 ModelFactory 主目录 | `src/DeploySharp.ModelFactory/catalog/deploysharp-official-catalog.json` |
| ModelPack 格式与校验 | `src/DeploySharp.ModelPack.Json/` |
| 模型清单、转换与交付工具 | 对应 `eng/models/` 子目录；测试图工具在 `eng/test-assets/` |
| 通用模型执行示例 | `samples/06-models/release-inference/`；检查支持分派是否需要扩展 |
| 每模型说明 | `samples/06-models/cases/`，目录名将模型 ID 的 `/` 替换为 `--` |
| 完整任务示例 | `samples/02-visual/` 或对应任务目录；多阶段任务不能只演示独立组件 |
| 合同、解码与集成测试 | 对应 `tests/DeploySharp.*.Tests/`，例如 `DeploySharp.Visual.Tests`、`DeploySharp.Visual.OpenCV.Tests` |
| 模型目录测试 | `tests/DeploySharp.ModelFactory.Tests/`；新格式还需检查 ModelPack 测试 |
| 性能工具 | 复用 `tools/DeploySharp.VisualBenchmark/` 或对应任务工具，先确认它确实执行新模型 |
| 支持与教程 | `docs/articles/model-support.md`、相应任务教程、`docs/articles/toc.yml` |
| 精确后端状态 | `docs/model-backend-verification-matrix.md`，是 case 后端表的事实来源 |
| 平台承诺 | `eng/platform/platform-support.json`、`docs/articles/platform-support.md`；只在有新增平台证据时调整 |
| 用户可见模型总览 | `README.md`、`README_cn.md` |

PaddleOCR 另有 `PaddleOcrModelCatalog`、`PaddleDocumentModelCatalog`、`PaddleOcrReleaseArtifacts` 和专用 Release Client。完整集合使用 `models-paddleocr`，ModelFactory 主目录的部分 PP-OCR 工件来自另一套清单；先检查真实文件身份，不混用同名模型，也不强行把所有 PaddleOCR 模型重复加入主目录。

## 二、开始时需要掌握的信息

| 项目 | 最少要确定什么 |
| --- | --- |
| 模型 | 完整名称、变体、任务、工件版本及本地/公开获取位置 |
| 输入 | 所有名称、dtype、layout、shape、动态范围和辅助数据 |
| 图像处理 | Resize/Letterbox/CenterCrop、插值、颜色、scale/mean/std、padding、坐标变换 |
| 输出 | 名称、shape、 logits/probabilities、解码布局、坐标单位及是否已经包含 NMS |
| 资源 | 类别、词典、Tokenizer、配置、外部权重及多阶段图之间的配对关系 |
| 执行 | 可用后端、设备、目标框架、是否有状态、真实 Batch 和并发约束 |
| 结果 | 用户要拿到的类型、原图坐标或任务结构、保存与展示方式 |

已经能从官方处理器、工件和项目实现确定的内容直接检查，不要求用户填写整张表。只有关键歧义才逐个询问。

## 三、验收要检查什么

- **输入与解码**：真实张量合同；空输出、多输出、数值和边界；非正方形图像的坐标与补边；按任务检查 NMS、Mask、关键点、文本或生成停止语义。
- **可配置性**：默认值匹配官方处理器；用户可替换模型路径、标签、输入尺寸和允许的预处理；禁止不合法组合而不是静默产生偏差。
- **Batch**：如果工件允许，验证 Batch=1、较大 Batch、尾批和逐图结果分离；固定 Batch 明确报错或走已有单图并发，不能把拼输入写成已经支持。
- **异步与生命周期**：使用已有调度器检查结果关联、取消/失败、Owned/Borrowed 输入以及输出有效期；有状态模型检查复用前的状态清理。
- **ROI 与大图**：有适用任务投影器时验证局部到原图、越界区域和过滤/合并；只有检测任务才要求评估通用滑动窗口入口。
- **真实结果**：至少一个精确后端端到端通过；多阶段任务需完整链路。保留官方参考或合理的金样与容差，不手工修正输出制造成功。
- **后端覆盖**：在现有可用环境中运行适用组合，记录导入/建会话/绑定/执行/解码分别在哪一步失败。ORT CUDA 与 CPU 分开记录；矩阵没有对应列时在该任务的验证说明中补充，不擅自破坏生成器表结构。
- **开销**：检查不必要的复制、分配和重复解码。新模型只需有针对性的代表输入；只有声称优化收益时才要求可比较的基线与改后数据，不自动扩大成长时间 soak。
- **交付**：代码、下载和可复现案例分别验收。没有公开资产时，不得填写 Release 校验 PASS 或把尚不存在的 URL 标为可下载。

## 四、现有同步与验证命令

从 Git 根目录运行；只选择与本次改动有关的项。修改源码时用 `ProjectReference` 验证新增能力；演示已发布包时使用对应版本 `PackageReference`，不能混淆。

构建与测试：使用受影响项目的实际路径；不要对尚未构建的代码使用 `--no-build`。下面是涉及通用 Visual 时的例子：

```powershell
dotnet restore src/DeploySharp.Visual/DeploySharp.Visual.csproj --locked-mode
dotnet build src/DeploySharp.Visual/DeploySharp.Visual.csproj -c Release --no-restore
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release --filter 'TestCategory!=ExternalModels'
```

按改动追加 OpenCV/TensorRT/ModelFactory 或任务测试。外部模型测试读取各自约定的模型路径/开关并实际运行；没有环境导致的 skip 不能当作推理通过。新增依赖时更新受影响锁文件，随后重新 locked restore，不为通过还原统一升级无关依赖。

主目录有变化时，更新源 JSON 后再生成目录表：

```powershell
pwsh -NoProfile -File eng/model-catalog/Update-ModelCatalogTable.ps1
pwsh -NoProfile -File eng/model-catalog/Update-ModelCatalogTable.ps1 -Check
```

先根据真实结果编辑精确后端矩阵，再同步案例后端表。脚本名字包含 `New`，实际**只读取矩阵并更新案例**，不会自动执行模型或新增成功状态：

```powershell
pwsh -NoProfile -File eng/model-catalog/New-ModelBackendVerificationMatrix.ps1
pwsh -NoProfile -File eng/model-catalog/New-ModelBackendVerificationMatrix.ps1 -Check
```

主目录模型的 Release 和 ModelPack 已发布且验证记录齐备后，检查案例覆盖；该脚本要求每个 catalog ID 都有案例、真实校验记录和可运行命令，不应用伪造 PASS 绕过：

```powershell
pwsh -NoProfile -File eng/model-catalog/Test-ModelSampleCoverage.ps1
```

需要核验已发布资产时，可使用 `eng/model-catalog/Test-PublishedModelCases.ps1`，以 `-ModelId` 限定本次模型；`-OutputPath` 和 `-CachePath` 指向仓库外的本次工作目录。`-UpdateReadmes` 会修改案例，应先检查其它会话的改动。该命令检查下载/清单完整性，不代替真实推理。

平台状态变化才检查平台配置；仅增加某个模型的运行证据，不代表整个后端、平台或 TFM 可以升级：

```powershell
pwsh -NoProfile -File eng/platform/Test-PlatformSupportMatrix.ps1
```

新增公共 API 使用 `eng/docs/Test-BilingualApiDocs.ps1 -DocumentationFile` 检查对应构建输出 XML。文档变化校验本地链接、示例 API 和命令；导航或 API 页面变化时运行现有文档构建：

```powershell
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
git diff --check
```

上述检查通过后，按当前任务范围结束验证。不要因该清单存在就重新测试所有模型、所有设备或全部发布包。

## 五、交付时逐项说明

| 交付项 | 完成依据 |
| --- | --- |
| 内置支持 | 公共入口、前处理、推理绑定、解码与结果类型实际可用 |
| 任务组合 | 多阶段流程已连通；单组件与完整流水线结论分开 |
| 模型获取 | 可用本地路径，或真实可下载且配套完整的公开资产 |
| 示例 | 每模型命令、输入、完整结果保存及用户替换方式 |
| 验证 | 精确工件、版本、输入和后端的真实结果及必要容差 |
| 性能 | 测过才给数字；环境、负载与计时协议明确 |
| 文档 | 模型列表、任务教程、矩阵、案例和适用版本已对齐 |
| Git | 仅本任务改动提交；明确是否已推送和仍待完成的项 |

如果只完成合同/本地试验，或资产、特定后端和流水线尚未完成，直接报告具体状态；不为了“完成”修改已有发布事实或删除阻断。
