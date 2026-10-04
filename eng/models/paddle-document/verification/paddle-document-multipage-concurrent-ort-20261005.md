# PP-Structure multi-page bounded concurrency: ORT CPU (2026-10-05)

This record exercises the real PP-Structure orientation -> layout stages with two independent pages and the new `PaddleDocumentPipeline.RunManyConcurrentAsync` API. It is an execution/provenance and one-host timing observation, not a quality score or a formal 5/50 performance benchmark.

## Environment and inputs

| Field | Value |
|---|---|
| Host | `JYPPX` |
| OS | Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64 |
| CPU threads reported | 16 |
| Backend | ONNX Runtime CPU |
| Source | `E:\Data\image\bus.jpg` |
| Source SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Pages | 2, page indexes `0` and `1` |
| Session concurrency per stage | 2 |
| Page concurrency limit | 2 |
| Source revision | `27c230126ba2bfda09fcbc35df2165e6e3a92454` |

Models:

- Orientation: `paddle-doc/pp-lcnet-x1-0-doc-ori`, SHA-256 `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`.
- Layout: `paddle-doc/pp-doclayout-l`, SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`.

## Result

| Execution | Wall time | Notes |
|---|---:|---|
| Sequential `RunManyAsync` | `882.267 ms` | Two pages executed in caller order |
| Concurrent `RunManyConcurrentAsync` | `791.9943 ms` | Hard page limit 2; same Pipeline uses two independent Sessions per stage |

The concurrent observation is about 10.2% lower than the sequential wall time in this single run. It must not be generalized as a device throughput ranking because the run uses one input duplicated across two pages, one warm-up state, and no 5/50 repetition.

Both pages returned orientation `0_degree`, 300 layout regions, the expected page indexes, and the exact source SHA. Per-page concurrent timings were:

| Page | Orientation | Layout | Total |
|---:|---:|---:|---:|
| 0 | `22.328 ms` | `706.3651 ms` | `728.7042 ms` |
| 1 | `20.4873 ms` | `751.2216 ms` | `771.7133 ms` |

Machine-readable evidence is [paddle-document-multipage-concurrent-ort-20261005.json](paddle-document-multipage-concurrent-ort-20261005.json).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_CONCURRENT_EVIDENCE_PATH = `
  'E:\Model\PaddleDocument\validation\paddle-document-multipage-concurrent.json'

dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentMultiPageIntegrationTests.TwoPageOrientationLayoutBookPreservesPageOrderAndExports'
```

The external gate requires the two ONNX files and `bus.jpg` at the paths above. The test fails closed when the gate is disabled or an asset is missing. The API does not create a true tensor Batch; it only overlaps independent page executions through independently-created backend Sessions.
