using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeploySharp.Data;
using System.Collections.Concurrent;
using System.Numerics;
using System.Diagnostics;
using System.Configuration;
using DeploySharp.Log;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv5 model for object detection
    /// YOLOv5目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv5 detection pipeline including:
    /// 提供标准YOLOv5检测流程，包括：
    /// - Input preprocessing
    ///   输入预处理
    /// - Output decoding
    ///   输出解码
    /// - Confidence filtering
    ///   置信度过滤
    /// - Non-Maximum Suppression
    ///   非极大值抑制
    /// </para>
    /// <para>
    /// Inherits from base IModel interface and implements YOLOv5-specific processing
    /// 继承自基础IModel接口并实现YOLOv5特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov5DetModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv5 detector
        /// 初始化YOLOv5检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov5DetModel(Yolov5DetConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
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
        protected override Result[] Postprocess(DataTensor dataTensor, ImageAdjustmentParam imageAdjustmentParam)
        {
            float[] result0 = dataTensor[0].DataBuffer as float[];

            var config = (Yolov5DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][1];
            int oneResultLen = config.OutputSizes[0][2];

            var candidateBoxes = new ConcurrentBag<BoundingBox>();

            // Parallel candidate box processing
            // 并行候选框处理
            Parallel.For(0, rowResultNum, i =>
            {
                // Filter by object confidence (P0)
                // 通过物体置信度(P0)过滤
                float conf = result0[oneResultLen * i + 4];
                if (conf <= 0.25f) return;

                // Check class probabilities (P5-Pn)
                // 检查类别概率(P5-Pn)
                for (int j = 5; j < oneResultLen; j++)
                {
                    float conf1 = result0[oneResultLen * i + j];
                    if (conf1 > config.ConfidenceThreshold)
                    {
                        // Decode box coordinates (cx,cy,w,h)
                        // 解码框坐标(cx,cy,w,h)
                        float cx = result0[oneResultLen * i];
                        float cy = result0[oneResultLen * i + 1];
                        float ow = result0[oneResultLen * i + 2];
                        float oh = result0[oneResultLen * i + 3];

                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = j - 5,
                            Confidence = conf1,
                            Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                            Angle = 0.0f
                        });
                    }
                }
            });

            // Apply Non-Maximum Suppression
            // 应用非极大值抑制
            var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

            // Package final detection results
            // 封装最终检测结果
            var detResult = new DetResult[boxes.Length];
            for (var i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                int classID = box.NameIndex;
                bool categoryFlag = config.CategoryDict.TryGetValue(classID, out string category);
                detResult[i] = new DetResult
                {
                    Id = classID,
                    Bounds = imageAdjustmentParam.AdjustRect(box.Box),
                    Confidence = box.Confidence,
                    Category = categoryFlag ? category : classID.ToString(),
                };
            }
            return detResult;
        }
    }


}
