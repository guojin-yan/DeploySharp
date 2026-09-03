# DeploySharp Device Benchmark

This portable Windows x64 package measures the same models and inputs on different computers. It includes a private .NET 10 runtime, DeploySharp assemblies, ONNX Runtime, OpenVINO and OpenCV native libraries, the complete official visual catalog, PaddleOCR v4/v5/v6 pipelines, CLIP/SAM/BLIP multi-artifact pipelines, and fixed test images. The target computer does not need a separate .NET installation.

## Run

Open PowerShell in this directory and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Run-DeviceBenchmark.ps1
```

The default run tests all single-model visual cases on all six backend paths in both cold and steady modes, then runs CLIP/SAM/BLIP and the complete PaddleOCR v4/v5/v6 pipelines. PaddleOCR emits one row per version/variant/backend after automatically selecting the fastest stable combination from batch sizes 1/2/4/8/16 and independently-created inference-channel counts 1/2/4. Each candidate must be internally deterministic and the formal rerun must exactly reproduce the selected candidate. It does not emit isolated det/cls/rec timing rows. When a GPU backend is selected, the wrapper also attempts to lock the selected GPU's graphics and memory clocks to the maximum supported values. The lock stays within the driver-reported range and is reset after the run. A shorter smoke test is:

```powershell
powershell -ExecutionPolicy Bypass -File .\Run-DeviceBenchmark.ps1 -Backend onnxruntime,openvino,opencv-dnn -Kind yolov8n -Mode steady -Warmup 1 -Iterations 3
```

Use `-SkipSpecialVisual` or `-SkipPaddleOcr` only for focused diagnostics. OCR formal sampling is controlled separately with `-OcrWarmup` and `-OcrIterations`; the candidate grids can be changed with `-OcrAutotuneChannels` and `-OcrAutotuneBatches`.

For a controlled A/B comparison, use a known sustained clock for that device:

```powershell
powershell -ExecutionPolicy Bypass -File .\Run-DeviceBenchmark.ps1 -GpuClockMode lock-custom -GpuGraphicsClockMHz 1875 -GpuMemoryClockMHz 7001
```

Use `-GpuClockMode none` to measure normal boost behavior. Clock control may require an elevated PowerShell window and is not supported by every WDDM/laptop driver. A failed lock never aborts the benchmark; its command output and exit code are written to `device-*.json`. The script always attempts `--reset-gpu-clocks` and `--reset-memory-clocks` after a successful lock. If the PowerShell process is forcibly terminated, reset manually with `nvidia-smi --reset-gpu-clocks --id=0` and `nvidia-smi --reset-memory-clocks --id=0`.

Do not run other CPU/GPU-heavy applications during a publishable measurement. Keep the machine connected to AC power and use the same OS power mode between devices.

## Results

Every run creates timestamped files under `results`:

- `visual-*.csv`: tabular model/backend timings.
- `visual-*.json`: timings plus device/runtime snapshots before and after the run.
- `special-visual-*.csv`: complete CLIP, SAM, and BLIP multi-artifact pipeline timings.
- `paddleocr-full-*.csv`: complete OCR pipeline timings and selected batch/channel combinations.
- `device-*.json`: wrapper parameters, GPU state, TensorRT paths and output paths.
- `console-*.log`: full console output and unavailable/unsupported reasons.
- `gpu-*.csv`: 500 ms NVIDIA utilization, clock, power, temperature and throttle-reason samples collected while the benchmark runs.

Return every file from the same timestamp. Do not edit the CSV or JSON before returning it.

## Backend notes

ONNX Runtime CPU, OpenVINO CPU and OpenCV DNN CPU use the DLLs carried in this package. The GPU bundle also contains CUDA 12.9 for TensorRT/CUDA preprocessing, the CUDA 13.2 libraries imported by ONNX Runtime 1.28, cuDNN 9.22 and TensorRT 11.0 runtime DLLs. A GPU target only needs a sufficiently recent compatible NVIDIA display driver; the startup script prepends the package-local runtime directories to `PATH`.

TensorRT engines are device/runtime-specific and therefore are not copied from the build machine. The script uses the bundled `trtexec.exe` and bridge to build `.onnx.engine` files on the target machine before measuring. If the NVIDIA driver or GPU architecture is incompatible, that backend is recorded as `unavailable`; the remaining backends still run.

The included TensorRT bridge targets the API line stated in `models/manifest.json`. The bundled TensorRT builder resources cover SM75, SM80, SM86, SM89, SM90, SM100 and SM120. Older unsupported NVIDIA architectures require a different TensorRT release.
