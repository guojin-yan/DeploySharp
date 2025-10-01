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
    /// Abstract base implementation of YOLOv11 model for object detection
    /// YOLOv11目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides standard YOLOv11 detection pipeline including:
    /// 提供标准YOLOv11检测流程，包括：
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
    /// 继承自基础IYolov8DetModel接口并实现YOLOv11特定处理
    /// </para>
    /// </remarks>
    public abstract class IYolov11DetModel : IYolov8DetModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv11 detector
        /// 初始化YOLOv11检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        public IYolov11DetModel(Yolov11DetConfig config) : base(config)
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

    }
}
