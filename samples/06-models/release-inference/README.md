# Published model inference workflows

This is the real end-to-end model case. ModelFactory downloads a Release manifest/model, verifies size and SHA256, Visual prepares the input, ONNX Runtime executes on CPU, and the task decoder writes an inspectable PGM mask.

```powershell
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-1.4 --image <image>
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id bria/rmbg-2.0 --precision fp32 --quantization none --image <image>
dotnet run --project samples/06-models/release-inference/ModelReleaseInference.csproj -c Release -- --model-id anomalib/padim/mvtec-bottle --image <image>
```
