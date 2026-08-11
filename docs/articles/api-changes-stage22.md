# Stage 22 API changes / 阶段 22 API 变更

## Visual / Visual

- Added immutable `PromptableSegmentationProfile`, exact `PromptableSegmentationArtifactContract`, role-bound `PromptableSegmentationArtifactBundle`, SAM v1 tensor map, prompt capabilities, and non-executable video-state blocker contract. / 新增不可变 Profile、精确子工件合同、角色绑定 Bundle、SAM v1 Tensor Map、Prompt 能力与不可执行视频状态 blocker 合同。
- Added typed `PromptPoint`, `PromptableSegmentationPrompt`, image/artifact identity, owned embedding summary, low-resolution logits/feedback, prompt provenance, timing, candidate, and `PromptableSegmentationResult`. Canonical masks/RLE/geometry remain existing `InstanceSegmentationResult` types. / 新增 typed Prompt、Identity、Embedding 摘要、低分辨率 Logit/Feedback、来源、Timing 与结果；规范 Mask/RLE/Geometry 继续复用现有实例分割类型。
- Added `PromptableSegmentationImageSession` with synchronous/asynchronous set-image and predict, clear, cached embedding identity, cancellation, single-writer concurrency, and deterministic two-session disposal. / 新增双 Session 图像会话，覆盖同步/异步、clear、缓存 Identity、取消、单写并发与确定性释放。
- Added stable `DS-VISUAL-4501` through `DS-VISUAL-4505` contract, capacity, state, identity, and concurrency mappings. / 新增五个稳定错误映射。

## Visual.OpenCV / Visual.OpenCV

- Added `OpenCvResizeMode.LongestSidePadBottomRight` and half-up rounding, plus `OpenCvPromptableSegmentationInputFactory` file/bytes/source entry points for official SAM v1 RGB normalization. / 新增最长边底/右补零、half-up 舍入及 SAM v1 file/bytes/source 输入工厂。
- The encoded source SHA is the cache identity and the single `ImageTransform` maps all prompt coordinates. / 编码源 SHA 是缓存 Identity，同一 Transform 映射全部 Prompt 坐标。

## ModelFactory / ModelFactory

- `ModelCatalogArtifact` now optionally carries bundle role/version, capabilities, and required sidecar IDs. JSON serialization/schema and validation preserve these fields and reject missing required sidecars. / Artifact 可选携带 Bundle Role/Version、Capabilities 与必需 Sidecar ID；序列化、Schema 与验证同步支持并拒绝缺 Sidecar。
- `ModelQuery` can filter exact model version and capability. `ModelBundleQuery` plus `ModelCatalogQuery.SelectBundles` selects complete multi-artifact bundles by family/version/task/capability/format/backend/precision and rejects missing roles, mixed versions, absent sidecars, or incomplete conversion records with `model-factory.bundle-invalid`. / 新增完整多工件 Bundle 查询与稳定拒绝规则。

No public API claims SAM 2/SAM 3 video execution, no single-model package was added, and TensorRT remains unimplemented. / 不公开宣称 SAM 2/3 视频执行，不新增单模型包，TensorRT 仍未实现。
