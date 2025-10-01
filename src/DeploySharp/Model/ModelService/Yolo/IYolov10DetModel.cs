using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
namespace DeploySharp.Model
{
    /// <summary>
    /// Implementation of YOLOv10 model for object detection
    /// YOLOv10目标检测模型的实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides improved object detection performance over previous YOLO versions.
    /// 相比之前版本的YOLO提供改进的目标检测性能。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特性:
    /// - Enhanced accuracy-speed tradeoff
    ///   改进的精度-速度平衡
    /// - Optimized architecture for efficient inference
    ///   针对高效推理优化的架构
    /// - Simplified output format (6 values per detection)
    ///   简化的输出格式(每个检测6个值)
    /// </para>
    /// <para>
    /// Output format explanation:
    /// 输出格式说明:
    /// Each detection contains 6 values per row:
    /// 每个检测包含每行6个值:
    /// [x1, y1, x2, y2, confidence, class_id]
    /// </para>
    /// </remarks>
    public abstract class IYolov10DetModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv10 detector
        /// 初始化YOLOv10检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov10DetModel(Yolov10DetConfig config) : base(config)
        {
            MyLogger.Log.Info($"Initializing {this.GetType().Name}, Config:\n{config}");
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, 配置:\n{config}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when input image is null/当输入图像为null时抛出</exception>
        public DetResult[] Predict(object img)
        {
            return base.Predict(img) as DetResult[];
        }

        /// <summary>
        /// Post-processes raw model output to extract detection results
        /// 对原始模型输出进行后处理以提取检测结果
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor/原始模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed detection results/处理后的检测结果数组</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when output tensor format is invalid/当输出张量格式无效时抛出
        /// </exception>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            // Validate and get raw output data
            // 验证并获取原始输出数据
            float[] result0 = dataTensor[0].DataBuffer as float[];
            var config = (Yolov10DetConfig)this.config;

            // Get output dimensions [batch_size, num_detections, 6]
            // 获取输出维度 [批量大小, 检测数量, 6]
            int rowResultNum = config.OutputSizes[0][1];  // Number of detections/检测数量
            int oneResultLen = config.OutputSizes[0][2];  // Should be 6 for YOLOv10/YOLOv10应为6

            List<DetResult> detResults = new List<DetResult>();

            // Process each detection candidate
            // 处理每个检测候选
            for (var i = 0; i < rowResultNum; i++)
            {
                int s = 6 * i;  // Starting index for each detection/每个检测的起始索引

                // Skip low-confidence detections
                // 跳过低置信度检测
                if (result0[s + 4] < config.ConfidenceThreshold)
                {
                    continue;
                }

                // Parse bounding box coordinates (x1,y1,x2,y2 format)
                // 解析边界框坐标(x1,y1,x2,y2格式)
                float cx = result0[s + 0];
                float cy = result0[s + 1];
                float dx = result0[s + 2];
                float dy = result0[s + 3];

                // Convert to width/height format
                // 转换为宽/高格式
                int width = (int)((dx - cx));
                int height = (int)((dy - cy));

                RectF box = new RectF
                {
                    X = cx,
                    Y = cy,
                    Width = width,
                    Height = height
                };

                // Get class information
                // 获取类别信息
                int classID = (int)result0[s + 5];
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);

                // Create detection result with adjusted coordinates
                // 创建带有调整坐标的检测结果
                detResults.Add(new DetResult
                {
                    Id = classID,                               // Class ID/类别ID
                    Bounds = imageAdjustmentParam.AdjustRect(box), // Adjusted rectangle/调整后的矩形
                    Confidence = result0[s + 4],                // Detection confidence/检测置信度
                    Category = categoryFlag ? category : classID.ToString() // Fallback to ID if category not found/如果类别不存在则回退到ID
                });
            }

            return detResults.ToArray();
        }
    }

}
