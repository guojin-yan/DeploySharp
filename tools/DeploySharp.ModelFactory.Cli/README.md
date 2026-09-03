# DeploySharp ModelFactory CLI

The read-only commands are useful for checking a checkout before a download:

~~~powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- doctor --json
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview --json
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- show --model-id bria/rmbg-2.0 --preview --json
~~~

Doctor reports local .NET and catalog metadata only. List emits one row per catalog artifact, and Show includes release assets, sizes, hashes, and sidecars. These inspection commands do not download model files or install native runtimes. Preview entries remain opt-in and require the --preview switch.

This small source-built CLI exposes the audited ModelFactory workflow without requiring application code to construct `OfficialModelCatalog`, `ModelFactoryOptions`, or `ModelFactoryClient` manually. It is intentionally source-built with the repository so it always uses the catalog revision under test.

```powershell
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- list --preview
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --backend onnxruntime --format onnx --preview
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id bria/rmbg-2.0 --backend onnxruntime --format onnx --precision fp32 --preview
dotnet run --project tools\DeploySharp.ModelFactory.Cli -- install --model-id bria/rmbg-2.0 --backend onnxruntime --format onnx --precision int8 --quantization dynamic --preview
```

`install` defaults to `%LOCALAPPDATA%\DeploySharp\ModelFactory`, verifies every downloaded asset and ModelPack, and prints the materialized package root. Use `--precision` and `--quantization` to choose an explicit catalog variant, `--cache <path>` to select an application-owned cache, and `--offline` to require a previously verified cache entry. Preview entries require the explicit `--preview` flag.
