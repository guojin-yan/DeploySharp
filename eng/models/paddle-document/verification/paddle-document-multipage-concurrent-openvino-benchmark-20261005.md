# PP-Structure multi-page bounded concurrency: OpenVINO CPU 5/50 benchmark (2026-10-05)

This record repeats the same two-page orientation -> layout pipeline on the OpenVINO CPU provider. Both pages reuse `bus.jpg` only to exercise page ordering, provenance and stage concurrency; the run is not a document-quality evaluation.

## Environment and protocol

| Field | Value |
|---|---|
| Host | `JYPPX`, Windows `10.0.26200` |
| Runtime | `.NET 10.0.12`, x64, 16 logical processors |
| Backend | OpenVINO CPU |
| Source | `E:\Data\image\bus.jpg`, duplicated as page indexes `0` and `1` |
| Source SHA-256 | `33b198a1d2839bb9ac4c65d61f9e852196793cae9a0781360859425f6022b69c` |
| Warm-ups / measurements | `5 / 50` per execution mode |
| Stage session concurrency | `2` |
| Page concurrency limit | `2` |
| Source revision | `28c1f4751264500954ef63aa129380e2eb869ea6` |

The orientation model is `paddle-doc/pp-lcnet-x1-0-doc-ori` (SHA-256 `96e898f047a0e460ba0652e9afb8c874e53872821cfd7a3fec53a5ab62df92f0`); the layout model is `paddle-doc/pp-doclayout-l` (SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`). Every measured result checked page order, source SHA and non-empty layout output.

## Results

| Execution | Min (ms) | P50 (ms) | P95 (ms) | Max (ms) |
|---|---:|---:|---:|---:|
| Sequential `RunManyAsync` | 548.9957 | 563.6314 | 647.9142 | 676.5299 |
| Concurrent `RunManyConcurrentAsync(2)` | 603.1573 | 631.2580 | 664.9269 | 683.0532 |

The concurrent mode was slower than sequential on this CPU workload: approximately `+12.0%` at P50 and `+2.6%` at P95. This is evidence that the stage session thread setting and page-level concurrency must be tuned together; it does not indicate a correctness failure. Both modes returned two pages in caller order, with orientation `0_degree` and `300` layout regions per page.

The machine-readable samples, model hashes and per-run contract are in [paddle-document-multipage-concurrent-openvino-benchmark-20261005.json](paddle-document-multipage-concurrent-openvino-benchmark-20261005.json). The ORT CPU 5/50 comparison is in [paddle-document-multipage-concurrent-ort-benchmark-20261005.md](paddle-document-multipage-concurrent-ort-benchmark-20261005.md).

## Reproduction

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_OPENVINO_MULTIPAGE = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_BENCHMARK = '1'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_WARMUPS = '5'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_MEASUREMENTS = '50'
$env:DEPLOYSHARP_PADDLE_DOCUMENT_MULTIPAGE_CONCURRENT_EVIDENCE_PATH = `
  'E:\Model\PaddleDocument\validation\paddle-document-multipage-concurrent-openvino-benchmark-{backend}.json'
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim()

dotnet test .\tests\DeploySharp.Visual.OpenCV.Tests\DeploySharp.Visual.OpenCV.Tests.csproj `
  -f net10.0 --no-restore `
  --filter 'FullyQualifiedName~PaddleDocumentMultiPageIntegrationTests.TwoPageOrientationLayoutBookPreservesPageOrderAndExports' `
  --logger 'console;verbosity=minimal'
```

This benchmark covers one host, one input and one OpenVINO CPU configuration. It is not a model Batch benchmark, a cross-device performance claim or a TensorRT page-concurrency result.
