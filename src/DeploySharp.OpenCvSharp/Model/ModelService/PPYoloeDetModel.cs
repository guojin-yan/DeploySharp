using DeploySharp.Data;
using DeploySharp.Log;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// PP-YOLOE object detection model implementation from PaddlePaddle.
    /// PaddlePaddle PP-YOLOE目标检测模型实现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// PP-YOLOE is an anchor-free object detection model developed by PaddlePaddle,
    /// designed for high performance and easy deployment in industrial applications.
    /// PP-YOLOE是PaddlePaddle开发的无锚点目标检测模型，专为高性能和工业应用中的轻松部署而设计。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特点：
    /// - Anchor-free detection architecture
    ///   无锚点检测架构
    /// - Lightweight and efficient backbone
    ///   轻量级高效骨干网络
    /// - Suitable for edge deployment
    ///   适用于边缘部署
    /// - Strong performance on various object sizes
    ///   对各种尺寸目标的强大性能
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create PP-YOLOE detector
    /// // 创建PP-YOLOE检测器
    /// var config = new PPYoloeDetConfig("ppyoloe.onnx")
    /// {
    ///     ConfidenceThreshold = 0.5f,
    ///     NmsThreshold = 0.45f,
    ///     InputSize = new Size(640, 640)
    /// };
    /// 
    /// using (var detector = new PPYoloeDetModel(config))
    /// {
    ///     using (Mat image = Cv2.ImRead("scene.jpg"))
    ///     {
    ///         var results = detector.Predict(image);
    ///         
    ///         foreach (var det in results)
    ///         {
    ///             Console.WriteLine($"{det.Category}: {det.Confidence:P}");
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="PPYoloeDetConfig"/>
    public class PPYoloeDetModel : IPPYoloeDetModel
    {
        /// <summary>
        /// Creates a new PP-YOLOE detection model instance.
        /// 创建新的PP-YOLOE检测模型实例。
        /// </summary>
        /// <param name="config">Model configuration parameters / 模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null / 当config为null时抛出</exception>
        /// <remarks>
        /// The configuration specifies model path, input size, confidence threshold, and other parameters.
        /// 配置指定模型路径、输入尺寸、置信度阈值和其他参数。
        /// </remarks>
        public PPYoloeDetModel(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// Performs object detection on a single image.
        /// 对单张图像执行目标检测。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <returns>Array of detection results with bounding boxes, classes, and confidence scores / 包含边界框、类别和置信度分数的检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when img is null / 当img为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when img is empty / 当img为空时抛出</exception>
        /// <remarks>
        /// Returns empty array if no objects are detected above the confidence threshold.
        /// 如果没有检测到高于置信度阈值的目标，则返回空数组。
        /// </remarks>
        /// <example>
        /// <code>
        /// using (Mat image = Cv2.ImRead("photo.jpg"))
        /// {
        ///     var detections = detector.Predict(image);
        ///     
        ///     foreach (var det in detections)
        ///     {
        ///         Cv2.Rectangle(image, CvDataExtensions.ToRect(det.Bounds), Scalar.Red, 2);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="PredictBatch"/>
        public DetResult[] Predict(Mat img)
        {
            return base.Predict(img) as DetResult[];
        }

        /// <summary>
        /// Performs batch object detection on multiple images.
        /// 对多张图像执行批量目标检测。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <returns>List of detection results for each image / 每张图像的检测结果列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        /// <remarks>
        /// Batch processing is more efficient than sequential single-image processing.
        /// 批处理比顺序单张图像处理更高效。
        /// </remarks>
        /// <example>
        /// <code>
        /// var images = new List&lt;Mat&gt; { image1, image2, image3 };
        /// var allResults = detector.PredictBatch(images);
        /// 
        /// for (int i = 0; i &lt; allResults.Count; i++)
        /// {
        ///     Console.WriteLine($"Image {i}: {allResults[i].Length} objects detected");
        /// }
        /// </code>
        /// </example>
        public List<DetResult[]> PredictBatch(List<Mat> imgs)
        {
            return base.PredictBatch(imgs.Cast<object>().ToList())
                .Cast<DetResult[]>()
                .ToList();
        }

        /// <summary>
        /// Preprocesses image for PP-YOLOE detection inference.
        /// 对图像进行预处理以进行PP-YOLOE检测推理。
        /// </summary>
        /// <param name="img">Input image (OpenCvSharp Mat) / 输入图像(OpenCvSharp Mat)</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for coordinate mapping / 用于坐标映射的输出调整参数</param>
        /// <returns>Preprocessed tensor data ready for model input / 准备好用于模型输入的预处理张量数据</returns>
        /// <exception cref="InvalidCastException">Thrown when img is not Mat / 当img不是Mat时抛出</exception>
        /// <remarks>
        /// <para>
        /// Preprocessing steps:
        /// 预处理步骤：
        /// 1. Resize to model input size
        ///    调整到模型输入尺寸
        /// 2. Normalize pixel values
        ///    归一化像素值
        /// 3. Convert to tensor format
        ///    转换为张量格式
        /// 4. Additional preprocessing for PP-YOLOE specific inputs including scale factors
        ///    PP-YOLOE特定输入的额外预处理，包括缩放因子
        /// </para>
        /// </remarks>
        protected override DataTensor Preprocess(object img, out ImageAdjustmentParam imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入尺寸: {(img as Mat)?.Size()}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageProcessToDataTensor(
                    (Mat)img,
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
        /// Preprocesses batch of images for PP-YOLOE detection inference.
        /// 对批量图像进行预处理以进行PP-YOLOE检测推理。
        /// </summary>
        /// <param name="imgs">List of input images / 输入图像列表</param>
        /// <param name="imageAdjustmentParam">Output adjustment parameters for each image / 每张图像的输出调整参数</param>
        /// <returns>Preprocessed batch tensor data / 预处理后的批量张量数据</returns>
        /// <exception cref="ArgumentNullException">Thrown when imgs is null / 当imgs为null时抛出</exception>
        protected override DataTensor PreprocessBatch(List<object> imgs, out ImageAdjustmentParam[] imageAdjustmentParam)
        {
            MyLogger.Log.Debug($"开始{config.ModelType.ToString()}预处理流程，输入Batch Size: {imgs.Count}");

            try
            {
                DataTensor dataTensors = CvDataProcessor.ImageListProcessToDataTensor(
                    imgs.OfType<OpenCvSharp.Mat>().ToList(),
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
