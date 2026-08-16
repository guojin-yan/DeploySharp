# TensorRT CUDA/RTC real-GPU proof and incremental admission / TensorRT CUDA/RTC 真实 GPU 证明与增量准入

Stage 55 executes the previously authorized native paths on one fully identified local matrix. It proves CUDA RTC compilation, Driver module loading, caller-owned stream/device-buffer preprocessing and postprocessing, synchronization-error propagation, active-launch/module disposal, TensorRT ONNX build, and actual engine inference. It does not claim model accuracy or performance. / 阶段 55 在一套完整标识的本地 matrix 上执行获授权的 native 路径，真实覆盖 RTC 编译、Driver module load、调用方 stream/device buffer 前后处理、同步错误传播、active-launch/module disposal、ONNX build 与 engine inference；不声明模型精度或性能。

## Incremental upstream decision / 上游增量决定

Authenticated read-only review found no upstream identity change:

| Field | Stage 55 value |
| --- | --- |
| Release | ID `368273346`; tag `v4.0.0`; `immutable=false`; published/updated `2026-08-11T00:49:26Z` |
| Tag commit | `673e120807d789d90a13a9f28a043282e95bb5e6` |
| Assets | 20; proof-named manifest/provenance/attestation/lock/assets assets `=0` |
| GitHub managed asset | ID `509456931`; 15,595,749 bytes; SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7` |
| NuGet.org admitted identity | 15,608,836 bytes; SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`; retained repository signature pass |
| GitHub attestations | both GitHub-asset and NuGet-package SHA256 subject lookups returned HTTP 404 |

The retained admission JSON remains 10,200 bytes with SHA256 `6ecd39df19bbd7a2c49d031da0e9db38a4523c2c8d5ad2388e51acc0e0c5c3f0`. Blocker delta is retained 2/new 0/disappeared 0. The eight-class package admission was not rerun and the retained JSON was not rewritten. / 上游 identity 未变化，因此不重跑八类 package admission、不改写 retained JSON。

## Supplied package decision / 用户提供包的决定

The supplied `C:\Users\guoji\Downloads\JYPPX.TensorRT.CSharp.API.4.0.0.nupkg` is 15,595,749 bytes with SHA256 `58add436d8f8e132349f84272fb985c83f38bb6897920f1bc163f1ceb38571d7`; `dotnet nuget verify --all` reports unsigned `NU3004`. It is the GitHub Release asset, not the admitted NuGet.org repository-signed package container. It was isolated under `E:\DeploySharp-External\stage55\github-asset-58add436d8f8e132`; it was used only as a precisely recorded local GPU managed input. / 用户给出的包是 GitHub Release unsigned asset，不是精确准入的 NuGet.org signed container；只作为已记录身份的本地 GPU managed 输入。

The subsequently supplied `C:\Users\guoji\Downloads\jyppx.tensorrt.csharp.api.4.0.0 (1).nupkg` exactly matches the admitted identity: 15,608,836 bytes, SHA256 `92bc106465dd87651118adbdaa8dbcb921cd117d685005ae1ae13f09cb80e038`, raw SHA512 `9VPO6fsj4uUWqURYoh5vxh4L8S6/y/RU+zXaKYJmFNpUhwev4DhExI67sG9eaAocIVYf9NqPvppNk2S7YtVgZw==`, retained contentHash, and a valid NuGet.org Repository signature. The package was copied to a hash-named External directory and the pure-package consumer passed from a three-package local-only source. The GitHub asset and rejected Stage 40 global-cache package were not substituted. / 用户随后提供的包精确匹配准入 identity 与 NuGet.org Repository signature；纯包 consumer 已从只含三个包的本地 source 通过，未使用 GitHub asset 或 Stage 40 旧缓存替代。

## Executed native matrix / 已执行 native matrix

| Component | Exact identity |
| --- | --- |
| GPU | ordinal 0; NVIDIA GeForce RTX 3060 Laptop GPU; UUID `GPU-34943fb3-11cd-dd8c-7dec-248781e47353`; compute capability 8.6 / `sm_86`; driver 576.02 |
| CUDA | toolkit/runtime 12.9; `cudart64_12.dll` SHA256 `cf68ac7d47c621988db3343b8a211188e6f94cccf30dec3b15dd52d69fdcf512` |
| Driver | `nvcuda.dll` SHA256 `5e37adf0e5457e15d2eb6ed820938fa3ac014e039622334d20ec74655f03f85a` |
| NVRTC | 12.9; `nvrtc64_120_0.dll` SHA256 `69a3b8e00fdf6c64805bd369b037db91f1ac1fb25b3411676b434736df945249`; builtins SHA256 `60c0705ef1bcef8de12f442b424e2edc649956611b8d04990ef17abb1df748a2` |
| TensorRT | 10.11.0.33 cu12; `nvinfer_10.dll` SHA256 `872eb3acdda69d85444c5ac59af0308546a1a41b1857fba547b7cef6cd4f3226`; parser SHA256 `d23c1c61f37ff0f640ee40bbca8001e0bc3cd3226ef2731b5ffba8059e1950be` |
| cuDNN | 9.22.0 CUDA 12.9; `cudnn64_9.dll` SHA256 `7cb8db5092dea703488a04210f96befc54de2aac51db1cf6a9c0a1f0e687f6bc` |
| Native bridge | TRT 10.11/CUDA 12.9/cuDNN 9.22 bridge 4.0.0; package SHA256 `0a4cb23b0175abdde7ff7b6fe16c9094829d0b17304cc5822baf477064e9e032`; DLL SHA256 `0da3f8751d10f3f221fa7c4145ba8dd2927b7d98a8afdce4645531792b3b42e8` |
| ONNX | local TensorRT MNIST sample; 26,454 bytes; SHA256 `2f06e72de813a8635c9bc0397ac447a601bdbfa7df4bebc278723b958831c9bf` |

The bridge package is an unsigned local matrix input from repository commit `0fbba41f20c3adafacb302d47bb80519f8bde302`. It is sufficient for this explicitly identified local execution record, but it is not formal release provenance and does not satisfy either publication blocker. / bridge 是已记录 commit/hash 的 unsigned 本地 matrix 输入，不能替代正式发布证明。

## CUDA/RTC execution / CUDA/RTC 执行

Both kernels compiled to in-memory PTX for `compute_86` with NVRTC 12.9, one virtual header, `--generate-line-info`, and `--std=c++14`. No PTX/CUBIN/fatbin or cache entry was persisted. A caller-owned non-default stream and a 32-byte caller-owned device buffer described as Float32 shape `[8]`, ReadWrite, byte range `[0,32)` were used. Both launches used grid `[1,1,1]`, block `[64,1,1]`, zero dynamic shared memory, explicit synchronization, and device ordinal 0. The separate fault launch used block `[32,1,1]`. / 两条前后处理路径均使用调用方非默认 stream 与 device buffer，不落盘 PTX 或 cache entry；独立 fault launch 使用 `[32,1,1]`。

| Path | Real result | Artifact SHA256 | Cache-key SHA256 |
| --- | --- | --- | --- |
| Preprocessing | input `[-1,0,1,2,3,4,5,6]`; output `[-1,1,3,5,7,9,11,13]`; matched | `d69d45471b951af998e5cf6240325086ae8fc515d1b701c43cd43decc4802ad9` | `ad79a90edd7f39a97f7e8c7cab74fa72e8b4c258dfadde0d1f33fbf9162a8c97` |
| Postprocessing | output `[0,1,3,5,6,6,6,6]`; matched | `79e7062ed5990adcde9e8e2f44b98f419bdf082fb2871b5ffe304e026ef623f6` | `b01546c94a5633adf1bfed24479c8588800b34715df1c913a042b00033882790` |

Cache identity binds source/header/options/artifact hashes, compiler version and binary identity, target and artifact kind, kernel entry, CUDA runtime and driver version/binary identity, GPU architecture/UUID, and native-bridge package/DLL identity. Launch identity separately binds artifact/kernel, grid/block/shared memory, synchronization mode, hashed scalar values, buffer descriptor identity, and device ordinals. / cache key 与 launch identity 覆盖编译、native、GPU、buffer 和 launch 的完整字段。

Disposing a module with an active launch was rejected as expected; disposing the launch and then the module passed. A separate caller-managed fault launch produced CUDA error 700 `CUDA_ERROR_ILLEGAL_ADDRESS` at synchronization, surfaced as `JYPPX.CudaSharp.CudaException` with Cuda/RuntimeError, after which launch/module/stream/device-memory disposal all succeeded. Its PTX SHA256 is `be807e9a94ae0b4108dd7d338739d69cb07b81acb21f459f5437c5b097352e45` and cache key is `76a25c060c4ac64c6009761b7ba31305dd51a551ded2ecfe3f0ef8ecb886266f`. / active-launch disposal 与同步错误传播均按预期真实执行，fault 属于 expected failure 而非 unexpected failure。

The selected bridge does not export `cudaStreamGetDevice`; the managed layer now falls back to `CudaDevice.Current` only for the exact `NotSupported` plus `Cuda` status, while all other CUDA errors propagate. The module and device-buffer ordinal comparisons remain enforced. / 仅对精确 unsupported CUDA 查询采用 current-device fallback，其他错误继续传播。

## ONNX build and inference / ONNX 构建与推理

TensorRT 10.11 built a Float32 engine from the local MNIST ONNX:

- input `Input3`, Float32 `[1,1,28,28]`; output `Plus214_Output_0`, Float32 `[1,10]`;
- engine 342,164 bytes, SHA256 `4033b945093d2837229faf0574bcf6974276b7119d7a971cc356049fa2470842`;
- managed `BuildInputsSha256` `164a2f7ddfb401a0f4bbf09f052af2a6a6b1368036b4bacae7c05627accf78d4`;
- the DeploySharp provider loaded the engine and performed actual TensorRT execution; all 10 output values were finite, with value SHA256 `59d7c74207b35e2bd0e083aeee325387a427d7dbbca6e32d9c1d93745b8a2f68`.

`TensorRtOnnxParser.SetBuilderConfig` is supported only by the TensorRT 11 bridge API. Stage 55 now calls it only for API line 11, allowing the TensorRT 10 parser/build path while preserving TensorRT 8/10/11 public options. This is an internal compatibility correction, not a public API change. / parser builder config 只在 TRT 11 调用，修复 TRT 10 真实 build 兼容性且不改变 public API。

The run proves that build and inference execute successfully for the recorded input and matrix. It does not establish MNIST accuracy, general algorithm correctness, or performance. / 本证据只证明该输入与 matrix 的实际 build/inference 成功，不推导算法精度或性能结论。

## Verification and ownership / 验证与所有权

| Gate | Stage 55 result |
| --- | --- |
| Focused TensorRT managed tests | pass: 17 / skip: 0 / fail: 0 |
| Real CUDA preprocessing/postprocessing | pass |
| Real synchronization error | expected failure observed; no unexpected failure |
| Real TensorRT builder/inference | pass; no accuracy/performance claim |
| Pure-package consumer | pass: exact admitted signed nupkg; local-only three-package source; 0 warnings/errors; marker `DEPLOYSHARP_TENSORRT_PACKAGE_CONSUMER_OK` |
| Dual candidate pack | pass: 10 packages / 83 TFMs; semantic 10/10 |
| Stage 35 positive/negative | pass; negative 5/5 |
| Stage 36 positive/negative | pass; 83 API/SourceLink/PDB contracts; negative 7/7 |
| Full solution | pass: 395 / skip: 50 / fail: 0 |
| NuGet audit | vulnerable 0; deprecated 0; outdated read-only, no upgrade |
| Inventory and exact Qwen | pass: inventory 69; Qwen `ADMITTED missing=none`; protected bytes unchanged |

The net8 public contract remains 215 members with SHA256 `d5b74032d2a0da2926595bc8db184aa3a1aa6b3f43ee97d60446594ad1c82452`. Core and ModelPack remain TensorRT-free. DeploySharp owns managed wrappers, builder temporary-write lifecycle, a loaded module, and launch owners. The caller owns native libraries, stream/device memory, model/ONNX, generated engine/plan, and External/local cache. No TensorRT-LLM, Core CUDA backend, persistent cache writer, native/model/package/catalog/inventory payload, or dependency upgrade was added. / public API 与 ownership 不变。

External GPU evidence is retained at `E:\DeploySharp-External\stage55\gpu-harness\evidence`: success JSON is 20,589 bytes, SHA256 `20d21872357f40d8d5d260c948df7640dc7fe3cb6424515377569e14da168495`; fault JSON is 11,532 bytes, SHA256 `ab7d0bb792b9ff896183c70cf3cdf48ec4ee9803569b36ffe1f0497a090d2eab`. Pure-package execution evidence is retained at `E:\DeploySharp-External\stage55\pure-package-consumer-evidence.json`, 3,952 bytes, SHA256 `c87e10bc5796933e6eb56a8a9f05e6aaae3fc825b0ec1fa496d41a910efe747e`. The generated engine, consumer worktree, temporary package source, and isolated restore cache were deleted; an External scan found no retained `.engine`, `.plan`, `.ptx`, `.cubin`, `.fatbin`, or `.tmp` artifact. / GPU 与纯包证据均保留在 External；engine、consumer/source/cache 与所有临时 native 工件已清理。

## Remaining blockers / 剩余阻断

Formal publication remains blocked only by:

1. an immutable cross-channel manifest binding repository/tag/commit, GitHub asset, and NuGet.org signed-package size/SHA256/SHA512/contentHash/catalog/signature identity;
2. same-build immutable provenance/attestation binding commit, lock/assets/build inputs, released assets, and exact output hashes.

The exact pure-package consumer and the real-GPU authorization/execution gate are both resolved for the recorded Stage 55 identities. Only the two upstream formal-publication proofs remain blocked. No commit, push, tag, signing, Release mutation, upload, or Actions run occurred. / 精确纯包 consumer 与所记录 matrix/input 的真实 GPU 门禁均已解决；只剩两项上游正式发布 proof blocker。
