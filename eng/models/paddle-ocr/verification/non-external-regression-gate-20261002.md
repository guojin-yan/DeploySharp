# PaddleOCR / PP-Structure non-external regression gate (2026-10-02)

This gate checks the current V2 worktree without enabling external model or network tests. It is intentionally separate from the real-model records: a skipped external test is not a failure and is not evidence that the corresponding model/backend is supported.

## Results

All commands used `-c Debug -f net10.0 --no-restore --filter "TestCategory!=ExternalModels"`.

| Test project | Passed | Skipped | Failed | Result |
| --- | ---: | ---: | ---: | --- |
| `DeploySharp.Visual.Tests` | 488 | 5 | 0 | pass |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 4 | 0 | pass |
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | pass |

The Visual and Visual.OpenCV runs include the current PaddleOCR/PP-Structure contracts, preprocessing, decoders, pipeline composition, session ownership and result export tests. The external-model skips were the existing Whisper, Donut, Chart2Table, and release/network gates controlled by explicit environment variables or missing local checkpoints; none was converted into a support claim.

## Reproduction

Run from the repository root:

```powershell
dotnet test tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj `
  -c Debug -f net10.0 --no-restore `
  --filter 'TestCategory!=ExternalModels' `
  --logger 'console;verbosity=minimal'

dotnet test tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj `
  -c Debug -f net10.0 --no-restore `
  --filter 'TestCategory!=ExternalModels' `
  --logger 'console;verbosity=minimal'

dotnet test tests/DeploySharp.ModelFactory.Tests/DeploySharp.ModelFactory.Tests.csproj `
  -c Debug -f net10.0 --no-restore `
  --filter 'TestCategory!=ExternalModels' `
  --logger 'console;verbosity=minimal'
```

The build emits dependency target-framework support warnings for the repository's legacy `net5.0`/`net6.0`/`net7.0`/`netcoreapp3.1` assets; the selected `net10.0` test targets completed with zero errors. This gate is a regression signal only. It does not replace model/backend execution, quality evaluation, GPU performance measurement or the remaining external gates in the unified plan.
