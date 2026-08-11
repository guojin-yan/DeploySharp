# Stage 22: Segment Anything family / 阶段 22：Segment Anything 模型族

- Added artifact-bound SAM/SAM 2/SAM 3 family, prompt, embedding, feedback, ownership, capacity, and video-blocker contracts in existing Visual packages. / 在现有 Visual 包新增模型族、Prompt、Embedding、Feedback、所有权、容量与视频 blocker 合同。
- SAM v1 ViT-B image encoder plus prompt/mask decoder runs on ORT and OpenVINO CPU, compares point+box+feedback source masks with the official predictor, and reuses existing mask/RLE/geometry results. / SAM v1 ViT-B 双子图在 ORT/OpenVINO CPU 运行并与官方 Predictor 比较 point+box+feedback 源图 Mask。
- Local SAM 2 external-data graphs have exact ORT/OpenVINO named-port evidence but are not promoted because Meta publishes no complete official native exporter and the local graph lacks feedback/video memory. / 本机 SAM 2 external-data 图有双后端端口证据，但因缺官方完整 native 导出及反馈/视频记忆不升级。
- Local SAM 3 four-graph metadata and partial ORT evidence are retained as External blockers; the custom SAM License, gated checkpoint, missing official export, and missing complete prompt/video state prevent execution claims. / SAM 3 只保留 External blocker 与局部证据。
- ModelFactory gains complete multi-artifact bundle queries and sidecar/version/conversion rejection. Three structured manifests and a package-only SAM consumer were added; official catalog and GitHub Release remain untouched. / ModelFactory 新增完整 Bundle 查询；新增三份结构化清单与仅包 Consumer；官方目录与 Release 不写入。
- No model/checkpoint/image/video/Python/native/TensorRT asset is included in NuGet. TensorRT remains unimplemented. / NuGet 不含模型与禁止项；TensorRT 仍未实现。
