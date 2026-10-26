# PaddleOCR-VL-1.6 realFormula: 121-image official-weight parity

Date: 2026-10-26. The pinned original PaddleOCR-VL-1.6 weights and recorded Transformers implementation generated all 121 labelled MathNet realFormula v1 images. This directly tests whether the production DeploySharp ONNX Runtime CPU path diverges from that reference on this corpus. It does not grade mathematical correctness.

The [full official manifest](paddleocr-vl-realformula-official-121-manifest-20261026.json) was generated from the dataset JSONL with SHA-256 `9bcabb811111538eb849ababa98aff32f7b7a3c59cdf4d16a840e946e3961a76`. Before writing the manifest, the generator verified all 121 source-image hashes and unique IDs. The official run used checkpoint revision `c5630abae1d940eafe0697512a0325494b02ab42`, `model.safetensors` SHA-256 `85a479d506a11e724e7285d395c551be69f41dbc16b6342d3cacfb189aed71db`, Ubuntu 22.04.5 x64, Python 3.10.12, torch 2.10.0+cpu, Transformers 5.20.2, Pillow 12.3.0, PIL Processor, CPU FP32, four threads, SDPA, cached greedy generation and a 512-new-token limit. The reference is the pinned Transformers built-in implementation with official weights, not a PaddleX predictor run.

| Check | Result |
| --- | ---: |
| Separate official processes / completed reports | 25 / 25 |
| Source images completed / unique IDs | 121 / 121 |
| Official EOS / truncation | 121 / 0 |
| Official generated Token IDs | 6,699 |
| Official vs production C# ORT CPU Token sequences | 121 / 121 exact |
| Official vs production C# ORT CPU decoded texts | 121 / 121 exact |
| Whitespace-normalized exact match to dataset annotation | 0 / 121 |
| Corpus character error rate against annotation | 55.1696% |

The [comparison JSON](paddleocr-vl-realformula-official-121-comparison-20261026.json) pins the 25 raw-report SHA-256 values and each shard's first/last ID, full manifest, annotation CSV, checkpoint/config/reference-script and implementation hashes, plus the C# source/assembly and four-graph identities through the [managed 121-image report](paddleocr-vl-realformula-ortcpu-linux-121-20261026.json). Its SHA-256 is `8424396cc9f7a99022b3b2b92d5d39666b16b1520f9c0ff3bfcfba67fac0cf4e`; a separate regeneration produced identical bytes. The comparator rejects missing or duplicate source images, incomplete reports, mismatched hashes, execution settings and EOS states, then records each image's exact Token/text decision and first divergence index. An independent read-only aggregation found 25 reports, 121 unique IDs, 6,699 generated Tokens, zero missing images and zero Token/text differences. The first five images were also rerun under the full manifest and matched the earlier [five-image reference](paddleocr-vl-realformula-official-five-20261026.md) exactly.

These results close the fixed-corpus **observed generation-parity** question for official weights versus this .NET ORT CPU export. The 0/121 strict match and 55.1696% character CER are the same outputs from both implementations, not an ONNX-only regression. Character edits can penalize equivalent TeX notation; conversely, `220312373-25.png` has a real square-root-scope error common to both outputs. This report does not independently compare C# and official intermediate image tensors for all 121 inputs, prove mathematical equivalence, validate arbitrary images or other checkpoint revisions, or turn the corpus into a formal latency benchmark. Earlier [ORT CPU/OpenVINO CPU 121-image Token parity](paddleocr-vl-realformula-ort-openvino-linux-121-20261026.md) is a separate same-build backend result; TensorRT corpus quality, other VL tasks, full-page quality, memory soak and online Release assets remain open.

## Reproduce

Obtain the public MathNet realFormula v1 images and fixed JSONL. Generate a new official manifest from the checked dataset, then place it beside `img-corrected` on the Linux host:

```powershell
& eng/models/paddle-ocr/scripts/New-PaddleOcrVlOfficialRealFormulaManifest.ps1 `
  -DatasetRoot /path/to/realFormula-public `
  -OutputPath /path/to/realFormula-public/official-121-manifest.json
```

Run the checked-in [bounded official runner](../scripts/Run-PaddleOcrVlOfficialRealFormulaShards.sh) on Linux with the pinned [reference implementation](../scripts/Invoke-PaddleOcrVlReference.py). The runner refuses to overwrite existing shards and starts a new Python model process for each group of up to five images:

```bash
bash eng/models/paddle-ocr/scripts/Run-PaddleOcrVlOfficialRealFormulaShards.sh \
  /path/to/venv/bin/python /path/to/Invoke-PaddleOcrVlReference.py \
  /path/to/pinned-checkpoint /path/to/realFormula-public/official-121-manifest.json \
  /path/to/empty-official-reports 0 5
```

On Windows, compare those reports against the checked-in C# result:

```powershell
$reports = @(Get-ChildItem /path/to/empty-official-reports -Filter 'official-offset-*.json' -File |
  Sort-Object { [int] ($_.BaseName -replace '^official-offset-', '') } | ForEach-Object FullName)
& eng/models/paddle-ocr/scripts/Compare-PaddleOcrVlOfficialRealFormula.ps1 `
  -ManifestPath /path/to/realFormula-public/official-121-manifest.json `
  -OfficialReportPaths $reports `
  -ManagedReportPath eng/models/paddle-ocr/verification/paddleocr-vl-realformula-ortcpu-linux-121-20261026.json `
  -OutputJsonPath official-comparison.json
```

The original 25 official JSON reports remain outside Git at `E:/DeploySharpEvidence/paddleocr-vl-official-realformula-121-20261026`; their total size is 2,281,627 bytes. The checked-in comparison stores every constituent file hash but no raw label or prediction. All 25 processes exited; no reference process was left running. Per-image times are one-pass observations with process restart, not a controlled P50/P95 or memory-stability result.
