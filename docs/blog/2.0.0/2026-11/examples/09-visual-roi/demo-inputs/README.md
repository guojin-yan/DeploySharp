# 演示图片来源

本目录沿用系列“智能巡检台”案例中的真实视频截图，没有合成目标或修改场景中的人员、车辆。

| 文件 | 来源 | 尺寸 |
| --- | --- | --- |
| `road-00.jpg`、`road-04.jpg` | Roboflow Supervision 的 vehicles 视频 | 1920×1080 |
| `courtyard-00.jpg`、`courtyard-04.jpg` | OpenCV samples 的 vtest 视频 | 768×576 |

原始视频：

- https://media.roboflow.com/supervision/video-examples/vehicles.mp4
- https://github.com/opencv/opencv/blob/4.x/samples/data/vtest.avi

这些帧用于区域配置、过滤与坐标还原演示，没有逐框真值标签。素材保留上游来源和适用条款；本库的 Apache 2.0 代码许可不替代第三方视频、模型的原始条款。
