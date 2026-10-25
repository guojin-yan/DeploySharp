# PaddleOCR-VL-1.6 official asset and input-contract inspection (2026-10-23)

Historical status at the time of this 2026-10-23 inspection: **input preparation foundations only; model inference had not yet been integrated**. This report records the official source assets and scalar/tokenizer checks before the later ONNX export and C# generation work. It is not itself an exported ONNX Bundle or a backend result; the current implementation and downloadable Bundle status are documented in the PaddleOCR README and current VL verification reports.

## Exact source and acquisition

- Model: [PaddlePaddle/PaddleOCR-VL-1.6](https://huggingface.co/PaddlePaddle/PaddleOCR-VL-1.6/tree/c5630abae1d940eafe0697512a0325494b02ab42), pinned revision `c5630abae1d940eafe0697512a0325494b02ab42`.
- Algorithm: [official description at a fixed PaddleOCR revision](https://github.com/PaddlePaddle/PaddleOCR/blob/dab3fe35379033fdcb2d0e9572fac0b36c9a9ebf/docs/version3.x/algorithm/PaddleOCR-VL/PaddleOCR-VL-1.6.en.md).
- Acquired 17 configuration, tokenizer, processor/model source, template and ancillary files; cache is outside Git. Every file was checked against its pinned Git blob or LFS object, and its SHA-256 is retained in the [machine report](paddleocr-vl-official-assets-20261023.json). A second run verified the cache.
- Official `model.safetensors` size is 1,917,255,968 bytes with upstream LFS SHA `85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db`. The weight file was **not downloaded, locally hash-verified or loaded** in this inspection. This fixed official file listing contains no ONNX graph.

## Confirmed input and tokenizer details

| Component | Inspected contract |
| --- | --- |
| Image | RGB, bicubic resize, rescale 1/255, mean/std 0.5; patch 14, spatial merge 2 |
| Resolution | `smart_resize` aligns both dimensions to 28 using round-to-even; processor min/max 112,896 / 1,003,520 pixels; these are rounding parameters rather than hard output-area bounds |
| Temporal image patch | Actual preprocessor configuration says 1; `vision_config` says 2, so using the latter directly would misdescribe still-image inputs |
| Spotting usage | If both original dimensions are below 1,500, official README applies 2x Lanczos before the usual bicubic step; spotting pixel budget 1,605,632 |
| Language | 18 layers, hidden 1,024, query/KV heads 16/2, head dimension 128, vocabulary 103,424, three-dimensional RoPE |
| Generation | Both official configs have `use_cache=false`; dynamic KV export still needs implementation and correctness checks |
| Tokenizer | Declared `LlamaTokenizer`; official `tokenizer.json` is BPE with whitespace replacement and byte fallback, and `tokenizer.model` is provided. Special/image markers must be handled explicitly |
| Image markers | Placeholder 100295, start 101305, end 101306; EOS 2; chat template starts with the CLS marker 100273 rather than substituting BOS 1 |

Six exact prompts were checked against official usage: `OCR:`, `Table Recognition:`, `Formula Recognition:`, `Chart Recognition:`, `Spotting:`, `Seal Recognition:`. Nine ordinary-text vectors cover Chinese, repeated/leading spaces, tabs/newlines, formula text and an emoji byte fallback. Local `tokenizers 0.23.2` and `sentencepiece 0.2.2` produce the same ordinary-text IDs. Six manually rendered single-image prompt references use the official tokenizer asset and record image-placeholder expansion count/hash. They are not yet full official `AutoProcessor.apply_chat_template` or C# tokenizer parity evidence.

## Scalar geometry and managed code

The inspection extracts only the pinned upstream `smart_resize` scalar function. It does not import the upstream processor/model modules or initialize torch. Six successful geometry vectors include a 3,200-wide image, a tiny source and a large phone image; one >200:1 aspect ratio is rejected. Full image interpolation, normalization, patch layout, grid ordering and output coordinates were not exercised.

The new managed [input planner](../../../../src/DeploySharp.Visual/Models/PaddleOcr/VisionLanguage/PaddleOcrVlInputPlanning.cs) exposes six task prompts, source/spotting-prepared/resized geometry, spatial grid and merged image-token count, with source-size/budget validation. It allocates no image/tensor. Its [tests](../../../../tests/DeploySharp.Visual.Tests/PaddleOcrVlInputPlanningTests.cs) pass **14/14**, using upstream scalar expectations and rejection checks; Spotting retains two distinct resize operations. Visual builds all 16 declared target frameworks with zero warnings/errors. Existing Visual regression separately passes 531 tests, with five unrelated optional-checkpoint tests skipped.

```csharp
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

// Input planning only: no model or tokenizer is run.
PaddleOcrVlImagePlan plan = PaddleOcrVlInputPlanning.PlanOfficialV16(
    new VisualSize(700, 1000), PaddleOcrVlTask.TableRecognition);
// ResizedSize = 700 x 1008, ImageTokenCount = 900.
string prompt = PaddleOcrVlInputPlanning.GetOfficialV16Prompt(plan.Task);
```

## Reproduction and limits

```powershell
python eng/models/paddle-ocr/scripts/Inspect-PaddleOcrVlOfficialAssets.py `
  --model-root E:/Model/PaddleOCR-VL/1.6/c5630abae1d940eafe0697512a0325494b02ab42 `
  --report-path artifacts/paddleocr-vl-inspection/report.json `
  --with-tokenizer-reference

dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj `
  -c Release --filter FullyQualifiedName~PaddleOcrVlInputPlanningTests
```

The optional tokenizer references require installed `tokenizers` and `sentencepiece`; omit that switch for standard-library-only asset/scalar inspection. The script uses the fixed official revision, checks cached data and writes no large model weights. Model source is saved as data; it is not loaded for generation.

Remaining work includes real-image official baselines, pixel/tokenizer/position parity, vision/projector + embedding + dynamic prefill/decode ONNX graphs, .NET generation/KV ownership, ROI/layout/multipage integration and exact ORT/OpenVINO/TensorRT/OpenCV validation. Initial VL and 1.5 weights require their own verification. No backend support, accuracy, memory capacity or latency claim follows from this report.
