# PaddleOCR-VL 1.6 five-graph local Bundle

Date: 2026-10-26. This record covers local packaging and integrity, not a GitHub Release download or a new inference benchmark. The pinned four original ONNX graphs and tokenizer sidecars remain usable as the eight-file v1 manifest. The optional derived no-past `prefill.onnx` produces a nine-file v2 manifest; the Release catalog then contains ten independent assets including the manifest.

## Pinned identity

The original decoder SHA-256 is `823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad`. The derived prefill SHA-256 is `9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc`. The writer refuses a five-graph package unless both match these fixed identities. The checkpoint revision remains `c5630abae1d940eafe0697512a0325494b02ab42`; this is not evidence for other PaddleOCR-VL weights.

| Local role | File size | SHA-256 |
| --- | ---: | --- |
| vision | 1,756,119,437 | `940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f` |
| embedding | 423,624,948 | `1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86` |
| decoder | 1,019,701,197 | `823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad` |
| head | 423,624,923 | `8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148` |
| prefill | 1,019,700,335 | `9247b437a1551652572b3e25162d4d86fc969edf12ae44577aa6fc75b9fc5bdc` |

The v2 `paddleocr-vl-bundle.json` SHA-256 is `ef9a3e6df5b619ddcf35f789aeca50980d22074e803eda8dfcd18c98aba9918b`; its `schemaVersion` is `deploysharp-paddleocr-vl-bundle-v2`, and its `artifacts` array lists all five graphs and four tokenizer files with sizes and SHA-256 values. The local output directory is `E:\DeploySharpEvidence\paddleocr-vl-five-graph-bundle-20261026` (10 files including the manifest, 4,655,764,785 bytes). Binary files are not committed to Git.

## Reproduce

1. Start with the fixed four-graph export and official tokenizer directory. Derive `prefill.onnx` using [`Derive-PaddleOcrVlPrefill.py`](../scripts/Derive-PaddleOcrVlPrefill.py); its ONNX checker and ORT CPU comparison of all 37 outputs must pass.
2. Run [`Write-PaddleOcrVlBundleManifest.py`](../scripts/Write-PaddleOcrVlBundleManifest.py) with `--graph-root`, `--tokenizer-root`, `--prefill-graph` and a fresh `--output-root`. Without `--prefill-graph`, it retains the eight-file v1 format. With it, the writer checks both pinned graph hashes before copying and emits v2.
3. Call `PaddleOcrVlBundleManifest.LoadFromDirectory(root).VerifyFiles(root)` before creating any Session. The production `PaddleOcrVlOnnxSession.CreateVerified` now creates the fifth Prefill Session only for v2; v1 still creates four. `PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` accepts the nine-asset v1 or ten-asset v2 catalog contract, with each asset downloaded and hashed separately. TensorRT still requires Engines built for the target device and input profile; this ONNX Bundle does not contain portable Engines.
4. Run `Publish-PaddleOcrModelsToGitHub.ps1 -PaddleOcrVlBundleRoot <root> -Check` before staging or upload. It validates every declared local role, name, format, size and SHA. The actual read-only check returned `vlGraphs=5 vlAssets=10`; the original v1 Bundle returned `vlGraphs=4 vlAssets=9`.

The real v2 Bundle passed `PinnedFiveGraphBundleVerifiesAllRealFiles` (`1/1`), including full file hashing. The complete Visual test project passed `703` tests with `7` optional external skips; the complete ModelFactory test project passed `67` with `2` optional external skips. Re-running the writer against the same output kept the manifest SHA unchanged. The separate [TensorRT generation report](paddleocr-vl-tensorrt-five-graph-20261026.md) proves six fixed tasks reached EOS and matched 765 reference tokens/text using the same pinned graphs, but this packaging check did not repeat generation. Current host free memory was about 4.3 GiB, so a second five-Session ORT load was not used as a packaging smoke.

## Distribution boundary

At the time of this local packaging check, the remote Release had 62 assets and no PaddleOCR-VL files. The subsequent [live distribution check](paddleocr-vl-release-live-20261026.md) records the ten-asset upload, fresh-cache download, offline reopen and nine-file Visual manifest verification; the Release now has 72 assets. A fixed generation task was **not** rerun from that fresh cache. Formal TensorRT performance beyond the fixed Table slice, page-level quality, arbitrary Engine profiles, complete device-resident generation and other PaddleOCR-VL versions remain separate gates.
