# PaddleOCR / PP-Structure non-external regression (2026-10-12)

## Environment and scope

- Repository HEAD during the run: `c79f038`
- Windows `10.0.26200`, .NET `10.0.401`
- Configuration: `Release`, target framework `net10.0`, `--no-restore`
- Filter: `TestCategory!=ExternalModels`
- The run covered the Core, ModelFactory, ModelPack.Json, Visual and Visual.OpenCV test projects. It did not enable external checkpoints, vendor GPU runtimes or model download gates.

## Result

| Test project | Passed | Skipped | Failed | Total |
|---|---:|---:|---:|---:|
| `DeploySharp.Core.Tests` | 33 | 0 | 0 | 33 |
| `DeploySharp.ModelFactory.Tests` | 63 | 2 | 0 | 65 |
| `DeploySharp.ModelPack.Json.Tests` | 29 | 3 | 0 | 32 |
| `DeploySharp.Visual.Tests` | 515 | 5 | 0 | 520 |
| `DeploySharp.Visual.OpenCV.Tests` | 99 | 4 | 0 | 103 |
| **Total** | **739** | **14** | **0** | **753** |

All 14 skips were explicit external-model/checkpoint or native-runtime gates. The run produced no test failures. Existing target-framework dependency support warnings for older TFMs remain; the net10.0 test executions passed.

## Reproduction

```powershell
$projects = @(
  'tests/DeploySharp.Core.Tests/DeploySharp.Core.Tests.csproj',
  'tests/DeploySharp.ModelFactory.Tests/DeploySharp.ModelFactory.Tests.csproj',
  'tests/DeploySharp.ModelPack.Json.Tests/DeploySharp.ModelPack.Json.Tests.csproj',
  'tests/DeploySharp.Visual.Tests/DeploySharp.Visual.Tests.csproj',
  'tests/DeploySharp.Visual.OpenCV.Tests/DeploySharp.Visual.OpenCV.Tests.csproj'
)
foreach ($project in $projects) {
  dotnet test $project -c Release -f net10.0 --no-restore --verbosity minimal `
    --filter 'TestCategory!=ExternalModels'
}
```

This is a clean contract/regression snapshot for the checked-out working tree. It does not prove external model quality, backend coverage, TensorRT/OpenVINO/OpenCV portability, controlled performance, or long-running stability.
