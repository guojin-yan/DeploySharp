# PP-Structure multi-page OpenVINO concurrency tuning (2026-10-05)

This report compares the two independent controls exposed by the multi-page evidence test: the maximum concurrent calls allowed by each visual Session and the page-level `RunManyConcurrentAsync` limit. It uses the same two-page `bus.jpg` workload, OpenVINO CPU provider, five warm-ups and 50 measurements for every row.

## Environment and fixed inputs

| Field | Value |
|---|---|
| Host | `JYPPX`, Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64, 16 logical processors |
| Backend | OpenVINO CPU |
| Input | `E:\Data\image\bus.jpg` duplicated as page indexes `0` and `1` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Models | `pp-lcnet-x1-0-doc-ori`, `pp-doclayout-l` |
| Source revision | `833e63d4156f49196e0aeed13fafe732c2ae73cb` |

Every iteration checked page order, source SHA and non-empty layout output. The complete samples and model hashes are in the [machine-readable tuning summary](paddle-document-multipage-openvino-concurrency-tuning-20261005.json); each row also links to its raw 50-sample evidence.

## Results

| Session concurrency | Page concurrency | Sequential P50/P95 (ms) | Concurrent P50/P95 (ms) | Concurrent delta P50/P95 | Raw evidence |
|---:|---:|---:|---:|---:|---|
| 1 | 1 | `561.8156 / 635.1391` | `582.6073 / 687.5513` | `+3.70% / +8.25%` | [s1/p1](paddle-document-multipage-openvino-s1-p1-20261005.json) |
| 1 | 2 | `633.1379 / 1256.4134` | `560.6427 / 1059.1884` | `-11.45% / -15.70%` | [s1/p2](paddle-document-multipage-openvino-s1-p2-20261005.json) |
| 2 | 1 | `583.1320 / 686.2640` | `643.5362 / 697.0025` | `+10.36% / +1.56%` | [s2/p1](paddle-document-multipage-openvino-s2-p1-20261005.json) |
| 2 | 2 | `566.1405 / 638.8373` | `638.5930 / 695.8447` | `+12.80% / +8.92%` | [s2/p2](paddle-document-multipage-openvino-s2-p2-20261005.json) |

The `session=1/page=2` row is the only candidate that reduced both P50 and P95 in this small run. The serial samples vary between rows, so this is a device-specific tuning candidate, not a default change. Applications should benchmark their real page count, model set and thermal state; the library keeps the conservative default of `2/2` in the external test and does not silently select a backend-specific value.

## Reproduction

Set `DEPLOYSHARP_PADDLE_DOCUMENT_SESSION_CONCURRENCY` and `DEPLOYSHARP_PADDLE_DOCUMENT_PAGE_CONCURRENCY` to each pair, keep the warm-up/measurement variables at `5/50`, and use a unique evidence path. The full command is shown in the [OpenVINO 5/50 protocol](paddle-document-multipage-concurrent-openvino-benchmark-20261005.md).

This report covers one Windows host, one input and one OpenVINO CPU configuration. It is not a quality evaluation, a model Batch benchmark, a universal optimum or a TensorRT page-concurrency result.
