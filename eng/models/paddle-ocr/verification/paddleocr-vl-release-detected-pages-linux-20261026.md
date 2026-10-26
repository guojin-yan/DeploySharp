# PaddleOCR-VL Release Bundle: automatic two-page document sample on Linux

Date: 2026-10-26. This is a bounded correctness check of the public `paddleocr-vl/1.6` five-graph Bundle in the independent document sample, with a real PP-DocLayout-L detector. It extends the [Release manual-region check](paddleocr-vl-release-document-pages-linux-20261026.md); it does not measure labelled document accuracy or formal latency.

| Identity | Value |
| --- | --- |
| Host | Ubuntu 22.04.5, Intel Core i7-1165G7, 15 GiB RAM, no swap |
| Runtime | .NET 10.0.9 with `DOTNET_ROLL_FORWARD=Major`, ONNX Runtime 1.28.0 CPU, OpenCV 5.0 |
| Source | `d6912d5` plus the sample's verified-Release entry point; successful-run sample DLL SHA-256 `9ae20372a2777de801e40d9a9fdad1891622c0e9c94df2e9097920089bc1c8cb` |
| Bundle | Public `models-paddleocr` Release, five graphs and ten independent assets; manifest SHA-256 `ef9a3e6df5b619ddcf35f789aeca50980d22074e803eda8dfcd18c98aba9918b` |
| Detector | `paddle-doc/pp-doclayout-l` ONNX SHA-256 `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`; the completed generation used a separate file beside the page manifest |
| Pages | Two preselected FUNSD test images, page indices 7 and 3; page manifest SHA-256 `0fa3bdbe1151099c600bf2315660c5a12aec7d5aa48056df3a58d7d93630f0ff` |

The Bundle was downloaded into a fresh Windows cache and checked against its Release manifest, then copied unchanged to Linux. The Linux sample used `--release <cache> <pages.json> <output> 1 --offline`. It exited 0 and produced both page JSON/Markdown files. Its page-7 table exported a 28-row, 6-column HTML table. Against the earlier [independent four-graph detector/reference run](paddleocr-vl-detected-pages-linux-20261024.md), structured comparison found zero mismatches in page input SHA, all eight detection categories/scores/bounds, region indices, routing and skip status. All six generated regions matched task, EOS, 653/653 Token IDs and complete decoded texts. The table HTML exactly matched the pinned [official PaddleX OTSL reference](paddleocr-vl-otsl-official-reference-20261024.json).

| Page | Detected | Generated | Skipped | Generated tokens |
| --- | ---: | ---: | ---: | ---: |
| 7 | 4 | 3 | 1 image | 633 |
| 3 | 4 | 3 | 1 image | 20 |

The raw outputs remain outside Git at `E:\DeploySharpEvidence\paddleocr-vl-release-detected-pages-20261026` and on the test host under `/home/ygj/deploysharp-validation/paddleocr-vl-release-detected-pages-20261026`. Their SHA-256 identities are `page-7.json` = `e4bd438fb8c9f061059aae702361b1bfcf4b81ecaf8d08b83370627e27a06dcf`, `page-3.json` = `0e0de6175c04ef97b0ba77ad9829d9c92ce20071751ea06a64dfb8c72bf19f27`, and `page-7-region-0.table.html` = `bd3c17d0ef53545d6e09b7baa98d924fecdef835eb67d29d92b2d061dd362511`. Reproduction requires the [document sample](../../../../samples/02-visual/paddleocr-vl-document/README.md), the separately provisioned detector of the SHA above, the two original FUNSD PNGs and the verified Release cache.

The successful run preceded a small follow-up fix: the sample previously read `layoutDetector.sha256` without checking it. The updated sample now validates the detector file before creating any Session; the new DLL SHA-256 is `9b0f6b589a60edf98dbb1d4a74c2e21496fa9128907bfac68f40b20397417467`. A wrong digest failed with `InvalidDataException`; the correct digest passed that guard and reached the deliberately invalid page-parallelism argument check. The full two-page generation was **not** rerun with the guard-only change. The repeated inference process started during that check was stopped by exact process identity, and no matching process remains.

The one completed run took 2:13.43 process wall time and reached 9,652,780 KiB peak RSS, including Bundle verification, model loading and output writing. It had no warm-up or locked clocks, so neither number is P50/P95, steady-state latency or a backend ranking. Two FUNSD pages and exact reference parity do not prove labelled reading-order, layout recall, OCR/table semantic quality, arbitrary inputs, other backends or long-running stability. The image category is intentionally skipped and could conceal misclassified text.

## Release detector acquisition follow-up

The sample now accepts a missing `layoutDetector.path` in `--release` mode and obtains `paddle-doc/pp-doclayout-l` through `PaddleOcrReleaseClient.GetModelAsync`. The client verifies the individual asset against the Release catalog; an optional manifest `sha256` additionally pins that catalog identity. Local-path mode retains direct file hashing, while `--bundle` and the legacy positional mode still require an explicit detector path. The new `--verify-only` form checks all selected assets before any inference Session or page image is opened.

Using the existing fresh five-graph cache, Windows x64 `.NET 8` `--release <cache> <manifest> --verify-only` fetched the additional 129,356,945-byte detector from the public Release and returned `bundle=paddleocr-vl/1.6 graphs=5 verified=9` with the detector cache path. An independent SHA-256 check of that downloaded file was `d65ead6d31c0535b99b7976840f038ba2e7e7be060476d0c413f72dca86b78a9`. The same command with `--offline` returned 0 from the complete cache; a wrong manifest hash returned `InvalidDataException`. `--bundle` without a detector path was rejected, while `--bundle --verify-only` with the matching local file returned 0. The three tiny verification manifests are retained outside Git alongside the raw page outputs. No full generation was repeated through this newly added acquisition branch; the earlier Linux inference result above remains the correctness evidence for the same pinned detector bytes and VL Bundle.
