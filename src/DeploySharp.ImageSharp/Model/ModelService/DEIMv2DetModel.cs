using DeploySharp.Data;
using DeploySharp.Log;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// DEIM (Detection with Improved Matching) v2 object detection model implementation
    /// DEIM (改进匹配的检测) v2 目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// DEIMv2 is an advanced object detection architecture that improves the matching process
    /// between predictions and ground truth, leading to better detection accuracy especially
    /// for small objects and crowded scenes.
    /// DEIMv2是一种先进的目标检测架构，改进了预测与真实值之间的匹配过程，
    /// 特别对小目标和拥挤场景有更好的检测精度。
    /// </para>
    /// <para>
    /// Model characteristics:
    /// 模型特点:
    /// - Improved bipartite matching for label assignment
    ///   改进的二分匹配用于标签分配
    /// - Enhanced feature pyramid network
    ///   增强的特征金字塔网络
    /// - Better handling of scale variations
    ///   更好地处理尺度变化
    /// </para>
    /// <para>
    /// Input format: RGB image resized to model input dimensions
    /// Additional input: Original image dimensions for coordinate scaling
    /// 输入格式: 调整到模型输入尺寸的RGB图像
    /// 额外输入: 用于坐标缩放的原始图像尺寸
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Config { ModelPath = "deimv2.onnx" };
    /// using var model = new DEIMv2DetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// </code>
    /// </example>
    /// <seealso cref="IDEIMv2DetModel"/>
    /// <seealso cref="CvDataProcessor.ImageProcessToDataTensor"/>
    public class DEIMv2DetModel : IDEIMv2DetModel
    {
        /// <summary>
        /// Initializes a new instance of DEIMv2DetModel with specified configuration
        /// 使用指定配置初始化DEIMv2DetModel的新实例
        /// </summary>
        /// <param name="config">Model configuration / 模型配置</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
        public DEIMv2DetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses a single image for inference with original dimensions
        /// 为推理预处理单张图像，包含原始尺寸信息
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>DataTensor with image data and original dimensions / 包含图像数据和原始尺寸的DataTensor</returns>
        /// <remarks>
        /// In addition to standard preprocessing, this method adds the original image
        /// dimensions as a second input node for coordinate scaling in post-processing.
        /// 除了标准预处理外，此方法还将原始图像尺寸作为第二个输入节点添加，用于后处理中的坐标缩放。
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Image<Rgb24>)?.Size()}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageProcessToDataTensor(
                    (Image<Rgb24>)img,
                    config,
                    out imageAdjustmentParam);

                long[] data = new long[config.InputSizes[1][1]];
                data[0] = (long)imageAdjustmentParam.RowImgSize.Width;
                data[1] = (long)imageAdjustmentParam.RowImgSize.Height;
                dataTensors.AddNode(
                    config.InputNames[1],
                    0,
                    TensorType.Input,
                    data,
                    config.InputSizes[1],
                    typeof(long));
                return dataTensors;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses a batch of images for inference
        /// 为推理预处理一批图像
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters array / 输出调整参数数组</param>
        /// <returns>DataTensor with batched data and dimensions / 包含批量数据和尺寸的DataTensor</returns>
        /// <remarks>
        /// Currently hardcoded for batch size 2. Each image's original dimensions are
        /// included in the second input node.
        /// 当前硬编码为批次大小2。每张图像的原始尺寸都包含在第二个输入节点中。
        /// </remarks>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<Image<Rgb24>>().ToList(),
                    config,
                    out imageAdjustmentParam);

                long[] data = new long[config.InputSizes[1][0] * config.InputSizes[1][1]];
                for (int b = 0; b < 2; ++b)
                {

                    data[b * config.InputSizes[1][1]] = (long)imageAdjustmentParam[b].RowImgSize.Width;
                    data[b * config.InputSizes[1][1] + 1] = (long)imageAdjustmentParam[b].RowImgSize.Height;
                }

                dataTensors.AddNode(
                    config.InputNames[1],
                    0,
                    TensorType.Input,
                    data,
                    config.InputSizes[1],
                    typeof(long));

                return dataTensors;

            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

    }
}
