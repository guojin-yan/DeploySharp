using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Represents one axis-aligned detection. / 表示一个轴对齐检测结果。
    /// </summary>
    public sealed class Detection
    {
        /// <summary>Initializes a detection. / 初始化检测结果。</summary>
        public Detection(RectangleF box, LabelScore label)
        {
            Box = box;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>Gets the bounding box in source-image coordinates. / 获取源图像坐标中的边界框。</summary>
        public RectangleF Box { get; }

        /// <summary>Gets the class and score. / 获取类别和分数。</summary>
        public LabelScore Label { get; }
    }

    /// <summary>
    /// Contains axis-aligned detections for one input. / 包含一个输入的轴对齐检测结果。
    /// </summary>
    public sealed class DetectionResult
    {
        private readonly IReadOnlyList<Detection> _detections;

        /// <summary>Initializes a detection result. / 初始化检测结果集合。</summary>
        public DetectionResult(IEnumerable<Detection> detections)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            var values = new List<Detection>();
            foreach (Detection detection in detections)
            {
                if (detection == null)
                {
                    throw new ArgumentException("Detections cannot contain null values.", nameof(detections));
                }

                values.Add(detection);
            }

            _detections = values.AsReadOnly();
        }

        /// <summary>Gets detections in decoder-defined order. / 按解码器定义的顺序获取检测结果。</summary>
        public IReadOnlyList<Detection> Detections => _detections;
    }
}
