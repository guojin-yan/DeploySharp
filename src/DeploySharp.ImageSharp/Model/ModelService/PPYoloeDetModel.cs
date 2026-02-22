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
    /// PP-YOLOE object detection model implementation from PaddlePaddle
    /// 来自PaddlePaddle的PP-YOLOE目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// PP-YOLOE is a high-performance object detector from Baidu's PaddlePaddle framework.
    /// It features anchor-free design, efficient backbone networks, and strong performance
    /// on various object detection benchmarks.
    /// PP-YOLOE是百度PaddlePaddle框架的高性能目标检测器。
    /// 它具有无锚点设计、高效骨干网络，在各种目标检测基准测试中表现出色。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点:
    /// - Anchor-free design reducing hyperparameters
    ///   无锚点设计减少超参数
    /// - Efficient backbone and neck architecture
    ///   高效的骨干和颈部架构
    /// - Strong performance on COCO and other datasets
    ///   在COCO和其他数据集上表现强劲
    /// </para>
    /// <para>
    /// Additional input: Scale factors (ratio) for coordinate transformation
    /// 额外输入: 用于坐标转换的缩放因子（比例）
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Config { ModelPath = "ppyoloe.onnx" };
    /// using var model = new PPYoloeDetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// </code>
    /// </example>
    /// <seealso cref="IPPYoloeDetModel"/>
    public class PPYoloeDetModel : IPPYoloeDetModel
    {
        /// <summary>
        /// Initializes a new instance of PPYoloeDetModel
        /// 初始化PPYoloeDetModel的新实例
        /// </summary>
        /// <param name="config">Model configuration / 模型配置</param>
        public PPYoloeDetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses image with scale ratio for PP-YOLOE
        /// 为PP-YOLOE预处理图像，包含缩放比例
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>DataTensor with image and scale data / 包含图像和比例数据的DataTensor</returns>
        /// <remarks>
        /// PP-YOLOE requires scale factors (width_ratio, height_ratio) as additional input
        /// for post-processing coordinate transformations.
        /// PP-YOLOE需要缩放因子（宽比例、高比例）作为额外输入，用于后处理坐标转换。
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

                float[] data = new float[config.InputSizes[1][1]];
                data[1] = (float)imageAdjustmentParam.Ratio.First;
                data[0] = (float)imageAdjustmentParam.Ratio.Second;
                dataTensors.AddNode(
                    config.InputNames[1],
                    0,
                    TensorType.Input,
                    data,
                    config.InputSizes[1],
                    typeof(float));
                return dataTensors;
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images with scale ratios
        /// 预处理批量图像，包含缩放比例
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>DataTensor with batched data / 包含批量数据的DataTensor</returns>
        /// <remarks>
        /// Batch preprocessing with scale factors for each image in the batch.
        /// 批次预处理，包含批次中每张图像的缩放因子。
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
