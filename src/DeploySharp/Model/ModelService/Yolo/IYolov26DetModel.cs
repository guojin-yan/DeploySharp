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
    /// Implementation of YOLOv26 model for object detection
    /// YOLOv26目标检测模型的实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides improved object detection performance over previous YOLO versions.
    /// 相比之前版本的YOLO提供改进的目标检测性能。
    /// </para>
    /// <para>
    /// Key features:
    /// 主要特性:
    /// - Enhanced accuracy-speed tradeoff
    ///   改进的精度-速度平衡
    /// - Optimized architecture for efficient inference
    ///   针对高效推理优化的架构
    /// - Simplified output format (6 values per detection)
    ///   简化的输出格式(每个检测6个值)
    /// </para>
    /// <para>
    /// Output format explanation:
    /// 输出格式说明:
    /// Each detection contains 6 values per row:
    /// 每个检测包含每行6个值:
    /// [x1, y1, x2, y2, confidence, class_id]
    /// </para>
    /// </remarks>
    public abstract class IYolov26DetModel : IYolov10DetModel
    {
        /// <summary>
        /// Initializes a new instance of YOLOv26 detector
        /// 初始化YOLOv26检测器的新实例
        /// </summary>
        /// <param name="config">Model configuration parameters/模型配置参数</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null/当config为null时抛出</exception>
        public IYolov26DetModel(Yolov26DetConfig config) : base(config)
        {
            MyLogger.Log.Info($"Initializing {this.GetType().Name}, Config:\n{config}");
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, 配置:\n{config}");
        }

      
    }

}
