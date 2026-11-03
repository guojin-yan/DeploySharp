# rf-detr/segment/nano

RF-DETR Seg Nano，官方 1.11.2，实例分割。**当前源码新增案例与浮点输入工厂，尚未发布包或模型 Release**；与旧 `rf-detr/segment` 不是同一份模型。

先按[导出工具](../../../../eng/models/rf-detr/README.md)生成本地工件，再从仓库根目录运行：

```powershell
dotnet run --project samples/06-models/rf-detr/RfDetrInference.csproj -c Release -p:DeploySharpPaddleOcrCuda12=true -- --contract ../models/rf-detr-1.11.2/segment-nano/rfdetr.json --backend ort --output ../results/rf-detr/segment-nano --validate ../models/rf-detr-1.11.2/segment-nano --concurrency 2
```

可用 `--image <图片>` 替换默认 `bus.jpg`；替换后省略 `--validate` 或重新导出同图参考。内置 `CreateRFDETRSeg` 返回源图大小的 `InstanceSegmentationResult`。案例保存 `result.json`、叠加 `result.jpg`、每实例 PNG 和 0/1 字节掩码。输入 `[1,3,312,312]`，输出 `dets [1,100,4]`、`labels [1,100,91]`、`masks [1,100,78,78]`；91 列包含稀疏 COCO ID，不是连续 COCO-80，也不额外删除末尾列。

| ORT CPU | ORT CUDA | OpenVINO CPU | OpenCV DNN CPU | TensorRT CUDA |
| :---: | :---: | :---: | :---: | :---: |
| ✓ | ✓ | ✓ | ✗ | ✓ |

状态绑定 [精确 ONNX 身份](../../../../eng/models/rf-detr/contracts/segment-nano/rfdetr.json)，不能推广给其它分割尺寸。固定 Batch=1；两个独立 Session、三输入有序异步与取消已实测。ROI 框/掩码回映射有合同测试。完整配置、后端环境和官方参考校验见[共享案例](../../rf-detr/README.md)与[后端矩阵](../../../../docs/model-backend-verification-matrix.md#rf-detr-1112-本地工件)。
