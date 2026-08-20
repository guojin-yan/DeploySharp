# Release and platform status / 发布与平台状态

## Current release

The repository is on `2.0.0-alpha.1`, branch `DeploySharpV2.0`. This is not a GA release. The release-candidate manifest requires `.snupkg` symbol packages, and CI normalizes and compares raw `.nupkg` and `.snupkg` containers from two independent pack invocations before signing. Release eligibility remains blocked by the dirty worktree, unsigned packages, missing publication authority, and outstanding dependency/model legal reviews. / 当前仓库为 `2.0.0-alpha.1`、分支 `DeploySharpV2.0`，不是 GA。候选清单要求 `.snupkg` 符号包，CI 会在签名前规范化并比较两次独立打包的原始 `.nupkg` 与 `.snupkg` 容器。由于工作区脏、包未签名、缺少发布授权以及依赖/模型法律审核仍未完成，当前仍不具备发布资格。

No formal GA Release is created by this stage. A future release must verify candidate packages, SBOM/third-party notices, NuGet.org clean installation, rollback/retraction procedure, and release-bound evidence before publication. / 本阶段不创建正式 GA Release。后续发布必须在发布前验证候选包、SBOM/第三方声明、NuGet.org 全新安装、回滚/撤回流程和与 Release 绑定的证据。

## TFM and backend matrix

Support claims use five ordered levels: `BuildOnly` (compiler/restore evidence only), `ManagedTested` (managed tests without a native execution claim), `NativeSmoke` (a named caller-owned native runtime executed), `GoldenVerified` (a fixed model/input/output identity was compared), and `ReleaseSupported` (all release, platform, and support obligations are closed). The current alpha matrix contains no `ReleaseSupported` claim. The machine-readable source is `eng/platform/platform-support.json`; `eng/platform/Test-PlatformSupportMatrix.ps1` binds its 12 Windows claims to the release-candidate package set, central runtime versions, evidence paths, and model hashes. / 支持声明采用五级顺序：`BuildOnly`、`ManagedTested`、`NativeSmoke`、`GoldenVerified`、`ReleaseSupported`。当前 alpha 矩阵没有任何 `ReleaseSupported` 声明。机器源为 `eng/platform/platform-support.json`；`eng/platform/Test-PlatformSupportMatrix.ps1` 将 12 条 Windows 声明绑定到候选包集合、中央 runtime 版本、证据路径和模型哈希。

| Component | Declared TFM boundary | Evidence status |
| --- | --- | --- |
| Core, Visual | `net46`-`net481`, `netstandard2.0`, `netcoreapp3.1`, `net5.0`-`net10.0` | Build/test evidence exists for the project matrix; old/EOL runtimes are compatibility targets, not security support. |
| LLM | `netstandard2.0`, `netcoreapp3.1`, `net5.0`-`net10.0` | Managed contract and selected CPU evidence; runtime capability depends on caller-owned LLamaSharp native assets. |
| ONNX Runtime | `netstandard2.0`, `net8.0` | Managed contract and local CPU smoke evidence; native providers remain consumer-owned. |
| OpenVINO | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | Managed build matrix and Windows runtime evidence; Linux/macOS/NPU are not verified here. |
| Visual.OpenCV | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | OpenCV preview managed/input evidence on Windows x64 CPU; no DNN backend claim. |
| TensorRT | `net8.0` only | Managed tests, pack and cache consumer; GPU/native execution is not run by ordinary CI and is not implied by the TFM. |
| Multimodal | `netstandard2.0`, `netcoreapp3.1`, `net5.0`-`net10.0` | Managed tests, pack, clean consumer and sample pass; Visual-native single-image adapter exists, while mtmd/OpenVINO GenAI native/model paths remain explicit unavailable probes. |
| OpenCV DNN | `net46`-`net481`, `netcoreapp3.1`, `net5.0`-`net10.0` | Build matrix plus real Windows x64 CPU ONNX golden, negative tests, package-only consumer and sample. Other platforms/operators/devices are unverified. |

Windows is the only platform with the complete local native evidence represented by the current clean consumers. Linux, macOS, ARM, NPU and untested GPU/provider combinations must be described as planned, untested, or unsupported until dedicated runners and repeatable evidence exist. / 当前 clean consumer 所代表的完整本机原生证据仅覆盖 Windows；Linux、macOS、ARM、NPU 及未测试的 GPU/provider 组合必须标记为 planned、untested 或 unsupported，直到有专用 runner 和可重复证据。

The executable matrix currently records seven platform/device scopes: Windows x64 CPU is `tested` with 12 package claims (7 `ManagedTested`, 2 `NativeSmoke`, 3 `GoldenVerified`); Linux x64/ARM64 CPU, macOS CPU, and Windows ARM64 CPU are `untested`; Windows x64 GPU and Windows NPU are `planned`. Untested/planned scopes are required to contain zero positive claims and explicit blockers. Five negative mutations prove that a premature `ReleaseSupported` promotion, missing evidence, runtime-version drift, an untested-platform claim, or a missing Windows package claim is rejected. / 可执行矩阵当前记录 7 个平台/设备范围：Windows x64 CPU 为 `tested`，包含 12 条包声明（7 条 `ManagedTested`、2 条 `NativeSmoke`、3 条 `GoldenVerified`）；Linux x64/ARM64 CPU、macOS CPU、Windows ARM64 CPU 为 `untested`；Windows x64 GPU 与 Windows NPU 为 `planned`。未测试/计划范围必须保持零正向声明并列出 blocker。5 类负向突变会拒绝提前提升 `ReleaseSupported`、证据缺失、runtime 版本漂移、未测试平台声明和 Windows 包声明缺项。

The manual alpha CI now prepares a backend-neutral `managed-cross-platform` job on `ubuntu-latest` and `macos-latest` for Core, ModelPack, and ModelFactory tests. Configuration is not execution evidence: Linux and macOS remain `untested` until a real workflow run is retained with runner image, SDK, command, result, and commit identity. / 手动 alpha CI 现配置 `ubuntu-latest` 与 `macos-latest` 的后端中立 `managed-cross-platform` job，运行 Core、ModelPack、ModelFactory 测试。配置不等于执行证据；在真实 workflow 结果绑定 runner image、SDK、命令、结果和 commit 前，Linux 与 macOS 继续保持 `untested`。

## GA blockers

- Licensed, exact native/model evidence for a production Multimodal path beyond the existing Visual-native adapter (ADR 0035).
- Linux/macOS and broader model/operator evidence for the OpenCV DNN preview (ADR 0036).
- Multi-platform/native matrix, symbol packages, NuGet.org new-install verification, rollback procedure, and release-bound supply-chain evidence.
- V1 `AlgorithmVerified` admission remains `0/32`; Preview assets must not be promoted automatically.
- The first PP-OCRv5 candidate review is `paddleocr/ppocrv5/mobile-cls`. Its source/export identity, immutable Preview Release binding, release-bound ORT/OpenVINO local golden, and independent official Paddle Predictor golden are closed. Algorithm admission remains blocked only by attributable redistribution approval. See [ADR 0037](../adr/0037-ppocrv5-first-algorithm-admission.md).
