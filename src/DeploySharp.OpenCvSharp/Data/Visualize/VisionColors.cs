
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// 计算机视觉颜色提供器（支持80类别ID的自动配色）
    /// * 边界框高对比度颜色
    /// * 语义分割掩膜色
    /// * 实例分割半透明填充色
    /// </summary>
    public class VisionColors
    {
        //------------------------- 基础配色方案 -------------------------
        private readonly Scalar[] _cocoPalette = GenerateCocoPalette();
        private readonly Scalar[] _ade20kPalette = GenerateAde20kPalette();

        //------------------------- 公共API -------------------------

        /// <summary>
        /// 获取边界框颜色（COCO标准高对比色）
        /// </summary>
        /// <param name="classId">类别ID (0-80)</param>
        /// <param name="alpha">透明度(0-255)，默认不透明</param>
        public Scalar GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, 80);
            Scalar color = _cocoPalette[classId];

            // 构造带透明度的新颜色（OpenCV中Scalar不包含alpha，需要单独处理）
            return new Scalar(color[0], color[1], color[2], alpha);
        }

        /// <summary>
        /// 获取语义分割掩膜颜色（ADE20K标准色）
        /// </summary>
        public Scalar GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
            return _ade20kPalette[classId];
        }

        /// <summary>
        /// 获取实例分割填充色（半透明版边界框颜色）
        /// </summary>
        public Scalar GetInstanceColor(int instanceId, byte alpha = 128)
        {
            return GetBoundingBoxColor(instanceId % 80, alpha);
        }

        //------------------------- 配色生成器 -------------------------

        /// <summary>
        /// 生成COCO数据集80类别标准配色
        /// </summary>
        private static Scalar[] GenerateCocoPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // 红-黄
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // 绿-青
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // 蓝
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // 紫-粉
            // 扩展颜色（确保80个不重复的高对比色）
            "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#33FFF5",
            "#F5FF33", "#FF1493", "#00FF00", "#FF4500", "#9400D3",
            "#4B0082", "#008080", "#800000", "#000080", "#8A2BE2",
            "#7CFC00", "#FFD700", "#4169E1", "#32CD32", "#BA55D3",
            "#FF00FF", "#FF8C00", "#9932CC", "#00FA9A", "#FF6347",
            "#9370DB", "#2E8B57", "#DA70D6", "#D2691E", "#B22222",
            "#20B2AA", "#6495ED", "#778899", "#FF69B4", "#CD5C5C",
            "#4682B4", "#9ACD32", "#8FBC8F", "#483D8B", "#E9967A",
            "#8B4513", "#5F9EA0", "#556B2F", "#6A5ACD", "#98FB98",
            "#DB7093", "#BC8F8F", "#8470FF", "#B8860B", "#C71585",
            "#708090", "#00BFFF", "#66CDAA", "#0000CD", "#FA8072",
            "#191970", "#7B68EE", "#48D1CC", "#DDA0DD", "#87CEFA"
        };

            var colors = new Scalar[80];
            for (int i = 0; i < 80; i++)
            {
                colors[i] = HexToScalar(hexColors[i % hexColors.Length]);
            }
            return colors;
        }

        /// <summary>
        /// 生成ADE20K语义分割标准配色
        /// </summary>
        private static Scalar[] GenerateAde20kPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // 红-黄
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // 绿-青
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // 蓝
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // 紫-粉
            // 扩展颜色（确保80个不重复的高对比色）
            "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#33FFF5",
            "#F5FF33", "#FF1493", "#00FF00", "#FF4500", "#9400D3",
            "#4B0082", "#008080", "#800000", "#000080", "#8A2BE2",
            "#7CFC00", "#FFD700", "#4169E1", "#32CD32", "#BA55D3",
            "#FF00FF", "#FF8C00", "#9932CC", "#00FA9A", "#FF6347",
            "#9370DB", "#2E8B57", "#DA70D6", "#D2691E", "#B22222",
            "#20B2AA", "#6495ED", "#778899", "#FF69B4", "#CD5C5C",
            "#4682B4", "#9ACD32", "#8FBC8F", "#483D8B", "#E9967A",
            "#8B4513", "#5F9EA0", "#556B2F", "#6A5ACD", "#98FB98",
            "#DB7093", "#BC8F8F", "#8470FF", "#B8860B", "#C71585",
            "#708090", "#00BFFF", "#66CDAA", "#0000CD", "#FA8072",
            "#191970", "#7B68EE", "#48D1CC", "#DDA0DD", "#87CEFA"
        };

            var colors = new Scalar[hexColors.Length];
            for (int i = 0; i < hexColors.Length; i++)
            {
                colors[i] = HexToScalar(hexColors[i]);
            }
            return colors;
        }

        //------------------------- 辅助方法 -------------------------

        /// <summary>
        /// 将十六进制颜色字符串转换为OpenCV Scalar
        /// </summary>
        private static Scalar HexToScalar(string hexColor)
        {
            // 去除可能的#前缀
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            // 解析RGB分量
            byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            // OpenCV默认为BGR顺序
            return new Scalar(b, g, r);
        }

        //------------------------- 安全边界处理 -------------------------
        private static int SafeClassId(int classId, int max)
        {
            // 手动实现Clamp功能
            if (classId < 0) return 0;
            if (classId > max) return max;
            return classId;
        }
    }

}
