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
    /// Abstract base implementation of YOLOv8 model for object detection
    /// YOLOv8目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv8 detection pipeline including:
    /// 提供标准YOLOv8检测流程，包括：
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
    /// Inherits from base IModel interface and implements YOLOv8-specific processing
    /// 继承自基础IModel接口并实现YOLOv8特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov8DetModel : IModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv8 detector
        /// 初始化YOLOv8检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov8DetModel(Yolov8DetConfig config) : base(config)
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

            var config = (Yolov8DetConfig)this.config;
            int rowResultNum = config.OutputSizes[0][2];
            int oneResultLen = config.OutputSizes[0][1];

            var candidateBoxes = new ConcurrentBag<BoundingBox>();

            // 4. 并行处理候选框检测
            Parallel.For(0, rowResultNum, i =>
            {
                for (int j = 4; j < oneResultLen; j++)  // Iterate through each class
                {
                    float conf = result0[rowResultNum * j + i];
                    int label = j - 4;
                    if (conf > config.ConfidenceThreshold)  // Confidence threshold filtering
                    {
                        // Parse center coordinates, width and height
                        float cx = result0[rowResultNum * 0 + i];
                        float cy = result0[rowResultNum * 1 + i];
                        float ow = result0[rowResultNum * 2 + i];
                        float oh = result0[rowResultNum * 3 + i];

                        candidateBoxes.Add(new BoundingBox
                        {
                            Index = i,
                            NameIndex = label,
                            Confidence = conf,
                            Box = new RectF(cx - 0.5f * ow, cy - 0.5f * oh, ow, oh),
                            Angle = 0.0f
                        });
                    }
                }
            });

            // 5. NMS处理
            var boxes = config.NonMaxSuppression.Run(candidateBoxes.ToList(), config.NmsThreshold);

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
