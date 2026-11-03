# rf-detr/keypoint-preview

RF-DETR Keypoint Preview，官方 1.11.2，COCO-17 人体关键点。**当前源码/未发布版本新增**；本地模型 ID 不属于现有 ModelFactory 42 条公开下载记录。

先按[导出工具](../../../../eng/models/rf-detr/README.md)生成本地工件，再从仓库根目录运行完整图片推理：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/keypoint/rfdetr.json --backend ort --output ../results/rf-detr/keypoint --validate ../models/rf-detr-1.11.2/keypoint
```

可用 `--image <图片>` 替换默认 `bus.jpg`；替换后省略 `--validate` 或重新导出同图参考。结果为 `PoseEstimationResult`，保存完整 `result.json` 和骨架叠加 `result.jpg`。输入 `[1,3,576,576]`，输出 `dets [1,100,4]`、`labels [1,100,2]`、`keypoints [1,100,34,8]`。背景/人体槽、findability 与 precision-trace 分数融合由内置 Decoder 处理，不对原始张量直接套用通用 DirectPose。

| ORT CPU | ORT CUDA | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
| :---: | :---: | :---: | :---: | :---: |
| ✓ | ✓ | ✓ | ✗ | ✓ |

状态绑定 [精确 ONNX 身份](../../../../eng/models/rf-detr/contracts/keypoint-preview/rfdetr.json)。固定 Batch=1；独立 Session 池异步并发与取消已验证，ROI 复用源图坐标和 Pose 过滤器。完整命令、运行时、下载状态及容差见[共享案例](../../rf-detr/README.md)与[后端矩阵](../../../../docs/model-backend-verification-matrix.md#rf-detr-1112-本地工件)。
