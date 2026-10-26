# FUNSD original-scan enhancement safety evidence (2026-10-23)

PP-OCRv6 Small was evaluated on **1,769 source-labelled words from ten original FUNSD test scans**, without generated degradation. The fixed three-recipe consensus policy increases aggregate character edits from **307 to 311** and word edits from **214 to 219**. Enhancement remains opt-in and disabled by default.

## Selection and protocol

- Source: FUNSD official archive SHA-256/revision `c31735649e4f441bcbb4fd0f379574f7520b42286e80b01d80b445649d54761f`.
- Selection: `funsd-test-smoke-001.jsonl`, SHA-256 `a5e30927be9695d05e103aef98ab8b6db636070c57ea4c55acb0200bebfddefa`. All ten pages and all 1,769 nonempty, non-ignored word regions run in manifest order; there is no filtering based on recognition results.
- The test checks the selection's image IDs against the prior 40-page controlled-enhancement parent manifest (`funsd-test-full-001.jsonl`, SHA `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`): **zero overlapping parents**. This is page-disjoint from that experiment, not a separate dataset or a guarantee of absence from model pretraining or unrelated OCR tests. No threshold is fitted on this selection.
- FUNSD describes its source as noisy, low-resolution scanned forms. The local manifest preserves that source-level description; it has no per-page severity, blur/noise/contrast ground truth, or natural orientation truth.
- Source quadrilaterals enter `OcrPipeline.RecognizeOnlyAsync`; DET and CLS are bypassed. Word-crop metrics therefore do not measure detection recall, reading order, complete-page accuracy or official FUNSD performance.
- Batch 16; one session per stage. `ContrastNormalize`, `GrayClahe`, and `UnsharpMask` use the same recipe parameters as the earlier SROIE experiment. Diagnostic selection is `ConsensusConfidenceGain(minimumGain=0, minimumMatchingCandidates=2, confidenceThreshold=1)`.
- Model/dictionary, manifest and image SHA values, runtime versions and host information are recorded in [the JSON summary](paddleocr-funsd-natural-low-quality-enhancement-20261023.json). Models are checked against the release catalog before execution.
- Host: `JYPPX`, Windows `10.0.26200`, Ryzen 7 5800H, 16 logical processors, .NET `10.0.12`; ORT CPU `1.28.0`, OpenVINO CPU `2026.2.1-21919-ede283a88e3-releases/2026/2`. The test assembly SHA and dirty source revision are in the JSON. Source test SHA: `d88c2167bdf97b460c95362ddf28b2ad62477643733ea50b25fe5de2b62b59ff`.

## Quality results

Metrics use invariant case folding and the library's OCR text-accuracy implementation. The denominator is **8,582 characters / 1,775 whitespace-delimited words**; a source region can contain more than one word.

| Backend | Selected regions | Selected CER better / worse / equal | Selected WER better / worse / equal | Aggregate CER edits | Aggregate WER edits |
|---|---:|---:|---:|---:|---:|
| ORT CPU | 938/1,769 | 7 / 11 / 920 | 7 / 11 / 920 | 307 → 311 | 214 → 219 |
| OpenVINO CPU | 937/1,769 | 7 / 11 / 919 | 7 / 11 / 919 | 307 → 311 | 214 → 219 |

CER changes from `3.57725%` to `3.62386%`; WER changes from `12.05634%` to `12.33803%`. The policy produces some improvements but more selected worsened regions. Agreement of two enhanced candidates is not proof of correctness.

Each recipe was attempted on every region, with identical text results across both backends:

| Recipe | CER better / worse / equal | WER better / worse / equal |
|---|---:|---:|
| ContrastNormalize | 24 / 60 / 1,685 | 19 / 50 / 1,700 |
| GrayClahe | 30 / 321 / 1,418 | 26 / 293 / 1,450 |
| UnsharpMask | 11 / 21 / 1,737 | 9 / 16 / 1,744 |

These counts overlap by region and are not additive. In particular, unrestricted CLAHE damages many already-readable source crops in this selection; this is evidence against a blanket enhancement default, not a statement that CLAHE is always unsuitable.

### Source entity-role breakdown (offline audit, 2026-10-27)

The pinned per-region details (`SHA-256 188e32d81b7bc7128252a2b9236e3f387c60f71619ca8a58fe29151bd63ea753`) were joined by `ImageId/InstanceId` to all 1,769 word instances in the pinned FUNSD manifest. All keys matched uniquely. This reuses the original predictions; no model was run again. ORT and OpenVINO have the same text-level error counts in every source entity role. The two near-threshold selection-metadata differences described below do not change this table.

| FUNSD entity role | Regions | ORT / OpenVINO selected | Changed text | CER better / worse | Character edits | Word edits |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| answer | 955 | 488 / 487 | 9 | 4 / 5 | 122 -> 123 | 111 -> 112 |
| question | 455 | 238 / 238 | 5 | 2 / 1 | 68 -> 67 | 61 -> 60 |
| other | 280 | 164 / 164 | 7 | 1 / 4 | 114 -> 117 | 39 -> 42 |
| header | 79 | 48 / 48 | 1 | 0 / 1 | 3 -> 4 | 3 -> 5 |

`answer` is a source annotation role, not a verified business-critical field or severity label. Its five worsened regions are a concrete false-correction signal, but this ten-page sample cannot set or pass a production business-field error budget. No per-page blur, noise or contrast ground truth exists in the manifest, so aggregate/page error rates must not be renamed as degradation-severity strata. B1b-B3 remain open; enhancement remains disabled by default.

## Backend differences and threshold boundaries

Original text, all three candidate texts and final text match on **1,769/1,769 regions**. Decision plus selected-candidate index match on **1,767/1,769**. The two metadata differences are preserved in the JSON:

- `82251504-entity-037-word-002`: ORT selects recipe 0 and OpenVINO recipe 2; all texts are identical. Maximum candidate-confidence drift is `1.19209e-7`.
- `82252956_2958-entity-016-word-000`: ORT selects a candidate with a tiny positive gain while OpenVINO retains the original; all texts are identical. Maximum candidate-confidence drift is `4.35114e-6`.

The overall maximum original/candidate confidence drift is `0.000490725`, within the predeclared `0.001` bound. The initial test incorrectly demanded exact decision parity and failed despite unchanged texts. The revised contract retains these differences explicitly, requires all original/candidate/final texts to agree and all confidence deltas to meet the numeric bound; it does not change runtime tie-breaking, gain thresholds or model outputs. This is **text parity with numerical selection differences**, not bitwise parity.

The post-hoc scan checks 874 ORT / 864 OpenVINO distinct positive selected-candidate gain cut points. Each backend has two nonempty points with zero worsened CER and a CER-improving region: they retain only two or one words and reduce the aggregate character edits by one. Those points use this same sample's labels, are not independent validation, and **must not be adopted as production thresholds**. They do not overturn the fixed-policy result above.

## Reproduction and evidence retention

With the pinned manifests, original images and models already installed:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_FUNSD_ENHANCEMENT_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_FUNSD_ENHANCEMENT_OUTPUT = Join-Path (Get-Location) 'artifacts/funsd-natural-enhancement'
$env:DEPLOYSHARP_CPU_MODEL = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim()
$env:DEPLOYSHARP_CPU_POWER_POLICY = (powercfg /GETACTIVESCHEME).Trim()
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim() # Append a dirty marker when applicable.
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore -m:1 `
  --filter FullyQualifiedName~NaturalFunsdWordsMeasureEnhancementOnDisjointPagesOnOrtAndOpenVino
```

Optional roots are `DEPLOYSHARP_PADDLEOCR_FUNSD_ENHANCEMENT_DATASET_ROOT` (default `F:\OCRBenchmarkTesting`) and `..._MODEL_ROOT` (default `E:\Model\paddleocr`). Missing external files cause an explicit inconclusive result; changed source revisions, selection/image/model hashes, word counts or split identities fail the test.

The summary references a SHA-pinned `*-details.json` containing per-region error metrics, candidate confidences and text hashes. It stays under the local ignored output directory, alongside the TRX, original images and labels; no FUNSD images, ground-truth text, or raw predictions are redistributed. FUNSD's non-commercial research/education terms and separate source-image rights remain applicable. The complete fixture run passed **3/3**, including the SROIE regression and [paired cost benchmark](paddleocr-funsd-paired-enhancement-cost-20261023.md).

This closes the original noisy-scan, page-disjoint diagnostic slice. Per-degradation quality strata, independent dataset validation, business-field false-correction budgets, full-page quality, remaining model/backend coverage and production approval remain open. The single-invocation page timings in this quality summary are not a formal performance benchmark; use the separately scoped paired cost report.
