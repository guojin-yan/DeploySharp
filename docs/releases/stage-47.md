# Stage 47: TensorRT release proof recheck / 阶段 47：TensorRT 发布证明再复核

Release ID `368273346` remains `immutable=false`, unchanged since `2026-08-11T00:49:26Z`, with the same 20 assets and zero proof assets. The `v4.0.0` tag still resolves to `673e120...`; its complete tree contains no lock/assets or generic release provenance. / Release、tag 与 tree 均无新的正式 proof。

The exact NuGet.org package, `Apache-2.0`, repository commit, SHA256/raw-SHA512/contentHash, 15 TFM/45 managed DLL payload, 311/4,374 net8 API, PE/XML contracts, managed-only payload, and Repository signature remain unchanged. Retained JSON is not rewritten. / 精确包、许可证、repository、hash、TFM/API、payload、签名与 retained JSON 均未变化。

Both current blockers remain retained; new and disappeared counts are zero. Stage 42 license/Owner decision blockers remain historical disappeared entries. No adapter/package/reference/API/TFM, DeploySharp lock/assets, consumer, native/GPU probe, engine/plan/cache, or algorithm/performance evidence was created. / 两项 blocker 继续 retained，未创建适配器或 GPU 证据。

TensorRT baseline, expected `-RequireAdmitted` failure, 8/8 negatives, and Repository signature verification pass their intended contracts. Full validation is recorded in the [Stage 47 review](../history/tensorrt-release-proof-recheck-stage47.md). / 门禁与签名检查符合合同，详见 [Stage 47 复核](../history/tensorrt-release-proof-recheck-stage47.md)。

Stage 35/36 pass 9 packages/82 TFMs with 5/5 and 7/7 negatives. The full solution passes `378/50/0`; inventory passes at 69 entries/56 manifests; exact Qwen is `ADMITTED missing=none`; NuGet vulnerable/deprecated rows are zero and 113 outdated rows are report-only. Retained Stage 36, TensorRT, Qwen, inventory, and catalog identities remain unchanged. / Stage 35/36、全解决方案、inventory、精确 Qwen 与 NuGet 报告均通过，retained evidence 与受保护状态不变。

Real engine/cache/inference remains blocked without an authorized exact ONNX and unique matching GPU/runtime identity. Temporary NuGet, mutation, and validation-pack inputs were removed; no model/tool download, dependency upgrade, commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 真实 engine/cache/infer 继续 blocked；临时输入已清理，未执行发布写入。
