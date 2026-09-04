# PaddleOCR backend benchmark

This Windows-only console tool discovers PaddleOCR v4, v5, and v6 ONNX files below <code>E:\\Model\\paddleocr</code> (or <code>DEPLOYSHARP_PADDLEOCR_ROOT</code>) and runs only the complete detection -> crop/batch -> optional orientation -> recognition -> merge pipeline on one real image. It does not emit isolated det/cls/rec benchmark rows. Each version/variant/backend produces one final row with the selected batch size, selected independently-created inference-channel count, stage breakdown, and end-to-end latency.

Detection uses one independently-created backend session. Classification and recognition use independent session pools and batches. ONNX Runtime and OpenVINO use the dynamic model contract; OpenCV DNN specializes symbolic dimensions into exact static contracts and pads the final fixed batch. Batches beyond pool capacity wait for an idle session, and their crop tensors are created only after a slot is available so a long document does not retain every prepared batch at once. The pipeline restores detector order before it writes a result.

~~~powershell
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'E:\\Model\\paddleocr'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '5'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '10'
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
~~~

Keep enough warm-up calls when comparing versions. The first supported pipeline in a new process also initializes JIT-compiled preprocessing code, OpenCV native paths, and worker threads; a one-warm-up, low-sample mean can therefore attribute process cold-start cost to that model. The default runner uses three warm-ups and fifteen measured calls; the command above uses five and ten for a quicker focused check.

On the dedicated Win10 host, a five-warm-up/ten-call v5 mobile ORT CPU run measured about 10 ms preprocessing; a previous approximately 72 ms value came from a short noisy sample. Treat preprocessing as workload/host dependent and compare P50/P95.

The default image is <code>E:\\Data\\ocr\\demo\\_1.jpg</code>. On the current workstation that name resolves to the existing file <code>E:\\Data\\ocr\\demo_1.jpg</code>; when it is not present, the runner downloads <code>ocr-demo_1.jpg</code> from the <code>test-assets.1</code> release, verifies SHA-256, and caches it under <code>%LOCALAPPDATA%\\DeploySharp\\TestImages</code>. Set <code>DEPLOYSHARP_TEST_IMAGE_ROOT</code> to reuse another cache or <code>DEPLOYSHARP_PADDLEOCR_IMAGE</code> to use an explicit image. The complete-pipeline CSV is written as <code>paddleocr-full-*.csv</code>. In addition to stage means, the final row contains <code>total_p50_ms</code> and <code>total_p95_ms</code> calculated from the timed calls; these are zero only for an internal single-sample autotune probe and are populated by the formal run.
Use <code>DEPLOYSHARP_PADDLEOCR_BACKENDS</code> with a comma-separated list (for example <code>opencv-dnn</code> or <code>onnxruntime,openvino</code>) to run only selected backends while troubleshooting.
Each full-pipeline call is bounded by <code>DEPLOYSHARP_PADDLEOCR_PIPELINE_TIMEOUT_MS</code> (default 15000 ms). A timeout is recorded as <code>unavailable</code> with a timeout detail instead of blocking the remaining model/backend rows.
For steady-state throughput, set <code>DEPLOYSHARP_PADDLEOCR_REUSE_INPUT=1</code>. The decoded OpenCV image and detector tensor are prepared once and reused for warmup/timed calls; <code>preprocess_ms=0</code> and <code>total_ms</code> then represent warm pipeline latency. Leave it unset for end-to-end latency that includes image decode and preprocessing.

Automatic tuning is enabled by default. For every version/variant/backend, the runner tests the Cartesian product of <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_CONCURRENCY</code> (default <code>1,2,4</code>) and <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_BATCHES</code> (default <code>1,2,4,8,16</code>). Candidate tuning reuses the decoded/prepared input and ranks complete-pipeline wall time; orientation-plus-recognition is used as the tie-breaker because those are the stages most affected by batch and channel counts. The selected combination is then rerun under the requested complete-pipeline cold/steady protocol. Every candidate must return a stable complete result-contract SHA-256 across its timed samples, and the formal rerun must exactly reproduce the selected candidate. Different maximum batch sizes can legitimately produce different dynamic widths and CTC timestep traces, so the report records the number of deterministic contract variants across shapes rather than comparing every shape with batch-one output. The final row records the choice in <code>selected_batch_size</code> and <code>selected_inference_channels</code>. Short tuning samples are controlled by <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_WARMUP</code> and <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE_ITERATIONS</code>; increase them when the device is noisy. Set <code>DEPLOYSHARP_PADDLEOCR_AUTOTUNE=0</code> only for a fixed-configuration diagnostic run.

The fixed-run and batching controls are:

- <code>DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY</code>: independently-created classifier/recognizer sessions (default <code>1</code>).
- <code>DEPLOYSHARP_OPENCV_NUM_THREADS</code>: optional process-global OpenCV CPU thread count applied before OCR sessions are created. Leave it unset for the native default; test <code>4</code>, <code>8</code>, and <code>16</code> when combining several OCR channels because this trades per-session parallelism against channel-level concurrency.
- <code>DEPLOYSHARP_PADDLEOCR_BATCH_SIZE</code>: maximum classifier/recognizer batch (default <code>4</code>).
- <code>DEPLOYSHARP_PADDLEOCR_INTRA_OP_THREADS</code>: explicit ONNX Runtime CPU threads per classifier/recognizer session. When unset during automatic tuning, CPU threads are divided by the candidate inference-channel count.
- <code>DEPLOYSHARP_PADDLEOCR_DETECTION_INTRA_OP_THREADS</code>: ONNX Runtime CPU threads for the single detector session (default <code>0</code>, the runtime default); it is independent of the recognition pool size.
- <code>DEPLOYSHARP_PADDLEOCR_MAX_PADDING_RATIO</code>: maximum padded-width work divided by natural-width work (default <code>2.0</code>). This permits useful batches for mixed-width text while bounding wasted padded computation.
- <code>DEPLOYSHARP_PADDLEOCR_VERSIONS</code>: comma-separated filter such as <code>v5,v6-tiny</code>.
- <code>DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE</code>: explicit TensorRT classification/recognition batch (default <code>1</code>; requires dynamic-batch engines).

Tune CPU thread counts and session count together. Multiplying full-core sessions usually oversubscribes the processor and can make a larger pool slower.

If the repository contains `.onnx.engine` sidecars built by a different TensorRT minor release, rebuild them into an isolated output directory before measuring TensorRT. This leaves the source model directory unchanged:

~~~powershell
pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/Build-TensorRtEngines.ps1
~~~

Then point the benchmark at that isolated directory with <code>DEPLOYSHARP_PADDLEOCR_ROOT=artifacts\local-model-benchmarks\paddleocr-trt11-rebuilt</code>. The script uses the v4/v5/v6 input profiles documented below and requires <code>trtexec.exe</code> from the selected TensorRT installation. It defaults to TensorRT <code>BuilderOptimizationLevel=3</code>; pass <code>-BuilderOptimizationLevel 0</code> only when engine build time matters more than steady-state latency. The optional <code>-Fp16</code> switch is capability checked before any model is copied or built. TensorRT 11 strongly typed builds do not expose <code>--fp16</code>; for that runtime, an FP16-typed ONNX graph is required and the script fails explicitly instead of labelling an FP32 graph as FP16.

To measure the TensorRT sidecars, configure a matching consumer-owned bridge and vendor runtimes in the same PowerShell process. The runner defaults to the TensorRT 11 API line; set <code>DEPLOYSHARP_TENSORRT_API_VERSION</code> to <code>8</code>, <code>10</code>, or <code>11</code> when the sidecars were built for another API line.

~~~powershell
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_TENSORRT_API_VERSION = '11'
$env:JYPPX_NATIVE_BRIDGE_PATH = '<path>\jyppxtrtbridge.dll'
$env:JYPPX_TENSORRT_ROOT = 'D:\Program Files\TensorRT-11.0.0.114-cu12'
$env:JYPPX_CUDA_ROOT = 'C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9'
$env:JYPPX_CUDNN_ROOT = 'D:\Program Files\cuDNN-9.22.0-cuda12.9'
$env:PATH = "$env:JYPPX_TENSORRT_ROOT\bin;$env:JYPPX_TENSORRT_ROOT\lib;$env:JYPPX_CUDA_ROOT\bin;$env:JYPPX_CUDNN_ROOT\bin;$env:PATH"
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
~~~

The bridge DLL must match the selected TensorRT API/CUDA line, and a serialized engine must also be built by the same TensorRT serialization version. A missing or incompatible bridge, or an engine deserialization mismatch, is recorded as <code>unavailable</code>; it is never reported as a timing pass. For example, an engine serialized by TensorRT 10.10 cannot be loaded by TensorRT 10.11 or 11.0.

The TensorRT backend also runs the complete OCR pipeline when all three stage engines are present. The retained v4/v5/v6 sidecars under the original model directory are static TensorRT 11 engines with <code>1x3x736x736</code> detection, fixed <code>1x3x48x320</code> recognition, and batch-one contracts; their results are a correctness/latency baseline. For production batch throughput, rebuild isolated dynamic-batch engines with <code>Build-TensorRtEngines.ps1 -StageOptBatch 4 -StageMaxBatch 8</code>. Detection remains one image per call, while classification/recognition accept batches from 1 through 8. Set <code>DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE=4</code> when using that isolated output; the runner will not claim a larger batch for static sidecars.

The complete-pipeline run uses the real image <code>E:\Data\ocr\demo_1.jpg</code> and reports separate preprocessing, detection, crop, orientation, recognition, merge, and total columns. On the dedicated Windows 10 / RTX 2060 machine, TensorRT 11.0 + CUDA 12.9 completed all five mobile pipelines (two warm-ups, five measured calls, stage concurrency one): the latest steady totals were v4 mobile <code>146.838 ms</code>, v5 mobile <code>190.556 ms</code>, v6 tiny <code>83.764 ms</code>, v6 small <code>167.552 ms</code>, and v6 medium <code>339.484 ms</code>. These runs reuse the decoded image and prepared detector input; model loading, engine building, and CUDA initialization are outside the timed region. Report: <code>artifacts/remote-test/paddleocr-full-tensorrt-all-rerun-20260829.csv</code>. A cold rerun (decode and preprocessing included) measured v4 mobile <code>189.896 ms</code>, v5 mobile <code>184.215 ms</code>, v6 tiny <code>92.890 ms</code>, v6 small <code>171.562 ms</code>, and v6 medium <code>343.556 ms</code>; report: <code>artifacts/remote-test/paddleocr-full-tensorrt-all-cold-rerun-20260829.csv</code>.

For the same full TensorRT run, point the root at the rebuilt engine directory and choose the TensorRT backend explicitly. This command still produces only complete pipeline rows; no stage-only benchmark mode is available:

~~~powershell
$env:DEPLOYSHARP_PADDLEOCR_ROOT = 'artifacts\local-model-benchmarks\paddleocr-trt11-rebuilt'
$env:DEPLOYSHARP_PADDLEOCR_IMAGE = 'E:\DeploySharp-Remote\data\ocr\demo_1.jpg'
$env:DEPLOYSHARP_PADDLEOCR_BACKENDS = 'tensorrt'
$env:DEPLOYSHARP_PADDLEOCR_WARMUP = '2'
$env:DEPLOYSHARP_PADDLEOCR_ITERATIONS = '5'
$env:DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY = '1'
$env:DEPLOYSHARP_PADDLEOCR_BATCH_SIZE = '4'
$env:DEPLOYSHARP_PADDLEOCR_TENSORRT_BATCH_SIZE = '4' # dynamic cls/rec engines only
$env:DEPLOYSHARP_PADDLEOCR_REUSE_INPUT = '1' # omit for cold/decode timing
$env:DEPLOYSHARP_TENSORRT_RUN_EXTERNAL = '1'
$env:DEPLOYSHARP_TENSORRT_API_VERSION = '11'
$env:DEPLOYSHARP_CUDA_ARCHITECTURE = 'compute_75' # RTX 2060; choose the target for the actual GPU
dotnet run --project tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj -c Release
~~~

On the dedicated Windows 10 / RTX 2060 host, the current code and dynamic TensorRT 11 engines with BuilderOptimizationLevel 3 were measured sequentially with ten warm-ups, fifty timed calls, stage concurrency one, and batch eight. With the CPU CTC fallback, the steady totals were v4 mobile <code>67.158 ms</code>, v5 mobile <code>104.018 ms</code>, v6 tiny <code>45.816 ms</code>, v6 small <code>88.648 ms</code>, and v6 medium <code>135.365 ms</code>. Baseline: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-singlepassctc-20260829.csv</code>.

Setting <code>DEPLOYSHARP_CUDA_ARCHITECTURE=compute_75</code> enables the TensorRT session's compact GPU sequence-argmax path. The recognizer output remains on its TensorRT stream, a lazily compiled CUDA kernel produces only per-timestep class/confidence traces, and the full logits tensor is not copied to the CPU. Under the same ten-warm-up/fifty-call protocol, all five pipelines returned 16 regions and measured v4 mobile <code>53.045 ms</code>, v5 mobile <code>60.489 ms</code>, v6 tiny <code>29.972 ms</code>, v6 small <code>42.703 ms</code>, and v6 medium <code>92.142 ms</code>. Relative to the CPU CTC baseline, these totals are approximately 21%, 42%, 35%, 52%, and 32% lower. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-20260829.csv</code>.

A separate three-warm-up/ten-call A/B run compared the complete public OCR result contract, not only recognized text. Region indices/scores/polygons, recognized confidence/charset, and every token timestep/class/confidence/blank/repeat/unknown/emitted flag are included in <code>result_contract_sha256</code>. The CPU and GPU rows matched for all five models. In that controlled sample, GPU CTC reduced recognition from 41.128 to 24.586 ms (v4), 77.202 to 32.138 ms (v5), 97.492 to 50.916 ms (v6 medium), 66.893 to 22.501 ms (v6 small), and 28.875 to 12.607 ms (v6 tiny). Managed pipeline allocation fell from 56-120 MB to approximately 21-26 MB per call. Reports: <code>artifacts/remote-test/paddleocr-all-trt-b8-cpuctc-contract-20260829.csv</code> and <code>artifacts/remote-test/paddleocr-all-trt-b8-gpuctc-contract-20260829.csv</code>. DB contour/box decoding and region crop preparation still run on the CPU; this is not yet a fully device-resident OCR pipeline.

The subsequent host hot-path pass reuses exact-size recognition tensor buffers and pooled DB workspaces, and computes convex hulls from connected-component boundary pixels instead of sorting every interior pixel. With ten warm-ups, fifty calls, batch eight, and one stage session, the sustained totals became v4 <code>44.652 ms</code>, v5 <code>54.210 ms</code>, v6 tiny <code>24.798 ms</code>, v6 small <code>37.477 ms</code>, and v6 medium <code>86.997 ms</code>. Managed allocation was approximately 14.1-16.8 MB per call and all five complete contract hashes remained unchanged. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-telemetry-run-20260829.csv</code>.

For compact GPU CTC, asynchronous calls now dispatch synchronous reductions across independent pooled sessions when <code>DEPLOYSHARP_PADDLEOCR_STAGE_CONCURRENCY</code> is greater than one. On the same host with two stage sessions, the final telemetry run measured v4 <code>41.249 ms</code>, v5 <code>50.797 ms</code>, v6 tiny <code>22.917 ms</code>, v6 small <code>35.328 ms</code>, and v6 medium <code>85.353 ms</code>. Recognition improved by roughly 4-11% versus the previously serialized compact path; it does not double because both execution contexts share one GPU. Two sessions also consume more engine/device memory, so benchmark one and two sessions on the deployment GPU rather than making two the universal default. Report: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-c2-asyncfix-telemetry-run-20260829.csv</code>.

The previous contract-preserving host pass writes OpenCV crop pixels directly from native `Mat` rows into the pooled Float32 tensor, initializes only actual right-side padding, fuses DB probability validation with the connected-component mask, and computes convex hulls from per-column extrema without sorting every boundary point. Under the same batch-eight/two-session/10+50 protocol, that historical pass measured v4 <code>36.060 ms</code>, v5 <code>48.711 ms</code>, v6 tiny <code>21.652 ms</code>, v6 small <code>34.375 ms</code>, and v6 medium <code>83.683 ms</code>. DB postprocessing measured <code>2.568-3.200 ms</code>, recognition preparation work <code>2.016-2.649 ms</code>, and compact CTC host postprocessing <code>0.023-0.124 ms</code>. Every row still returned 16 regions with the same complete contract hash. Report: <code>artifacts/remote-test/paddleocr-all-trt-c2-paddingonly-20260829.csv</code>.

The current best complete-pipeline result on the dedicated host is the device-cache reuse run: v4 <code>32.319 ms</code>, v5 <code>44.655 ms</code>, v6 tiny <code>17.541 ms</code>, v6 small <code>30.017 ms</code>, and v6 medium <code>80.024 ms</code>. It uses 10 warm-ups, 50 timed calls, two stage sessions, recognition batch 8, prepared-input reuse, and CUDA CTC argmax. See <code>artifacts/remote-test/paddleocr-all-trt-c2-devicecache-20260829.csv</code> and the device-grouped [performance record](../../docs/articles/device-performance-benchmarks.md).

The no-softmax CTC decoder now validates probabilities during its required argmax scan instead of scanning the whole tensor twice, and it no longer allocates an unused class workspace. In a controlled v6-tiny batch-16 comparison, this reduced recognition from <code>32.778 ms</code> to <code>28.250 ms</code> and total latency from <code>53.990 ms</code> to <code>49.332 ms</code>, with the recognized-text SHA-256 unchanged. Report: <code>artifacts/remote-test/paddleocr-v6tiny-trt-opt3-b16-singlepassctc-20260829.csv</code>. A pure recognition-engine test increased image throughput from approximately <code>4,659 images/s</code> at batch eight to <code>5,768 images/s</code> at batch sixteen, but complete-pipeline latency improves less because single-image detection and host-side DB region decoding remain on the critical path.

BuilderOptimizationLevel 5 was also tested against level 3 with otherwise identical v6-tiny batch-16 profiles. Recognition was approximately <code>0.14%</code> slower and detection approximately <code>0.37%</code> faster, while engine construction took about 200 seconds. The difference is within run-to-run noise, so level 3 remains the default.

The command writes a CSV under <code>artifacts/local-model-benchmarks</code> by default and emits one <code>PADDLEOCR_BENCHMARK</code> line per model/backend. Full-pipeline rows include detector inference/postprocessing, recognition preparation/inference/postprocessing work, recognition batch count, <code>result_text_sha256</code>, and <code>result_contract_sha256</code>; the latter covers geometry, region metadata, recognized text metadata, and the complete CTC token trace. Work timings are summed across concurrently executing batches and therefore may exceed recognition wall time. The detail column states whether TensorRT CUDA sequence argmax was enabled and records its architecture target. <code>pass</code> includes mean, P50, and P95 milliseconds; <code>unavailable</code> means runtime/device initialization or TensorRT engine deserialization failed; <code>unsupported</code> means the backend importer rejected the graph; <code>skip</code> means the backend gate was not enabled. TensorRT measurements require the consumer-owned bridge/runtime and <code>DEPLOYSHARP_TENSORRT_RUN_EXTERNAL=1</code>.

The pipeline benchmark keeps model loading and OpenVINO compilation outside the timed region and measures OCR postprocessing in the stage columns. The crop_ms column is the bounded batch-planning/grouping cost; actual crop tensor materialization is performed just before each recognizer call and is included in recognition_ms so the reported stage sum remains an end-to-end wall-time accounting when batches overlap. The shared OpenCV Pillow-compatible resamplers use parallel row passes for large images (BLIP/Donut/SAM); small OCR crops stay single-threaded to avoid scheduler overhead. Results are local evidence and are not cross-machine performance guarantees.

The OCR adapter reuses thread-local perspective-warp and resize Mats plus point buffers. This keeps the returned crop Mat independently owned while removing temporary native allocations from the per-region hot path. The visual pipeline also caches the immutable Core input collection for prepared-frame reuse. / OCR 适配器会复用线程本地透视变换/缩放 Mat 及角点缓冲；返回的 crop Mat 仍独立拥有，同时移除每个区域热路径中的临时 native 分配。视觉 Pipeline 还会缓存已准备帧对应的不可变 Core 输入集合。

CTC token lists are transferred through a trusted internal read-only path and are not copied again while restoring source-region indices; public result constructors remain defensive. / CTC token 列表通过受信任的内部只读路径转移，在恢复源区域索引时不再重复复制；公共结果构造函数仍保持防御性复制。

Do not compare pool sizes while unrelated workloads are active. For a publishable run, pin the same image, runtime versions, CPU/GPU power mode, warm-up and iteration counts, batch size, padding ratio, session count, and per-session thread count. The dedicated Win10 rerun on 2026-08-28 confirmed ORT CPU and OpenCV DNN CPU full-pipeline execution for v5 mobile and v6 tiny/small/medium; ORT CUDA was attempted explicitly and recorded as <code>unavailable</code> with <code>DS-ORT-5008</code> (CUDA 801) when the device context could not initialize.

GPU clocks must be sampled during the timed workload, not inferred from an idle <code>nvidia-smi</code> snapshot. Use <code>Invoke-WithGpuTelemetry.ps1</code> to run a benchmark while recording P-state, utilization, graphics/SM/memory clocks, power, temperature, and NVIDIA clock-event reasons. The summary is calculated only from samples at or above the selected utilization threshold. A low clock with low utilization indicates an input/feed gap rather than a locked GPU; power-cap regulation, thermal slowdown, and hardware slowdown are counted separately.

During the final two-session five-model run, 63 active samples averaged <code>58.2%</code> GPU utilization and <code>1,828 MHz</code> graphics clock, reached <code>1,875 MHz</code>, averaged <code>78.4 W</code>, and peaked at <code>60 C</code>. Thermal and hardware-slowdown samples were both zero. NVIDIA reported software power-cap regulation in 20 samples; this is normal boost control near the configured power limit, not fixed-frequency or thermal throttling. A pure recognizer load independently reached 99% utilization, 1,800-1,845 MHz, and 113-117 W. The device therefore was not locked at the approximately 1,005 MHz clock observed during idle or short CPU-fed gaps. Raw telemetry: <code>artifacts/remote-test/paddleocr-all-trt-opt3-b8-gpuctc-dbpool-c2-asyncfix-telemetry-20260829.csv</code>.

~~~powershell
pwsh -NoProfile -File tools/DeploySharp.PaddleOcrBenchmark/Invoke-WithGpuTelemetry.ps1 `
    -Executable dotnet `
    -ArgumentList @('run', '--project', 'tools/DeploySharp.PaddleOcrBenchmark/DeploySharp.PaddleOcrBenchmark.csproj', '-c', 'Release') `
    -OutputPath artifacts/local-model-benchmarks/paddleocr-gpu-telemetry.csv
~~~
