# ROI package consumer / ROI 包消费验证

Runs the complete ROI workflow through NuGet references only (no project reference): geometry, JSON round-trip, atomic snapshot replacement, detection filtering, and bounded video events. It reuses the public sample source so package validation cannot silently test a different API.

仅通过本地 NuGet 包运行完整 ROI 示例，不引用源码项目。JSON 已包含在 Visual 包中，无需单独安装 Configuration.Json。

```powershell
dotnet pack src/DeploySharp.Core/DeploySharp.Core.csproj -c Release -p:DeploySharpLibraryTargetFrameworks=net8.0 -p:DeploySharpVersionSuffix=roi-smoke -p:NuGetLockFilePath=obj/roi-package.lock.json -o artifacts/roi-packages
dotnet pack src/DeploySharp.Visual/DeploySharp.Visual.csproj -c Release -p:DeploySharpLibraryTargetFrameworks=net8.0 -p:DeploySharpVersionSuffix=roi-smoke -p:NuGetLockFilePath=obj/roi-package.lock.json -o artifacts/roi-packages
dotnet run --project tests/clean-consumer/visual-roi/VisualRoiConsumer.csproj -c Release
```

This local validation version is not a published release. Real backend/model and long-running GPU evidence are recorded separately in the [ROI matrix](../../../docs/articles/roi-backend-performance-matrix.md).
