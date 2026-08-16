# TensorRT formal proof and GPU gate / TensorRT 正式证明与 GPU 门禁

Stage 50 performed a read-only GitHub review with the installed `gh` CLI. Release ID `368273346` remains `immutable=false`, `updated_at=2026-08-11T00:49:26Z`, with 20 uploaded assets. No release asset or release-body field supplies the required cross-channel machine-readable manifest. The `v4.0.0` tag still resolves to approved commit `673e120807d789d90a13a9f28a043282e95bb5e6`; its complete 3,559-entry tree is not truncated, but contains no exact `packages.lock.json` or `project.assets.json` build proof. GitHub attestation lookups for the GitHub managed-asset SHA256 and the NuGet.org package SHA256 both returned HTTP 404, so no repository attestation is listable for either subject. / 阶段 50 使用已安装 `gh` 做只读复核：Release、tag、完整 tree 与 attestation API 均未提供两项正式 proof。

The two release conditions remain formal-publication blockers only: an immutable manifest binding repository/tag/commit plus both channel hashes/signature/catalog fields, and same-build immutable lock/assets or equivalent provenance bound to commit `673e120...` and the exact package hashes. Blocker delta is retained 2, new 0, disappeared 0. The Stage 49 adapter admission and its retained package JSON are unchanged. / 两项条件继续只阻止正式发布，blocker 为 retained 2、new 0、disappeared 0；adapter 准入与 retained JSON 不变。

## Verification / 验证

- TensorRT focused tests: `7 passed / 0 skipped / 0 failed`.
- Package-only consumer: `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK native=consumer-owned engine=external gpu=not-run`.
- Inventory check: 69 entries passed. Exact Qwen admission remains `ADMITTED missing=none`, 491,400,032 bytes, SHA256 `74a4da8c9fdbcd15bd1f6d01d621410d31c6fc00986f5eb687824e7b93d7a9db`.
- NuGet vulnerability and deprecated checks report no packages. Outdated output is report-only; no dependency was upgraded.
- Full solution and Stage 35/36 were not rerun because neither source/API nor package/Release identity changed; their last verified Stage 49 results remain applicable.

No exact local plan/ONNX, matching NVIDIA GPU, single CUDA/cuDNN/TensorRT/native-bridge matrix, runtime identity, or approved cache-key fields were supplied. Therefore `GPU validation skipped/blocked`; no CPU, mock, ORT, engine build, cache, native probe, algorithm claim, or performance claim was used. No Git/GitHub write occurred. / 未提供真实 GPU 前置，因此 GPU validation skipped/blocked，不声称 TensorRT 算法或性能通过，也未执行发布写入。
