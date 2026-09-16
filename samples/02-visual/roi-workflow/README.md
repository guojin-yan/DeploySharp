# ROI workflow / ROI 完整工作流

This sample demonstrates a complete backend-neutral ROI workflow: normalized polygon and exclusion-zone configuration, immutable snapshot replacement from JSON, source-space Detection filtering, video enter/line-crossing event processing, and bounded lazy-frame execution with `VisualRoiVideoRunner<TFrame>`. It intentionally uses canonical test detections, so it runs without downloading a model or native runtime; replace the `DetectionResult` construction and tracker callback with a real `VisualPipeline` result and application tracker.

本案例演示完整的后端无关 ROI 工作流：归一化多边形和排除区配置、JSON 原子快照替换、源图 Detection 过滤、视频进入/穿线事件处理，以及使用 `VisualRoiVideoRunner<TFrame>` 进行有界延迟帧运行。示例使用规范测试检测结果，因此无需下载模型或 native runtime 即可运行；接入真实项目时，将 `DetectionResult` 构造和跟踪器回调替换为真实 `VisualPipeline` 推理结果及应用跟踪器即可。

Run from the repository root / 在仓库根目录运行：

```powershell
dotnet run --project samples/02-visual/roi-workflow/VisualRoiWorkflow.csproj -c Release
```

The output includes the installed snapshot version, serialized configuration size, retained detections, emitted event count, video frame/event totals, and peak active-track count. The sample is a contract demonstration, not a performance benchmark; use the model benchmark workflows for backend latency measurements.

输出包含快照版本、序列化配置大小、保留检测数、事件数、视频帧/事件总数和活动 Track 峰值。本案例用于演示合同和资源边界，不是性能基准；后端延迟请使用模型基准工具测量。
