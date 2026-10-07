# PaddleOCR / PP-Structure regression gate (2026-10-07)

This record captures the local Release regression pass used while closing the PaddleOCR / PP-Structure plan. It covers the five source test projects present in this checkout; external model tests remain opt-in and are reported as skips when their environment gates are not enabled. All five projects were run sequentially from the same Windows working tree.

## Environment

- Repository revision (HEAD at test time): `c14908a` (`DeploySharpV2.0`)
- Working-tree state: already modified before this run; results describe that exact local snapshot and do not independently certify a clean checkout of `c14908a`.
- Test date: `2026-10-07`
- Configuration: `Release`
- Target framework: `net10.0`
- Command shape: `dotnet test <project> -f net10.0 --configuration Release --no-restore --verbosity minimal`
- Working directory: `E:\GitSpace\DeploySharp-V2.0\DeploySharp`

## Results

| Project | Passed | Skipped | Failed | Total | Scope |
|---|---:|---:|---:|---:|---|
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | 65 | model catalog and release client contracts |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 3 | 0 | 32 | model-pack and PaddleOCR manifest contracts |
| `DeploySharp.Visual.Tests` | 498 | 5 | 0 | 503 | core Visual contracts and opt-in model tests |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 113 | 0 | 212 | OpenCV contracts and gated external model probes |
| `DeploySharp.Visual.TensorRT.Tests` | 8 | 12 | 0 | 20 | TensorRT contracts and gated external PP-Structure probes |

The five projects completed `832` tests with `697` passed, `135` explicit skips and `0` failures. The skips are environment gates for missing or intentionally disabled external assets; they are not backend support claims and are not counted as failures. The additional OpenCV skip is the newly added UVDoc dynamic Batch test, which is opt-in and was separately run successfully with its gate enabled.

## Warnings and boundaries

- Legacy `net5.0`, `net6.0`, `net7.0` and `netcoreapp3.1` targets emit dependency support warnings. The Release `net10.0` test assets compiled and ran without failures.
- `DeploySharp.Visual.Configuration.Json.Tests` has no source `.csproj` in this checkout, so it is not included in the count.
- The repository was already dirty before the test run; the recorded revision is the Git HEAD, while the test result applies to the modified working tree snapshot.
- This gate does not close FormulaNet/UniMERNet OpenVINO, Chart2Table OpenCV autoregressive decode, remaining TensorRT PP-Structure opt-in runs, natural long-text accuracy, C3 layout labels, or long-running soak.

## Reproduction

Run each project separately from the repository root:

```powershell
dotnet test tests/DeploySharp.ModelFactory.Tests/DeploySharp.ModelFactory.Tests.csproj -f net10.0 --configuration Release --no-restore --verbosity minimal
dotnet test tests/DeploySharp.ModelPack.Json.Tests/DeploySharp.ModelPack.Json.Tests.csproj -f net10.0 --configuration Release --no-restore --verbosity minimal
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj -f net10.0 --configuration Release --no-restore --verbosity minimal
dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj -f net10.0 --configuration Release --no-restore --verbosity minimal
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj -f net10.0 --configuration Release --no-restore --verbosity minimal
```

To exercise external tests, set the documented environment variables and provide the exact model/checkpoint assets. Do not convert an asset-gated skip into a support claim by copying this result to another machine.
