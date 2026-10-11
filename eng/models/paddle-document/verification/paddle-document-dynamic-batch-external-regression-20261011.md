# PP-Structure 动态 Batch 外部回归（2026-10-11）

本记录汇总 Windows 主机上 `PaddleDocumentDynamicBatchIntegrationTests` 的一次完整外部运行。它证明的是动态 Batch 的输入绑定、批内行隔离、Decoder 映射以及已知后端边界；不等同于准确率、吞吐排名、正式 P50/P95 性能或所有后端支持。

## 运行结果

| 项目 | 结果 |
| --- | --- |
| 测试项目 | `tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj` |
| TFM / 配置 | `net10.0` / `Release` |
| 测试筛选 | `FullyQualifiedName~PaddleDocumentDynamicBatchIntegrationTests` |
| 外部开关 | `DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL=1`、`DEPLOYSHARP_EXTERNAL_MODELS=1` |
| 设备 | Windows 10.0.26200 x64，16 logical processors，.NET 10.0.12 |
| 测试用例 | **22/22 通过，0 失败，0 跳过** |
| 精确模型×后端检查 | **59** |
| 执行通过 | **46** |
| 明确不支持 | **13**，均为 OpenCV DNN 动态 Batch 边界 |
| TensorRT | 本轮未运行，不计为通过 |

## 覆盖范围

- ORT CPU：PP-LCNet 文档方向/表格分类、PP-DocLayout/PP-DocBlockLayout、RT-DETR 版面和表格单元格、SLANeXt wired/wireless、UVDoc、六个公式模型等，共 24 个精确检查通过。
- OpenVINO CPU：与 ORT 对应的 PP-Structure 动态输入/Decoder 合同，共 18 个精确检查通过。原始 SLANeXt 图仍受 `Loop` importer 限制时使用单独哈希的兼容图；这不改变原始图的状态。
- OpenCV DNN CPU：PP-LCNet 分类 2 个模型、PP-OCRv4 印章 mobile/server 2 个模型的动态 Batch 合同通过；版面、表格单元格和公式的 13 个精确组合按 `DS-OCV-8004` 或 `DS-OCV-8002` 记录为不支持。
- TensorRT：本次没有动态 Batch 运行，不能从本报告推断 TensorRT Batch 支持。

22 个原始机器报告（包含每个模型、输入 shape、模型 SHA、逐行输出摘要和错误边界）位于同一测试输出目录；本仓库保留的逐项摘要和来源文件名见机器可读汇总 [`paddle-document-dynamic-batch-external-regression-20261011.json`](paddle-document-dynamic-batch-external-regression-20261011.json)。

## OpenCV 边界

OpenCV DNN 的失败是预期的、可复现的边界，不是测试框架失败：

- `DS-OCV-8004 / Requested blob not found`：PP-DocLayout、RT-DETR layout、RT-DETR table-cell 的 `forward_many` 动态 Batch 组合。
- `DS-OCV-8002`：六个公式动态导出在 OpenCV DNN 动态图导入/输入特化阶段不满足当前 importer 合同。

这些状态只针对报告中精确的 ONNX、OpenCV 5.0 runtime 和 batch=2 合同。单图 `✓` 不应被解读为动态 Batch `✓`，也不把失败外推到其它 OpenCV 版本。

## 边界与复现

这是一次 dirty worktree 上的 Windows 外部回归，不能替代 clean-checkout、人工标注质量集、5/50 受控性能、长时间稳定性、跨设备矩阵或 TensorRT Batch 实测。复现命令：

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_DYNAMIC_BATCH_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_EXTERNAL_MODELS = '1'
dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 -c Release --no-build --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentDynamicBatchIntegrationTests'
```
