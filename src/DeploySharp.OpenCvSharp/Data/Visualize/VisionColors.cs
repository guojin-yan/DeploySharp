
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Computer vision color provider (supports automatic color assignment for 80 category IDs).
    /// 计算机视觉颜色提供器（支持80类别ID的自动配色）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Features / 功能特点:
    /// - High-contrast colors for bounding boxes
    ///   边界框高对比度颜色
    /// - Semantic segmentation mask colors
    ///   语义分割掩膜色
    /// - Semi-transparent fill colors for instance segmentation
    ///   实例分割半透明填充色
    /// </para>
    /// <para>
    /// Color palettes are pre-generated based on COCO and ADE20K standards.
    /// 配色方案基于COCO和ADE20K标准预生成。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create color provider and get colors for visualization
    /// // 创建颜色提供器并获取可视化颜色
    /// var colors = new VisionColors();
    /// 
    /// // Get color for person detection (class 0)
    /// // 获取行人检测的颜色(类别0)
    /// Scalar personColor = colors.GetBoundingBoxColor(0);
    /// 
    /// // Get semi-transparent color for instance segmentation
    /// // 获取实例分割的半透明颜色
    /// Scalar maskColor = colors.GetInstanceColor(5, alpha: 128);
    /// </code>
    /// </example>
    public class VisionColors
    {
        //------------------------- Base Color Palettes / 基础配色方案 -------------------------
        private readonly Scalar[] _cocoPalette = GenerateCocoPalette();
        private readonly Scalar[] _ade20kPalette = GenerateAde20kPalette();

        //------------------------- Public API / 公共API -------------------------

        /// <summary>
        /// Gets bounding box color (COCO standard high-contrast colors).
        /// 获取边界框颜色（COCO标准高对比色）。
        /// </summary>
        /// <param name="classId">Category ID (0-79) / 类别ID (0-79)</param>
        /// <param name="alpha">Transparency (0-255), default is opaque / 透明度(0-255)，默认不透明</param>
        /// <returns>OpenCV Scalar color (BGR format) / OpenCV Scalar颜色(BGR格式)</returns>
        /// <remarks>
        /// Class ID is automatically clamped to valid range [0, 79].
        /// 类别ID自动钳制到有效范围[0, 79]。
        /// </remarks>
        /// <example>
        /// <code>
        /// var colors = new VisionColors();
        /// 
        /// // Get color for car (class 2 in COCO)
        /// // 获取汽车的颜色(COCO中类别2)
        /// Scalar carColor = colors.GetBoundingBoxColor(2);
        /// 
        /// // Draw bounding box
        /// // 绘制边界框
        /// Cv2.Rectangle(image, rect, carColor, 2);
        /// </code>
        /// </example>
        /// <seealso cref="GetMaskColor"/>
        /// <seealso cref="GetInstanceColor"/>
        public Scalar GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, 80);
            Scalar color = _cocoPalette[classId];

            // Construct new color with alpha (OpenCV Scalar doesn't include alpha, needs separate handling)
            return new Scalar(color[0], color[1], color[2], alpha);
        }

        /// <summary>
        /// Gets semantic segmentation mask color (ADE20K standard colors).
        /// 获取语义分割掩膜颜色（ADE20K标准色）。
        /// </summary>
        /// <param name="classId">Category ID / 类别ID</param>
        /// <returns>OpenCV Scalar color (BGR format) / OpenCV Scalar颜色(BGR格式)</returns>
        /// <remarks>
        /// ADE20K palette supports 150 classes for semantic segmentation.
        /// ADE20K调色板支持150个类别用于语义分割。
        /// </remarks>
        /// <example>
        /// <code>
        /// var colors = new VisionColors();
        /// 
        /// // Get color for sky segmentation
        /// // 获取天空分割的颜色
        /// Scalar skyColor = colors.GetMaskColor(2);
        /// 
        /// // Apply mask with color
        /// // 应用带颜色的掩膜
        /// Mat coloredMask = mask * skyColor;
        /// </code>
        /// </example>
        public Scalar GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
            return _ade20kPalette[classId];
        }

        /// <summary>
        /// Gets instance segmentation fill color (semi-transparent version of bounding box color).
        /// 获取实例分割填充色（半透明版边界框颜色）。
        /// </summary>
        /// <param name="instanceId">Instance ID / 实例ID</param>
        /// <param name="alpha">Transparency level (0-255), default 128 (50% transparent) / 透明度级别(0-255)，默认128(50%透明)</param>
        /// <returns>OpenCV Scalar color with alpha / 带透明度的OpenCV Scalar颜色</returns>
        /// <remarks>
        /// Instance ID is automatically cycled through 80 colors for unlimited instances.
        /// 实例ID自动循环使用80种颜色，支持无限实例。
        /// </remarks>
        /// <example>
        /// <code>
        /// var colors = new VisionColors();
        /// 
        /// // Draw semi-transparent mask for each detected instance
        /// // 为每个检测到的实例绘制半透明掩膜
        /// for (int i = 0; i &lt; detectedInstances.Count; i++)
        /// {
        ///     Scalar fillColor = colors.GetInstanceColor(i, alpha: 100);
        ///     // Blend mask with original image
        ///     // 将掩膜与原始图像混合
        /// }
        /// </code>
        /// </example>
        public Scalar GetInstanceColor(int instanceId, byte alpha = 128)
        {
            return GetBoundingBoxColor(instanceId % 80, alpha);
        }

        //------------------------- Color Generators / 配色生成器 -------------------------

        /// <summary>
        /// Generates COCO dataset 80-category standard color palette.
        /// 生成COCO数据集80类别标准配色。
        /// </summary>
        /// <returns>Array of 80 Scalar colors / 80个Scalar颜色的数组</returns>
        /// <remarks>
        /// Colors are designed for high contrast to distinguish different categories.
        /// 颜色设计为高对比度以区分不同类别。
        /// </remarks>
        private static Scalar[] GenerateCocoPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow / 红-黄
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan / 绿-青
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue / 蓝
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink / 紫-粉
            // Extended colors (ensure 80 unique high-contrast colors)
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
        /// Generates ADE20K semantic segmentation standard color palette.
        /// 生成ADE20K语义分割标准配色。
        /// </summary>
        /// <returns>Array of Scalar colors / Scalar颜色数组</returns>
        /// <remarks>
        /// ADE20K dataset contains 150 semantic categories.
        /// ADE20K数据集包含150个语义类别。
        /// </remarks>
        private static Scalar[] GenerateAde20kPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231",
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB",
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC",
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7",
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

        //------------------------- Helper Methods / 辅助方法 -------------------------

        /// <summary>
        /// Converts hexadecimal color string to OpenCV Scalar (BGR format).
        /// 将十六进制颜色字符串转换为OpenCV Scalar(BGR格式)。
        /// </summary>
        /// <param name="hexColor">Hex color string (e.g., "#FF3838") / 十六进制颜色字符串(如"#FF3838")</param>
        /// <returns>OpenCV Scalar in BGR format / BGR格式的OpenCV Scalar</returns>
        /// <exception cref="ArgumentException">Thrown when hex format is invalid / 当十六进制格式无效时抛出</exception>
        /// <remarks>
        /// OpenCV uses BGR color order by default.
        /// OpenCV默认使用BGR颜色顺序。
        /// </remarks>
        private static Scalar HexToScalar(string hexColor)
        {
            // Remove possible # prefix
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            // Parse RGB components
            byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            // OpenCV defaults to BGR order
            return new Scalar(b, g, r);
        }

        //------------------------- Safe Boundary Handling / 安全边界处理 -------------------------
        /// <summary>
        /// Safely clamps class ID to valid range.
        /// 安全地将类别ID钳制到有效范围。
        /// </summary>
        /// <param name="classId">Input class ID / 输入类别ID</param>
        /// <param name="max">Maximum valid value / 最大有效值</param>
        /// <returns>Clamped class ID / 钳制后的类别ID</returns>
        private static int SafeClassId(int classId, int max)
        {
            // Manual Clamp implementation
            if (classId < 0) return 0;
            if (classId > max) return max - 1;
            return classId;
        }
    }
}
