# Segment Anything model family / Segment Anything 模型族

Stage 22 adds a backend-neutral, artifact-bound promptable image contract to `JYPPX.DeploySharp.Visual`. The executable path is a two-session SAM v1 pipeline: set one image once, reuse its exact embedding for multiple point/box/mask-feedback decodes, then clear or dispose. SAM 2 and SAM 3 are represented only to the level supported by audited official exports and local evidence; no Python resident process, silent fallback, reimplemented prompt algorithm, video-memory imitation, model-specific NuGet, or TensorRT path is used. / 阶段 22 在现有 Visual 包中增加后端无关、绑定工件的可提示图像合同。可执行路径是双 Session 的 SAM v1 流水线：图像只编码一次，随后针对同一精确 Embedding 多次执行点/框/Mask Feedback 解码，最后 clear 或 dispose。SAM 2/3 只表达官方导出与本机证据实际支持的范围；不使用 Python 常驻进程、静默回退、自制提示/视频记忆算法、单模型 NuGet 或 TensorRT。

## Quick start / 快速开始

```csharp
PromptableSegmentationProfile profile = PromptableSegmentationProfiles.CreateSamV1(
    "external/sam-v1-vit-b",
    new ModelId("external/sam-v1-vit-b-encoder"),
    new ModelId("external/sam-v1-vit-b-decoder"),
    encoderSha256,
    decoderSha256,
    "dca509fe793f601edb92606367a655c15ac00fdf",
    "traceable official image-encoder wrapper; torch 2.9.1; opset 17",
    "official export_onnx_model.py plus dynamo=false; torch 2.9.1; opset 17");

var bundle = new PromptableSegmentationArtifactBundle(profile, new[]
{
    new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.ImageEncoder,
        profile.GetArtifact(PromptableSegmentationArtifactRole.ImageEncoder).CreateArtifact(encoderPath, OnnxRuntimeBackendProvider.BackendId)),
    new PromptableSegmentationArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder,
        profile.GetArtifact(PromptableSegmentationArtifactRole.PromptMaskDecoder).CreateArtifact(decoderPath, OnnxRuntimeBackendProvider.BackendId))
});

using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
using var session = new PromptableSegmentationImageSession(
    registry, bundle, new BackendRequest(BackendCapabilities.TensorInference, OnnxRuntimeBackendProvider.BackendId, "cpu"));
using PreparedVisualInput image = new OpenCvPromptableSegmentationInputFactory().CreateSamV1FromFile(imagePath);
PromptableImageEmbedding embedding = session.SetImage(image);
PromptableSegmentationResult masks = session.Predict(new PromptableSegmentationPrompt(
    new[] { new PromptPoint(430, 280, PromptPointLabel.Foreground) },
    new RectangleF(200, 80, 450, 480),
    returnMultipleMasks: true));
PromptableMaskFeedback feedback = masks.Candidates[0].LowResolutionLogits.CreateFeedback();
PromptableSegmentationResult refined = session.Predict(new PromptableSegmentationPrompt(
    new[] { new PromptPoint(430, 280, PromptPointLabel.Foreground) },
    new RectangleF(200, 80, 450, 480), feedback, returnMultipleMasks: false));
```

The registry and prepared input remain caller-owned unless `VisualExecutionOptions.DisposeOwnedInputOnCompletion` is selected. The image session owns both backend sessions and one cached embedding. Every returned source mask, RLE, quality value, provenance record, and low-resolution feedback tensor is caller-owned and remains valid after the next prediction or session disposal. / Registry 与 prepared input 默认由调用方拥有；图像 Session 拥有两个 Backend Session 与一个缓存 Embedding。返回的源图 Mask、RLE、质量、来源与低分辨率反馈 Tensor 全部归调用方所有，在下一次预测或 Session 释放后仍有效。

## Audited model matrix / 已审计模型矩阵

| Family / 模型族 | Official snapshot and license / 官方快照与许可证 | Audited native components / 已审计 native 组件 | Status / 状态 |
| --- | --- | --- | --- |
| SAM | `dca509fe793f601edb92606367a655c15ac00fdf`, Apache-2.0 | ViT-B image encoder wrapper + official `SamOnnxModel` prompt/mask decoder, opset 17 | Complete image point/box/mask-feedback on ORT/OpenVINO CPU; External, not `AlgorithmVerified` / 图像路径完成；External，非 AlgorithmVerified |
| SAM 2 | `2b90b9f5ceec907a1c18123530e92e794ad901a4`, Apache-2.0 | Local opset-18 external-data image encoder + prompt/mask decoder | Exact ORT/OpenVINO named-port evidence only; local graph lacks feedback and video memory, export/checkpoint provenance unverified / 仅精确端口证据；缺反馈/视频记忆且来源未核验 |
| SAM 3 | `96914d2425f90a64f45ca977c2b5165418099543`, custom SAM License | Local opset-21 vision/text/geometry/decoder metadata; ORT geometry+decoder observation | Incomplete non-official conversion; gated checkpoint, no official ONNX/OpenVINO export, no point/feedback/video state / 不完整非官方转换；checkpoint 受限且缺官方导出与完整状态 |
| SAM 2 video | same official commit / 同上 | Official PyTorch video predictor, memory encoder/bank/attention | Native execution blocked: no complete official ONNX/OpenVINO state export / native 执行阻断 |
| SAM 3 video | same official commit / 同上 | Official PyTorch detector/tracker state | Native execution blocked: no complete official ONNX/OpenVINO state export / native 执行阻断 |

The official SAM ViT-B checkpoint is `https://dl.fbaipublicfiles.com/segment_anything/sam_vit_b_01ec64.pth`, 375,042,383 bytes, SHA256 `ec2df62732614e57411cdcf32a23ffdf28910380d03139ee0f4fcbe91eb8c912`. The isolated exports are 359,210,513-byte encoder SHA `95ea8873d6dbbf1226bf124f56930c1652c09c19f84c032b3721979699a21c3a` and 16,496,903-byte decoder SHA `b520bc95e049862bde768b959c124d6c2a53436df81bf9c5e8689f6e406ba21a`. / 官方 checkpoint 及隔离导出的大小与 SHA 如上。

The official SAM 2.1 Hiera Tiny checkpoint is `https://dl.fbaipublicfiles.com/segment_anything_2/092824/sam2.1_hiera_tiny.pt`, 156,008,466 bytes, SHA256 `7402e0d864fa82708a20fbd15bc84245c2f26dff0eb43a4b5b93452deb34be69`; it was downloaded only into the isolated temporary directory and was not exported or published. The official SAM 3 checkpoint URI is `https://huggingface.co/facebook/sam3`; access is gated, so no checkpoint size/SHA or export claim is recorded. / SAM 2.1 Tiny 官方 checkpoint 已仅下载到隔离目录计算大小/SHA，未导出或发布。SAM 3 官方 checkpoint 受访问控制，因此不填写大小/SHA 或导出声明。

The decoder command was `python scripts/export_onnx_model.py --checkpoint <checkpoint> --model-type vit_b --output <decoder.onnx> --opset 17`. With torch 2.9.1 the unmodified command selected the dynamo exporter and reproducibly failed at `masks[..., : prepadded_size[0], : prepadded_size[1]]` with `GuardOnDataDependentSymNode`; the isolated checkout was changed only to pass `dynamo=False`, producing the recorded legacy-exporter artifact. The image encoder used a small traceable wrapper over the exact official `sam.image_encoder`, `torch.onnx.export(..., opset_version=17, dynamo=False)`. Python 3.13.12, torch 2.9.1+cpu, torchvision 0.24.1+cpu, onnx 1.20.0, and onnxruntime 1.23.2 were recorded. User models were not overwritten. / Decoder 命令与默认失败证据如上；仅在隔离 checkout 增加 `dynamo=False`。Image Encoder 使用官方模块的可追溯薄包装。完整版本已记录，未覆盖用户模型。

## Exact SAM v1 ports / SAM v1 精确端口

| Component / 组件 | Named inputs / 具名输入 | Named outputs / 具名输出 |
| --- | --- | --- |
| Image encoder | `images` Float32 `[1,3,1024,1024]` | `image_embeddings` Float32 `[1,256,64,64]` |
| Prompt/mask decoder | `image_embeddings`; `point_coords [1,N,2]`; `point_labels [1,N]`; `mask_input [1,1,256,256]`; `has_mask_input [1]`; `orig_im_size [2]` | source-size `masks [1,4,H,W]`; `iou_predictions [1,4]`; `low_res_masks [1,4,256,256]` |

Point labels are Float32 `0` background and `1` foreground. A box is encoded as two coordinates labelled `2` and `3`; it is not inferred from rank. With no point/box and mask feedback present, a dummy label `-1` preserves the official prompt schema. `orig_im_size` is `[height,width]`. Token zero is the single-mask candidate; tokens one through three are multimask candidates sorted by predicted IoU descending, then source index. Mask logits use strict `> 0`; equality is background. / 点标签、Box 角标签、dummy 标签、原图尺寸顺序、Token 选择与严格阈值规则如上，均不从 Rank 推断。

## Geometry, cache, and state ownership / 几何、缓存与状态所有权

OpenCV decodes PNG/JPEG/file/bytes once, converts gray/alpha/BGR to RGB, resizes the longest side with half-up rounding, pads only bottom/right to 1024, and applies `(pixel - [123.675,116.28,103.53]) / [58.395,57.12,57.375]`. The encoded source SHA256 becomes the image identity. Points and boxes are mapped by that same `ImageTransform`; the adapter/backend never recomputes geometry. Decoder source masks are already restored by the official ONNX wrapper and are materialized through existing `InstanceBinaryMask`, `InstanceMaskRle`, and `InstanceSegmentationResult`. / OpenCV 单次解码并执行官方 RGB、最长边、half-up、底/右补零与 mean/std；编码源 SHA 作为图像 Identity。点/框只通过同一 `ImageTransform` 映射，Adapter/Backend 不重复计算。源图 Mask 复用现有 Mask/RLE/InstanceSegmentationResult。

`PromptableImageIdentity` binds profile ID, ordered artifact-role SHA identity, encoded image SHA, source size, and model size. Feedback from a different image/profile/artifact is rejected with `DS-VISUAL-4504`. Set-image/predict/clear are single-writer operations and concurrent calls fail with `DS-VISUAL-4505`; async cancellation never installs partial embedding state or returns partial masks. Dispose cancels an active operation, waits for unwind, clears the embedding, and disposes both sessions exactly once. / Identity 绑定 Profile、工件 SHA、图像 SHA 与尺寸。跨图/跨 Profile/跨工件反馈稳定失败；状态操作拒绝并发；取消不提交部分状态；Dispose 等待退出并仅一次释放两个 Session。

Video contracts describe frame order, state mutation, cancellation consistency, capacities, and blocker, but no `initialize/add prompt/propagate/reset` runtime API is exposed until a complete official native memory/tracker bundle is executable. / 视频合同记录帧序、状态变更、取消一致性、容量与 blocker；在完整官方 native memory/tracker bundle 可执行前，不暴露伪造的视频运行 API。

## Real evidence and single observations / 真实证据与单次观测

The authorized `boy.jpg` input is 860x573, SHA256 `bb6082ec3bb90dde8f7553f9bdfb7c09d438a74397df0b2ebabda55c6bcc0df3`. For points `(430,280,+)` and `(300,150,-)`, box `(200,80)-(650,560)`, and best-mask feedback, ORT/OpenVINO matched candidate order, quality within `0.0001`, source masks at IoU >= `0.999`, RLE and bounds. ORT masks compared with the official PyTorch predictor at IoU `0.976115`, `0.967546`, `0.963047`, and feedback `0.989333`. One observed run recorded ORT encoder/prompt/restore/refine `5301.603/55.040/68.760/51.068 ms` and OpenVINO `3226.449/59.152/37.472/31.417 ms`. These are single diagnostics, not P50/P95, throughput, memory, or accuracy claims. / 真实图片、提示、IoU 与单次 timing 如上；不构成分位数、吞吐、内存或精度结论。

Local SAM 2 ORT on the same image observed encoder/decoder `3930.314/78.425 ms`; the SHA-bound .NET ORT exact-named-port zero-image gate observed `1630.380/43.331 ms`; local OpenVINO zero-image named-port evidence observed `1112.975/75.741 ms`. Local SAM 3 geometry+decoder zero-feature ORT observed `94.895/6136.058 ms`. These prove only exact local graph execution, not official predictor fidelity. / SAM 2 的同图 ORT、绑定 SHA 的 .NET ORT 具名端口门禁及 OpenVINO 门禁数值如上；SAM 2/3 数值只证明本机图合同执行，不证明官方 Predictor 保真。

## Diagnostics and platform matrix / 诊断与平台矩阵

| Code / 代码 | Meaning / 含义 |
| --- | --- |
| `DS-VISUAL-4501` | profile, tensor, prompt, transform, NaN/Infinity, or feedback shape invalid / 合同、Tensor、Prompt、Transform 或数值无效 |
| `DS-VISUAL-4502` | prompt/candidate/pixel/tensor capacity exceeded / 超容量 |
| `DS-VISUAL-4503` | predict before set-image or invalid state transition / 状态顺序无效 |
| `DS-VISUAL-4504` | embedding/profile/artifact/image identity mismatch / 缓存 Identity 不匹配 |
| `DS-VISUAL-4505` | concurrent stateful operation rejected / 拒绝并发状态操作 |

`JYPPX.DeploySharp.Visual` and `Visual.OpenCV` retain their declared managed TFM matrices. The validated clean consumer is `net10.0/win-x64`, ORT 1.28.0 and OpenCV runtime 5.0.0-preview.1 selected explicitly by the application. OpenVINO evidence uses managed 3.3.0 plus Windows runtime 2026.2.1. Other RIDs/devices are not claimed. NuGet contains only managed DLL/XML, README, and logo; it contains no model, checkpoint, image, video, Python, external-data, native runtime, or TensorRT file. / TFM 不变；clean consumer 的 native runtime 由应用显式选择。其他 RID/设备不作声明，NuGet 禁止项保持为空。

The separate SAM v1 ViT-B release manifest is `redistributionAllowed:true` and available as a ModelFactory Preview in the [shared vision collection](models-vision-collection.md). The historical SAM v1 development manifest and the SAM 2/3 manifests remain External and are not `AlgorithmVerified`; local paths are never serialized into a public catalog or package. / 独立 SAM v1 ViT-B 发布清单允许再分发，并作为 ModelFactory Preview 收录到[共享视觉模型集合](models-vision-collection.md)。历史 SAM v1 开发清单及 SAM 2/3 清单继续保持 External，且均非 `AlgorithmVerified`。
