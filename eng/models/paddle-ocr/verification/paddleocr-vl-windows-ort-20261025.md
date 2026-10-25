# PaddleOCR-VL-1.6 Windows ONNX Runtime CPU verification

Date: 2026-10-25

The production C# `PaddleOcrVlOnnxSession` and `PaddleOcrVlImageProcessor` were run on Windows 10.0.26200 with ONNX Runtime 1.28.0 CPU, .NET 10.0.12, 16 logical processors and four inference threads. The pinned 1.6 revision, four dynamic graphs and managed RGB fixtures were verified by SHA-256 before generation.

All six real task fixtures reached EOS and matched the pinned official reference Token IDs and decoded text:

| Task | Tokens | Prompt tokens | Elapsed observation |
| --- | ---: | ---: | ---: |
| OCR | 162 | 337 | 23,162.225 ms |
| Table | 71 | 163 | 7,185.727 ms |
| Formula | 80 | 173 | 8,264.475 ms |
| Chart | 137 | 453 | 21,659.248 ms |
| Spotting | 286 | 1,309 | 132,272.477 ms |
| Seal | 29 | 543 | 19,739.617 ms |

The machine-readable summary is [`paddleocr-vl-windows-ort-20261025.json`](paddleocr-vl-windows-ort-20261025.json). The complete external run JSON is retained at `E:/DeploySharpEvidence/paddleocr-vl-windows-ort-20261025.json` with SHA-256 `875b82d72168ac89e37df3a520c8e2afd42287b79d97038c69966c541e6ebabf`.

This closes the Windows ORT CPU generation-correctness gap for the fixed 1.6 four-graph bundle. The elapsed values are one run per task and are not a P50/P95 benchmark; the fixture RGB bytes were prepared by the pinned upstream/Pillow comparison path. OpenCV JPEG/PNG decoding, CUDA, TensorRT, OpenCV DNN, labelled quality and page-level accuracy remain separate boundaries. Windows OpenVINO CPU has a separate six-task report; this ORT record does not merge the two runtime results. The result does not imply that the remote `models-paddleocr` Release has been updated.
