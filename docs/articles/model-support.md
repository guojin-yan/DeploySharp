# Model support / 模型支持

DeploySharp <code>2.0.0-alpha.1</code> contains 42 official catalog entries and 43 artifact variants. Every entry has a dedicated reproducibility case under <code>samples/06-models/cases</code>. The catalog status is **Preview**: it means the ModelFactory entry can be selected and its ModelPack/download identity can be checked; it does not mean every backend supports it. / <code>2.0.0-alpha.1</code> 包含 42 个官方目录条目和 43 个工件变体。每个条目在 <code>samples/06-models/cases</code> 下都有独立复现案例。目录状态为 **Preview**：表示可以选择目录条目并校验 ModelPack/下载身份，不表示所有后端都支持。

## Model families / 模型族

| Family / 模型族 | Catalog entries / 条目 | Current task coverage / 当前任务覆盖 |
| --- | ---: | --- |
| YOLO v5-v13/v26 | 22 | Detection, classification, segmentation, pose, and OBB; exact backend cells are in the matrix |
| DETR family | 8 | DEIMv2, PP-YOLOE, RF-DETR detection/segmentation, RT-DETR decoded/raw variants |
| PP-OCRv5 | 6 | Mobile/server classification, detection, and recognition |
| Anomalib / BRIA | 3 entries, 4 artifacts | PaDiM bottle anomaly; RMBG 1.4; RMBG 2.0 FP32 and dynamic-int8 variants |
| Vision-language | 2 | CLIP image/text embedding; BLIP caption generation |
| Segmentation | 1 | SAM v1 ViT-B promptable image segmentation |
| Local LLM | 1 | Qwen2.5 0.5B Instruct Q4_K_M GGUF |

## Complete catalog IDs / 完整目录 ID

The current catalog IDs are listed below. The exact artifact, precision, size, SHA-256, Release tag, and backend result are maintained in the [verification matrix](../model-backend-verification-matrix.md). / 当前目录 ID 如下；精确工件、精度、大小、SHA-256、Release 标签和后端结果维护在[验证矩阵](../model-backend-verification-matrix.md)。

### YOLO / YOLO

<code>yolo/v5/detect/n</code>, <code>yolo/v5/segment/s</code>, <code>yolo/v6/detect/s</code>, <code>yolo/v7/detect/base</code>, <code>yolo/v8/classify/s</code>, <code>yolo/v8/detect/n</code>, <code>yolo/v8/obb/s</code>, <code>yolo/v8/pose/s</code>, <code>yolo/v8/segment/n</code>, <code>yolo/v9/detect/s</code>, <code>yolo/v9/segment/c</code>, <code>yolo/v10/detect/n</code>, <code>yolo/v11/detect/n</code>, <code>yolo/v11/obb/s</code>, <code>yolo/v11/pose/s</code>, <code>yolo/v11/segment/s</code>, <code>yolo/v12/detect/n</code>, <code>yolo/v13/detect/n</code>, <code>yolo/v26/detect/n</code>, <code>yolo/v26/obb/s</code>, <code>yolo/v26/pose/s</code>, <code>yolo/v26/segment/s</code>.

### DETR and OCR / DETR 与 OCR

<code>deim/v2/detect</code>, <code>pp-yoloe/plus-crn-l</code>, <code>rf-detr/detect</code>, <code>rf-detr/segment</code>, <code>rt-detr/r50vd-decoded-vector-ir</code>, <code>rt-detr/r50vd-decoded-vector-onnx</code>, <code>rt-detr/r50vd-raw-query</code>, <code>paddleocr/ppocrv5/mobile-cls</code>, <code>paddleocr/ppocrv5/mobile-det</code>, <code>paddleocr/ppocrv5/mobile-rec</code>, <code>paddleocr/ppocrv5/server-cls</code>, <code>paddleocr/ppocrv5/server-det</code>, <code>paddleocr/ppocrv5/server-rec</code>.

### Vision, anomaly, and language / 视觉、异常与语言

<code>anomalib/padim/mvtec-bottle</code>, <code>bria/rmbg-1.4</code>, <code>bria/rmbg-2.0</code> (<code>onnx.fp32</code> and <code>onnx.dynamic-int8</code>), <code>segmentation/sam-v1-vit-b</code>, <code>vision-language/clip-vit-b-32</code>, <code>generative-vision-language/blip-caption-base</code>, <code>llm/qwen2.5-0.5b-instruct-q4-k-m</code>.

## Status symbols / 状态符号

| Symbol / 符号 | Meaning / 含义 |
| --- | --- |
| <code>✓</code> | Exact local artifact built/loaded and inference passed on that backend |
| <code>✗</code> | Exact compatibility test ran and the backend is currently unsupported for that artifact |
| <code>—</code> | No matching local artifact or not applicable to that backend |

Use the per-model case READMEs under <code>samples/06-models/cases</code> for commands and prerequisites. Use the [matrix](../model-backend-verification-matrix.md) for the complete 43-row artifact result table.
