using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv9 model for object Segmentation
    /// YOLOv9分割模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv9 Segmentation pipeline including:
    /// 提供标准YOLOv9检测流程，包括：
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
    /// Inherits from base IModel interface and implements YOLOv9-specific processing
    /// 继承自基础IModel接口并实现YOLOv9特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov9SegModel : IYolov8SegModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv9 detector
        /// 初始化YOLOv9检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov9SegModel(Yolov9SegConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }

        /// <summary>
        /// Predicts objects in input image and returns detection results
        /// 预测输入图像中的目标并返回检测结果
        /// </summary>
        /// <param name="img">Input image in ImageSharp format/OpenCV Mat格式的输入图像</param>
        /// <returns>Array of detection results/检测结果数组</returns>
        public SegResult[] Predict(object img)
        {
            return base.Predict(img) as SegResult[];
        }
    }
}