# Chart2Table fixed-sample steady-state benchmark (2026-10-17)

This report uses one pinned ChartQA validation image and the same four-graph Bundle/decoder on three backends. Each backend uses one session and one channel, with five warm-ups followed by fifty measured generations. It is a latency and execution-contract record, not a ChartQA accuracy score.

## Protocol and result contract

- ChartQA revision: `044eabfc306abfe9340c5741f0093aefc5973d06`; input `OECD_GROSS_PENSION_REPLACEMENT_RATES_HUN_MEX_000001.png`; input SHA-256 `c2e49c1f2593d65e0c5cce5b573f122f076a0bf0f429121d96e9463c6404a530`.
- Result on all 55 invocations per backend: `EndOfSequence`, `21` tokens, generated-text SHA-256 `ebd99e6bb36ee2bc510932a58c049d0d1c2d765c473ec98d6c3a48529fb2d484`.
- CPU/GPU clocks were not locked. The values are therefore host/runtime observations, not a universal ranking.

| Backend | Total P50/P95 ms | Vision P50/P95 ms | Prefill P50/P95 ms | Decode-token P50/P95 ms | Evidence |
|---|---:|---:|---:|---:|---|
| onnxruntime-cpu | 8526.26 / 9042.12 | 5194.00 / 5619.23 | 783.42 / 850.88 | 99.08 / 182.53 | [chart2table-fixed-benchmark-20261017-ort.json](chart2table-fixed-benchmark-20261017-ort.json) |
| openvino-cpu | 7962.20 / 12161.73 | 4523.54 / 6704.73 | 637.05 / 1209.61 | 117.47 / 398.92 | [chart2table-fixed-benchmark-20261017-openvino.json](chart2table-fixed-benchmark-20261017-openvino.json) |
| tensorrt-cuda | 2884.91 / 3456.44 | 235.89 / 381.24 | 240.07 / 406.29 | 98.37 / 298.41 | [chart2table-fixed-benchmark-20261017-tensorrt.json](chart2table-fixed-benchmark-20261017-tensorrt.json) |

All three backends produced the same fixed-sample output contract in every warm-up and measured invocation. TensorRT uses the previously built library plans on the recorded RTX 3060/TensorRT 10.11 runtime; this does not establish plan portability to another GPU or runtime.

## Reproduction

Run the corresponding external test with the backend gate and paths documented in the PP-Structure README. Set ``DEPLOYSHARP_CHART2TABLE_BENCHMARK_REPORT_PATH`` to an output path, then summarize the three JSON files with ``pwsh -File eng/models/paddle-document/scripts/Summarize-Chart2TableSteadyBenchmark.ps1 -HarnessRevision <commit>``.
