using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    /// <summary>
    /// Abstract base implementation of YOLOv7 model for object detection
    /// YOLOv7目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv7 detection pipeline including:
    /// 提供标准YOLOv7检测流程，包括：
    /// - Output decoding
    ///   输出解码
    /// - Confidence filtering
    ///   置信度过滤
    /// - Coordinate adjustment
    ///   坐标调整
    /// </para>
    /// <para>
    /// Inherits from base IModel interface and implements YOLOv7-specific processing
    /// 继承自基础IModel接口并实现YOLOv7特定处理
    /// </para>
    /// <para>
    /// Note: YOLOv7 uses different output format compared to YOLOv5:
    /// 注意：相比YOLOv5，YOLOv7使用不同的输出格式：
    /// Each detection result contains 7 values per row:
    /// 每个检测结果包含每行7个值：
    /// [batch_id, x1, y1, x2, y2, class_id, confidence]
    /// </para>
    /// </remarks>
    public abstract class IYolov7DetModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv7 detector
        /// 初始化YOLOv7检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when config is null/当config为null时抛出
        /// </exception>
        public IYolov7DetModel(Yolov7DetConfig config) : base(config)
        {
            MyLogger.Log.Info($"Initializing {this.GetType().Name}, \n {config.ToString()}");
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input image is null/当输入图像为null时抛出
        /// </exception>
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
            var config = (Yolov7DetConfig)this.config;

            // Get output dimensions
            // 获取输出维度
            int rowResultNum = config.OutputSizes[0][0];  // Number of detections/检测数量
            int oneResultLen = config.OutputSizes[0][1];  // Should be 7 for YOLOv7/YOLOv7应为7

            List<DetResult> detResults = new List<DetResult>();

            // Process each detection candidate
            // 处理每个检测候选
            for (var i = 0; i < rowResultNum; i++)
            {
                int s = 7 * i;  // Starting index for each detection/每个检测的起始索引

                // Skip low-confidence detections
                // 跳过低置信度检测
                if (result0[s + 6] < config.ConfidenceThreshold)
                {
                    continue;
                }

                // Parse bounding box coordinates (x1,y1,x2,y2 format)
                // 解析边界框坐标(x1,y1,x2,y2格式)
                float cx = result0[s + 1];
                float cy = result0[s + 2];
                float dx = result0[s + 3];
                float dy = result0[s + 4];

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

                // Create detection result with adjusted coordinates
                // 创建带有调整坐标的检测结果
                detResults.Add(new DetResult
                {
                    Id = (int)result0[s + 5],                    // Class ID/类别ID
                    Bounds = imageAdjustmentParam.AdjustRect(box), // Adjusted rectangle/调整后的矩形
                    Confidence = result0[s + 6],                  // Detection confidence/检测置信度
                    Category = config.CategoryDict.TryGetValue((int)result0[s + 5], out string category)
                                ? category
                                : result0[s + 5].ToString()       // Fallback to ID if category not found/如果类别不存在则回退到ID
                });
            }

            return detResults.ToArray();
        }
    }
}

