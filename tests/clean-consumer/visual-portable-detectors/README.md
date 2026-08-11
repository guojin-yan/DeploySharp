# DeploySharp portable-detector clean consumer

This package-only sample executes the Stage 18 RF-DETR detection and instance-segmentation contracts from packed DeploySharp packages. It has no project reference and does not include model or image files. / 此仅包消费者使用打包后的 DeploySharp 包执行阶段 18 的 RF-DETR 检测与实例分割合同。它不包含项目引用，也不携带模型或图片文件。

Set `DEPLOYSHARP_DETR_RF_DET_MODEL`, `DEPLOYSHARP_DETR_RF_SEG_MODEL`, and `DEPLOYSHARP_DETR_IMAGE` to audited external files, then run: / 将这些环境变量设置为已审核的外部文件后运行：

```powershell
dotnet restore --configfile .\NuGet.Config --locked-mode
dotnet run -c Release --no-restore
```

The process prints `DEPLOYSHARP_VISUAL_DETR_CONSUMER_OK` only after both real ONNX Runtime CPU pipelines return the expected domain result types. / 只有两个真实 ONNX Runtime CPU 管线都返回预期领域结果类型后，进程才会输出成功标记。
