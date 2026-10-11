# PaddleOCR / PP-Structure non-external regression (2026-10-11)

The following five test projects were run serially from the current working tree on Windows:

```powershell
dotnet test <project> -c Release --no-restore --framework net10.0 `
  --filter "TestCategory!=ExternalModels" --logger "console;verbosity=minimal"
```

| Project | Passed | Skipped | Failed | Total |
|---|---:|---:|---:|---:|
| `DeploySharp.Core.Tests` | 33 | 0 | 0 | 33 |
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | 65 |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 3 | 0 | 32 |
| `DeploySharp.Visual.Tests` | 507 | 5 | 0 | 512 |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 4 | 0 | 103 |
| **Total** | **731** | **14** | **0** | **745** |

All failures: `0`. The 14 skips are explicit external model, checkpoint or native-runtime gates and are not counted as support evidence. Older target-framework dependency compatibility warnings remain during multi-target builds, but the `net10.0` test runs completed successfully.

This is a contract/regression snapshot from a dirty working tree. It does not replace clean-checkout validation, external model execution, backend parity, quality datasets, controlled P50/P95 benchmarks or long-running stability tests. Machine-readable details are in [the JSON report](paddleocr-ppstructure-non-external-regression-20261011.json).
