# ADR 0017: OCR polygon, crop, CTC, and model-suite boundary / OCR polygon、裁剪、CTC 与模型套件边界

- Status / 状态: Accepted / 已接受
- Date / 日期: 2026-08-05

## Context / 背景

OCR combines two independently deployable models, image-library-specific perspective operations, model-specific polygon semantics, a character set, and sequence decoding. Putting these concerns in Core or a backend would leak vendor types and duplicate selection/lifecycle logic. Treating detector and recognizer files as unrelated downloads would lose suite integrity. / OCR 组合两个可独立部署模型、图像库特定透视操作、模型特定 polygon 语义、字符表与序列解码。将这些内容放入 Core 或后端会泄漏 vendor 类型并重复选择/生命周期逻辑；将检测和识别文件视为无关下载又会失去套件完整性。

## Decision / 决策

1. Core remains unchanged. Visual owns immutable OCR domain results, explicit polygon and CTC schemas, deterministic managed decoding, bounded orchestration, and the `IOcrImageInput` extension point. / Core 保持不变；Visual 拥有不可变 OCR 结果、显式 polygon/CTC Schema、确定性 managed 解码、有界编排与 `IOcrImageInput` 扩展点。
2. Visual implements exact bounded convex polygon IoU/NMS and explicit TL/TR/BR/BL crop roles. It never guesses exporter point order. Alpha.1 accepts explicit polygon/score output and does not advertise probability-map contour extraction. / Visual 实现精确有界凸 polygon IoU/NMS 与显式角点角色，绝不猜测导出器点序；alpha.1 接受显式 polygon/score 输出，不声明 probability-map 轮廓提取。
3. Visual.OpenCV decodes once and owns native source/crop resources. It implements perspective warp, configured right-angle rotation, resize/pad, color/normalization/layout, then returns copied managed tensors. OpenCV types do not enter Visual contracts. / Visual.OpenCV 单次解码并拥有 native 源图/crop 资源，执行透视、配置的直角旋转、resize/pad、颜色/归一化/layout 后返回复制的 managed tensor；OpenCV 类型不进入 Visual 契约。
4. Greedy CTC declares layout, blank, unknown, repeat, softmax, character-set and confidence semantics. It uses lowest-index tie breaking and Unicode scalars. Beam search and implicit blank placement are unsupported. / Greedy CTC 显式声明 layout、blank、unknown、repeat、softmax、字符表与置信度语义，使用同分最小索引和 Unicode scalar；不支持 beam search 或隐式 blank 位置。
5. `OcrPipeline` creates two `VisualPipeline` sessions through the existing Core registry. One timeout and cancellation budget spans both stages. Concurrency, result size, workspaces, batches, and disposal are bounded. / `OcrPipeline` 通过现有 Core registry 创建两个 `VisualPipeline` session；一个超时/取消预算覆盖双阶段，并发、结果、workspace、batch 与释放均有界。
6. Existing ModelPack multi-artifact support is sufficient: one OCR suite model ID has uniquely named detector/recognizer artifacts per format, while versioned `deploysharp.ocr.*` extensions bind roles, profiles, character set and semantic versions. Character-set and IR sidecar files are individually integrity protected and appear only once in the manifest. No schema version change is required. / 现有 ModelPack 多工件能力足够：一个 OCR 套件 ModelId 按格式包含唯一命名的检测/识别工件，版本化扩展绑定角色、Profile、字符表与语义版本。字符表和 IR sidecar 分别受完整性保护，且在清单中只出现一次；无需升级 schema。

## Consequences / 影响

- Users can replace OpenCV with another `IOcrImageInput` without changing model/backend contracts. / 用户可用其他 `IOcrImageInput` 替换 OpenCV，而不改变模型/后端契约。
- Detection and recognition may use different registered backends without vendor leakage. / 检测与识别可使用不同已注册后端，且不泄漏 vendor 类型。
- General concave polygons, probability-map morphology/contours, automatic orientation classification, beam search, and layout analysis require separate verified modules. / 一般凹 polygon、probability-map 形态学/轮廓、自动方向分类、beam search 与版面分析需要独立验证模块。
- Constant fixtures prove adapter contracts only. Production catalog admission requires the OCR AlgorithmVerified template, legal assets, official semantic parity, golden tolerance and performance evidence. / 常量夹具只证明适配器合同；正式目录准入需要 OCR AlgorithmVerified 模板、合法资产、官方语义一致性、黄金容差与性能证据。
