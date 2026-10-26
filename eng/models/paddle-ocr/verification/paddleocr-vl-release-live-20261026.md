# PaddleOCR-VL 1.6 Release download verification

Date: 2026-10-26. This is a distribution and integrity check of the pinned five-graph ONNX Bundle, not a new generation or accuracy run.

The existing [`models-paddleocr` Release](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr) now contains 72 assets, including ten separately downloadable `paddleocr-vl-1.6-*` assets: five ONNX graphs, four tokenizer/template files and `paddleocr-vl-1.6-bundle.json`. The updated Release catalog maps these unique asset names to nine local Bundle files plus the manifest. The Release checksum list has 71 entries (it does not hash itself). The previous 62 assets were retained. The ten new asset sizes and SHA-256 values match both the catalog and checksum list.

`PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` was run against the published Release from an empty dedicated cache. It fetched the catalog and all ten assets, verified every declared size/SHA-256, and reopened the same Bundle with the client in offline mode. The external ModelFactory test `PublishedPaddleOcrVlBundleDownloadsAndReopensOffline` passed 1/1; a second run from the populated cache also passed 1/1. On the downloaded directory, the Visual test `PinnedFiveGraphBundleVerifiesAllRealFiles` passed 1/1, including full hashing of the nine manifest artifacts and the pinned prefill SHA.

The fresh-client cache is retained outside Git at `E:\DeploySharpEvidence\paddleocr-vl-release-sync-20261026\fresh-client-cache`. Reproduce the client check with at least 5 GB free:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_VL_RELEASE_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_VL_RELEASE_CACHE = 'E:\your-dedicated-cache'
dotnet test tests/DeploySharp.ModelFactory.Tests/DeploySharp.ModelFactory.Tests.csproj --filter 'FullyQualifiedName~PublishedPaddleOcrVlBundleDownloadsAndReopensOffline'
```

After download, set `DEPLOYSHARP_PADDLEOCR_VL_FIVE_GRAPH_BUNDLE_ROOT` to the returned Bundle directory and run `dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj --filter 'FullyQualifiedName~PinnedFiveGraphBundleVerifiesAllRealFiles'`. Do not assume that an arbitrary cache root is itself the Bundle directory.

The same pinned graph identities previously completed six fixed-task generations in the [TensorRT five-graph run](paddleocr-vl-tensorrt-five-graph-20261026.md), and the four original graphs completed ORT/OpenVINO generation separately. This download check did **not** create five inference Sessions or rerun a task from the fresh cache. It does not establish portable TensorRT Engines, arbitrary profiles, page-level quality, all-backend availability or a formal performance result.
