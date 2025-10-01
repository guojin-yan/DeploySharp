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
    /// Abstract base implementation of YOLOv11 Pose Estimation model
    /// YOLOv11姿态估计模型的抽象基类实现
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
    public abstract class IYolov11PoseModel : IYolov8PoseModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv11 Pose detector
        /// 初始化YOLOv11姿态检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov11PoseModel(Yolov11PoseConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
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

    }
}
