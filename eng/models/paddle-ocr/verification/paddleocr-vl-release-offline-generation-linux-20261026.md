# PaddleOCR-VL 1.6 published Bundle offline generation on Linux

Date: 2026-10-26. This closes one Release-provenance generation check, not the complete VL quality or performance gates.

The ten PaddleOCR-VL assets were first downloaded from the public `models-paddleocr` Release into a fresh Windows cache by the [live download check](paddleocr-vl-release-live-20261026.md), with size/SHA-256 verification and offline reopening. That exact cache was copied to an authorized Linux test host because the host could not reach GitHub directly. The Linux sample rehashed all nine manifest artifacts (`--bundle --verify-only`: `graphs=5 verified=9`), then opened the copied cache through `--release ... --offline`, created ONNX Runtime CPU Sessions, decoded a real JPEG with OpenCV, and generated OCR text. No model graph was substituted with the host's older exports.

| Identity / protocol | Value |
| --- | --- |
| Device | Ubuntu 22.04.5, kernel `6.8.0-138-generic`, Intel Core i7-1165G7, 15 GiB RAM, no swap |
| Runtime | .NET 10.0.9 with `DOTNET_ROLL_FORWARD=Major` for the `net8.0` sample; ONNX Runtime 1.28.0 CPU; OpenCV 5.0 native runtime |
| Model | `paddleocr-vl/1.6`, checkpoint `c5630abae1d940eafe0697512a0325494b02ab42`, five graphs |
| Bundle manifest SHA-256 | `ef9a3e6df5b619ddcf35f789aeca50980d22074e803eda8dfcd18c98aba9918b` |
| Input | Official `demo_1.jpg`, 500 x 500, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`, EXIF Orientation `3` |
| Sample assembly SHA-256 | `9fd462713f38ce10d4fb4d1d94c8af42efd3f00f0685b2648182f7df4ae655a3` |
| Generation | Greedy, `maximumNewTokens=512`, four ORT inference threads, one measured correctness run, no warm-up |

The default sample mode normalizes EXIF orientation for upright page coordinates. On this JPEG it reached EOS with **159 tokens**, but differed from the official raw-pixel reference. This is an input-orientation distinction, not evidence that the Bundle weights drifted. The new explicit `--raw-exif` mode preserves the encoded pixels as the official Pillow processor does. The same published Bundle then reached EOS with **162 tokens**, Token ID CSV SHA-256 `339d579620cc8c44eaa06f40b438fab73c8b5cdf35176e6ddd3f2456f087e314` and UTF-8 text SHA-256 `6a4c394384e6c36aead17f2f3149781ae4017427dd5828ef0941964c24c5862e`. Both hashes match `ocr-demo-1` in the [pinned official reference](paddleocr-vl-reference-summary-linux-20261023.json). An independent run using the official Patch binary and the same copied Bundle also reached EOS with 162 tokens and the same visible text.

The final `--release --offline --raw-exif` run reported generation time `19,311.2 ms`, process wall time `35.23 s` including startup/loading/verification, and maximum RSS `6,668,880 KiB` (about 6.36 GiB). The earlier `--bundle --raw-exif` run returned the same Token ID hash with generation time `19,506.7 ms`; two sequential correctness runs on an unlocked CPU are not P50/P95 or a controlled backend comparison. Native OpenCV/ORT libraries were reused from the same-version Linux validation environment and were not downloaded by this sample.

Reproduce on a host with the verified Release cache, matching native libraries and sufficient RAM:

```bash
DOTNET_ROLL_FORWARD=Major LD_LIBRARY_PATH=/path/to/native \
  dotnet PaddleOcrVlOnnx.dll --release /path/to/cache /path/to/demo_1.jpg \
  TextRecognition --offline --raw-exif
```

The cache path is the **client cache root**, not the Bundle directory; `--bundle` takes the latter. The [sample README](../../../../samples/02-visual/paddleocr-vl-onnx/README.md) documents both modes. This result covers one fixed OCR image and ORT CPU only. Other tasks from the newly downloaded cache, arbitrary images/EXIF/ICC variants, page-level quality, TensorRT/OpenVINO from this cache, formal `5+50` latency and long-running memory stability remain open.
