# Open-vocabulary detection and Grounded-SAM / 开放词汇检测与 Grounded-SAM

Stage 23 adds artifact-bound Grounding DINO, YOLO-World, YOLOE, and detector-to-SAM contracts to the existing Visual packages. Prompt behavior is never inferred from a filename or tensor rank. The executable local path is an Ultralytics YOLO-Worldv2 ONNX whose ordered vocabulary was fixed to `person,bus` before export. / 阶段 23 在既有 Visual 包新增开放词汇与检测到 SAM 的工件绑定合同。提示行为绝不从文件名或 Rank 推测；当前可执行路径是在导出前固定为 `person,bus` 的 Ultralytics YOLO-Worldv2 ONNX。

## Quick start / 快速开始

```csharp
OpenVocabularyDetectionProfile profile =
    OpenVocabularyDetectionProfiles.CreateUltralyticsYoloWorldV2PersonBus();
using var registry = new BackendRegistry();
registry.UseOnnxRuntime();
var request = new BackendRequest(BackendCapabilities.TensorInference,
    OnnxRuntimeBackendProvider.BackendId, "cpu");
var profiles = new VisualProfileRegistry();
profiles.Register(profile.VisualProfile);
profiles.Freeze();
using var pipeline = new VisualPipeline(registry,
    profiles.Select(profile.CreateArtifact(modelPath, OnnxRuntimeBackendProvider.BackendId),
        registry, request, VisualTaskId.ObjectDetection), request);
using PreparedVisualInput input =
    new OpenCvOpenVocabularyInputFactory().CreateFromFile(imagePath, profile);
OpenVocabularyDetectionResult result =
    pipeline.Run(input).GetValue<OpenVocabularyDetectionResult>();
```

`result.Detections` is the existing canonical `DetectionResult`; `Matches` adds phrase, vocabulary index, audited token IDs, prompt mode, and vocabulary SHA. No second box DTO or coordinate restoration path exists. / `Detections` 复用现有规范结果；`Matches` 仅追加 Phrase、词汇索引、Token、提示模式与 SHA，不创建第二套框 DTO 或坐标恢复。

## Official snapshot and exact contracts / 官方快照与精确合同

| Family / 模型族 | Commit, license, checkpoint / Commit、许可证与 Checkpoint | Native contract and status / Native 合同与状态 |
| --- | --- | --- |
| Ultralytics YOLO-Worldv2 | current `76595b8030abf57c6d1580b1cbc62640c58880a7`; export 8.2.2 `1110258d379bed8d623068ff7ceda8c9290f0774`; AGPL-3.0; source 25,920,600 bytes SHA `7c951d3b...519c8` | `images` Float32 `[1,3,640,640]` to `output0` Float32 `[1,6,N]`, opset 17, ONNX 51,252,911 bytes SHA `42f9d408...9bdd`; ORT/OpenVINO verified |
| AILab-CVC YOLO-World | `4f70adbaacf5685bd9ec5bea85f1f91057f6fc0b`; GPL-3.0; checkpoint 305,052,941 bytes SHA `55b943ea...a1a6` | image-only `images -> num_dets,boxes,scores,labels`; blocked because vocabulary/tokenizer/embedding identity is absent |
| Grounding DINO | `856dde20aee659246248e20734ef9ba5214f5e44`; Apache-2.0; Swin-T OGC 693,997,677 bytes SHA `3b3ca256...799` | official PyTorch BERT-base-uncased plus image/text fusion; blocked because no complete official local ONNX/IR bundle exists |
| YOLOE | `40cd606cabdbe2b566d6f14a6b162c89206e9a1b`; AGPL-3.0; `yoloe-v8s-seg.pt` SHA `ac2b90ed...0536`, prompt-free SHA `6535c03e...bc20` | text, visual, prompt-free and reparameterized modes documented; blocked because no matching local official native bundle exists |

Official YOLO-World reparameterizes text features before its image-only export. The executable Profile binds vocabulary `person,bus` SHA `0098f12e...78db`, CLIP BPE token rows `[49406,2533,49407,...]` and `[49406,2840,49407,...]`, tokenizer SHA `924691ac...804a`, CLIP ViT-B/32 SHA `40d36571...50af`, and embedding `[1,2,512]` SHA `e047a003...4753`. Any different word, order, tokenizer, encoder, embedding, or detector requires another Profile. / 固定导出绑定完整词汇/Tokenizer/Encoder/Embedding/Detector Identity；任一变化都必须使用新 Profile。

The detector uses centered RGB NCHW 640 letterbox, pad 114, divide by 255. Raw `cxcywh` class probabilities use strict score `> 0.25`, best-class selection, DeploySharp-owned class-aware NMS IoU `0.7`, and maximum 300. One `ImageTransform` restores source coordinates; the backend computes no geometry. / 检测前后处理、严格阈值、NMS 所有权与坐标恢复如上。

Grounding DINO official inference lowercases/trims captions, appends a period, resizes to 800 with max 1333, applies ImageNet normalization, filters sigmoid logits by box/text thresholds, and returns normalized `cxcywh`. DeploySharp does not invent replacement tokenization, fusion, phrase decoding, or prompt memory. / Grounding DINO 官方 Caption、Resize、Normalize、阈值与 Box 合同已记录；DeploySharp 不手写替代算法。

## Grounded-SAM state and ownership / Grounded-SAM 状态与所有权

`CreateGroundedSam` decodes one image exactly once and creates detector and SAM tensors sharing encoded-byte SHA/source size. `SetImage` runs detector and SAM encoder, installing both states only after success. `SegmentDetections` passes each existing source box directly to the Stage 22 SAM session. Existing `ImageTransform`, `DetectionResult`, `InstanceSegmentationResult`, mask/RLE, quality, feedback logits and result ownership remain authoritative. / 工厂单次解码并生成共享 Identity 的双路 Tensor；SetImage 原子安装检测与 Embedding；检测框直接传给既有 SAM 会话，所有规范结果和几何实现均复用。

Operations are single-writer. Codes `DS-VISUAL-4601` through `4605` cover contract, capacity, state, identity, and concurrency. Async cancellation publishes no partial state/result; `ClearImage` clears both caches; dispose cancels, waits, then releases detector plus both SAM sessions once. / 状态操作拒绝并发，取消不发布部分状态，Clear 同时清缓存，Dispose 等待退出并仅一次释放三条 Session。

## Real evidence, platforms, and packages / 真实证据、平台与包

Authorized `bus.jpg` is 810x1080, SHA `33b198a1...b69c`. Five fixed-vocabulary detections matched the official Ultralytics ONNX predictor and ORT/OpenVINO fields. One total/inference observation was `170.430/153.741 ms` ORT and `91.707/89.875 ms` OpenVINO. / 同图五个检测在官方 Predictor 与双后端对齐；Timing 为单次观测。

The five boxes then drove official SAM ViT-B and DeploySharp composition. Official source-mask IoUs were `0.989146,0.992322,0.996768,0.996456,0.973675`. Detector/encoder/five-prompt observations were `182.238/6243.519/607.475 ms` ORT and `100.368/3580.620/405.440 ms` OpenVINO; official PyTorch SAM observed `6151.304/305.829 ms`. Reset reproduced mask SHA `9f5699cf...b008`. These are diagnostics, not benchmark, throughput, memory, or accuracy claims. / 五个官方 Mask IoU、双后端组合与 Reset 已验证；Timing 不构成 Benchmark 或精度结论。

The package-only `tests/clean-consumer/visual-open-vocabulary` app uses `net10.0/win-x64`, ORT 1.28.0 and OpenCV 5.0.0-preview.1 selected by the application. OpenVINO evidence uses managed 3.3.0 plus Windows runtime 2026.2.1. Missing external files print stable skip; success prints `DEPLOYSHARP_VISUAL_OPEN_VOCAB_CONSUMER_OK` and optionally `DEPLOYSHARP_VISUAL_GROUNDED_SAM_CONSUMER_OK`. Other RIDs/devices are not claimed. / 仅包 Consumer、成功/Skip 标记及已验证 TFM/RID/native 组合如上。

All five manifests are External, `redistributionAllowed:false`, not `AlgorithmVerified`, excluded from the empty official catalog, and absent from NuGet/Release. No model, checkpoint, tokenizer, image, golden mask, Python, native runtime, or TensorRT asset is packaged. TensorRT remains unimplemented. / 五份清单均为 External、禁止再分发、非 AlgorithmVerified、不进入空目录/NuGet/Release；TensorRT 仍未实现。
