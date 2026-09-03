# 异常检测与背景移除

本页汇总两类输出不同的视觉任务：异常检测返回图像级分数和可选像素图，背景移除返回连续 alpha 蒙版。它们都需要使用与导出模型匹配的 Profile，不能把检测阈值或蒙版后处理混用。

## 异常检测

AnomalyPipeline 负责准备输入、调用后端和解码分数图。PaDiM、PatchCore 等模型通常还需要外部特征统计量或参考库；这些数据必须与模型版本和输入尺寸一致。

~~~csharp
using var pipeline = new AnomalyPipeline(
    registry, selection, request,
    new SessionOptions(maxConcurrency: 1));
AnomalyDetectionResult result = pipeline.Run(input);
Console.WriteLine($"score={result.ImageScore}; anomalous={result.IsAnomalous}");
~~~

阈值、热图尺寸和二值 mask 规则由 AnomalyDecoderOptions 固定。批量接口只有在导出图声明支持动态 batch 时才可使用；否则应通过独立 session 池并发处理 batch-one 输入。

## BRIA RMBG

背景移除模型返回 Float32 alpha，调用方可以直接用于合成或保存。RMBG 1.4 与 2.0 的输入尺寸、归一化和输出布局不同，应使用各自 Profile；不能用普通语义分割 decoder 代替。

当模型导出支持动态 batch 时，可把多个图像放入 BackgroundRemovalBatchResult；默认 Profile 仍为 batch-one。GPU 后端可选择设备侧 alpha 前处理，但结果必须在同一 ImageTransform 下还原到源图尺寸。

## 输入、生命周期与性能

图像工厂应只解码一次，长期复用可复用的预处理缓冲和 Session。模型推理、热图/轮廓后处理、蒙版还原分别计时，避免把图像编码、模型加载和首次 JIT 混入稳态结果。需要视频或大量图片时，使用 RunManyAsync、RunPrefetchedAsync 或有限的 InferenceBatchScheduler。

具体模型与后端状态以[模型支持指南](model-support.md)和[模型后端验证矩阵](../model-backend-verification-matrix.md)为准；不同设备的完整耗时见[设备性能实测](device-performance-benchmarks.md)。
