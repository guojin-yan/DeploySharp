# Backend workflow / 后端工作流

OpenCvDnnContractInspection loads a pinned ONNX fixture through the real OpenCV DNN backend, creates a Core session, sends a named tensor, checks the output shape/value, and disposes the native session.

```powershell
dotnet run --project samples/03-backends/OpenCvDnnContractInspection.csproj -c Release
```

The case demonstrates native ownership, backend selection, tensor execution, golden checking, and cleanup as one workflow.
