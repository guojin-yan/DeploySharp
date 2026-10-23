# Natural wide-line TensorRT 11 run — interrupted native enqueue (2026-10-23)

Status: **quality unverified for this exact run**. The same 13 pinned HierText quadrilaterals were scheduled for v6 Small/Medium REC, Clamp/SlidingWindow, height 48, maximum width 3,200 and batch/Session count one. DET/CLS are bypassed; crop, CTC and merging use the shared CPU path. This is not a device-resident full OCR pipeline.

DeploySharp's Builder successfully produced nonempty Small/Medium engines (23,709,884 / 79,354,412 bytes), with runtime-default graph precision, optimization level 0, 256 MiB workspace and dynamic width profiles. Engine and source hashes are in the [machine report](paddleocr-hiertext-natural-wide-tensorrt-interrupted-20261023.json). The final per-row report was not produced, so no completed row count, CER/WER, parity or latency is asserted.

The run began at 17:02:45 +08:00 and was stopped at 17:24:56 after native diagnostic capture. Two stack captures showed the call at `NativeBridgeApi.EnqueueAsync`, through the TensorRT execution context/bindings and `TensorRtSession.RunCore`; process threads were waiting. Only this task's testhost was stopped after validating its exact executable path. Source workloads were left running. The TRX therefore reports an aborted run, not a passing test.

The RTX 3060 Laptop has 6,144 MiB VRAM and driver 576.02. Before scheduling, a snapshot showed 5,684 MiB used and 71% GPU utilization; during the run another showed 5,589 MiB and 94%. No clocks were changed. These observations make this unsuitable for a speed comparison, but **do not prove resource load caused the native wait**. The precise model, width and native cause still need isolation.

Next execution should use the added engine/line checkpoints and a bounded process watchdog (`dotnet test --blame-hang --blame-hang-timeout 180s`), then retest on an idle GPU. Checkpoints were added after the interrupted run and cannot reconstruct its in-flight line. Preserve the engines and stack/TRX hashes; if the wait persists, compare the precise TRT/bridge API and dynamic shapes independently. Do not convert a process interruption into a model-family unsupported flag or overwrite previous exact TensorRT evidence.

The [ORT/OpenVINO refreshed reference](paddleocr-hiertext-natural-wide-cpu-reference-20261023.md) and [OpenCV/ORT comparison](paddleocr-hiertext-natural-wide-opencv-parity-20261023.md) completed separately. They do not supply the missing TensorRT result. A2 remains open.
