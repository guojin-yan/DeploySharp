# PP-Chart2Table OpenCV DNN isolated boundary (2026-10-13)

This record re-runs the four Chart2Table ONNX components in isolated `dotnet test` processes on the current source revision. It classifies the exact OpenCV DNN adapter boundary; it is not an end-to-end generation, quality, or performance report.

## Environment

| Item | Value |
| --- | --- |
| Source revision | `ee51d3a8af363d7607a0b1bcb686f69ea640e048` |
| OS | Windows `10.0.26200` |
| .NET SDK / target | `10.0.401` / `net10.0` |
| OpenCV managed API | `JYPPX.OpenCV.CSharp.API 5.0.0` |
| Native runtime | OpenCV `5.0.0` |
| Test | `PaddleChart2TableOpenCvIsolatedIntegrationTests.SelectedChart2TableGraphLoadsOnOpenCvDnn` |

## Results

| Component | SHA-256 | Result | Observed boundary |
| --- | --- | --- | --- |
| Vision projector | `dea9eb175e46bb77b0c93dae08730530cd075500e6d514b15e0bc0cdc6b9a88d` | pass | Session created for `[1,3,1024,1024] -> [1,256,1024]`. |
| Token embedding | `b83c0ce88deedd2a83d2c89e733fdbd84074d7c4ff47de797434870012389443` | pass | Session created for dynamic rank-2 token IDs. |
| Text prefill | `8ac0f38fe924511fe7ed28c7339309c94c58299b81c48bab7043d518f4d76a75` | unsupported | Rank-3 `inputs_embeds [1,286,1024]` is rejected by the auxiliary-input contract before inference. |
| Dynamic-past decode | `043b7f618872b1ba46135928ab05128e5510ee60b7b8803719af0b24cfb2015a` | unsupported | Rank-3 embeddings and rank-4 dynamic KV inputs are rejected by the auxiliary-input contract before inference. |

Both blocked components throw the same fail-closed exception:

```text
System.ArgumentException: OpenCV DNN auxiliary inputs must be scalar, one- or two-dimensional tensors with positive or dynamic dimensions. (Parameter 'inputs')
```

## Conclusion

The exact four-graph Bundle is **unsupported for full autoregressive generation on the current OpenCV DNN adapter/runtime**. Two components loading successfully is not partial generation support. The matrix therefore uses `unsupported`, not `unverified` or `pass`.

This is an adapter capability boundary: the current C# `Mat` bridge and `OpenCvDnnModelContract` do not expose the N-D auxiliary tensor path required by prefill and dynamic KV decode. It does not invalidate the ONNX Bundle on ONNX Runtime, OpenVINO, or TensorRT, which have separate full-generation evidence.

Machine-readable details, graph sizes, exact paths and hashes are in [`chart2table-opencv-isolated-20261013.json`](chart2table-opencv-isolated-20261013.json).
