# 巡检示例输入

六张 JPEG 用于复现第 07 篇的固定视角区域巡检流程，不是作者采集的园区监控，也不是人工标注准确率数据集。

| 文件 | 公开来源 | 抽帧与处理 |
| --- | --- | --- |
| `road-00/04/08.jpg` | Roboflow Supervision 的 `vehicles.mp4` 示例视频 | 视频 0、4、8 秒，3840×2160 等比缩至 1920×1080，JPEG |
| `courtyard-00/04/08.jpg` | OpenCV 示例 `vtest.avi` | 视频 0、4、8 秒，保留 768×576，JPEG |

```text
https://media.roboflow.com/supervision/video-examples/vehicles.mp4
https://github.com/opencv/opencv/blob/4.x/samples/data/vtest.avi
```

图片是上述第三方素材的教程抽帧，原始素材的权利归其原权利人；本项目代码的 Apache 2.0 声明不为第三方素材授予额外许可。生产使用或再分发前请按来源确认适用条件。

换自己的输入时，修改 `inspection.json` 中的 `Image`、`CameraId`、`SourceSeconds`，并针对新相机视角重画归一化多边形。`SourceSeconds` 是来源片段内的位置，不是拍摄日期或现场告警时间。
