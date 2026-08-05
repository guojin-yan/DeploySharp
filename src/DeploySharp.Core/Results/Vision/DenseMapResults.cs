using System;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Contains an anomaly score and optional anomaly mask. / 包含异常分数和可选异常图。
    /// </summary>
    public sealed class AnomalyResult
    {
        /// <summary>Initializes an anomaly result. / 初始化异常结果。</summary>
        public AnomalyResult(float score, Tensor<float>? anomalyMap = null)
        {
            if (float.IsNaN(score) || float.IsInfinity(score)) throw new ArgumentOutOfRangeException(nameof(score));
            Score = score;
            AnomalyMap = anomalyMap;
        }

        /// <summary>Gets the image-level anomaly score. / 获取图像级异常分数。</summary>
        public float Score { get; }

        /// <summary>Gets the optional dense anomaly map. / 获取可选的密集异常图。</summary>
        public Tensor<float>? AnomalyMap { get; }
    }

    /// <summary>
    /// Contains a dense single-channel depth map. / 包含密集单通道深度图。
    /// </summary>
    public sealed class DepthResult
    {
        /// <summary>Initializes a depth result. / 初始化深度结果。</summary>
        public DepthResult(Tensor<float> depthMap)
        {
            DepthMap = depthMap ?? throw new ArgumentNullException(nameof(depthMap));
            if (depthMap.Shape.Rank != 2)
            {
                throw new ArgumentException("A depth map must have shape [height,width].", nameof(depthMap));
            }
        }

        /// <summary>Gets the dense depth map. / 获取密集深度图。</summary>
        public Tensor<float> DepthMap { get; }
    }
}
