# RT-DETR table-cell TensorRT dynamic Batch evidence (2026-10-11)

## Result

On the Windows RTX 3060 Laptop host with TensorRT `11.0.0.114-cu12`, CUDA `12.9` and cuDNN `9.22`, DeploySharp's `TensorRtOnnxEngineBuilder` rebuilt both official RT-DETR-L table-cell ONNX artifacts with a dynamic Batch=2 profile. The opt-in integration test passed `1/1` test case and covered both models (`2/2` model rows).

| artifact | input/profile | Engine | ORT/TensorRT rows | minimum IoU across 600 comparisons |
|---|---|---:|---:|---:|
| `paddle-table/rt-detr-l-wired-cell-det` | `image=[2,3,640,640]`; `im_shape=[2,2]`; `scale_factor=[2,2]`; min=1,opt=max=2 | 157,543,060 bytes | 2 × 300 / 2 × 300 | 0.9998514 |
| `paddle-table/rt-detr-l-wireless-cell-det` | same | 157,922,260 bytes | 2 × 300 / 2 × 300 | 0.9998784 |

Each model used the top and bottom non-overlapping regions of `E:\Data\image\bus.jpg`. The test bound all three inputs, ran ORT CPU as the reference, ran the freshly built TensorRT engines, decoded both rows with the same Paddle NMS decoder, and compared every returned candidate by IoU and score. Batch order and row ownership were preserved; each model's four row-level comparisons used 300 matched candidates and no score assertion failed.

The exact ONNX/Engine SHA-256 values, per-row counts and IoU minima are in the [machine-readable report](paddle-document-table-cell-dynamic-batch-tensorrt-20261011.json). Reproduce with:

```powershell
$env:DEPLOYSHARP_PADDLE_DOCUMENT_TENSORRT_RUN_EXTERNAL = '1'
dotnet test tests/DeploySharp.Visual.TensorRT.Tests/DeploySharp.Visual.TensorRT.Tests.csproj `
  --no-restore `
  --filter 'FullyQualifiedName~TableCellRunsDynamicBatchTwoOnTensorRtAndMatchesOrtRows'
```

This is an exact graph/engine/decoder and row-isolation contract on one Windows GPU/runtime. It is not a cell recall, table-structure accuracy, P50/P95 throughput benchmark, cross-device portability claim, or evidence that the original SLANeXt TensorRT path is supported. OpenCV DNN dynamic Batch remains explicitly unsupported for these two artifacts (`DS-OCV-8004`).
