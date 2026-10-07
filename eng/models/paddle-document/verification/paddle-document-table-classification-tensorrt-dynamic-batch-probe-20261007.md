# PP-LCNet TensorRT dynamic-batch probe (2026-10-07)

## Result

The official `paddle-table/pp-lcnet-x1-0-table-cls` ONNX graph accepts dynamic batch in vendor TensorRT 11.0. `trtexec` built an engine with `x` profile min/opt/max batch `1/2/2`, deserialized it, and executed `x=[2,3,224,224]` to `fetch_name_0=[2,2]` on an RTX 3060 Laptop GPU. This is a vendor-tool probe only: random inputs were used and both host/device transfers were disabled.

For this GPU-only probe, batch-query latency was P50 `0.417847 ms`, P95 `0.518143 ms`, mean `0.43232 ms`; throughput was `2304.08` batch queries/s. TensorRT reported `13.7767%` coefficient of variation for GPU compute time and warned that the measurement was unstable. These numbers exclude preprocessing, data transfers, DeploySharp session setup and decoding, and are not a library benchmark.

DeploySharp did not reach inference with this engine. Using the local TensorRT 11 bridge, provider runtime creation raised structured exception `3228369022` (`0xC06D007E`). A separate retry with the matching TensorRT 10.11 NuGet bridge also stopped at builder creation; the existing batch-1 classifier baseline and the new batch-2 test were both inconclusive in this current run. The historical 2026-10-04 TensorRT 10.11 batch-1 result remains an earlier exact run, but today's rerun did not reproduce it. The dynamic-batch TensorRT path therefore remains unverified in DeploySharp; no backend status checkmark is added from this probe.

## Artifacts and host

| Item | Value |
|---|---|
| ONNX SHA-256 | `04a862a81e3c466d6ce7ef8398ea2464e46dbbdbfaa53278ad2b42427be8eb45` |
| Engine SHA-256 / size | `c1d1e70a30f937cc4b238c8d070839fa9e8f19bd00bc5f5f428b18bb8ad3ef87` / `7,871,732` bytes |
| Host | Windows x64, OS build `10.0.26200`; RTX 3060 Laptop GPU, compute capability `8.6` |
| Driver / CUDA / cuDNN | `576.02` / `12.9` / `9.22` |
| TensorRT used by `trtexec` | `11.0.0.114-cu12` |
| Timing protocol | `trtexec`, 200 ms warm-up, 2 s measurement, CUDA Graph enabled, no H2D/D2H |

The machine-readable report includes both bridge hashes and the exact managed-path blocker: [JSON](paddle-document-table-classification-tensorrt-dynamic-batch-probe-20261007.json).

## Reproduction boundary

The vendor-side probe can be repeated with the exact ONNX and profile:

```powershell
$env:PATH = 'D:\Program Files\TensorRT-11.0.0.114-cu12\bin;D:\Program Files\TensorRT-11.0.0.114-cu12\lib;C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9\bin;D:\Program Files\cuDNN-9.22.0-cuda12.9\bin;' + $env:PATH
& 'D:\Program Files\TensorRT-11.0.0.114-cu12\bin\trtexec.exe' `
  '--onnx=E:\Model\PaddleDocument\onnx\pp-lcnet-x1-0-table-cls.onnx' `
  '--minShapes=x:1x3x224x224' '--optShapes=x:2x3x224x224' '--maxShapes=x:2x3x224x224' `
  '--saveEngine=<engine-path>' --skipInference
& 'D:\Program Files\TensorRT-11.0.0.114-cu12\bin\trtexec.exe' `
  '--loadEngine=<engine-path>' '--shapes=x:2x3x224x224' `
  '--duration=2' '--warmUp=200' '--percentile=50,95'
```

This command does not exercise DeploySharp's backend. The opt-in managed integration test is `TableClassificationRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows`; retry only after resolving the bridge/runtime initialization failure with a matching TensorRT bridge. Until that test produces two decoded rows and bounded ORT parity, dynamic TensorRT batch remains `△`.
