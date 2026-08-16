# Stage 61 release note: TensorRT local cache / 阶段 61 发布说明：TensorRT 本地缓存

Date / 日期: 2026-08-16
Version / 版本: `2.0.0-alpha.1`
Status / 状态: managed implementation; native execution evidence retained / managed 实现完成；保留 native 执行证据

## Added / 新增

- Added bounded local storage for PTX, CUBIN, `.engine` and `.plan` artifacts with strict versioned manifests, SHA256/length validation and same-directory atomic publication. / 新增 PTX、CUBIN 与 engine/plan 的有界本地存储、严格 manifest、SHA256/长度检查和同目录原子发布。
- Added deterministic pre-build CUDA and engine identities based on compatibility inputs. Physical GPU UUIDs are excluded so compatible GPUs can share entries. / 新增基于兼容输入的构建前 key，并排除物理 GPU UUID。
- Added `TensorRtLocalCacheOptions` and `TensorRtLocalSessionFactory` for explicit cache-root selection, resolve/build/compile orchestration and session creation. / 新增显式 cache root 与 build/compile/session 编排门面。
- Added one bounded native-load recovery attempt: invalidate the exact entry, recreate once, then propagate any repeated failure. / native load 失败时仅失效精确条目并重建一次。
- Hardened the local closure audit: absolute roots, canonical header order, same-key factory-failure sharing, different-key independence, hardlink/reparse rejection and cancellation without invalidation are now explicit managed proofs. / 本地闭环审计补齐绝对 root、header 规范顺序、同 key 失败共享、不同 key 独立、hardlink/reparse 拒绝和取消不失效证明。

## Boundaries / 边界

Compiler, builder, provider, session and inference defaults do not access the cache. The facade uses a stable per-user local application data root unless the application supplies an absolute path. Same-process factories are deduplicated; different processes must not write the same root concurrently. No distribution or governance subsystem is included. / 既有默认路径不访问缓存；门面使用稳定的每用户目录或调用方绝对路径。同进程去重，不同进程不得并发写同一 root；不包含分发或治理子系统。

Cache artifacts remain consumer-owned runtime data and are excluded from packages, Git, releases, inventory and the official catalog. No native runtime, model or generated cache payload is shipped. Core and ModelPack remain TensorRT-free. / 缓存仍为 consumer-owned runtime data，不进入包、Git、Release、inventory 或 official catalog；Core 与 ModelPack 继续零 TensorRT 依赖。

Manually copied engines remain loadable through the existing provider without using the cache. Managed identity checks cannot guarantee native compatibility; TensorRT deserialization is the final authority. DeploySharp packages do not contain native runtime, engine, plan, PTX or CUBIN payloads. / 手工复制的 engine 仍可直接交给现有 provider；managed identity 不能替代 TensorRT native deserialize 的最终兼容性判断，DeploySharp 包不携带 native runtime 或生成工件。

Managed-only tests cover local engine and CUDA artifact miss/hit behavior, identity separation, corruption/path rejection, same-process deduplication, facade root selection, cancellation and the one-retry rule. Real GPU validation remains conditional on an already installed, exactly matching native environment. / managed-only 测试覆盖 miss/hit、identity、安全边界、同进程去重、root、取消与单次重试；真实 GPU 验证以本机已安装且精确匹配的 native 环境为前提。
