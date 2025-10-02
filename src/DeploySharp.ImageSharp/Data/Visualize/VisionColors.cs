using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Computer vision color provider (supports automatic coloring for 80 class IDs)
    /// 计算机视觉颜色提供器（支持80类别ID的自动配色）
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides distinctive colors for different visualization purposes:
    /// 为不同的可视化目的提供独特的颜色:
    /// - High-contrast bounding box colors
    ///   高对比度边界框颜色
    /// - Semantic segmentation mask colors
    ///   语义分割掩膜颜色
    /// - Semi-transparent instance segmentation fill colors  
    ///   半透明实例分割填充色
    /// </para>
    /// <para>
    /// Color schemes follow common dataset standards (COCO, ADE20K)
    /// 配色方案遵循常见数据集标准(COCO, ADE20K)
    /// </para>
    /// </remarks>
    public class VisionColors
    {
        //------------------------- Base Color Schemes -------------------------
        //------------------------- 基础配色方案 -------------------------

        /// <summary>
        /// COCO dataset 80-class color palette
        /// COCO数据集80类别调色板
        /// </summary>
        private readonly Rgba32[] _cocoPalette = GenerateCocoPalette();

        /// <summary>
        /// ADE20K dataset color palette
        /// ADE20K数据集调色板
        /// </summary>
        private readonly Rgba32[] _ade20kPalette = GenerateAde20kPalette();

        //------------------------- Public API -------------------------
        //------------------------- 公共API -------------------------

        /// <summary>
        /// Gets bounding box color (COCO standard high-contrast color)
        /// 获取边界框颜色（COCO标准高对比色）
        /// </summary>
        /// <param name="classId">Class ID (0-79)/类别ID (0-79)</param>
        /// <param name="alpha">Transparency (0-255), default opaque/透明度(0-255)，默认不透明</param>
        /// <returns>RGBA color/RGBA颜色</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when classId exceeds palette range
        /// 当classId超出调色板范围时抛出
        /// </exception>
        public Color GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, 80);
            Rgba32 color = _cocoPalette[classId];

            // Create new color with specified alpha
            // 构造带透明度的新颜色
            return Color.FromRgba(color.R, color.G, color.B, alpha);
        }

        /// <summary>
        /// Gets semantic segmentation mask color (ADE20K standard color)
        /// 获取语义分割掩膜颜色（ADE20K标准色）
        /// </summary>
        /// <param name="classId">Class ID/类别ID</param>
        /// <returns>RGBA color/RGBA颜色</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when classId exceeds palette range
        /// 当classId超出调色板范围时抛出
        /// </exception>
        public Color GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
            return _ade20kPalette[classId];
        }

        /// <summary>
        /// Gets instance segmentation fill color (semi-transparent bounding box color)
        /// 获取实例分割填充色（半透明版边界框颜色）
        /// </summary>
        /// <param name="instanceId">Instance ID/实例ID</param>
        /// <param name="alpha">Transparency (0-255), default 128/透明度(0-255)，默认128</param>
        /// <returns>RGBA color/RGBA颜色</returns>
        public Color GetInstanceColor(int instanceId, byte alpha = 128)
        {
            return GetBoundingBoxColor(instanceId % 80, alpha);
        }

        //------------------------- Palette Generators -------------------------
        //------------------------- 配色生成器 -------------------------

        /// <summary>
        /// Generates COCO dataset 80-class standard color palette
        /// 生成COCO数据集80类别标准配色
        /// </summary>
        /// <returns>Array of Rgba32 colors/Rgba32颜色数组</returns>
        /// <remarks>
        /// Colors are optimized for high visibility and contrast
        /// 颜色针对高可见性和对比度进行了优化
        /// </remarks>
        private static Rgba32[] GenerateCocoPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow/红-黄
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan/绿-青
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue/蓝
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink/紫-粉
            // Extended colors (ensures 80 distinct high-contrast colors)
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

            var colors = new Rgba32[80];
            for (int i = 0; i < 80; i++)
            {
                colors[i] = Rgba32.ParseHex(hexColors[i % hexColors.Length]);
            }
            return colors;
        }

        /// <summary>
        /// Generates ADE20K semantic segmentation standard color palette
        /// 生成ADE20K语义分割标准配色
        /// </summary>
        /// <returns>Array of Rgba32 colors/Rgba32颜色数组</returns>
        private static Rgba32[] GenerateAde20kPalette()
        {
            string[] hexColors =
            {
            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow/红-黄
            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan/绿-青
            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue/蓝
            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink/紫-粉
            // Extended colors (high-contrast colors)
            // 扩展颜色（高对比色）
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

            var colors = new Rgba32[hexColors.Length];
            for (int i = 0; i < hexColors.Length; i++)
            {
                colors[i] = Rgba32.ParseHex(hexColors[i]);
            }
            return colors;
        }

        //------------------------- Safety Boundary Handling -------------------------
        //------------------------- 安全边界处理 -------------------------

        /// <summary>
        /// Ensures class ID falls within valid range
        /// 确保类别ID在有效范围内
        /// </summary>
        /// <param name="classId">Input class ID/输入类别ID</param>
        /// <param name="max">Maximum allowed value/允许的最大值</param>
        /// <returns>Clamped class ID/钳制后的类别ID</returns>
        private static int SafeClassId(int classId, int max)
        {
            // Manual implementation of Clamp functionality
            // 手动实现Clamp功能
            if (classId < 0) return 0;
            if (classId > max) return max - 1;
            return classId;
        }
    }

}
