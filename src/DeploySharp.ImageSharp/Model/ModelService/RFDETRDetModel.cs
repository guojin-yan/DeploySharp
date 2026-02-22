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
    /// RF-DETR (Recurrent Feature DETR) object detection model implementation
    /// RF-DETR (循环特征DETR) 目标检测模型实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// RF-DETR is a variant of the DETR (Detection Transformer) architecture that incorporates
    /// recurrent feature extraction for improved detection of objects at various scales.
    /// RF-DETR是DETR（检测Transformer）架构的变体，结合了循环特征提取以改进各种尺度目标的检测。
    /// </para>
    /// <para>
    /// Model characteristics:
    /// 模型特点:
    /// - Transformer-based architecture without NMS
    ///   基于Transformer的架构，无需NMS
    /// - Recurrent feature refinement
    ///   循环特征细化
    /// - End-to-end training and inference
    ///   端到端训练和推理
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var config = new Config { ModelPath = "rfdetr.onnx" };
    /// using var model = new RFDETRDetModel(config);
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// var results = model.Predict(image);
    /// </code>
    /// </example>
    /// <seealso cref="IRFDETRDetModel"/>
    public class RFDETRDetModel : IRFDETRDetModel
    {
        /// <summary>
        /// Initializes a new instance of RFDETRDetModel
        /// 初始化RFDETRDetModel的新实例
        /// </summary>
        /// <param name="config">Model configuration / 模型配置</param>
        public RFDETRDetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Preprocesses image for RF-DETR inference
        /// 为RF-DETR推理预处理图像
        /// </summary>
        /// <param name="img">Input image / 输入图像</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters / 输出调整参数</param>
        /// <returns>Preprocessed DataTensor / 预处理的DataTensor</returns>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Image<Rgb24>)?.Size()}");

            try
            {
                return CvDataProcessor.ImageProcessToDataTensor(
                    (Image<Rgb24>)img,
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Preprocesses batch of images for RF-DETR inference
        /// 为RF-DETR推理预处理批量图像
        /// </summary>
        /// <param name="img">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters array / 输出调整参数数组</param>
        /// <returns>Preprocessed DataTensor for batch / 批量预处理的DataTensor</returns>
        protected override DataTensor PreprocessBatch(List<object> img, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {img.Count}");

            try
            {
                return CvDataProcessor.ImageListProcessToDataTensor(
                    img.OfType<Image<Rgb24>>().ToList(),
                    config,
                    out imageAdjustmentParam);
            }
            catch (Exception ex)
            {
                MyLogger.Log.Error($"预处理过程中发生异常: {ex.Message}", ex);
                throw;
            }
        }
    }
}
