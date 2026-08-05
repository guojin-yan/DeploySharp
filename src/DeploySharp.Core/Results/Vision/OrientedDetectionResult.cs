using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Represents one rotated detection. / 表示一个旋转检测结果。
    /// </summary>
    public sealed class OrientedDetection
    {
        /// <summary>Initializes a rotated detection. / 初始化旋转检测结果。</summary>
        public OrientedDetection(RotatedRectangleF box, LabelScore label)
        {
            Box = box;
            Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>Gets the rotated bounding box. / 获取旋转边界框。</summary>
        public RotatedRectangleF Box { get; }

        /// <summary>Gets the class and score. / 获取类别和分数。</summary>
        public LabelScore Label { get; }
    }

    /// <summary>
    /// Contains rotated detections for one input. / 包含一个输入的旋转检测结果。
    /// </summary>
    public sealed class OrientedDetectionResult
    {
        private readonly IReadOnlyList<OrientedDetection> _detections;

        /// <summary>Initializes a rotated detection result. / 初始化旋转检测结果集合。</summary>
        public OrientedDetectionResult(IEnumerable<OrientedDetection> detections)
        {
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            var values = new List<OrientedDetection>();
            foreach (OrientedDetection detection in detections)
            {
                if (detection == null)
                {
                    throw new ArgumentException("Detections cannot contain null values.", nameof(detections));
                }

                values.Add(detection);
            }

            _detections = values.AsReadOnly();
        }

        /// <summary>Gets rotated detections. / 获取旋转检测结果。</summary>
        public IReadOnlyList<OrientedDetection> Detections => _detections;
    }
}
