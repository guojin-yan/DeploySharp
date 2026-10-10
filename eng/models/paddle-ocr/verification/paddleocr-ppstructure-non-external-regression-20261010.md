# PaddleOCR / PP-Structure non-external regression — 2026-10-10

This snapshot reruns the five non-external test projects that cover the PaddleOCR / PP-Structure contracts and their shared Visual infrastructure. It uses the current working tree at `6741db41f83d0120819f483ee161f5d0acc48403`, Release configuration, `net10.0`, `--no-restore`, and excludes tests tagged `ExternalModels`.

| Project | Passed | Skipped | Failed | Total |
| --- | ---: | ---: | ---: | ---: |
| `DeploySharp.Core.Tests` | 33 | 0 | 0 | 33 |
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | 65 |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 3 | 0 | 32 |
| `DeploySharp.Visual.Tests` | 502 | 5 | 0 | 507 |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 4 | 0 | 103 |
| **Total** | **726** | **14** | **0** | **740** |

Command used for each project:

```powershell
dotnet test <project> -c Release --framework net10.0 --no-restore --filter "TestCategory!=ExternalModels" --logger "console;verbosity=minimal"
```

The run passed with zero failures. The 14 skipped cases are explicit external-model, checkpoint, or environment gates; they are not support claims. Existing legacy-target-framework dependency warnings remain non-fatal. Because the working tree was not clean, this is a current-source regression snapshot rather than a clean-checkout release gate. It also does not replace model accuracy, cross-backend parity, controlled performance, or long-running soak evidence.

Machine-readable counts and the exact scope are in [`paddleocr-ppstructure-non-external-regression-20261010.json`](paddleocr-ppstructure-non-external-regression-20261010.json).
