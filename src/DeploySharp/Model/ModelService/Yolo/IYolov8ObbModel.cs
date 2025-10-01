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
    /// Abstract base implementation of YOLOv8 Oriented Bounding Box (OBB) detection model
    /// YOLOv8旋转框目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized for detecting objects with oriented bounding boxes (rotated rectangles).
    /// 专门用于检测带旋转边框(旋转矩形)的目标。
    /// </para>
    /// <para>
    /// Handles angle-aware detection results from YOLOv8 OBB models.
    /// 处理来自YOLOv8旋转框模型的带角度检测结果。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特性:
    /// - Angle-aware bounding box processing
    ///   带角度的边界框处理
    /// - Parallel confidence filtering
    ///   并行置信度过滤
    /// - Rotated rect coordinate adjustment
    ///   旋转矩形坐标调整
    /// </para>
    /// </remarks>
    public abstract class IYolov8ObbModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv8 OBB detector
        /// 初始化YOLOv8旋转框检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov8ObbModel(Yolov8ObbConfig config) : base(config)
        {
            MyLogger.Log.Info($"Initializing {this.GetType().Name}, \nConfiguration:\n{config}");
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n配置参数:\n{config}");
        }

        /// <summary>
        /// Predicts objects in input image and returns oriented bounding box results
        /// 预测输入图像中的目标并返回旋转框结果
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of OBB detection results/旋转框检测结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when input image is null/当输入图像为null时抛出</exception>
        public ObbResult[] Predict(object img)
        {
            return base.Predict(img) as ObbResult[];
        }

        /// <summary>
        /// Post-processes raw model output to extract oriented bounding box results
        /// 对原始模型输出进行后处理以提取旋转框结果
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor/原始模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed OBB results/处理后的旋转框结果数组</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when output tensor format is invalid/当输出张量格式无效时抛出
        /// </exception>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            // Get raw output data and configuration
            // 获取原始输出数据和配置
            float[] result0 = dataTensor[0].DataBuffer as float[];
            var config = (Yolov8ObbConfig)this.config;

            // Get output dimensions [batch_size, num_classes+5, num_predictions]
            // 获取输出维度 [批量大小, 类别数+5, 预测数量]
            int rowResultNum = config.OutputSizes[0][2];  // Number of predictions/预测数量
            int oneResultLen = config.OutputSizes[0][1];  // Length per prediction (classes + 5 values)/每个预测的长度(类别+5个值)

            var candidateBoxes = new ConcurrentBag<BoundingBox>();

            // Parallel processing of detection candidates
            // 并行处理检测候选
            Parallel.For(0, rowResultNum, i =>
            {
                // Process each class probability (skip first 4 channels: cx,cy,w,h)
                // 处理每个类别概率(跳过前4个通道: cx,cy,w,h)
                for (int j = 4; j < oneResultLen - 1; j++)
                {
                    float conf = result0[rowResultNum * j + i];
                    int label = j - 4;  // Convert channel index to class ID/转换通道索引为类别ID

                    // Confidence threshold filtering
                    // 置信度阈值过滤
                    if (conf > config.ConfidenceThreshold)
                    {
                        // Get box coordinates and angle
                        // 获取框坐标和角度
                        float cx = result0[rowResultNum * 0 + i];  // Center x/中心x坐标
                        float cy = result0[rowResultNum * 1 + i];  // Center y/中心y坐标
                        float ow = result0[rowResultNum * 2 + i];  // Width/宽度
                        float oh = result0[rowResultNum * 3 + i];  // Height/高度
                        float angle = result0[rowResultNum * (oneResultLen - 1) + i];  // Rotation angle/旋转角度

                        // Normalize angle to [-π/2, π/2] range
                        // 将角度归一化到[-π/2, π/2]范围
                        if (angle >= Math.PI && angle <= 0.75 * Math.PI)
                        {
                            angle -= (float)Math.PI;
                        }
                        angle *= (float)(180f / Math.PI);  // Convert to degrees/转换为角度制

                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = label,
                            Confidence = conf,
                            Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                            Angle = angle
                        });
                    }
                }
            });

            // Apply Non-Maximum Suppression
            // 应用非极大值抑制
            var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

            // Convert to final results with rotated rectangles
            // 转换为带旋转矩形的最终结果
            var detResult = new ObbResult[boxes.Length];
            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                int classID = box.NameIndex;

                detResult[i] = new ObbResult
                {
                    Id = classID,
                    Bounds = RotatedRect.FromAxisAlignedRect(
                        imageAdjustmentParam.AdjustRectF(box.Box),
                        box.Angle),
                    Confidence = box.Confidence,
                    Category = config.CategoryDict.TryGetValue(classID, out string category)
                               ? category
                               : classID.ToString(),
                };
            }

            return detResult;
        }
    }
}
