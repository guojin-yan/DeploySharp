using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv26 Oriented Bounding Box (OBB) detection model
    /// YOLOv26旋转框目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized for detecting objects with oriented bounding boxes (rotated rectangles).
    /// 专门用于检测带旋转边框(旋转矩形)的目标。
    /// </para>
    /// <para>
    /// Handles angle-aware detection results from YOLOv8 OBB models.
    /// 处理来自YOLOv26旋转框模型的带角度检测结果。
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
    public abstract class IYolov26ObbModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv26 OBB detector
        /// 初始化YOLOv26旋转框检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov26ObbModel(Yolov26ObbConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
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
            var config = (Yolov26ObbConfig)this.config;

            // Get output dimensions [batch_size, num_classes+5, num_predictions]
            // 获取输出维度 [批量大小, 类别数+5, 预测数量]
            int rowResultNum = config.OutputSizes[0][1];  // Number of predictions/预测数量
            int oneResultLen = config.OutputSizes[0][2];  // Length per prediction (classes + 5 values)/每个预测的长度(类别+5个值)

            var candidateBoxes = new ConcurrentBag<BoundingBox>();

            List <ObbResult> detResult = new List<ObbResult>();
            for (int i = 0; i < rowResultNum; ++i)
            {
                if (result0[i * oneResultLen + 4] > config.ConfidenceThreshold)
                {
                    // Get box coordinates and angle
                    // 获取框坐标和角度
                    float cx = result0[oneResultLen * i + 0];  // Center x/中心x坐标
                    float cy = result0[oneResultLen * i + 1];  // Center y/中心y坐标
                    float ow = result0[oneResultLen * i + 2];  // Width/宽度
                    float oh = result0[oneResultLen * i + 3];  // Height/高度
                    float angle = result0[oneResultLen * i + 6];  // Rotation angle/旋转角度

                    // Normalize angle to [-π/2, π/2] range
                    // 将角度归一化到[-π/2, π/2]范围
                    if (angle >= Math.PI && angle <= 0.75 * Math.PI)
                    {
                        angle -= (float)Math.PI;
                    }
                    angle *= (float)(180f / Math.PI);  // Convert to degrees/转换为角度制

                    detResult.Add( new ObbResult
                    {
                        Id = i,
                        Bounds = RotatedRect.FromAxisAlignedRect(
                            imageAdjustmentParam.AdjustRectF(new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh)),
                            angle),
                        Confidence = result0[oneResultLen * i + 4],
                        Category = config.CategoryDict.TryGetValue((int)result0[oneResultLen * i + 5], out string category)
                                   ? category
                                   : result0[oneResultLen * i + 5].ToString(),
                    });
                }
            }



          

            return detResult.ToArray();
        }
        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (Yolov8DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][1];
            int oneResultLen = config.OutputSizes[0][2];
            int batchSize = config.InferBatch;
            int resultSizePerBatch = rowResultNum * oneResultLen;
            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                List<ObbResult> detResult = new List<ObbResult>();
                for (int i = 0; i < rowResultNum; ++i)
                {
                    if (result0[i * oneResultLen + 4 + resultSizePerBatch * b] > config.ConfidenceThreshold)
                    {
                        // Get box coordinates and angle
                        // 获取框坐标和角度
                        float cx = result0[oneResultLen * i + 0 + resultSizePerBatch * b];  // Center x/中心x坐标
                        float cy = result0[oneResultLen * i + 1 + resultSizePerBatch * b];  // Center y/中心y坐标
                        float ow = result0[oneResultLen * i + 2 + resultSizePerBatch * b];  // Width/宽度
                        float oh = result0[oneResultLen * i + 3 + resultSizePerBatch * b];  // Height/高度
                        float angle = result0[oneResultLen * i + 6 + resultSizePerBatch * b];  // Rotation angle/旋转角度

                        // Normalize angle to [-π/2, π/2] range
                        // 将角度归一化到[-π/2, π/2]范围
                        if (angle >= Math.PI && angle <= 0.75 * Math.PI)
                        {
                            angle -= (float)Math.PI;
                        }
                        angle *= (float)(180f / Math.PI);  // Convert to degrees/转换为角度制

                        detResult.Add(new ObbResult
                        {
                            Id = i,
                            Bounds = RotatedRect.FromAxisAlignedRect(
                                imageAdjustmentParams[b].AdjustRectF(new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh)),
                                angle),
                            Confidence = result0[oneResultLen * i + 4 + resultSizePerBatch * b],
                            Category = config.CategoryDict.TryGetValue((int)result0[oneResultLen * i + 5 + resultSizePerBatch * b], out string category)
                                       ? category
                                       : result0[oneResultLen * i + 5 + resultSizePerBatch * b].ToString(),
                        });
                    }
                }

                results[b] = detResult.ToArray();

            }
            return new List<Result[]>(results);
        }

    }
}
