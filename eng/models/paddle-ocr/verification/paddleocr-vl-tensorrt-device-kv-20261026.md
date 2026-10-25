# PaddleOCR-VL 1.6 TensorRT device-KV validation

Date: 2026-10-26. This is a fixed-input Windows 10.0.26200 / RTX 3060 Laptop (6 GiB) / TensorRT 10.11.0.33 / CUDA 12.9 / cuDNN 9.22 / .NET 10.0.12 result. The five graph identities and the task-specific Vision and Prefill profiles are recorded in the [five-graph validation](paddleocr-vl-tensorrt-five-graph-20261026.md). Engines were built for this device and are not portable Release assets.

`PaddleOcrVlTensorRtDeviceSession` accepts five caller-owned TensorRT Sessions. After host-side Vision and Prefill, it keeps all 36 past/present KV tensors in two CUDA banks and enqueues Decoder followed by Head on the same stream without reading the intermediate hidden state. Each step still runs Token Embedding through the host and reads back the full logits for greedy selection. The CPU/ORT/OpenVINO path is unchanged. Calls on one instance are serialized, and the two KV banks are bounded by `MaximumCacheBytes`.

## Correctness

The pinned official OCR, Table, Formula, Chart, Spotting and Seal images all reached EOS and exactly matched their reference Token IDs and text: **6/6 tasks, 765/765 tokens**. Each task used a new process and a matching task-specific Engine profile. The [machine-readable summary](paddleocr-vl-tensorrt-device-kv-20261026.json) links the six raw reports and their SHA-256 values. This is single-image generation correctness, not labelled task quality or page-level parity.

The summary records two byte identities for each copied report: `reportSha256` is the original Windows CRLF output, while `repositoryLfSha256` is the LF-normalized Git blob. They contain the same JSON data; use the identity matching the byte source being checked.

## Table latency

Both 5+50 rows used the same Table input Patch SHA `c66c539d...dbcbb`, the same reference and the same five Engine SHA values recorded in the summary. Each iteration reached EOS and matched all 71 Token IDs and text. Wall time surrounds one generation call; Engine/Session loading, caller image preparation and JSON writes are excluded. Percentiles use nearest rank.

| Path | Warm-up / measured | Wall P50 / P95 (ms) | Session P50 / P95 (ms) | Raw report |
| --- | ---: | ---: | ---: | --- |
| Host-owned KV | 5 / 50 | 20,360.0165 / 23,810.9774 | 20,359.5002 / 23,809.9229 | [JSON](paddleocr-vl-tensorrt-table-host-kv-20261026.json) |
| Device KV, pre-final build | 5 / 50 | 9,329.2314 / 9,650.9849 | 9,324.0775 / 9,645.9727 | [JSON](paddleocr-vl-tensorrt-table-device-kv-20261026.json) |
| Device KV, final-build recheck | 1 / 5 | 9,106.1978 / 9,150.6258 | 9,102.0617 / 9,145.4740 | [JSON](paddleocr-vl-tensorrt-table-device-kv-final-short-20261026.json) |

The two 5+50 runs were sequential, GPU clocks were not locked, and their test/device assemblies differ. The device 5+50 report predates the final CUDA-device and fixed-Vision-grid guards; its loaded Visual.TensorRT DLL was observed before rebuild with SHA `d69c43fe9e486034cb9c8d9f500ea03dc6751d921332356ec7865b1e6e5f2fdd`. The final-build 1+5 report records Visual.TensorRT DLL SHA `b056fb5fa96c4800dc3965ee01fb1dc24e4818e7c0f825e8a465e6c6c2280f76` and device-session source SHA `361e404497c91186c52c0ac48b92f6dc278623dd57bf486278117900fca8c99e`. Thus the observed latency difference is useful but **not a controlled speedup estimate**, and the short recheck must not be described as a final-build 5+50 result. Other backends, even on this GPU, have separate measurements and cannot be ranked from this comparison.

An intermediate guard probe failed before inference because the installed CUDA library does not expose `cudaStreamGetDevice`; the final code uses the supported current-device query, and the subsequent full Table generation and 1+5 recheck passed. That failed probe is not a model or Engine failure.

## Reproduce and remaining boundaries

Use `eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlTensorRtGeneration.ps1` with the pinned Bundle, official fixture/reference, five matching Engines and native runtime roots described in the [five-graph report](paddleocr-vl-tensorrt-five-graph-20261026.md). Add `-DeviceKv -Latency -Warmups 5 -Measurements 50 -CaseId table-official-example` for the standard Table protocol. The runner builds the test assembly, records source/assembly hashes and checks EOS, every Token ID and text on every iteration. Use a fresh `-OutputRoot` for each run.

Remaining: full device-resident Vision/Prefill/Embedding/argmax, arbitrary image/profile coverage, labelled Table/Chart/Seal/Spotting quality, page/multi-page TensorRT execution, another GPU, locked-clock comparison, long-running memory stability and actual downloadable VL Release assets. OpenCV DNN still has the original dynamic graph importer blocker. This report admits only the exact fixed 1.6 setup above.
