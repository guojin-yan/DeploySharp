# Official SAM video ROI

This complete application connects `VisualRoiVideoPromptRunner` to Meta's official
SAM2 or SAM3 video predictor. It decodes a short real video, initializes an ROI,
propagates masks, corrects the prompt halfway through, emits ROI events from tracked
mask centroids, and checks reset/reinitialize consistency. PNG masks and a JSON report
remain in the output directory.

The adapter is application-owned. It does not add Python/PyTorch to any DeploySharp
NuGet dependency or claim an ONNX Runtime/OpenVINO/TensorRT SAM video implementation.

## Install the official predictors

Use an isolated Python 3.12 environment. Install the PyTorch build appropriate to
your device, then install the official repositories at the revisions used for the run:

```bash
python3.12 -m venv .venv
source .venv/bin/activate
pip install torch==2.10.0 torchvision==0.25.0 --index-url https://download.pytorch.org/whl/cu128
pip install opencv-python hydra-core iopath einops psutil pycocotools decord scipy
git clone https://github.com/facebookresearch/sam2.git
git -C sam2 checkout 2b90b9f5ceec907a1c18123530e92e794ad901a4
SAM2_BUILD_CUDA=0 pip install -e ./sam2
git clone https://github.com/facebookresearch/sam3.git
git -C sam3 checkout 660a5e9e1b8b4c02c0ad97229b88a09a6e4ff5b7
pip install -e ./sam3
```

For a CPU smoke run use the official PyTorch CPU index instead. The CUDA extension
disabled above is SAM2's optional hole-filling extension; this must be recorded when
comparing results. SAM3 follows Meta's [video object segmentation recipe](https://github.com/facebookresearch/sam3/blob/660a5e9e1b8b4c02c0ad97229b88a09a6e4ff5b7/examples/sam3_for_sam2_video_task_example.ipynb):
the official tracker shares the official detector backbone. This sample uses ROI
points/boxes, not SAM3 text-driven detection.

SAM2 expects the official **sam2.1_hiera_tiny.pt**, paired with
`configs/sam2.1/sam2.1_hiera_t.yaml`. SAM3 expects **sam3.pt**, not SAM3.1 multiplex
weights and not an ONNX export. Checkpoints remain caller-owned.

## Run

```bash
dotnet run --project samples/02-visual/roi-official-sam/OfficialSamRoi.csproj -c Release -- \
  /absolute/path/.venv/bin/python sam2 /models/sam2.1_hiera_tiny.pt \
  /video/bedroom.mp4 /results/sam2-run-1 \
  2b90b9f5ceec907a1c18123530e92e794ad901a4 6 0.25,0.2,0.5,0.7
```

Replace `sam2`, the checkpoint and revision for SAM3. The last argument is a
normalized `x,y,width,height` ROI. The output directory must not already contain a
`frames` directory, preventing stale frames from entering another test. Each run is
limited to 3–100 frames; it is a smoke test, not a long-duration benchmark.

The JSON records the official source revision, checkpoint/video SHA-256, device,
PyTorch/CUDA versions, precision, per-frame predictor and mask-export time, mask
SHA-256, foreground area, centroid, GPU allocated memory and reset consistency.
No result is fabricated if model loading, decoding or inference fails.

Cancellation or an external predictor error terminates the worker: its mutated
native state cannot be assumed rolled back. Create a new adapter and planner to
retry. A successful `ResetAsync` clears object and propagation state and can reuse
the loaded model. The report's predictor time excludes process/model startup;
source-mask download, PNG output and JSON export are separately timed.

[中文说明](README_cn.md) · [ROI guide](../../../docs/articles/visual-roi-configuration.md)

## Verified scope (2026-09-16)

SAM2 tiny passed a six-frame official `bedroom.mp4` test on Ubuntu 22.04 / i7-1165G7 with Python 3.12.14 and PyTorch 2.10.0+cpu: initialization, propagation, correction, source masks and identical first-frame mask hashes after reset. See the [device evidence](../../../docs/articles/roi-backend-performance-matrix.md). This is not a GPU throughput or native SAM Bundle claim.

The pinned SAM3 version fails CPU construction because its position-encoding cache explicitly allocates CUDA tensors. Use matching CUDA PyTorch and adequate VRAM; this adapter's SAM3 path is not yet verified end to end. CPU wheel availability must not be interpreted as upstream SAM3 CPU support.

CUDA follow-up: Python 3.12.14 / PyTorch 2.10.0+cu128 correctly detected the RTX2060. SAM3 failed its first inference with CUDA OOM on this 6GB device. A reduced-weight-precision experiment also exhausted VRAM and is not retained as the default. Use a device with more available VRAM and revalidate; this configuration is not an end-to-end SAM3 pass.
