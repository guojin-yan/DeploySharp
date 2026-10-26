# PaddleOCR-VL-1.6 realFormula: five official-weight comparisons

Date: 2026-10-26. This checks whether selected errors in the 121-image realFormula result arise from the DeploySharp ONNX path or are also produced by the pinned official model. The five-image selection includes the first five corpus rows and the known square-root-scope error. This is a diagnostic subset, not a new corpus accuracy score.

The reference used `PaddlePaddle/PaddleOCR-VL-1.6` revision `c5630abae1d940eafe0697512a0325494b02ab42`, verified `model.safetensors` SHA-256 `85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db`, and the recorded Transformers 5.20.2 built-in model/Processor implementation. It ran with Pillow/PIL preprocessing, CPU FP32, four threads, SDPA, cached greedy generation and a 512-new-token limit; this is not a PaddleX predictor run. The comparison uses the production C# ONNX Runtime CPU [121-image report](paddleocr-vl-realformula-ort-openvino-linux-121-20261026.md): the same source-image hashes, Formula prompt, greedy limit and fixed 1.6 checkpoint revision. All ten executions reached EOS without truncation.

| realFormula image | Generated tokens | Token IDs vs official | Text vs official | Strict label match |
| --- | ---: | :---: | :---: | :---: |
| `220808471-63.png` | 95 | Yes | Yes | No |
| `220312373-25.png` | 25 | Yes | Yes | No |
| `230110053-1.png` | 78 | Yes | Yes | No |
| `210508113-24.png` | 80 | Yes | Yes | No |
| `220511982-14.png` | 37 | Yes | Yes | No |

The official and DeploySharp outputs match **315/315 generated Token IDs and 5/5 decoded texts**. Each official report verified the encoded image SHA from the [fixed five-image manifest](paddleocr-vl-realformula-official-samples-20261026.json); the [comparison JSON](paddleocr-vl-realformula-official-five-comparison-20261026.json) checks report completion, pinned weights/processor, inputs, prompt protocol, EOS and every Token ID. It contains no copied labels or predictions. The official processor's five pixel-tensor SHA values are recorded, but this comparison did not independently hash the C# intermediate tensors on these five images; prior fixed six-task processor parity is a separate result.

`220312373-25.png` illustrates a genuine formula-structure problem rather than just TeX formatting: the annotation places `+1` **inside** the square root, while both official and DeploySharp outputs place it **outside**. Exact generation parity on this image means that this particular discrepancy is not introduced by the ONNX export or the managed decoder. It does not establish that every error in the other 116 images has the same cause. The first image also contains notation changes such as `P` to `\mathbb{P}` that inflate character edits without by themselves proving a mathematical error. The 121-image whitespace-normalized CER remains 55.1696%, not a semantic accuracy score.

## Reproduce

Place the public MathNet realFormula v1 `img-corrected` directory next to a copy of the fixed manifest. Run the pinned [official reference](../scripts/Invoke-PaddleOcrVlReference.py) once with `--case 220808471-63`, then again with the four remaining `--case` IDs; use `--use-cache --device cpu --dtype float32 --processor-backend pil --attention sdpa --threads 4 --maximum-new-tokens 512`. Supply the official checkpoint directory to `--model-root`, the copied manifest to `--manifest`, and distinct JSON paths to `--output`. The reference checks model weight and image SHA values before generating.

Compare both official JSON files to the checked-in 121-image C# report:

```powershell
& eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlOfficialRealFormula.ps1 `
  -ManifestPath eng/models/paddle-ocr/verification/paddleocr-vl-realformula-official-samples-20261026.json `
  -OfficialReportPaths @('official-first.json', 'official-remaining4.json') `
  -ManagedReportPath eng/models/paddle-ocr/verification/paddleocr-vl-realformula-ortcpu-linux-121-20261026.json `
  -OutputJsonPath official-comparison.json
```

The retained raw official reports are outside Git at `E:/DeploySharpEvidence/paddleocr-vl-official-realformula-{first,remaining4}-20261026.json`; their SHA-256 values are `d64e6b771741233e8be5e76fd07a1356f1c993980710c6fddc0c6dc2a4f9dc74` and `1303db12445db59e218757af846c36e1475c0c581a50e8c1bfc16567eb864b5d`. The comparison also pins the C# source commit and assembly hashes. Official timings are one pass per image, not a controlled P50/P95 benchmark. Other VL tasks, full-page quality, the other 116 official-weight comparisons, TensorRT corpus quality and online Bundle availability remain open.
