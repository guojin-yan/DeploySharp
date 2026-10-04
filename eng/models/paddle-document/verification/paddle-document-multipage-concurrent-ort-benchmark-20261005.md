# PP-Structure multi-page bounded concurrency: ORT CPU benchmark (2026-10-05)

This record repeats the real orientation -> layout two-page case with the same `PaddleDocumentPipeline` instance. It compares sequential `RunManyAsync` with the explicit page-level `RunManyConcurrentAsync` path; it is not a tensor Batch benchmark and does not measure document quality.

## Protocol

| Field | Value |
|---|---|
| Host | `JYPPX`, Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64, 16 logical processors |
| Backend | ONNX Runtime CPU |
| Input | `E:\Data\image\bus.jpg`, duplicated as page indexes `0` and `1` |
| Input SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Warm-ups | `5` per execution mode |
| Measurements | `50` per execution mode |
| Stage session concurrency | `2` |
| Page concurrency limit | `2` |
| Source revision | `3bbfa09f69f2cad63cc4c747322756fc2bfdc785` |

Orientation is `paddle-doc/pp-lcnet-x1-0-doc-ori` (SHA-256 `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`); layout is `paddle-doc/pp-doclayout-l` (SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`). Every iteration checked page order, source SHA and non-empty layout output.

## Results

| Execution mode | P50 | P95 | Min | Max |
|---|---:|---:|---:|---:|
| Sequential `RunManyAsync` | `841.0031 ms` | `918.2798 ms` | `769.6060 ms` | `972.1842 ms` |
| Concurrent `RunManyConcurrentAsync(2)` | `708.4469 ms` | `873.7787 ms` | `665.2102 ms` | `916.3753 ms` |

On this one host and protocol, the concurrent median was approximately `15.8%` lower and the P95 approximately `4.8%` lower than the sequential median/P95. This is an observation for the duplicated `bus.jpg` two-page workload, not a general speedup promise. The run does not control GPU clocks, compare devices, or establish OpenVINO/TensorRT behavior; those remain separate validation items.

The complete per-iteration samples and machine metadata are in [the JSON report](paddle-document-multipage-concurrent-ort-benchmark-20261005.json). The single-run provenance record remains available separately as [paddle-document-multipage-concurrent-ort-20261005.md](paddle-document-multipage-concurrent-ort-20261005.md).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_BENCHMARK = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_MEASUREMENTS = '50'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_CONCURRENT_EVIDENCE_PATH = `
  'E:\Model\PaddleDocument\validation\paddle-document-multipage-concurrent.json'

dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentMultiPageIntegrationTests.TwoPageOrientationLayoutBookPreservesPageOrderAndExports'
```

The external gate requires the two ONNX files and `bus.jpg` at the paths used by the test. The test fails closed when an asset is missing or the gate is disabled.
