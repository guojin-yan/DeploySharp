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
    /// Abstract base implementation of YOLOv26 Pose Estimation model
    /// YOLOv26姿态估计模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized for detecting human poses with keypoints in addition to bounding boxes.
    /// 专门用于检测带有关键点的人类姿态。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特性:
    /// - Jointly predicts bounding boxes and human keypoints
    ///   联合预测边界框和人体关键点
    /// - Processes 17/21 standard human pose keypoints (configurable)
    ///   处理17/21个标准人体姿态关键点(可配置)
    /// - Simultaneous confidence filtering for boxes and keypoints
    ///   同时对框和关键点进行置信度过滤
    /// </para>
    /// <para>
    /// Output format explanation:
    /// 输出格式说明:
    /// Each prediction contains:
    /// 每个预测包含:
    /// [cx,cy,w,h,conf, kpx1,kpy1,kpconf1, ..., kpxN,kpyN,kpconfN]
    /// </para>
    /// </remarks>
    public abstract class IYolov26PoseModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv26 Pose detector
        /// 初始化YOLOv26姿态检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov26PoseModel(Yolov26PoseConfig config) : base(config)
        {
            MyLogger.Log.Info($"Initializing {this.GetType().Name}, Config:\n{config}");
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, 配置:\n{config}");
        }

        /// <summary>
        /// Predicts human poses in input image and returns keypoint results
        /// 预测输入图像中的人体姿态并返回关键点结果
        /// </summary>
        /// <param name="img">Input image in OpenCV Mat format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of pose estimation results/姿态估计结果数组</returns>
        /// <exception cref="ArgumentNullException">Thrown when input image is null/当输入图像为null时抛出</exception>
        public KeyPointResult[] Predict(object img)
        {
            return base.Predict(img) as KeyPointResult[];
        }

        /// <summary>
        /// Post-processes raw model output to extract pose estimation results
        /// 对原始模型输出进行后处理以提取姿态估计结果
        /// </summary>
        /// <param name="dataTensor">Raw model output tensor/原始模型输出张量</param>
        /// <param name="imageAdjustmentParam">Image transformation parameters/图像变换参数</param>
        /// <returns>Array of processed pose results/处理后的姿态结果数组</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when output tensor format is invalid/当输出张量格式无效时抛出
        /// </exception>
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            // Validate and get raw output data
            // 验证并获取原始输出数据
            float[] result0 = dataTensor[0].DataBuffer as float[];
            var config = (Yolov26PoseConfig)this.config;

            // Get output dimensions [batch_size, num_keypoints*3+5, num_detections]
            // 获取输出维度 [批量大小, 关键点数*3+5, 检测数量]
            int rowResultNum = config.OutputSizes[0][1];  // Number of detections/检测数量
            int oneResultLen = config.OutputSizes[0][2];  // Length per detection/每个检测的长度

            var candidateBoxes = new ConcurrentBag<BoundingBox>();
            var keyPointResults = new List< KeyPointResult>();
            // Process each detection candidate
            // 处理每个检测候选
            for (var i = 0; i < rowResultNum; i++)
            {
                int s = oneResultLen * i;  // Starting index for each detection/每个检测的起始索引

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

                // Calculate the number of keypoints: (output_length - 5(box+conf)) / 3(x,y,conf)
                // 计算关键点数量: (输出长度 - 5(框+置信度)) / 3(x,y,置信度)
                int pointNum = (oneResultLen - 5) / 3;
           

   
                KeyPoint[] keyPoints = new KeyPoint[pointNum];

                // Extract and adjust each keypoint
                // 提取并调整每个关键点
                for (int k = 0; k < pointNum; ++k)
                {
                    keyPoints[k] = new KeyPoint
                    {
                        Point = imageAdjustmentParam.AdjustPoint(
                            new Point(
                                result0[s + (5 + 3 * k + 1)],  // Keypoint X
                                result0[s + (5 + 3 * k + 2)]   // Keypoint Y
                            )),
                        Confidence = result0[s +(5 + 3 * k + 3)]  // Keypoint confidence
                    };
                }

                // Get category name (should always be "person" for pose estimation)
                // 获取类别名称(姿态估计应始终为"person")
                bool categoryFlag = config.CategoryDict.TryGetValue((int)result0[s + 5], out string category);

                keyPointResults.Add(new KeyPointResult
                {
                    Id = i,
                    Bounds = imageAdjustmentParam.AdjustRect(box),
                    Confidence = result0[s + 4],
                    Category = categoryFlag ? category : ((int)result0[s + 5]).ToString(),
                    KeyPoints = keyPoints
                });
            }

            return keyPointResults.ToArray();
        }

        protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (Yolov26DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][2];
            int oneResultLen = config.OutputSizes[0][1];
            int batchSize = config.InferBatch;
            int resultSizePerBatch = rowResultNum * oneResultLen;
            Result[][] results = new Result[batchSize][];

            for (var b = 0; b < batchSize; b++)
            {
                var candidateBoxes = new ConcurrentBag<BoundingBox>();

                // Parallel processing of detection candidates
                // 并行处理检测候选
                Parallel.For(0, rowResultNum, i =>
                {
                    // Only process person class (class index 0)
                    // 只处理人类类别(类别索引0)
                    for (int j = 4; j < 5; j++)
                    {
                        float conf = result0[rowResultNum * j + i + resultSizePerBatch * b];
                        if (conf > config.ConfidenceThreshold)
                        {
                            // Parse bounding box coordinates (cx,cy,w,h format)
                            // 解析边界框坐标(cx,cy,w,h格式)
                            float cx = result0[rowResultNum * 0 + i + resultSizePerBatch * b];
                            float cy = result0[rowResultNum * 1 + i + resultSizePerBatch * b];
                            float ow = result0[rowResultNum * 2 + i + resultSizePerBatch * b];
                            float oh = result0[rowResultNum * 3 + i + resultSizePerBatch * b];

                            candidateBoxes.Add(new BoundingBox
                            {
                                Index = i,
                                NameIndex = 0,  // Only person class/仅人类类别
                                Confidence = conf,
                                Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                                Angle = 0.0f
                            });
                        }
                    }
                });

                // Apply Non-Maximum Suppression to remove duplicate detections
                // 应用非极大值抑制去除重复检测
                var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

                // Calculate the number of keypoints: (output_length - 5(box+conf)) / 3(x,y,conf)
                // 计算关键点数量: (输出长度 - 5(框+置信度)) / 3(x,y,置信度)
                int pointNum = (oneResultLen - 5) / 3;
                var keyPointResults = new KeyPointResult[boxes.Length];

                // Process each valid detection after NMS
                // 处理NMS后的每个有效检测
                for (var i = 0; i < boxes.Length; i++)
                {
                    var box = boxes[i];
                    KeyPoint[] keyPoints = new KeyPoint[pointNum];

                    // Extract and adjust each keypoint
                    // 提取并调整每个关键点
                    for (int k = 0; k < pointNum; ++k)
                    {
                        keyPoints[k] = new KeyPoint
                        {
                            Point = imageAdjustmentParams[b].AdjustPoint(
                                new Point(
                                    result0[rowResultNum * (5 + 3 * k + 0) + box.Index + resultSizePerBatch * b],  // Keypoint X
                                    result0[rowResultNum * (5 + 3 * k + 1) + box.Index + resultSizePerBatch * b]   // Keypoint Y
                                )),
                            Confidence = result0[rowResultNum * (5 + 3 * k + 2) + box.Index + resultSizePerBatch * b]  // Keypoint confidence
                        };
                    }

                    // Get category name (should always be "person" for pose estimation)
                    // 获取类别名称(姿态估计应始终为"person")
                    bool categoryFlag = config.CategoryDict.TryGetValue(box.NameIndex, out string category);

                    keyPointResults[i] = new KeyPointResult
                    {
                        Id = box.NameIndex,
                        Bounds = imageAdjustmentParams[b].AdjustRect(box.Box),
                        Confidence = box.Confidence,
                        Category = categoryFlag ? category : box.NameIndex.ToString(),
                        KeyPoints = keyPoints
                    };
                }

                results[b] = keyPointResults;

            }
            return new List<Result[]>(results);
        }
    }

}
