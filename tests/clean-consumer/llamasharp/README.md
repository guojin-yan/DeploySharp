# LLamaSharp clean consumer / LLamaSharp 干净使用者

This project references only the two public DeploySharp LLM packages plus the consumer-selected `LLamaSharp.Backend.Cpu` native package. Core and managed LLamaSharp arrive transitively. Pack the repository first, then restore, build, and run with the colocated `NuGet.Config`. / 本项目只直接引用两个 DeploySharp LLM 公共包，以及由使用者选择的 `LLamaSharp.Backend.Cpu` 原生包。Core 和托管 LLamaSharp 通过传递依赖获得。请先打包仓库，再使用同目录的 `NuGet.Config` 还原、构建并运行。

```powershell
dotnet restore tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj --configfile tests\clean-consumer\llamasharp\NuGet.Config
dotnet build tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj -c Release --no-restore
dotnet run --project tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj -c Release --no-build
```
