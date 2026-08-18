# DeploySharp ModelFactory CLI

This small source-built CLI exposes the audited ModelFactory workflow without requiring application code to construct `OfficialModelCatalog`, `ModelFactoryOptions`, or `ModelFactoryClient` manually. It is intentionally source-built with the repository so it always uses the catalog revision under test.

```powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --backend onnxruntime --format onnx --preview
```

`install` defaults to `%LOCALAPPDATA%\DeploySharp\ModelFactory`, verifies every downloaded asset and ModelPack, and prints the materialized package root. Use `--cache <path>` to select an application-owned cache and `--offline` to require a previously verified cache entry. Preview entries require the explicit `--preview` flag.
