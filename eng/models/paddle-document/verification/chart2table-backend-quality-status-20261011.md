# PP-Chart2Table backend and quality status (2026-10-13)

This index separates four claims that must not be conflated:

1. whether the published four-graph bundle can finish generation;
2. whether a bounded labeled ChartQA selection exposes task-quality behavior;
3. whether two backends reproduce the same generated output; and
4. whether a backend is unsupported or remains unverified.

The machine-readable source is [`chart2table-backend-quality-status-20261011.json`](chart2table-backend-quality-status-20261011.json).

## Current backend boundary

| Backend | Full generation evidence | Bounded quality evidence | Current conclusion |
|---|---|---|---|
| ONNX Runtime CPU | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match | Deterministic 48-image ChartQA `val` selection: 48/48 EOS, 47/48 parseable, 33/48 matching dimensions, 17/48 exact text, 205/319 rows and 450/1051 cells | Supported for the published bundle; quality numbers are diagnostic only |
| OpenVINO CPU | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match | Same 48-image selection and aggregate metrics; generated table SHA matches ORT for 48/48 | Supported for the published bundle; quality numbers are diagnostic only |
| TensorRT CUDA | Official sample and 4 curated ChartQA human charts reach EOS; all 4 expected table hashes match; Builder-created plans pass | Same 48-image selection and aggregate metrics; generated table SHA matches ORT for 46/48, token count for 47/48 | Supported on the recorded RTX 3060/TensorRT 10.11 profile; plans are not portable without rebuild |
| OpenCV DNN CPU | Vision/Projector and token-embedding graphs load in isolation | No full generation | Unsupported for the current adapter contract: rank-3 prefill and rank-4 dynamic-KV decode inputs fail closed before inference |

## Forty-eight-image evidence

- The deterministic selection contains 16 OECD, 16 `two_col` and 16 other filenames from ChartQA `val` revision `044eabfc306abfe9340c5741f0093aefc5973d06`. Its timestamp-independent selection identity is `137dee01beac021e004d6ec2af0c00dc94cd4ef834558f2bbbe61863f9aaedcb`.
- All three reports retain the earlier 24 samples with zero artifact/output mismatches. All three reach EOS for 48/48 and report the same task totals: 47/48 structurally valid, 33/48 matching dimensions, 17/48 exact table text, 205/319 exact rows and 450/1051 exact cells.
- ORT/OpenVINO match generated table SHA, token count, finish reason, dimensions, parseability, structure, exact-text flags and cell counts for 48/48.
- ORT/TensorRT match generated table SHA for 46/48 and token count for 47/48. `08852562005685.png` uses 254 versus 258 tokens and differs in generated SHA. `OECD_ENTERPRISES_BY_BUSINESS_SIZE_BEL_BGR_CZE_NOR_SWE_000003.png` uses 177 tokens on both backends but differs in generated SHA. Both rows have the same EOS, dimensions, parseability, structure/exact-text flags and exact row/cell counts across the compared reports.
- Total latency P50/P95 observed in the quality runs was 20,614.832/50,117.460 ms for ORT CPU, 18,442.476/47,943.732 ms for OpenVINO CPU and 7,614.772/25,819.988 ms for TensorRT CUDA. These are single-run order statistics without a common warm-up/repetition or clock-control protocol and are not a backend ranking.

## Evidence and interpretation

- The published `paddle-chart/pp-chart2table` entry is a seven-file four-graph ONNX/tokenizer bundle in the `models-paddleocr` Release. The upstream source archive remains `conversion-blocked` only because it is a generative checkpoint rather than a standard Paddle inference pair; this does not invalidate the derived bundle.
- The four human-chart regressions are qualitative full-generation checks. They do not establish ChartQA split accuracy.
- The 48-image reports validate every image/table hash against an external manifest and preserve only hashes and generated evidence in Git. The raw ChartQA images, tables and manifest remain outside the repository.
- TensorRT timing and plans are device/runtime/profile specific. The recorded plans were built for an RTX 3060 Laptop with TensorRT 10.11, CUDA 12.9 and cuDNN 9.22; the GPU clock was not locked.

## Source records

- [Four-graph component validation](chart2table-component-validation.json)
- [ORT four-image regression](chart2table-ort-multi-image-20260929.json) · [OpenVINO four-image regression](chart2table-openvino-multi-image-20260929.json) · [TensorRT four-image regression](chart2table-tensorrt-multi-image-20260930.json)
- [ORT 48-image summary](chart2table-extended-quality-ort-48-20261013.md) · [raw JSON](chart2table-extended-quality-ort-48-20261013.json)
- [OpenVINO 48-image summary](chart2table-extended-quality-openvino-48-20261013.md) · [raw JSON](chart2table-extended-quality-openvino-48-20261013.json)
- [TensorRT 48-image summary](chart2table-extended-quality-tensorrt-48-20261013.md) · [raw JSON](chart2table-extended-quality-tensorrt-48-20261013.json)
- [ORT/OpenVINO 48-image alignment](chart2table-extended-quality-ort-openvino-48-alignment-20261013.md) · [ORT/TensorRT 48-image alignment](chart2table-extended-quality-ort-tensorrt-48-alignment-20261013.md)
- [OpenCV isolated boundary](chart2table-opencv-isolated-20261013.md) · [machine-readable result](chart2table-opencv-isolated-20261013.json)

## Not closed by this index

OpenCV full autoregressive generation is an explicit current adapter/runtime unsupported boundary, not an unverified row. ChartQA split-level accuracy, question-answering accuracy, independently labeled larger-scale evaluation, clock-locked repeated performance and TensorRT portability across runtime/GPU versions remain open. The two ORT/TensorRT generated-text differences prevent a bitwise-parity claim. No `✓` in the backend matrix should be read as a claim that every chart style or every PP-Structure model is accurate.
