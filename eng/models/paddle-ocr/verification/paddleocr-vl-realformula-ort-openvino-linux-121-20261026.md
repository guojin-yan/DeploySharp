# PaddleOCR-VL-1.6 realFormula: Linux ORT CPU / OpenVINO CPU, 121 images

Date: 2026-10-26. The fixed four-graph FP32 ONNX export and official tokenizer ran the 121 annotated MathNet realFormula v1 images on one Ubuntu 22.04.5 x64 host. Both backends used the production C# `PaddleOcrVlOnnxSession`, raw-EXIF OpenCV image preparation, `FormulaRecognition`, greedy generation and a 512-new-token limit. The dataset manifest SHA-256 is `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76` and the annotation CSV SHA-256 is `b7a4679f7d9203f1040c3945aaceca5b73c3d110a70dd35487d4ec5462d8d6f1`.

Each backend ran **25 separate processes**, at most five manifest-contiguous images per process, with one image in the last process. This bounded OpenVINO's native memory on the 16 GiB/no-swap host. It is not a one-Session 121-image soak or a latency benchmark. The same published test runner, model, tokenizer, image files and preprocessing protocol were used for both backends. The source marker is commit `5a821bd903725609784f478ed00401283bf123a0`; the Visual, Visual.OpenCV and test assembly SHA-256 values are respectively `f84469f65f0204b226410f1e7b87e67811334bafb9353c3289c3a1f8463173cd`, `8fa8eae0ae8cc7a55be45e3899fdd0b2ffb1d88c7dea1a0bcfec52922f5d7b0f`, and `8851387147100b4fc5030950be3bbf7fede97c54ee04b1e5ee66bc48b13f76fe`. The source marker is not a claim of a clean checkout; these assembly hashes identify the actual binaries used. Each JSON also records the four graph and tokenizer hashes. The opt-in test dependencies were OpenCV Ubuntu 22.04 native 5.0.0, OpenVINO native 2026.3.1 and ORT 1.28.0; the host used .NET 10.0.9, `MALLOC_ARENA_MAX=2` and a 1 GiB *managed-heap* limit. No process RSS limit is implied.

| Measure | ONNX Runtime CPU | OpenVINO CPU |
| --- | ---: | ---: |
| Annotated images completed | 121 / 121 | 121 / 121 |
| EOS / MaxTokens / empty output | 121 / 0 / 0 | 121 / 0 / 0 |
| Exact match after removing whitespace | 0 / 121 | 0 / 121 |
| Corpus character error rate | 55.1696% | 55.1696% |
| Mean per-image character error rate | 60.0780% | 60.0780% |
| Separate process shards | 25 | 25 |

The [same-build comparison JSON](paddleocr-vl-realformula-ortcpu-openvino-linux-121-20261026.json) reports **121/121 exact decoded text, generated Token IDs, Token counts, finish reasons and annotation character edits**. It rejects different manifest/CSV, model graph/tokenizer, source/assembly identity and generation protocol. There were no divergent images. The merged per-image predictions, labels, Token IDs and constituent shard SHA-256 values are retained in the [ORT CPU JSON](paddleocr-vl-realformula-ortcpu-linux-121-20261026.json) and [OpenVINO CPU JSON](paddleocr-vl-realformula-openvino-linux-121-20261026.json).

| Checked artifact | SHA-256 |
| --- | --- |
| ORT CPU merged JSON | `ce487d51d25f4c31b18bbd01581d55495dadef289049233f66f9dc71317e631e` |
| OpenVINO CPU merged JSON | `7e402d999499e09f429f47475f9a2984930035b61313c51023af4e95573297f7` |
| Backend comparison JSON | `762abc41a0977d81aeb1b331a24122f1c6a9d7f5f6885f5b2c4eeb1620adc8b4` |
| External ORT shard archive | `e171ba6c82455af1253d35c1b7cd826ab822e5f5906bc5d12b75596f5563596e` |
| External OpenVINO shard archive | `77f7e59e2ec3e25591775174985a775f92127bd4a9f07d752d3835e60416f361` |

The two shard archives remain outside Git under `E:/DeploySharpEvidence`; the merged JSONs contain a SHA for every original shard. The corpus CER is a whitespace-normalized UTF-16 Levenshtein measure, **not mathematical semantic equivalence**. Different but equivalent LaTeX can count as wrong. Training-set overlap is unknown, and 0/121 strict exact matches is a quality warning, not a passing formula-quality gate. No conclusion follows for other PaddleOCR-VL weights, OpenCV DNN, TensorRT formula quality, page composition or stable memory bounds. Earlier OpenVINO OOM observations remain valid for different/default memory configurations.

## Reproduce

Publish the opt-in Ubuntu test runner with the distro RID and RID graph, as described in the [five-image probe](paddleocr-vl-realformula-openvino-linux-probe-20261026.md). On the Linux host, set `DOTNET_ROOT` to the local .NET installation and run each backend with the same paths and full source commit:

```bash
bash eng/models/paddle-ocr/scripts/Run-PaddleOcrVlRealFormulaShards.sh \
  /path/to/published-runner /path/to/four-graph-bundle \
  /path/to/pinned-tokenizer /path/to/realFormula-public \
  /path/to/new-reports/openvino-cpu openvino-cpu \
  5a821bd903725609784f478ed00401283bf123a0
# Repeat into a distinct output directory with backend onnxruntime-cpu.
```

The runner refuses to overwrite existing shards. Recover all 25 JSON reports per backend, pass them to `Merge-PaddleOcrVlRealFormulaShards.ps1` with the pinned manifest, then pass the two completed merged reports to `Compare-PaddleOcrVlRealFormulaBackends.ps1`. The merger checks source image/annotation identities and manifest order before reporting 121 completed rows. None of these one-pass timings should be promoted to formal P50/P95 or a backend speed ranking.
