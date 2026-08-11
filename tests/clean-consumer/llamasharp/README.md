# LLamaSharp clean consumer / LLamaSharp 干净使用者

This project references only the two public DeploySharp LLM packages plus the consumer-selected `LLamaSharp.Backend.Cpu` native package. Core and managed LLamaSharp arrive transitively. Pack the repository first, then restore, build, and run with the colocated `NuGet.Config`. / 本项目只直接引用两个 DeploySharp LLM 公共包，以及由使用者选择的 `LLamaSharp.Backend.Cpu` 原生包。Core 和托管 LLamaSharp 通过传递依赖获得。请先打包仓库，再使用同目录的 `NuGet.Config` 还原、构建并运行。

With no exact GGUF it prints `DEPLOYSHARP_LLAMA_CONSUMER_SKIP reason=missing-exact-gguf`. A real run is permitted only after the selected model and `DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST` pass `eng/models/llm/Test-GgufAdmission.ps1 -RequireAdmitted`; set `DEPLOYSHARP_LLAMA_SHA256` so the package-only load repeats the exact hash check. The consumer still owns the matching native backend. Restoring with `-p:IncludeLlamaNativeBackend=false` and running with `DEPLOYSHARP_LLAMA_EXPECT_NO_NATIVE=1` must produce `DEPLOYSHARP_LLAMA_NO_NATIVE_OK error=DS-NATIVE-6001`, proving the real missing-native diagnostic without removing any caller files. / 缺少精确 GGUF 时输出稳定 skip 标记。真实运行还须设置精确 SHA256，使纯包加载重复哈希校验。consumer 继续持有匹配的原生后端；显式移除该包的验证模式必须返回稳定的缺原生运行时诊断。

```powershell
dotnet restore tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj --configfile tests\clean-consumer\llamasharp\NuGet.Config
dotnet build tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj -c Release --no-restore
dotnet run --project tests\clean-consumer\llamasharp\DeploySharp.LlamaSharp.CleanConsumer.csproj -c Release --no-build
```

After packing Backend.LlamaSharp, run `eng/pack/Test-LlamaSharpPackageBoundary.ps1 -PackagePath <backend-nupkg>` to verify central versions, lock/assets, nuspec groups, payloads, and managed assembly references before using this consumer matrix. Passing a second independently packed file with `-ComparisonPackagePath` checks semantic payload reproducibility and reports raw archive identity separately. / Backend.LlamaSharp 打包后先运行包边界门；可传入第二次独立打包结果，分别检查语义 payload 复现与原始包字节身份。
