# PaddleOCR-VL-1.6 real-image Python reference — 2026-10-23

All six requested task prompts generated non-empty output and reached EOS on the pinned official 1.6 weights. The original upstream image processor and the recorded Transformers PIL processor produced exactly equal grids and normalized patch tensors for the six inputs. This establishes a reproducible Python correctness baseline; DeploySharp ONNX/C# model inference is still in development.

## Model, implementation and device

| Item | Actual configuration |
|---|---|
| Checkpoint | `PaddlePaddle/PaddleOCR-VL-1.6`, revision `c5630abae1d940eafe0697512a0325494b02ab42` |
| Weights | `model.safetensors`, 1,917,255,968 bytes, SHA256 `85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db`; verified on Windows acquisition and Linux loading |
| Device | Intel Core i7-1165G7, 4 cores / 8 logical CPUs, 16 GB RAM; Ubuntu 22.04.5 / kernel 6.8.0-138; [device snapshot](paddleocr-vl-reference-device-linux-20261023.json) |
| Execution | Python 3.10.12, CPU FP32, four PyTorch threads; the installed RTX 2060 was **not** used |
| Libraries | torch `2.10.0+cpu`, torchvision `0.25.0+cpu`, transformers `5.20.2`, tokenizers `0.23.2`, safetensors `0.8.0`, Pillow `12.3.0`, NumPy `2.2.6` |
| Processor | Explicit `PaddleOCRVLImageProcessorPil`; RGB/bicubic, 1/255, mean/std 0.5, patch 14, merge 2, temporal patch 1; Spotting uses the official two-step image preparation |
| Generation | Greedy, `do_sample=false`, maximum 512 new tokens, explicitly `use_cache=true`; raw checkpoint defaults to `use_cache=false` |
| Runtime source | Transformers built-in model/processor, module SHA256 recorded; no model-source patch, automatic remote-code execution or system Python modification |

The integrated Transformers implementation is separately identified from the older Python modeling source in the checkpoint. Pixel parity with the original source was actually tested; full original-model-versus-integrated-model generation parity and multimodal-position parity remain open. The loader reported the legacy `mrope_section` configuration notice and separate embedding/head weights; it retained the distinct checkpoint weights instead of tying them.

## Task results

These are single supplied image/region calls. They include no layout detector, ROI routing or multi-page pipeline. The fixed [input manifest](paddleocr-vl-reference-cases-20261023.json) records each image SHA256 and source.

| Task | Input | Image tokens | Generated tokens, including EOS | Termination | Single-run generation time |
|---|---|---:|---:|---|---:|
| OCR | User `demo_1.jpg`, 500×500 | 324 | 162 | EOS | 16.438 s |
| Table | Existing `table_recognition.jpg`, 551×132 | 150 | 71 | EOS | 7.278 s |
| Formula | Existing `general_formula_rec_001.png`, 448×64 | 160 | 80 | EOS | 8.056 s |
| Chart | Existing `chart_parsing_02.png`, 625×570 | 440 | 137 | EOS | 17.357 s |
| Spotting | User `demo_1.jpg`, 500×500 → 1000×1000 preparation | 1,296 | 286 | EOS | 52.426 s |
| Seal | Official `seal_text_det.png`, 640×640 | 529 | 29 | EOS | 11.132 s |

Timing scope is `generate` only after input preparation and model loading, with zero warm-up and one call per task. It is **not P50/P95 or a formal performance result**. CPU clocks and concurrent-load telemetry were not sampled continuously. The six-task raw report records the 534.8 ms model-load call separately; it must not be read as a cold-start disk benchmark. Nothing here measures GPU speed.

All 765 generated tokens and raw decoded outputs are retained in the [full six-task report](paddleocr-vl-six-task-reference-linux-20261023.json). Table output includes native structure markers such as `<fcel>`; Spotting output includes location markers. Parsing those into public HTML/coordinate results, correct geometry, labeled CER/WER, structure/formula/chart metrics and seal accuracy are still separate work. Reaching EOS does not prove output accuracy.

## Cache and pixel checks

- All six cached runs match an independently executed uncached first-eight-token reference.
- Every cached run has exactly one image-bearing prefill; subsequent calls contain one text token, increasing past length and no image tensor. This verifies actual dynamic KV use, not just a configuration flag.
- The complete formula generation was independently repeated with caching disabled: all 80 tokens, raw decoded text and EOS match the cached run. The raw [uncached formula record](paddleocr-vl-formula-uncached-reference-linux-20261023.json) took 191.974 s; these sequential smoke timings are not a controlled speedup benchmark. Other five tasks have prefix-only cache comparisons.
- [`Compare-PaddleOcrVlProcessors.py`](../scripts/Compare-PaddleOcrVlProcessors.py) imports the SHA-verified, unmodified original image-processing module and compares it to the integrated PIL processor on all six actual images. Shapes, image grids and every normalized patch value agree, maximum absolute difference zero; [raw comparison](paddleocr-vl-image-processor-parity-20261023.json). Generation pixel SHA256 is checked against that comparison.
- The C# tokenizer tests also consume these six real-image prompt/completion records: expanded IDs, image grids and both decoded output forms must match. This verifies token handling, not C# model inference or pixel processing.
- The [summary verifier](../scripts/Summarize-PaddleOcrVlReference.py) rejects incomplete runs, wrong checkpoint weights, truncated output, different uncached decisions, repeated image encoding, unrelated pixels and full formula token drift. Eight positive/negative verifier tests pass.

## Reproduce

Create an isolated Python 3.10 environment and install the [fixed CPU reference requirements](../scripts/paddleocr-vl-reference.requirements.txt). Acquire small checkpoint assets with [the asset inspector](../scripts/Inspect-PaddleOcrVlOfficialAssets.py), then download/verify the weights with [`Get-PaddleOcrVlWeights.py`](../scripts/Get-PaddleOcrVlWeights.py). Images stay outside Git; place the five manifest images in `inputs/` beside a copy of the manifest and verify their exact hashes.

```bash
python -m pip install -r eng/models/paddle-ocr/scripts/paddleocr-vl-reference.requirements.txt

python eng/models/paddle-ocr/scripts/Get-PaddleOcrVlWeights.py \
  --model-root /path/to/checkpoint \
  --report /path/to/results/weight-acquisition.json

HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1 \
python eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlReference.py \
  --model-root /path/to/checkpoint \
  --manifest /path/to/reference/paddleocr-vl-reference-cases-20261023.json \
  --output /path/to/results/generation-six-tasks.json \
  --use-cache --compare-uncached-tokens 8 --maximum-new-tokens 512

HF_HUB_OFFLINE=1 TRANSFORMERS_OFFLINE=1 \
python eng/models/paddle-ocr/scripts/Invoke-PaddleOcrVlReference.py \
  --model-root /path/to/checkpoint \
  --manifest /path/to/reference/paddleocr-vl-reference-cases-20261023.json \
  --output /path/to/results/generation-formula-uncached.json \
  --case formula-official-example --maximum-new-tokens 256
```

Generate the pixel report with `Compare-PaddleOcrVlProcessors.py --model-root ... --manifest ... --output ...`. Run the fail-closed report verifier with `Summarize-PaddleOcrVlReference.py`; its `--help` lists the generation, uncached formula, processor-parity, manifest and device inputs. The checked-in [validated summary](paddleocr-vl-reference-summary-linux-20261023.json) preserves their actual SHA256 identities and all module/runtime versions. Weight acquisition evidence is [separate](paddleocr-vl-weight-acquisition-20261023.json).

## Remaining work

Next acceptance items are original-model/position comparison, .NET pixel/patch execution, dynamic vision/projector/prefill/decode ONNX graphs, C# generation with independent request state, then ORT/OpenVINO/TensorRT validation and labeled quality/formal performance. ROI/layout/multi-page integration, runtime model catalog entries and downloadable Bundle assets are not yet complete. No supported-backend checkmark or software release is added by these Python reference results.
