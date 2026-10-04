# PaddleOCR / PP-Structure regression gate (2026-10-04)

This record captures the local regression pass used while closing the PaddleOCR / PP-Structure plan. It covers contract and decoder suites; external model tests remain opt-in and are reported as skips when their environment gates are not enabled.

## Environment

- Repository revision: `45be9bf` (`DeploySharpV2.0`)
- Configuration: `Release`
- Runtime used by the completed suites: `net10.0`
- Command working directory: `E:\GitSpace\DeploySharp-V2.0\DeploySharp`
- Test command shape: `dotnet test <project> --configuration Release --no-restore --logger "console;verbosity=minimal"`

## Results

| Project | Passed | Skipped | Failed | Total | Scope |
|---|---:|---:|---:|---:|---|
| `DeploySharp.Visual.Tests` | 488 | 5 | 0 | 493 | core Visual contracts; opt-in model/checkpoint tests skipped |
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | 65 | model catalog/release client contracts |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 3 | 0 | 32 | model-pack and PaddleOCR manifest contracts |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 106 | 0 | 205 | OpenCV contracts plus gated external model probes |
| `DeploySharp.Visual.TensorRT.Tests` | 8 | 10 | 0 | 18 | TensorRT contracts; gated external PP-Structure/Chart2Table/UVDoc probes |

The five executed projects therefore completed `687` tests with `0` failures and `126` explicit skips. The skips are environment gates for missing or intentionally disabled external assets; they are not backend support claims and are not counted as failures.

## Warnings and boundaries

- The build emitted dependency target-framework support warnings for legacy `net5.0`, `net6.0`, `net7.0` and `netcoreapp3.1` targets. No test failure or compile error occurred in the executed `net10.0` test assets.
- `DeploySharp.Visual.Configuration.Json.Tests` has no source `.csproj` in this checkout (the directory contains only `bin`/`obj` outputs), so it was not included in the count.
- This gate does not close the remaining real-asset gates: FormulaNet/UniMERNet OpenVINO, Chart2Table OpenCV autoregressive decode, TensorRT PP-Structure opt-in runs, natural long-text accuracy, C3 layout labels, or long-running soak.

## Reproduction

Run each project separately from the repository root. To exercise external tests, set the documented environment variables in that test project and provide the exact model/checkpoint assets; do not convert a missing-asset skip into a failure by copying this result to another machine.
