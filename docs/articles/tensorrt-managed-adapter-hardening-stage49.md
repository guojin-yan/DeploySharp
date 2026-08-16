# TensorRT managed adapter hardening / TensorRT managed adapter 加固

Stage 49 keeps the Stage 48 admission decision: the isolated managed adapter is locally admitted, while the two missing upstream proofs block formal publication only. A read-only `gh` review found Release ID `368273346` unchanged at `updated_at=2026-08-11T00:49:26Z`, `immutable=false`, with 20 assets and no machine-readable manifest, provenance, attestation, `packages.lock.json`, or `project.assets.json` asset. The lightweight `v4.0.0` tag still resolves directly to commit `673e120807d789d90a13a9f28a043282e95bb5e6`. / 阶段 49 保持 Stage 48 的准入决定：本地 managed adapter 已准入，两项缺失的上游证明只阻止正式发布。只读 `gh` 复核确认 Release 与 tag identity 未变化，且仍无 manifest/provenance/lock/assets proof。

## Managed boundary fixes / Managed 边界修复

The adapter now validates and reads each External `.engine/.plan` from the same open file handle, eliminating a verify-then-reopen race. Session creation rejects host-resident, vectorized, non-linear, or shape-I/O bindings before execution. Input ranks, static dimensions, and optimization-profile bounds are checked before enqueue; output allocation requires a concrete post-shape-inference shape, and the allocated byte count must exactly match shape and element type. Failure cleanup attempts every owned managed wrapper even when an earlier `Dispose` throws. / 适配器从同一个打开的文件句柄完成校验和读取，消除校验后重开文件的竞态；执行前拒绝 host、vectorized、非 linear 和 shape-I/O binding，校验输入 rank、静态维度与 profile 边界；动态输出必须在 shape inference 后得到具体 shape，且 buffer 字节数必须精确匹配。失败清理会尝试释放所有已拥有的 managed wrapper。

These changes do not add a builder, cache writer, native probe, native payload, TensorRT-LLM, or any Core/ModelPack dependency on TensorRT. CUDA, cuDNN, TensorRT, the NVIDIA driver, the native bridge, GPU selection, and plan lifecycle remain consumer-owned. `.engine/.plan` remains External device/runtime/profile-bound local-cache data. / 本轮不新增 builder、cache writer、native probe、native payload 或 TensorRT-LLM，Core/ModelPack 仍不依赖 TensorRT；native graph 与 plan 生命周期继续由 consumer 持有。

## Verification and status / 验证与状态

- Focused adapter build: zero warnings and errors; managed contract tests: `7 passed / 0 skipped / 0 failed`.
- Full solution using current binaries: `385 passed / 50 skipped / 0 failed`. A full rebuild attempt was blocked by the machine's missing .NET Framework 4.6 targeting pack and incomplete offline netstandard2.0 reference assemblies; this is recorded as an environment failure, not a TensorRT test failure.
- Stage 35: 10 packages, 83 TFMs, 5/5 negative scenarios. Stage 36: 10 packages, 83 API contracts, 48 managed dependencies, 83/83 SourceLink/PDB/API, 7/7 negative scenarios.
- Upstream package/binding identity did not change, so the TensorRT package baseline, `-RequireAdmitted`, and eight package-admission mutations were not repeated and the retained JSON was not rewritten.

Blocker delta is retained 2, new 0, disappeared 0. `formal publication blocked` remains accurate. No exact authorized plan/model, matching NVIDIA GPU, unique CUDA/cuDNN/TensorRT/bridge matrix, or recordable runtime identity was supplied, so `GPU validation skipped/blocked`; TensorRT algorithm and performance are not claimed as passed. No Git/GitHub publication write occurred. / blocker 变化为 retained 2、new 0、disappeared 0；正式发布仍阻断。因真实 GPU 前置条件未获授权，GPU 验证跳过/阻断，不声称算法或性能通过，也未执行 Git/GitHub 发布写入。
