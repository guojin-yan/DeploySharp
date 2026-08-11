# DeploySharp YOLO multitask clean consumer

This package-only sample loads the four Stage 17 task contracts from published DeploySharp packages: classification, instance segmentation, Pose, and oriented bounding boxes. It does not reference the DeploySharp solution or package model files.

Set `DEPLOYSHARP_YOLO_CLS_MODEL`, `DEPLOYSHARP_YOLO_SEG_MODEL`, `DEPLOYSHARP_YOLO_POSE_MODEL`, `DEPLOYSHARP_YOLO_OBB_MODEL`, and `DEPLOYSHARP_YOLO_IMAGE` to audited external files, then run:

```powershell
dotnet restore --configfile .\NuGet.Config --locked-mode
dotnet run -c Release --no-build
```

The process prints `DEPLOYSHARP_VISUAL_YOLO_MULTITASK_CONSUMER_OK` only after all four package-only pipelines complete. Model files remain user-owned and are never bundled in the package.
