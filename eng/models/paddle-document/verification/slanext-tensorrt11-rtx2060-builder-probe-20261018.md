# SLANeXt TensorRT 11 RTX 2060 Builder cross-check (2026-10-18)

## Scope

This is a native `trtexec` Builder-only cross-device check of the same four exact graphs previously tested on the local RTX 3060 Laptop. The official wired/wireless ONNX files and separately derived OpenVINO-compatible files were copied to the remote validation directory and SHA-256 checked there before invoking TensorRT. The optimization profile was `x=[1,3,512,512]` minimum, `[2,3,512,512]` optimum and maximum. `--skipInference` was used; this did not enter DeploySharp or run inference.

## Environment

- Host `ygj-NUC11PHi7`, Ubuntu 22.04.5 LTS, Linux `6.8.0-138-generic`.
- NVIDIA GeForce RTX 2060 6 GB, driver `580.178.04`.
- TensorRT `11.0.0.114` native `trtexec`; no .NET runtime or DeploySharp bridge was used.
- The pre-test idle snapshot was P8 at 300/405 MHz graphics/memory, with 209 MiB in use. After Builder attempts the snapshot was P0 at 1005/5500 MHz, 209 MiB in use and 0–1% GPU utilization. This short Builder probe did not configure or verify clock locking and provides no inference-performance measurement.

## Results

| Artifact | SHA-256 | Builder result | Time |
|---|---|---|---:|
| `paddle-table/slanext-wired` (official) | `0a6e063b56e35a434eb6669eb2342113c6bd76a6ce5acaa0331f370c9e00732f` | Failed, exit `1`; zero-byte engine placeholder | 0.262 s |
| `paddle-table/slanext-wireless` (official) | `5c79ee87cce6712f8f640394decce72157bd1df13c9bccf86d071bd07a6e9f97` | Failed, exit `1`; zero-byte engine placeholder | 0.228 s |
| `paddle-table/slanext-wired-openvino-compat` | `2a72212d681400ea514edbf007ab669c32f69f5813c6cac5c3b557bff0581ef0` | Failed, exit `1`; zero-byte engine placeholder | 0.231 s |
| `paddle-table/slanext-wireless-openvino-compat` | `9769807f5505dd09a88dbeea9b2daa6f085dc5ad0df343b93026d3793a9353c6` | Failed, exit `1`; zero-byte engine placeholder | 0.228 s |

All four attempts reported the same TensorRT Recurrence contract error: the initial state has scalar shape `[]`, while the recurrence-body value is `[-1,8]`. The logs also print an `Engine built in ...` timing line, but the process exits with code `1` and the requested serialized-engine files are zero bytes; that line is not a successful build. The machine-readable report records exact file sizes, hashes, times and GPU snapshots.

## Conclusion and boundary

This second-device check reproduces the same exact-graph blocker seen on the RTX 3060 Laptop under TensorRT 10.11/API 10 and TensorRT 11/API 11. It helps separate this recurrence-shape issue from the remote GPU being unavailable, but it is not a DeploySharp Builder or Session test. No Decoder, table HTML parity, quality, or latency result was produced. Keep these four exact graph/runtime combinations unsupported. Do not claim that all TensorRT versions reject SLANeXt; a future support attempt requires model-side export/rewrite with compatible recurrence state, then a non-empty Engine, execution, decoder parity and real table-quality checks.

The full verbose native logs and zero-byte engine placeholders remain in the remote test directory `/home/ygj/deploysharp-validation/slanext-trt`; no credentials are recorded in this report. For the local Windows TensorRT API-10 result, see the [four-artifact TensorRT 10.11 report](slanext-tensorrt10.11-builder-probe-20261018.md) and [JSON](slanext-tensorrt10.11-builder-probe-20261018.json).
