# Visual workflow / Visual 工作流

VisualProfileInspection composes profile creation, exact named input/output contracts, task registration, decoder ownership, and registry freezing. It is a complete profile lifecycle case rather than a single constructor call.

```powershell
dotnet run --project samples/02-visual/VisualProfileInspection.csproj -c Release
```

The real image workflows are under samples/06-models/release-inference and the task-specific clean consumers under tests/clean-consumer. The [ROI workflow](roi-workflow/README.md) combines normalized geometry, JSON snapshot replacement, Detection filtering, video events, and bounded lazy-frame execution in one runnable case.

The [official SAM video adapter](roi-official-sam/README.md) / [官方 SAM 视频适配案例](roi-official-sam/README_cn.md) connects the ROI planner to Meta's Python predictor, decodes a bounded real video, exports source-space masks, and checks correction/reset consistency. It is not a native ONNX/TensorRT SAM backend.
