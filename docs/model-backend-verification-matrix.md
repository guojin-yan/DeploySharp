# 模型 × 后端验证矩阵

本页是当前公开模型工件的后端验证总表。验证范围为 Windows x64；`✓` 表示该模型工件在对应后端完成加载、推理和结果解码，`✗` 表示已尝试但当前后端不能完成该工件，`—` 表示没有适用工件或该格式不属于该后端。表格不把“可构建”或“存在适配器”当作推理通过。

模型目录中的 `External` 条目不纳入本表的通过统计。SAM2/SAM3 视频、Whisper 完整发布 Bundle、Donut 原生多页/TensorRT、BLIP VQA/BLIP-2/InstructBLIP、Qwen2.5-VL、Phi Vision、SigLIP 2，以及部分 LayoutLMv3/Pix2Struct 任务头仍处于合同或局部实验阶段，不能宣称全部后端可用。模型目录、下载边界和状态语义见[模型支持指南](articles/model-support.md)；可复现性能结果见[设备性能实测](articles/device-performance-benchmarks.md)。

## 当前工件矩阵

| 模型工件 | ONNX Runtime CPU | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA | LLamaSharp |
|---|:---:|:---:|:---:|:---:|:---:|
| `yolo/v5/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v6/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v7/detect/base` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/detect/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v10/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v12/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v13/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/detect/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/classify/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v5/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/segment/n` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v9/segment/c` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/segment/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/pose/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v8/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v11/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `yolo/v26/obb/s` | ✓ | ✓ | ✓ | ✓ | — |
| `deim/v2/detect` | ✓ | — | ✗ | ✓ | — |
| `pp-yoloe/plus-crn-l` | ✓ | — | ✗ | ✓ | — |
| `rf-detr/detect` | ✓ | ✗ | ✗ | ✓ | — |
| `rf-detr/segment` | ✓ | ✗ | ✗ | ✓ | — |
| `rt-detr/r50vd-decoded-vector-ir` | — | ✓ | — | — | — |
| `rt-detr/r50vd-decoded-vector-onnx` | ✓ | — | ✗ | ✓ | — |
| `rt-detr/r50vd-raw-query` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/mobile-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv4/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-cls` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv5/mobile-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/tiny-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/small-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-det` | ✓ | ✓ | ✓ | ✓ | — |
| `paddleocr/ppocrv6/medium-rec` | ✓ | ✓ | ✓ | ✓ | — |
| `anomalib/padim/mvtec-bottle` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-1.4` | ✓ | ✓ | ✓ | ✓ | — |
| `bria/rmbg-2.0 (onnx.fp32)` | ✓ | — | ✗ | ✓ | — |
| `bria/rmbg-2.0 (onnx.dynamic-int8)` | ✓ | — | ✗ | ✗ | — |
| `llm/qwen2.5-0.5b-instruct-q4-k-m` | — | — | — | — | — |
| `vision-language/clip-vit-b-32` | — | — | — | — | — |
| `segmentation/sam-v1-vit-b` | — | — | — | — | — |
| `generative-vision-language/blip-caption-base` | — | — | — | — | — |

## 阅读规则

- 通过只对表中精确工件成立；更换导出文件、输入尺寸、引擎或运行时版本后需要重新验证。
- TensorRT 列仅表示 CUDA 引擎路径通过；引擎必须与本机 TensorRT/CUDA 版本和输入 profile 匹配。
- PaddleOCR 的单图完整流水线、batch 和并发通道组合单独记录在[设备性能实测](articles/device-performance-benchmarks.md)；这里的单元格只表达阶段工件是否可执行。
- OpenCV DNN 的 `✗` 是当前 importer、动态 shape 或辅助输入限制，不代表其他后端的结果。
- 未验证的模型保持 `—`，不会用相近模型、脚本退出码或合同测试代替真实推理证据。
