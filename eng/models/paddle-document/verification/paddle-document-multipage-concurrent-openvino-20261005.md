# PP-Structure multi-page bounded concurrency: OpenVINO CPU (2026-10-05)

This record runs the same two-page orientation -> layout pipeline on the OpenVINO CPU provider. The two pages reuse `bus.jpg` only to exercise page ordering, provenance and stage concurrency; this is not a quality score or a formal repeated performance benchmark.

## Environment and inputs

| Field | Value |
|---|---|
| Host | `JYPPX`, Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64, 16 logical processors |
| Backend | OpenVINO CPU |
| Source | `E:\Data\image\bus.jpg`, page indexes `0` and `1` |
| Source SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Stage session concurrency | `2` |
| Page concurrency limit | `2` |
| Source revision | `a4bc341f608e332a438d625c049f4b4e8eb54af3` |

Orientation is `paddle-doc/pp-lcnet-x1-0-doc-ori` (SHA-256 `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`); layout is `paddle-doc/pp-doclayout-l` (SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`).

## Result

| Execution | Wall time | Output contract |
|---|---:|---|
| Sequential `RunManyAsync` | `898.9205 ms` | Two pages in caller order |
| Concurrent `RunManyConcurrentAsync(2)` | `644.0613 ms` | Two pages in caller order |

Both pages returned orientation `0_degree`, 300 layout regions and the exact source SHA. Per-page concurrent totals were `633.953 ms` and `628.3422 ms`; the concurrent wall time is lower than the sequential wall time in this single observation, but no speedup claim is generalized from it.

The machine-readable evidence, including stage timings and model hashes, is [paddle-document-multipage-concurrent-openvino-20261005.json](paddle-document-multipage-concurrent-openvino-20261005.json). Repeated OpenVINO 5/50 P50/P95 measurements remain open; the ORT repeated protocol is recorded separately in [paddle-document-multipage-concurrent-ort-benchmark-20261005.md](paddle-document-multipage-concurrent-ort-benchmark-20261005.md).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_MULTIPAGE = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_CONCURRENT_EVIDENCE_PATH = `
  'E:\Model\PaddleDocument\validation\paddle-document-multipage-concurrent-{backend}.json'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()

dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentMultiPageIntegrationTests.TwoPageOrientationLayoutBookPreservesPageOrderAndExports'
```

The OpenVINO gate requires the same two ONNX artifacts and `bus.jpg` as the ORT case. The evidence path uses `{backend}` so an ORT and OpenVINO run cannot overwrite each other.
