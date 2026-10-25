# PaddleOCR-VL-1.6 release bundle verification

Date: 2026-10-25
Release: [models-paddleocr](https://github.com/guojin-yan/DeploySharp/releases/tag/models-paddleocr)
Bundle ID: `paddleocr-vl/1.6`

## What was published

The stable model release contains PaddleOCR-VL-1.6 as nine independently downloadable assets. The four ONNX graphs are the fixed dynamic FP32 export used by `PaddleOcrVlOnnxSession`; the remaining files are the pinned tokenizer, template and manifest sidecars.

| Role | Release asset | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| vision | `paddleocr-vl-1.6-vision.onnx` | 1,756,119,437 | `940600685c0b69453bbfb28371fee5c89782c2ea47fb0ab6f52876ad9f46dd4f` |
| embedding | `paddleocr-vl-1.6-embedding.onnx` | 423,624,948 | `1d268b40696b0f1ebf8ee67f6706a9e2feb3b64b71484d6fa87ec624ff734d86` |
| decoder | `paddleocr-vl-1.6-decoder.onnx` | 1,019,701,197 | `823f85b8f634f8522d11c4e0b33bed8edfcd18da65f643f0aa43ca496fd138ad` |
| head | `paddleocr-vl-1.6-head.onnx` | 423,624,923 | `8663d1fd3f2ca1cf2af308c76fadefcee12e65a6a1f402603a81085daa6d0148` |
| tokenizer model | `paddleocr-vl-1.6-tokenizer.model` | 1,614,363 | `34ef7db83df785924fb83d7b887b6e822a031c56e15cff40aaf9b982988180df` |
| tokenizer JSON | `paddleocr-vl-1.6-tokenizer.json` | 11,189,060 | `c8a215a59183d0d0781adc33bacd3ce6162716f7fd568fb30234a74d69803a7d` |
| tokenizer config | `paddleocr-vl-1.6-tokenizer-config.json` | 186,947 | `1f979337347cc0cb72a6282d8a23ed183539aa81a87a906f022aee2bab83c7c5` |
| chat template | `paddleocr-vl-1.6-chat-template.jinja` | 1,474 | `2f27812dab7f333e471884e0c803d807f11953d5453140dfb1aaba234f872bc8` |
| manifest | `paddleocr-vl-1.6-bundle.json` | 1,960 | `58dd1e945563f7c2f0f49a5bde094971e7a3d0e5797b39c0f46c00c2b98db0f4` |

The same `models-paddleocr` release retains the seven independently downloadable PP-Chart2Table assets. The generated catalog contains both `paddle-chart/pp-chart2table` and `paddleocr-vl/1.6`; the release-level `SHA256SUMS` covers all assets. The staged catalog and checksum file used for this upload were:

```text
paddleocr-release-catalog.json  bfdf6b54a6b7ba0137c9dc9f66ebed9afd6a589da35f2ef8d0eb48ae6426219c
SHA256SUMS                     78f12576f989f339689f744a1fd492636afe918b62e95c1498b1ec9c502a1684
```

## Download and cache behavior

`PaddleOcrReleaseClient.GetBundleAsync("paddleocr-vl/1.6")` reads the catalog, downloads only the nine declared assets, checks size and SHA-256, and materializes the local names required by `PaddleOcrVlOnnxSession.CreateVerified`. The globally unique Release names are deliberately different from the bundle-local names (`vision.onnx`, `tokenizer_config.json`, and so on). Chart2Table and PaddleOCR-VL therefore use separate deterministic cache directories even when their sidecars have the same local filename.

The cache isolation regression covers online download, offline reuse and conflicting remote asset names. The focused ModelFactory suite passed with `6 passed / 0 skipped / 0 failed` on the development checkout. This verifies the download contract; it does not prove that a downloaded graph is supported by every execution backend.

## Runtime evidence and support boundary

The fixed bundle has production C# generation evidence on Linux x64 with ONNX Runtime CPU and OpenVINO CPU. OCR, table, formula, chart, spotting and seal prompts reached EOS and matched the pinned official reference tokens/text. The OpenCV adapter can decode an image once, prepare the official RGB patch tensor, and feed the same session; page/region and bounded multi-page orchestration are covered by the linked examples.

The support matrix remains deliberately narrower than the download catalog:

| Backend | Status | Evidence boundary |
| --- | --- | --- |
| ONNX Runtime CPU | `✓` | Linux x64 six-task generation and exact token/text comparison |
| OpenVINO CPU | `✓` | Linux x64 six-task generation and exact token/text comparison |
| ONNX Runtime CUDA | `△` | RTX2060 6 GB partial memory experiment; Spotting allocation still fails |
| TensorRT | `△` | No complete four-graph real-model run |
| OpenCV DNN | `△` | No complete four-graph real-model run |
| Windows | `△` | Bundle is downloadable, but no Windows VL acceptance run is recorded |

The export also retains a known boundary: one intermediate Spotting vision feature comparison exceeded the strict elementwise threshold, while the production greedy tokens/text matched. The release must therefore be described as an experimental PaddleOCR-VL ONNX generation path, not as universal multi-backend or intermediate-tensor parity.

## Reproduction

1. Download the catalog and the nine assets listed under `paddleocr-vl/1.6` from the stable release.
2. Let `PaddleOcrReleaseClient` verify and materialize the bundle, or place the files in a local bundle directory with the manifest.
3. Run `samples/02-visual/paddleocr-vl-onnx` for a single prepared patch, or `samples/02-visual/paddleocr-vl-document` for decoded pages, region routing and JSON/Markdown export.
4. Select ONNX Runtime CPU or OpenVINO CPU until an additional backend has its own real-model evidence.

This report records asset publication and client reproducibility. It does not convert experimental status into a general quality, latency or backend-support claim.
