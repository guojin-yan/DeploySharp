using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// Abstract base implementation of YOLOv11 model for object Segmentation
    /// YOLOv11分割模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv11 Segmentation pipeline including:
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
    /// Inherits from base IModel interface and implements YOLOv11-specific processing
    /// 继承自基础IModel接口并实现YOLOv11特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov11SegModel : IYolov8SegModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv11 detector
        /// 初始化YOLOv11检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov11SegModel(Yolov11SegConfig config) : base(config)
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