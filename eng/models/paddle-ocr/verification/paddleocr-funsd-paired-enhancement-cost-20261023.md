# FUNSD paired OCR enhancement cost (2026-10-23)

The opt-in three-recipe consensus policy was compared against a separately instantiated no-enhancement pipeline using **5 warm-up pairs + 50 measured pairs per CPU backend**. On the fixed 16-word input, median paired runtime ratios are **3.68× ORT / 3.93× OpenVINO**, with no CER/WER improvement on this subset. This measures enhancement overhead; it does not approve a quality/cost tradeoff.

## Fixed input and timing contract

- The first page of pinned FUNSD `funsd-test-smoke-001.jsonl`, `funsd-test-82092117`, and its first 16 valid words in manifest order are selected before recognition. No best-performing crops or fastest iterations are selected afterward.
- PP-OCRv6 Small REC, matching dictionary and required DET profile; batch 16; one session per stage in each independently instantiated baseline/enhanced pipeline. The actual path is `RecognizeOnlyAsync`, so DET and CLS are not executed.
- Baseline uses the original recognition crop profile; enhancement uses `ContrastNormalize`, `GrayClahe`, and `UnsharpMask`, consensus with minimum gain `0`, minimum matching candidates `2`, and diagnostic confidence threshold `1`.
- Each measured pair has the same preloaded page input and 16 quadrilaterals. Exactly 48 additional candidate attempts run for each enhanced invocation; the baseline runs zero. This is an all-candidate cost probe, not the cost of a lower-threshold production retry policy.
- The timed scope includes source-region crop preparation, normalization, recognition, CTC decoding and optional enhancement retry/selection. It excludes file I/O, page input preparation, model/session creation, warm-up, fingerprinting and assertions.
- Baseline-first and enhancement-first order alternate, giving 25 of each among the 50 measured pairs. The report preserves every sample, paired difference and paired ratio; percentiles use linear interpolation over `(n-1)*p`.
- Each pair verifies that enhanced original recognition matches the separately run baseline. Original, final and all candidate texts remain stable for all 50 measured pairs on both backends. These fingerprints intentionally exclude floating confidence/tie metadata; the [larger quality run](paddleocr-funsd-natural-low-quality-enhancement-20261023.md) records those separately.
- No forced GC or clock control. Host: `JYPPX`, Ryzen 7 5800H, Windows `10.0.26200`, .NET `10.0.12`, Balanced power plan. Runtime versions, assembly/source revision, model/dictionary/manifest/image hashes are in [the JSON](paddleocr-funsd-paired-enhancement-cost-20261023.json). No during-run frequency/thermal telemetry was collected.

## Measurements

Times below are **milliseconds for all 16 words in one call**, not per-word or full-page latency.

| Backend | Baseline P50 / P95 | Enhanced P50 / P95 | Paired additional P50 / P95 | Paired ratio P50 / P95 | Stable measured pairs |
|---|---:|---:|---:|---:|---:|
| ORT CPU 1.28.0 | 69.17 / 104.88 | 258.29 / 347.18 | 192.49 / 259.42 | 3.68× / 4.43× | 50/50 |
| OpenVINO CPU 2026.2.1 | 91.53 / 127.09 | 364.82 / 541.88 | 279.06 / 427.30 | 3.93× / 5.33× | 50/50 |

The paired additional percentile is computed from each pair's `enhanced - baseline`, not from subtracting the two marginal percentiles. Likewise the paired-ratio percentile is not the ratio of marginal P50/P95 values.

Both backends have `9/55` character edits and `3/16` word edits before and after selection on this fixed subset; twelve regions select a candidate but none changes the aggregate error counts. This small predeclared subset supports a cost observation only. The larger 1,769-word natural scan run actually has worse aggregate CER/WER after enhancement, reinforcing the default-off policy.

CPU scheduling, thermal state and the sequential backend execution can affect these observations. They are not a backend leaderboard, clock-locked hardware limit, GPU comparison, or complete model/backend cost matrix. A business-specific extra-latency budget and a validated quality benefit are still required before enabling retry.

## Reproduction

Use the same pinned inputs described by the [natural-scan report](paddleocr-funsd-natural-low-quality-enhancement-20261023.md), then:

```powershell
$env:DEPLOYSHARP_PADDLEOCR_FUNSD_ENHANCEMENT_BENCHMARK_RUN = '1'
$env:DEPLOYSHARP_PADDLEOCR_FUNSD_ENHANCEMENT_OUTPUT = Join-Path (Get-Location) 'artifacts/funsd-enhancement-cost'
$env:DEPLOYSHARP_CPU_MODEL = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim()
$env:DEPLOYSHARP_CPU_POWER_POLICY = (powercfg /GETACTIVESCHEME).Trim()
$env:DEPLOYSHARP_BENCHMARK_SOURCE_REVISION = (git rev-parse HEAD).Trim() # Append a dirty marker when applicable.
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Release -f net10.0 --no-restore -m:1 `
  --filter FullyQualifiedName~NaturalFunsdWordsMeasurePairedFormalEnhancementCostOnOrtAndOpenVino
```

The same external fixture passed **3/3** with SROIE and FUNSD quality checks enabled. This closes the bounded CPU paired-cost measurement gap; it does not close B2/B3 business quality/error/cost budgets, full-page performance, other models/backends or second-device coverage.
