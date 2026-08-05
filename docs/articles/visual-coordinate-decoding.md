# Visual coordinates, decoding, and NMS / Visual 坐标、解码与 NMS

## Coordinate convention / 坐标约定

Visual uses finite `float` coordinates and half-open rectangles `[x, y, right, bottom)`. Width is `right - x`; touching edges have zero intersection. Source and model sizes use positive integer pixels. / Visual 使用有限 `float` 坐标和半开矩形 `[x, y, right, bottom)`。宽度为 `right - x`，仅边缘接触时交集为零。源图与模型尺寸使用正整数像素。

`ImageTransform.Resize`, `Letterbox`, and `Crop` record the forward source-to-model mapping and inverse model-to-source mapping. A detection decoder applies the inverse transform, clips to source bounds, and removes empty boxes. Do not apply a second scale in application code. / `ImageTransform.Resize`、`Letterbox` 和 `Crop` 记录源图到模型的正向映射及模型到源图的逆映射。检测解码器应用逆变换、裁剪到源图边界并移除空框。应用代码不要再次缩放。

## Classification / 分类

`ClassificationDecoder` accepts `[classes]` or `[1, classes]` Float32/Float64 outputs. `Logits` mode uses a numerically stable softmax by subtracting the maximum logit; `Probabilities` mode validates every value is in `[0,1]`. NaN and infinity are rejected. Results are ordered by descending score, then class index, and filtered by threshold/TopK. / `ClassificationDecoder` 接受 `[classes]` 或 `[1, classes]` 的 Float32/Float64 输出。`Logits` 模式通过减去最大 logit 实现数值稳定 softmax；`Probabilities` 模式验证每个值位于 `[0,1]`。NaN 和无穷大被拒绝。结果按分数降序、类别索引升序排列，再应用阈值和 TopK。

## Generic dense detection / 通用稠密检测

`DetectionDecoder` accepts `[candidates, fields]` or `[1, candidates, fields]`. Fields 0–3 contain a box in the declared `DetectionBoxFormat`; class scores start at `ClassScoreOffset`. Confidence is either the best class score or `objectness × best class score`. All confidence values must be in `[0,1]`. / `DetectionDecoder` 接受 `[candidates, fields]` 或 `[1, candidates, fields]`。字段 0–3 按声明的 `DetectionBoxFormat` 保存边界框；类别分数从 `ClassScoreOffset` 开始。置信度为最佳类别分数或 `objectness × 最佳类别分数`。所有置信度值必须位于 `[0,1]`。

NMS first sorts by descending score, class index, and original candidate index. Class-aware mode suppresses overlap only inside one class; class-agnostic mode suppresses across classes. A candidate is removed when IoU is greater than the configured threshold. This ordering makes equal-score results deterministic. / NMS 先按分数降序、类别索引和原始候选索引排序。ClassAware 仅在同类内抑制；ClassAgnostic 跨类别抑制。当 IoU 大于配置阈值时移除候选。该排序使同分结果保持确定性。

The generic decoder does not guess YOLO/SSD/vendor layouts. Register a dedicated `IVisualDecoder` and profile when a model transposes dimensions, separates boxes/scores, embeds post-processing, or uses task-specific semantics. / 通用解码器不会猜测 YOLO、SSD 或厂商布局。若模型转置维度、分离 boxes/scores、内嵌后处理或使用任务特定语义，应注册专用 `IVisualDecoder` 与 Profile。
