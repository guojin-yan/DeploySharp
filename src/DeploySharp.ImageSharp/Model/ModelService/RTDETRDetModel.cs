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
    /// RT-DETR (Real-Time Detection Transformer) object detection model implementation
    /// RT-DETR (实时检测Transformer) 目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// RT-DETR is a real-time variant of DETR that achieves competitive performance
    /// with faster inference speed, making it suitable for real-time applications.
    /// RT-DETR是DETR的实时变体，实现了有竞争力的性能和更快的推理速度，适合实时应用。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点:
    /// - Real-time performance with transformer architecture
    ///   Transformer架构的实时性能
    /// - Efficient hybrid encoder design
    ///   高效的混合编码器设计
    /// - Query selection for faster convergence
    ///   查询选择以加快收敛
    /// </para>
    /// <para>
    /// Additional input: Original image dimensions for coordinate scaling
    /// 额外输入: 用于坐标缩放的原始图像尺寸
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Config { ModelPath = "rtdetr.onnx" };
    /// using var model = new RTDETRDetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// </code>
    /// </example>
    /// <seealso cref="IRTDETRDetModel"/>
    public class RTDETRDetModel : IRTDETRDetModel
    {
        /// <summary>
        /// Initializes a new instance of RTDETRDetModel
        /// 初始化RTDETRDetModel的新实例
        /// </summary>
        /// <param name="config">Model configuration / 模型配置</param>
        public RTDETRDetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses image with original dimensions for RT-DETR
        /// 为RT-DETR预处理图像，包含原始尺寸
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>DataTensor with image and dimension data / 包含图像和尺寸数据的DataTensor</returns>
        /// <remarks>
        /// RT-DETR requires original image dimensions for proper coordinate transformation.
        /// RT-DETR需要原始图像尺寸进行正确的坐标转换。
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
        /// Preprocesses batch of images with dimensions
        /// 预处理批量图像，包含尺寸信息
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>DataTensor with batched data / 包含批量数据的DataTensor</returns>
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
