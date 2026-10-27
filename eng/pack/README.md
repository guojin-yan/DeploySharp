# 包验证与发布

`publish-nuget` 工作流默认 `publish=false`，只构建、测试并保留制品。只有手动指定 `publish=true` 才写入发布渠道：预览版仅发布 NuGet；正式版发布 NuGet、GitHub Packages 和 GitHub Release，符号包保存在 NuGet 与 Release。GitHub Release 先保持 Draft，全部推送成功后才公开。跨服务发布无法原子回滚；失败时先检查已存在的包和 Draft，再处理重试，不要覆盖已有资产。

版本由 `Directory.Build.props` 定义，包清单与依赖约束由 `release-candidate-packages.json` 定义。变更版本时同步检查 `release-evidence` 和 `docs/releases`，工作流会拒绝尚未核对的版本。默认源码按正式版候选构建；预览构建可传入 `-p:DeploySharpReleaseChannel=preview -p:DeploySharpVersionSuffix=preview.1`。

本地仅打包：

```powershell
dotnet restore DeploySharp.sln --locked-mode
dotnet build DeploySharp.sln -c Release --no-restore -m:1
$baseline = Get-Content eng/pack/release-candidate-packages.json -Raw | ConvertFrom-Json
foreach ($package in $baseline.packages) {
    dotnet pack $package.projectPath -c Release --no-build --no-restore -o <temporary-package-directory>
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $($package.packageId)" }
}
```

将 `<temporary-package-directory>` 替换为仓库外的临时目录。随后运行 `Test-ReleaseCandidatePackages.ps1` 和 `Test-ReleaseEvidence.ps1`。`-RequireReleaseEligible` 额外检查实际发布授权和工作树状态；普通验证不需要开启发布。

`Test-ReleaseEvidence.ps1 -WriteBaseline -EvidenceDirectory <temporary-evidence-directory>` 仅生成候选证据。先比较依赖、API、PDB 和 SourceLink 的实际变化，再更新已审阅基线，不要仅为让检查通过而覆盖基线。原始测试日志、开发审计与临时产物保留在公开仓库外。

`Invoke-ReleaseEvidenceNegativeTests.ps1` 默认执行全部异常场景；定位单项问题时可用 `-Scenario sourcelink-drift,api-signature` 选择场景，并为每次运行指定新的仓库外工作目录。

NuGet 使用 Repository Secret `NUGET_API_KEY`；GitHub 两个渠道使用工作流 `GITHUB_TOKEN` 的 `contents: write` 与 `packages: write` 权限。现有 GitHub Package 必须允许本仓库工作流写入。包签名默认可选；如维护者主动切换为 `required`，必须配置证书并完成签名后才能通过检查。第三方依赖元数据属于信息记录，不引入模型许可证审批或非商业用途限制。
