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
    /// Abstract base implementation of YOLOv11 Oriented Bounding Box (OBB) detection model
    /// YOLOv11旋转框目标检测模型的抽象基类实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Specialized for detecting objects with oriented bounding boxes (rotated rectangles).
    /// 专门用于检测带旋转边框(旋转矩形)的目标。
    /// </para>
    /// <para>
    /// Handles angle-aware detection results from YOLOv8 OBB models.
    /// 处理来自YOLOv11旋转框模型的带角度检测结果。
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
    public abstract class IYolov11ObbModel : IYolov8ObbModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv11 OBB detector
        /// 初始化YOLOv11旋转框检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov11ObbModel(Yolov11ObbConfig config) : base(config)
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

    }
}
