using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Associates one detection with a source-resolution or model-resolution binary mask. / 将一个检测结果与源分辨率或模型分辨率二值掩码关联。
    /// </summary>
    public sealed class SegmentationInstance
    {
        /// <summary>Initializes a segmentation instance. / 初始化分割实例。</summary>
        public SegmentationInstance(Detection detection, Tensor<byte> mask)
        {
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            Mask = mask ?? throw new ArgumentNullException(nameof(mask));
            if (mask.Shape.Rank != 2)
            {
                throw new ArgumentException("An instance mask must have shape [height,width].", nameof(mask));
            }
        }

        /// <summary>Gets the associated detection. / 获取关联的检测结果。</summary>
        public Detection Detection { get; }

        /// <summary>Gets the binary mask. / 获取二值掩码。</summary>
        public Tensor<byte> Mask { get; }
    }

    /// <summary>
    /// Contains instance segmentation results for one input. / 包含一个输入的实例分割结果。
    /// </summary>
    public sealed class SegmentationResult
    {
        private readonly IReadOnlyList<SegmentationInstance> _instances;

        /// <summary>Initializes a segmentation result. / 初始化分割结果。</summary>
        public SegmentationResult(IEnumerable<SegmentationInstance> instances)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var values = new List<SegmentationInstance>();
            foreach (SegmentationInstance instance in instances)
            {
                if (instance == null) throw new ArgumentException("Instances cannot contain null values.", nameof(instances));
                values.Add(instance);
            }

            _instances = values.AsReadOnly();
        }

        /// <summary>Gets segmentation instances. / 获取分割实例。</summary>
        public IReadOnlyList<SegmentationInstance> Instances => _instances;
    }
}
