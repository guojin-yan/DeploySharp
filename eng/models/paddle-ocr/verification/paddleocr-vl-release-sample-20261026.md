# PaddleOCR-VL 1.6 Release sample smoke

Date: 2026-10-26. Source marker: `65e5019+working-tree-release-sample`. This verifies the public sample entry point and published Bundle integrity, not model generation.

The `samples/02-visual/paddleocr-vl-onnx` project now accepts an image through `--release <cache-root> <image-path> <task>` or a previously materialized manifest directory through `--bundle <bundle-root> <image-path> <task>`. The Release mode obtains `paddleocr-vl/1.6` with `PaddleOcrReleaseClient`, then uses the isolated local directory for the pinned tokenizer and `PaddleOcrVlOnnxSession.CreateVerified`. `OpenCvPaddleOcrVlInputFactory` decodes and prepares the image. The legacy six-argument official float32 patch mode continues to use the original four-graph export-directory loader and does not require a manifest.

| Check | Result |
| --- | --- |
| `net8.0` Release sample build with `SuppressTfmSupportBuildWarnings=true` | 0 warnings, 0 errors |
| `--help` | Lists Release, Bundle, offline, verify-only and original patch modes |
| `--release <existing fresh-client-cache> --verify-only --offline` | Exit 0; fixed `paddleocr-vl/1.6`, 5 graphs and 9 manifest artifacts verified |
| `--bundle <downloaded-bundle-directory> --verify-only` | Exit 0; same 5 graphs and 9 artifacts verified |
| Missing image path in offline Release mode | Exit 2 before download or Session creation |
| `OpenCvPaddleOcrVlInputFactoryTests` | 8 passed, 1 optional external fixture skipped |

The cache was produced by the independent [fresh Release download test](paddleocr-vl-release-live-20261026.md) and is retained outside Git at `E:\DeploySharpEvidence\paddleocr-vl-release-sync-20261026\fresh-client-cache`. The local manifest directory is the `bundles/paddleocr-vl_1.6-2323c453` child of that cache. Both verify-only checks hashed the real nine local files; they did not initialize ONNX Runtime Sessions or decode an image. Reproduce the sample check from the repository root with:

```powershell
dotnet run --project samples/02-visual/paddleocr-vl-onnx/PaddleOcrVlOnnx.csproj -c Release -- `
  --release E:\DeploySharpEvidence\paddleocr-vl-release-sync-20261026\fresh-client-cache --verify-only --offline
```

The current Windows host had approximately 2.8 GiB free physical memory at the decision point, while the Bundle files alone total about 4.66 GB; no five-Session generation was attempted on that host. Public-key SSH to the previously used Linux account was rejected, so this check did not move to the remote host. A fixed image generation from the newly downloaded cache, its EOS/token/text comparison and target-device memory/performance evidence remain open. Existing independent fixed-graph generation records are not reclassified as a fresh-download execution result.
