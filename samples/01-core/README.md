# Core workflow / Core 工作流

CoreContractInspection demonstrates a complete backend-neutral contract path: model identity, tensor shape/type declarations, profile registration, immutable result metadata, and deterministic success output. It is the foundation used by every backend and model case.

```powershell
dotnet run --project samples/01-core/CoreContractInspection.csproj -c Release
```

This case intentionally does not download a model or claim inference support; it proves the stable Core boundary before a runtime is selected.
