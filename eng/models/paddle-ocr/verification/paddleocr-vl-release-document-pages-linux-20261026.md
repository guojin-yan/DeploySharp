# PaddleOCR-VL Release Bundle: two-page document sample on Linux

Date: 2026-10-26. This checks the public five-graph `paddleocr-vl/1.6` Bundle through the independent multi-page sample, not just the single-image generation sample. The Bundle was downloaded and SHA-256 checked from the public `models-paddleocr` Release into a fresh Windows client cache, then copied unchanged to the authorized Linux host because that host could not reach GitHub. Both entry points revalidated the local Bundle before opening ONNX Runtime Sessions; neither run downloaded from Linux.

| Identity | Value |
| --- | --- |
| Host | Ubuntu 22.04.5, kernel `6.8.0-138-generic`, Intel Core i7-1165G7, 15 GiB RAM, no swap |
| Runtime | .NET 10.0.9 with explicit major roll-forward for the `net8.0` sample; ONNX Runtime 1.28.0 CPU; OpenCV 5.0 native runtime |
| Model | `paddleocr-vl/1.6`, checkpoint `c5630abae1d940eafe0697512a0325494b02ab42`, five ONNX graphs; Bundle manifest SHA-256 `ef9a3e6df5b619ddcf35f789aeca50980d22074e803eda8dfcd18c98aba9918b` |
| Sample assembly | `PaddleOcrVlDocument.dll` SHA-256 `9ae20372a2777de801e40d9a9fdad1891622c0e9c94df2e9097920089bc1c8cb` |
| Input | Existing two-page, three-manual-table-region fixture: `pages.json` SHA-256 `af03f514ba01f8c6814aaa90102d0ff8cfa5dc4194550985228d595595235135`; page 7 PNG `01955520fe000b2f116d3a8b8ff6a022ab4006078ec12766f8a91dc20a835d08`; page 3 PNG `be7a55a2e3ff9923d403c79668da9aa82e29ec72c796b40ae66b6148538da19b` |

The sample was published for `ubuntu.22.04-x64` with `-p:UseRidGraph=true --self-contained false`. Its `--release <client-cache-root> <pages.json> <output> 2 --offline` run exited 0: page 7 had 2 succeeded/0 failed, page 3 had 1 succeeded/0 failed. All three regions reached EOS with 71 generated tokens each; strict OTSL parsing succeeded with 4 rows and 4 columns per region. The independent `--bundle <materialized-bundle-root> <pages.json> <output> 2` run also exited 0 with the same counts. `--bundle --offline` and duplicate `--offline` options were separately rejected with exit code 2 by the argument parser.

All 213 generated Token IDs, three texts, three crop input SHA-256 values and all three HTML outputs matched between the Release and Bundle runs. The same Token IDs, texts and input identities matched the earlier [four-graph page result](paddleocr-vl-regional-pages-linux-20261024.md). The three generated HTML files have identical SHA-256 `413d9fab89ad01168097c4149d0f9739092e672d6f62384af945f8ac3f39a7b8` in both runs. Raw page JSON/Markdown/HTML outputs remain in the test host's dedicated result directories and in the external `E:\DeploySharpEvidence` copy, not in Git.

Reproduce with the [document sample](../../../../samples/02-visual/paddleocr-vl-document/README.md), a verified Release cache and a `pages.json` with images at paths relative to that manifest:

```bash
env -u LD_LIBRARY_PATH DOTNET_ROLL_FORWARD=Major \
  dotnet PaddleOcrVlDocument.dll --release /path/to/client-cache \
  /path/to/pages.json /path/to/output 2 --offline
env -u LD_LIBRARY_PATH DOTNET_ROLL_FORWARD=Major \
  dotnet PaddleOcrVlDocument.dll --bundle /path/to/materialized-bundle \
  /path/to/pages.json /path/to/second-output 2
```

The Release run used 39.59 s process wall time and 5,897,740 KiB peak RSS; the Bundle run used 35.79 s and 6,775,172 KiB. Both are single correctness runs with model loading, file hashing, image decoding and output writing included, no warm-up, one shared VL Session and unlocked CPU clocks. They are **not** P50/P95, throughput, or an entry-point speed ranking. This fixture uses caller-supplied regions assembled from a pinned real table image. It does not establish automatic layout detection, independent multi-page labelled quality, other task semantics, other backends, arbitrary inputs, or long-running resource stability.
