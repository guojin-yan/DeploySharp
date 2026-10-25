# PaddleOCR-VL 混合页面阅读顺序：实现与验证

日期：2026-10-25。开发基线：`c5707f8fb3b87fdcfd246dcc4b2d9ed1bfcf2a14`，实际测试使用本次修改后的工作树。范围是几何排序、结果索引、导出及 OpenCV 适配器；本轮没有重新运行真实 VL 权重，不改变后端模型支持结论。

## 行为

`PaddleDocumentReadingOrder` 提供原顺序、从上到下和递归 XY-cut。XY-cut 优先沿垂直空白分栏，没有垂直贯通空白时沿水平空白分区；没有可用间隙或达到深度限制时按上边界/阅读方向起始边/原索引排序。默认间隙占整页宽/高的 2%/0.5%，默认最大深度 32，支持左右读栏。

混合任务阶段、OpenCV 页面适配器及检测→VL 共享底图入口通过 `readingOrderOptions` 显式开启。默认保留来源顺序。执行/结果/JSON 区域保留原 `RegionIndex`，另提供 `ReadingOrder`/`ReadingExecutions`，Markdown 使用阅读顺序。所有跳过/失败项保留；自定义排列必须完整、唯一、范围合法且被复制。原公共构造器/工厂签名保留转发重载。

只处理框和索引，不修改原框、不复制像素/张量、不重新生成文本。几何模式在生成前校验框，排序几何可裁至页面。重叠区域、复杂浮动版面、竖排和标注集准确率不在此次验收内；并非官方 PaddleOCR 阅读顺序模型。

## 验证结果

| 检查 | 结果 | 范围 |
|---|---|---|
| 排序与 VL 路由专项 | 33 passed / 0 skipped / 0 failed | 乱序双栏、错行、跨栏标题/表格/页脚、RTL、阈值、裁边、稳定回退、深度、取消、异常及导出 |
| OpenCV 新功能定向合同 | 10 passed / 0 skipped / 0 failed | 原生解码/crop 共享、tiny ONNX 检测、阅读配置传递、多页并发、异常与取消；生成使用注入委托 |
| Visual 完整回归，配置本机固定 tokenizer | 695 passed / 7 optional skipped / 0 failed | 702 项；可选资产跳过项含 Whisper、长行 trace、Chart2Table fixture 和 VL 官方 RGB fixture，不计入模型通过证据 |
| OpenCV VL 合同回归 | 21 passed / 3 optional skipped / 0 failed | 3 项依赖外部真实布局/VL资产；没有把跳过项写成实测成功 |
| Visual 16 TFM Release 编译 | 成功，0 error / 14 warning | net46 至 net10.0 的全部既定目标；警告来自依赖包对旧 TFM 的支持声明 |
| .NET 8 / win-x64 文档示例 | 成功，0 error / 28 warning | 同类依赖 TFM 警告；未在本机执行真实 VL 权重 |

原始日志/TRX：`E:\DeploySharpEvidence\paddleocr-vl-reading-order-20261025`。构建首次使用不合适的全局 TFM 覆盖、随后并行写相同输出出现文件占用，均未计成功；最终正常目标图和串行构建通过，不修改项目目标框架。

维护范围本地 Markdown 链接检查 `1205/0`（检查/断链），全局 `git diff --check` 通过；链接检查不验证远程 URL 或 Markdown 锚点。

| 文件 | SHA-256 |
|---|---|
| `PaddleDocumentReadingOrder.cs` | `401b1fd62e3bd47bfa7179d5c3da82d3d7fff84fb48f5b23732552a568aac63f` |
| Visual 测试 DLL | `281dc3e3f149c7ccf8126ef8546b483a133cc67de0f8f2d9cfed4e95994f62a1` |
| OpenCV 测试 DLL | `a89b1be5ef4c152e83772943ae4b6c731894c33dc64815b12e3665d8cae43467` |
| `visual-regression-with-tokenizer.trx` | `fb2afdc177676dadb93613e47655f5d793026bb5cb86ccbe38edf576030a8d3f` |
| `vl-opencv-regression.trx` | `0124c292427826390367e0047507c6e002e5c3a74dc124166836cf9b673b4561` |

## 复现

在仓库根目录运行，先完成项目正常 restore。测试目标为 .NET 10；VL 生产 API 位于既有 .NET 8/9/10 包资产，通用几何工具也在其它 Visual TFM 编译。

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT = '<固定 1.6 tokenizer 目录>'
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -c Release --no-restore -m:1
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -c Release --no-restore -m:1 --filter 'FullyQualifiedName~PaddleOcrVl&TestCategory!=ExternalIntegration'
dotnet build src/DeploySharp.Visual/DeploySharp.Visual.csproj -c Release --no-restore -m:1
dotnet build samples/02-visual/paddleocr-vl-document/PaddleOcrVlDocument.csproj -c Release --no-restore -m:1
```

调用和 JSON 配置见[使用教程](../../../../docs/articles/visual-paddle-structure.md#多栏阅读顺序与稳定区域索引)及[文档示例](../../../../samples/02-visual/paddleocr-vl-document/README.md#可选的多栏阅读顺序)。既有 [Linux ORT 六任务](paddleocr-vl-managed-csharp-ort-linux-20261024.json)、[Linux OpenVINO CPU 六任务](paddleocr-vl-openvino-linux-20261024.md)、[Windows ORT 六任务](paddleocr-vl-windows-ort-20261025.md)和 [Windows OpenVINO 六任务](paddleocr-vl-windows-openvino-20261025.md)与 [CUDA 显存阻断](paddleocr-vl-cuda-head-group-20261024.md)仍分别保留原始边界。独立页面质量、其它任务语义、正式性能和实际模型 Release 下载仍待完成。
