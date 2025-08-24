using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    /// <summary>
    /// 机器学习模型配置类，包含模型路径、输入输出参数、推理设置等
    /// 实现IConfig接口保证配置可序列化
    /// </summary>
    public class YoloConfig : IConfig
    {
        // 推理参数
        /// <summary>
        /// 置信度阈值（0-1范围，默认0.5）
        /// 低于此值的预测结果将被过滤
        /// </summary>
        public float ConfidenceThreshold { get; set; } = 0.5f;

        /// <summary>
        /// 非极大值抑制(NMS)阈值（0-1范围）
        /// 用于消除重叠检测框
        /// </summary>
        public float NmsThreshold { get; set; } = 0.5f;

        /// <summary>
        /// 类别名称列表（与模型输出类别索引对应）
        /// 示例：new List<string>{ "person", "car", "dog" }
        /// </summary>
        public List<string> CategoryNames { get; set; }

        public ResizeMode ImgResizeMode = ResizeMode.Pad;


        /// <summary>
        /// 生成配置摘要（仅显示已赋值的属性）
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            AppendIfSet(sb, "Confidence Threshold", ConfidenceThreshold, 0.5f);
            AppendIfSet(sb, "NMS Threshold", NmsThreshold);
            AppendIfSet(sb, "Categories", CategoryNames != null ? string.Join(", ", CategoryNames.Take(3)) + "..." : null);
            return base.ToString() + sb.ToString();

        }
    }
}
