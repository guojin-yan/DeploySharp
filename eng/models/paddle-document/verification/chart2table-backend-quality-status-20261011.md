# PP-Chart2Table backend and quality status (2026-10-12)

This index consolidates the existing Chart2Table evidence, including the new 24-image ORT CPU quality extension. It separates three claims that must not be conflated:

1. whether the four-graph bundle can execute and finish generation;
2. whether a bounded set of labeled ChartQA examples has a useful task-quality diagnostic; and
3. whether a backend is still blocked or unverified.

The machine-readable source is [`chart2table-backend-quality-status-20261011.json`](chart2table-backend-quality-status-20261011.json).

## Current backend boundary

| Backend | Full generation evidence | Bounded quality evidence | Current conclusion |
|---|---|---|---|
| ONNX Runtime CPU | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match | 24-image ChartQA `val` selection: 24/24 EOS, 16/24 structure matches, 6/24 exact text, 211/547 cells | Supported for the published bundle; quality numbers are diagnostic only |
| OpenVINO CPU | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match | Pinned 24-image `val` selection: 24/24 EOS, 16/24 structure matches, 6/24 exact text, 211/547 cells; generated table SHA matches ORT for 24/24 | Supported for the published bundle; quality numbers are diagnostic only |
| TensorRT CUDA | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match; Builder-created plans pass | ChartQA `val` selection: 12/12 EOS, 9/12 structure matches, 3/12 exact text, 140/293 cells | Supported on the recorded RTX 3060/TensorRT 10.11 profile; plans are not portable without rebuild |
| OpenCV DNN CPU | Vision/Projector and token-embedding graphs load in isolation | No full generation | Unsupported for the current adapter contract: rank-3 prefill and rank-4 dynamic-KV decode inputs fail closed before inference |

## Evidence and interpretation

- The published `paddle-chart/pp-chart2table` entry is a seven-file four-graph ONNX/tokenizer bundle in the `models-paddleocr` Release. The upstream source archive remains `conversion-blocked` only because it is a generative checkpoint rather than a standard Paddle inference pair; this does not invalidate the derived bundle.
- The four human-chart regressions are qualitative full-generation checks. They do not establish ChartQA split accuracy.
- The 6/12-image `ChartQA/val` records use a pinned upstream revision and retain image/table hashes. The 24-image ORT report is [`chart2table-extended-quality-ort-20261012.json`](chart2table-extended-quality-ort-20261012.json), and the matching OpenVINO report is [`chart2table-extended-quality-openvino-20261012.json`](chart2table-extended-quality-openvino-20261012.json). Generated table SHA values match `24/24`; the reusable [alignment report](chart2table-extended-quality-backend-alignment-20261012.md) separately checks image/table SHA, EOS, token count, dimensions and cell totals. These bounded selections are useful for detecting structure and cell errors, but are too small and selected to be a leaderboard or release-quality score.
- The ORT/TensorRT 12-image alignment report compares already-generated files and confirms identical input selection, finish reasons, structure flags and exact-cell totals. It is not a new inference run.
- TensorRT timing is device- and plan-specific. The recorded plans were built for an RTX 3060 Laptop with TensorRT 10.11/CUDA 12.9/cuDNN 9.22 and an unlocked clock; it must not be generalized to other devices.

## Source records

- [Four-graph component validation](chart2table-component-validation.json)
- [ORT four-image regression](chart2table-ort-multi-image-20260929.json) · [OpenVINO four-image regression](chart2table-openvino-multi-image-20260929.json) · [TensorRT four-image regression](chart2table-tensorrt-multi-image-20260930.json)
- [ORT 24-image quality](chart2table-extended-quality-ort-20261012.json) · [OpenVINO 24-image quality](chart2table-extended-quality-openvino-20261012.json) · [ORT 12-image quality](chart2table-extended-quality-ort-20261004.json) · [TensorRT 12-image quality](chart2table-extended-quality-tensorrt-20261005.json)
- [ORT/OpenVINO 24-image alignment](chart2table-extended-quality-backend-alignment-20261012.json) · [ORT/TensorRT 12-image alignment](chart2table-extended-quality-backend-alignment-20261005.json)
- [OpenCV isolated boundary](chart2table-opencv-isolated-20260929.json)

## Not closed by this index

OpenCV full autoregressive generation, TensorRT rerun on the 24-image selection, split-level ChartQA accuracy, clock-locked repeated performance, and TensorRT portability across runtime/GPU versions remain open. No `✓` in the backend matrix should be read as a claim that every chart style or every PP-Structure model is accurate.
