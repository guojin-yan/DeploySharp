---
name: deploysharp-performance-analysis
description: Benchmark and optimize DeploySharp preprocessing, inference, decoding, batching, Session pools, memory copies, and allocations. Use for latency, throughput, P50/P95, CPU/GPU utilization, or regression investigations.
---

# DeploySharp Performance Analysis

Performance work must preserve the result contract. Establish correctness and a repeatable baseline before changing copies, buffers, batching, threads, streams, or decoder algorithms.

## Measure The Right Boundary

Report cold/end-to-end and steady separately. Split image decode, preprocessing, host-to-device transfer, backend execution, device-to-host transfer, decoder/postprocessing, merge, and total wall time. For OCR, measure the complete `det -> crop -> cls/orientation -> rec -> merge` pipeline and record the selected recognition Batch and independent Session count.

Use at least 5 warm-ups and 50 measured calls for a formal row. Compute mean, P50, P95, minimum, maximum, allocations, peak process memory, and GPU memory from raw samples. Record model/input/engine SHA-256, source revision, runtime versions, power policy, clocks or Boost state, precision, shape/profile, Batch, Sessions, streams, and input reuse.

## Optimization Priorities

1. Remove repeated decode, resize, normalization, tensor copies, LINQ materialization, and per-call large allocations.
2. Reuse bounded buffers and prepared inputs without allowing mutable state to escape its owner.
3. Use true model Batch when the graph and decoder support row isolation; otherwise use a bounded pool of independently created Sessions.
4. Overlap preparation of the next frame with current inference through bounded prefetch. Avoid unbounded `Task.Run` queues.
5. Keep GPU data resident only when the backend contract and result validation support it; retain a tested CPU fallback.

Judge changes by correctness, P50/P95, throughput, allocations, and memory together. A lower mean with worse P95 or unbounded memory is not a general improvement.

## Repository Outputs

Use `tools/DeploySharp.VisualBenchmark` and `tools/DeploySharp.PaddleOcrBenchmark` where applicable. Write raw results to a caller-owned directory outside Git. Update `docs/articles/device-performance-benchmarks.md` only with concise, comparable summaries; do not commit machine logs or one-off reports.
