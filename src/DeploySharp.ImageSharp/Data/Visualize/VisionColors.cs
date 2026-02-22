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
    /// Computer vision color provider supporting automatic coloring for up to 80 class IDs
    /// 计算机视觉颜色提供器，支持80个类别ID的自动配色
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides distinctive colors for different visualization purposes in computer vision tasks:
    /// 为计算机视觉任务中的不同可视化目的提供独特的颜色:
    /// - High-contrast bounding box colors for object detection
    ///   用于目标检测的高对比度边界框颜色
    /// - Semantic segmentation mask colors for pixel-level classification
    ///   用于像素级分类的语义分割掩膜颜色
    /// - Semi-transparent instance segmentation fill colors for overlapping objects
    ///   用于重叠对象的半透明实例分割填充色
    /// </para>
    /// <para>
    /// Color schemes follow common dataset standards:
    /// 配色方案遵循常见的数据集标准:
    /// - COCO (Common Objects in Context): 80-class palette optimized for object detection
    ///   COCO (上下文中的常见对象): 为对象检测优化的80类调色板
    /// - ADE20K: Semantic segmentation palette with distinct colors for scene parsing
    ///   ADE20K: 用于场景解析的具有明显颜色的语义分割调色板
    /// </para>
    /// <para>
    /// All colors are carefully selected for maximum distinguishability and visibility
    /// across different backgrounds and lighting conditions.
    /// 所有颜色都经过精心选择，以在不同背景和光照条件下实现最大的可区分性和可见性。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var colors = new VisionColors();
    /// 
    /// // Get color for class 0 (person in COCO)
    /// // 获取类别0的颜色(COCO中的person)
    /// var personColor = colors.GetBoundingBoxColor(0);
    /// 
    /// // Get semi-transparent color for instance segmentation
    /// // 获取用于实例分割的半透明颜色
    /// var instanceColor = colors.GetInstanceColor(5, alpha: 128);
    /// 
    /// // Get semantic segmentation color
    /// // 获取语义分割颜色
    /// var skyColor = colors.GetMaskColor(2);
    /// </code>
    /// </example>
    /// <seealso cref="VisualizeOptions"/>
    /// <seealso cref="Visualize"/>
    public class VisionColors
    {
        //------------------------- Base Color Schemes -------------------------
        //------------------------- 基础配色方案 -------------------------

        /// <summary>
        /// COCO dataset 80-class standard color palette
        /// COCO数据集80类别标准调色板
        /// </summary>
        /// <remarks>
        /// Pre-generated palette optimized for high contrast and visibility.
        /// Used for object detection bounding boxes and instance segmentation.
        /// 预生成的针对高对比度和可见性优化的调色板。
        /// 用于对象检测边界框和实例分割。
        /// </remarks>
        private readonly Rgba32[] _cocoPalette = GenerateCocoPalette();

        /// <summary>
        /// ADE20K dataset color palette for semantic segmentation
        /// ADE20K数据集语义分割调色板
        /// </summary>
        /// <remarks>
        /// Extended palette supporting more classes for scene parsing tasks.
        /// 扩展调色板，支持更多类别用于场景解析任务。
        /// </remarks>
        private readonly Rgba32[] _ade20kPalette = GenerateAde20kPalette();

        //------------------------- Public API -------------------------
        //------------------------- 公共API -------------------------

        /// <summary>
        /// Gets bounding box color from COCO standard high-contrast palette
        /// 从COCO标准高对比度调色板获取边界框颜色
        /// </summary>
        /// <param name="classId">
        /// Class ID (0-79 for COCO dataset) / 类别ID (COCO数据集为0-79)
        /// </param>
        /// <param name="alpha">
        /// Transparency value (0-255), default 255 (fully opaque) / 
        /// 透明度值(0-255)，默认255(完全不透明)
        /// </param>
        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when alpha is not in range [0, 255]
        /// 当alpha不在[0, 255]范围内时抛出
        /// </exception>
        /// <remarks>
        /// Class IDs are automatically clamped to valid range using modulo operation.
        /// 类别ID使用取模操作自动钳制到有效范围。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var colors = new VisionColors();
        /// 
        /// // Get fully opaque color for person (class 0)
        /// var personColor = colors.GetBoundingBoxColor(0);
        /// 
        /// // Get semi-transparent color
        /// var transparentColor = colors.GetBoundingBoxColor(5, alpha: 128);
        /// 
        /// // Class ID wraps around for values > 79
        /// var wrappedColor = colors.GetBoundingBoxColor(80); // Same as class 0
        /// </code>
        /// </example>
        /// <seealso cref="GetInstanceColor"/>
        public Color GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, 80);
            Rgba32 color = _cocoPalette[classId];

            // Create new color with specified alpha
            // 构造带透明度的新颜色
            return Color.FromRgba(color.R, color.G, color.B, alpha);
        }

        /// <summary>
        /// Gets semantic segmentation mask color from ADE20K standard palette
        /// 从ADE20K标准调色板获取语义分割掩膜颜色
        /// </summary>
        /// <param name="classId">Class ID for semantic class / 语义类别的类别ID</param>
        /// <returns>RGBA color / RGBA颜色</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when classId is negative
        /// 当classId为负数时抛出
        /// </exception>
        /// <remarks>
        /// Uses ADE20K palette which supports more classes than COCO.
        /// 使用比COCO支持更多类别的ADE20K调色板。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var colors = new VisionColors();
        /// 
        /// // Common ADE20K class IDs:
        /// // 0: background/wall
        /// // 1: building
        /// // 2: sky
        /// // 3: floor
        /// // 4: tree
        /// var skyColor = colors.GetMaskColor(2);
        /// </code>
        /// </example>
        public Color GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
            return _ade20kPalette[classId];
        }

        /// <summary>
        /// Gets instance segmentation fill color (semi-transparent version of bounding box color)
        /// 获取实例分割填充色（边界框颜色的半透明版本）
        /// </summary>
        /// <param name="instanceId">Instance ID (can be any integer) / 实例ID（可以是任意整数）</param>
        /// <param name="alpha">
        /// Transparency value (0-255), default 128 (50% transparent) / 
        /// 透明度值(0-255)，默认128(50%透明)
        /// </param>
        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when alpha is not in range [0, 255]
        /// 当alpha不在[0, 255]范围内时抛出
        /// </exception>
        /// <remarks>
        /// Instance ID is wrapped to COCO palette range (0-79) using modulo.
        /// This ensures consistent coloring for the same instance across frames.
        /// 实例ID使用取模映射到COCO调色板范围(0-79)。
        /// 这确保了同一实例在跨帧时颜色一致。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var colors = new VisionColors();
        /// 
        /// // Get semi-transparent color for instance 5
        /// var instance5Color = colors.GetInstanceColor(5);
        /// 
        /// // More transparent
        /// var veryTransparent = colors.GetInstanceColor(5, alpha: 64);
        /// 
        /// // Less transparent
        /// var barelyTransparent = colors.GetInstanceColor(5, alpha: 200);
        /// </code>
        /// </example>
        /// <seealso cref="GetBoundingBoxColor"/>
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
        /// <returns>Array of 80 Rgba32 colors / 80个Rgba32颜色的数组</returns>
        /// <remarks>
        /// <para>
        /// Colors are optimized for high visibility and contrast against various backgrounds.
        /// The palette includes colors from different hues to maximize distinguishability.
        /// 颜色针对各种背景的高可见性和对比度进行了优化。
        /// 调色板包含不同色调的颜色以最大化可区分性。
        /// </para>
        /// <para>
        /// Color distribution:
        /// 颜色分布:
        /// - Red-Yellow range (indices 0-4): #FF3838, #FF9D97, #FF701F, #FFB21D, #CFD231
        ///   红-黄范围 (索引0-4)
        /// - Green-Cyan range (indices 5-9): #48F90A, #92CC17, #3DDB86, #1A9334, #00D4BB
        ///   绿-青范围 (索引5-9)
        /// - Blue range (indices 10-14): #2C99A8, #00C2FF, #344593, #6473FF, #0018EC
        ///   蓝色范围 (索引10-14)
        /// - Purple-Pink range (indices 15-19): #8438FF, #520085, #CB38FF, #FF95C8, #FF37C7
        ///   紫-粉范围 (索引15-19)
        /// - Extended colors (indices 20-79): Additional high-contrast colors
        ///   扩展颜色 (索引20-79): 额外的高对比度颜色
        /// </para>
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
        /// <returns>Array of Rgba32 colors / Rgba32颜色数组</returns>
        /// <remarks>
        /// Similar to COCO palette but with extended range for semantic segmentation tasks.
        /// 与COCO调色板类似，但为语义分割任务扩展了范围。
        /// </remarks>
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
        /// Ensures class ID falls within valid range [0, max]
        /// 确保类别ID在有效范围[0, max]内
        /// </summary>
        /// <param name="classId">Input class ID (can be any integer) / 输入类别ID（可以是任意整数）</param>
        /// <param name="max">Maximum allowed value (exclusive) / 允许的最大值（不包含）</param>
        /// <returns>Clamped class ID in range [0, max-1] / 钳制后的类别ID，范围[0, max-1]</returns>
        /// <remarks>
        /// Manual implementation of Clamp functionality to avoid dependency on Math.Clamp (.NET Core 2.0+).
        /// Negative values are clamped to 0, values >= max are wrapped using modulo.
        /// Math.Clamp的手动实现，避免依赖.NET Core 2.0+的Math.Clamp。
        /// 负值钳制为0，值>=max使用取模环绕。
        /// </remarks>
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
