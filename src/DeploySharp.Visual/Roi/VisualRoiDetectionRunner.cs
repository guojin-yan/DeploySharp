using System;
using JYPPX.DeploySharp.Results.Vision;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Runs the FilterResults ROI mode for axis-aligned detections. / 执行轴对齐检测的 FilterResults ROI 模式。</summary>
    public sealed class VisualRoiDetectionRunner
    {
        /// <summary>Filters one already decoded source-space result against an immutable snapshot. / 针对不可变快照过滤一个已解码的源图结果。</summary>
        public RoiDetectionResult Run(DetectionResult detections, VisualRoiSnapshot snapshot, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return VisualRoiDetectionFilter.Filter(detections, snapshot, VisualTaskId.ObjectDetection, thresholdOverride, coordinateContext);
        }

        /// <summary>Filters one result using an explicit task identity. / 使用明确任务标识过滤一个结果。</summary>
        public RoiDetectionResult Run(DetectionResult detections, VisualRoiSnapshot snapshot, VisualTaskId task, float? thresholdOverride = null, VisualRoiCoordinateContext? coordinateContext = null)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return VisualRoiDetectionFilter.Filter(detections, snapshot, task, thresholdOverride, coordinateContext);
        }
    }
}
