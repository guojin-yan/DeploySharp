# ADR 0019: OCR Orientation and Single-Decode Correction

## Status / 状态

Accepted for `2.0.0-alpha.1` contract and preview fixtures. / 已接受，用于 `2.0.0-alpha.1` 契约和 Preview 夹具。

## Decision / 决策

`JYPPX.DeploySharp.Visual` owns the four-class orientation schema, explicit class-to-angle mapping, confidence rejection, result provenance, and the shared `VisualPipeline` lifecycle. `JYPPX.DeploySharp.Visual.OpenCV` owns image decoding, native rotation, pixel layout, and the corrected `IOcrImageInput`. / `JYPPX.DeploySharp.Visual` 负责四分类方向 Schema、显式类别到角度映射、置信度拒绝、结果来源和共享 `VisualPipeline` 生命周期；`JYPPX.DeploySharp.Visual.OpenCV` 负责图像解码、native 旋转、像素布局和纠正后的 `IOcrImageInput`。

The classifier never guesses class order. The contract records a clockwise correction mapping for `0/90/180/270`; logits use explicit softmax and probabilities are validated. Low confidence is `Rejected`, with no implicit zero-degree fallback. / 分类器绝不猜测类别顺序。契约记录 `0/90/180/270` 的顺时针纠正映射；logits 显式 softmax，概率经过范围和总和校验。低置信度返回 `Rejected`，不隐式回退为零度。

OpenCV decodes the encoded image once. The orientation input and detector input are prepared from the same decoded `Mat`; correction performs one `Rotate` call. Zero degrees transfers the owned `Mat` without a pixel copy. The corrected image then enters the existing OCR detector/recognizer pipeline, and `OcrResult` carries orientation provenance and both original/corrected sizes. / OpenCV 对编码图像只解码一次。方向输入和检测器输入来自同一个已解码 `Mat`；纠正只执行一次 `Rotate`。零度转移已拥有的 `Mat`，不复制像素。纠正图像随后进入现有 OCR 检测/识别 Pipeline，`OcrResult` 携带方向来源和原始/纠正后尺寸。

## Consequences / 影响

The Visual package remains independent of OpenCV and inference backends. OpenCV is optional and remains a separate package. Contract fixtures are `ContractVerified` only; no official OCR algorithm accuracy claim or ModelFactory official catalog entry is created. / Visual 包仍独立于 OpenCV 和推理后端，OpenCV 继续是可选独立包。契约夹具仅为 `ContractVerified`，不宣称正式 OCR 算法精度，也不写入 ModelFactory 官方目录。

The workflow restores authoritative polygons from corrected-image coordinates to original-image coordinates with the explicit inverse right-angle transform while preserving upright reading order. Crop-corner roles are not relabeled after rotation; the final result retains the authoritative original-space polygon. The preview fixture still does not substitute for vendor-specific orientation model documentation. / 工作流使用显式直角逆变换把权威 polygon 从纠正图坐标恢复到原图坐标，并保留正常阅读顺序；旋转后不错误重标裁剪角点角色，最终结果保留原图空间权威 polygon。Preview 夹具仍不能替代厂商方向模型文档。
