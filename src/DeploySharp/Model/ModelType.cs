using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    //public enum ModelType
    //{
    //    // Yolo系列
    //    YOLOv5Det,
    //    YOLOv5Seg,
    //    YOLOv6Det,
    //    YOLOv7Det,
    //    YOLOv8Det,
    //    YOLOv8Seg,
    //    YOLOv8Obb,
    //    YOLOv8Pose,
    //    YOLOv9Det,
    //    YOLOv9Seg,
    //    YOLOv10Det,
    //    YOLOv11Det,
    //    YOLOv11Seg,
    //    YOLOv11Obb,
    //    YOLOv11Pose,
    //    YOLOv12Det,
    //    YOLOv13Det,

    //    // 其他检测模型
    //    AnomalibSeg,
    //};

    /// <summary>
    /// Represents different types of computer vision models supported by the framework
    /// 表示框架支持的不同类型计算机视觉模型
    /// </summary>
    /// <remarks>
    /// <para>
    /// Organized into YOLO family models (v5-v13 with various capabilities) and special-purpose models.
    /// YOLO models are grouped by version and ordered chronologically.
    /// </para>
    /// <para>
    /// 分为YOLO系列模型(v5-v13，具备多种功能)和专用模型。
    /// YOLO模型按版本分组并按时间顺序排列。
    /// </para>
    /// </remarks>
    public enum ModelType
    {
        #region YOLO Series Models
        // YOLOv5 Family
        /// <summary>
        /// YOLOv5 Object Detection model
        /// YOLOv5目标检测模型
        /// </summary>
        YOLOv5Det,

        /// <summary>
        /// YOLOv5 Instance Segmentation model
        /// YOLOv5实例分割模型
        /// </summary>
        YOLOv5Seg,

        // YOLOv6 Family
        /// <summary>
        /// YOLOv6 Object Detection model
        /// YOLOv6目标检测模型
        /// </summary>
        YOLOv6Det,

        // YOLOv7 Family
        /// <summary>
        /// YOLOv7 Object Detection model
        /// YOLOv7目标检测模型
        /// </summary>
        YOLOv7Det,

        // YOLOv8 Family
        /// <summary>
        /// YOLOv8 Object Detection model
        /// YOLOv8目标检测模型
        /// </summary>
        YOLOv8Det,

        /// <summary>
        /// YOLOv8 Instance Segmentation model
        /// YOLOv8实例分割模型
        /// </summary>
        YOLOv8Seg,

        /// <summary>
        /// YOLOv8 Oriented Bounding Box model
        /// YOLOv8旋转框检测模型
        /// </summary>
        YOLOv8Obb,

        /// <summary>
        /// YOLOv8 Human Pose Estimation model
        /// YOLOv8人体姿态估计模型
        /// </summary>
        YOLOv8Pose,

        // YOLOv9 Family
        /// <summary>
        /// YOLOv9 Object Detection model (newest version)
        /// YOLOv9目标检测模型(最新版本)
        /// </summary>
        YOLOv9Det,

        /// <summary>
        /// YOLOv9 Instance Segmentation model
        /// YOLOv9实例分割模型
        /// </summary>
        YOLOv9Seg,

        // YOLOv10 Family
        /// <summary>
        /// YOLOv10 Object Detection model
        /// YOLOv10目标检测模型
        /// </summary>
        YOLOv10Det,

        // YOLOv11 Family
        /// <summary>
        /// YOLOv11 Object Detection model
        /// YOLOv11目标检测模型
        /// </summary>
        YOLOv11Det,

        /// <summary>
        /// YOLOv11 Instance Segmentation model
        /// YOLOv11实例分割模型
        /// </summary>
        YOLOv11Seg,

        /// <summary>
        /// YOLOv11 Oriented Bounding Box model
        /// YOLOv11旋转框检测模型
        /// </summary>
        YOLOv11Obb,

        /// <summary>
        /// YOLOv11 Human Pose Estimation model
        /// YOLOv11人体姿态估计模型
        /// </summary>
        YOLOv11Pose,
        // YOLOv12 Family
        /// <summary>
        /// YOLOv12 Object Detection model
        /// YOLOv12目标检测模型
        /// </summary>
        YOLOv12Det,
        // YOLOv13 Family
        /// <summary>
        /// YOLOv13 Object Detection model
        /// YOLOv13目标检测模型
        /// </summary>
        YOLOv13Det,
        #endregion

        #region Special-Purpose Models
        /// <summary>
        /// Anomalib Segmentation model for industrial anomaly detection
        /// 用于工业异常检测的Anomalib分割模型
        /// </summary>
        /// <remarks>
        /// Specifically designed for defect detection in manufacturing
        /// 专为制造业缺陷检测设计
        /// </remarks>
        AnomalibSeg,
        #endregion
    }
}
