
//using OpenCvSharp;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DeploySharp.Data
//{
//    /// <summary>
//    /// Computer vision color provider (supports automatic color assignment for 80 category IDs).
//    /// 计算机视觉颜色提供器（支持80类别ID的自动配色）。
//    /// </summary>
//    /// <remarks>
//    /// <para>
//    /// Features / 功能特点:
//    /// - High-contrast colors for bounding boxes
//    ///   边界框高对比度颜色
//    /// - Semantic segmentation mask colors
//    ///   语义分割掩膜色
//    /// - Semi-transparent fill colors for instance segmentation
//    ///   实例分割半透明填充色
//    /// </para>
//    /// <para>
//    /// Color palettes are pre-generated based on COCO and ADE20K standards.
//    /// 配色方案基于COCO和ADE20K标准预生成。
//    /// </para>
//    /// </remarks>
//    /// <example>
//    /// <code>
//    /// // Create color provider and get colors for visualization
//    /// // 创建颜色提供器并获取可视化颜色
//    /// var colors = new VisionColors();
//    /// 
//    /// // Get color for person detection (class 0)
//    /// // 获取行人检测的颜色(类别0)
//    /// Scalar personColor = colors.GetBoundingBoxColor(0);
//    /// 
//    /// // Get semi-transparent color for instance segmentation
//    /// // 获取实例分割的半透明颜色
//    /// Scalar maskColor = colors.GetInstanceColor(5, alpha: 128);
//    /// </code>
//    /// </example>
//    public class VisionColors
//    {
//        //------------------------- Base Color Palettes / 基础配色方案 -------------------------
//        private readonly Scalar[] _cocoPalette = GenerateCocoPalette();
//        private readonly Scalar[] _ade20kPalette = GenerateAde20kPalette();

//        //------------------------- Public API / 公共API -------------------------

//        /// <summary>
//        /// Gets bounding box color (COCO standard high-contrast colors).
//        /// 获取边界框颜色（COCO标准高对比色）。
//        /// </summary>
//        /// <param name="classId">Category ID (0-79) / 类别ID (0-79)</param>
//        /// <param name="alpha">Transparency (0-255), default is opaque / 透明度(0-255)，默认不透明</param>
//        /// <returns>OpenCV Scalar color (BGR format) / OpenCV Scalar颜色(BGR格式)</returns>
//        /// <remarks>
//        /// Class ID is automatically clamped to valid range [0, 79].
//        /// 类别ID自动钳制到有效范围[0, 79]。
//        /// </remarks>
//        /// <example>
//        /// <code>
//        /// var colors = new VisionColors();
//        /// 
//        /// // Get color for car (class 2 in COCO)
//        /// // 获取汽车的颜色(COCO中类别2)
//        /// Scalar carColor = colors.GetBoundingBoxColor(2);
//        /// 
//        /// // Draw bounding box
//        /// // 绘制边界框
//        /// Cv2.Rectangle(image, rect, carColor, 2);
//        /// </code>
//        /// </example>
//        /// <seealso cref="GetMaskColor"/>
//        /// <seealso cref="GetInstanceColor"/>
//        public Scalar GetBoundingBoxColor(int classId, byte alpha = 255)
//        {
//            classId = SafeClassId(classId, 80);
//            Scalar color = _cocoPalette[classId];

//            // Construct new color with alpha (OpenCV Scalar doesn't include alpha, needs separate handling)
//            return new Scalar(color[0], color[1], color[2], alpha);
//        }

//        /// <summary>
//        /// Gets semantic segmentation mask color (ADE20K standard colors).
//        /// 获取语义分割掩膜颜色（ADE20K标准色）。
//        /// </summary>
//        /// <param name="classId">Category ID / 类别ID</param>
//        /// <returns>OpenCV Scalar color (BGR format) / OpenCV Scalar颜色(BGR格式)</returns>
//        /// <remarks>
//        /// ADE20K palette supports 150 classes for semantic segmentation.
//        /// ADE20K调色板支持150个类别用于语义分割。
//        /// </remarks>
//        /// <example>
//        /// <code>
//        /// var colors = new VisionColors();
//        /// 
//        /// // Get color for sky segmentation
//        /// // 获取天空分割的颜色
//        /// Scalar skyColor = colors.GetMaskColor(2);
//        /// 
//        /// // Apply mask with color
//        /// // 应用带颜色的掩膜
//        /// Mat coloredMask = mask * skyColor;
//        /// </code>
//        /// </example>
//        public Scalar GetMaskColor(int classId)
//        {
//            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
//            return _ade20kPalette[classId];
//        }

//        /// <summary>
//        /// Gets instance segmentation fill color (semi-transparent version of bounding box color).
//        /// 获取实例分割填充色（半透明版边界框颜色）。
//        /// </summary>
//        /// <param name="instanceId">Instance ID / 实例ID</param>
//        /// <param name="alpha">Transparency level (0-255), default 128 (50% transparent) / 透明度级别(0-255)，默认128(50%透明)</param>
//        /// <returns>OpenCV Scalar color with alpha / 带透明度的OpenCV Scalar颜色</returns>
//        /// <remarks>
//        /// Instance ID is automatically cycled through 80 colors for unlimited instances.
//        /// 实例ID自动循环使用80种颜色，支持无限实例。
//        /// </remarks>
//        /// <example>
//        /// <code>
//        /// var colors = new VisionColors();
//        /// 
//        /// // Draw semi-transparent mask for each detected instance
//        /// // 为每个检测到的实例绘制半透明掩膜
//        /// for (int i = 0; i &lt; detectedInstances.Count; i++)
//        /// {
//        ///     Scalar fillColor = colors.GetInstanceColor(i, alpha: 100);
//        ///     // Blend mask with original image
//        ///     // 将掩膜与原始图像混合
//        /// }
//        /// </code>
//        /// </example>
//        public Scalar GetInstanceColor(int instanceId, byte alpha = 128)
//        {
//            return GetBoundingBoxColor(instanceId % 80, alpha);
//        }

//        //------------------------- Color Generators / 配色生成器 -------------------------

//        /// <summary>
//        /// Generates COCO dataset 80-category standard color palette.
//        /// 生成COCO数据集80类别标准配色。
//        /// </summary>
//        /// <returns>Array of 80 Scalar colors / 80个Scalar颜色的数组</returns>
//        /// <remarks>
//        /// Colors are designed for high contrast to distinguish different categories.
//        /// 颜色设计为高对比度以区分不同类别。
//        /// </remarks>
//        private static Scalar[] GenerateCocoPalette()
//        {
//            string[] hexColors =
//            {
//            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow / 红-黄
//            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan / 绿-青
//            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue / 蓝
//            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink / 紫-粉
//            // Extended colors (ensure 80 unique high-contrast colors)
//            "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#33FFF5",
//            "#F5FF33", "#FF1493", "#00FF00", "#FF4500", "#9400D3",
//            "#4B0082", "#008080", "#800000", "#000080", "#8A2BE2",
//            "#7CFC00", "#FFD700", "#4169E1", "#32CD32", "#BA55D3",
//            "#FF00FF", "#FF8C00", "#9932CC", "#00FA9A", "#FF6347",
//            "#9370DB", "#2E8B57", "#DA70D6", "#D2691E", "#B22222",
//            "#20B2AA", "#6495ED", "#778899", "#FF69B4", "#CD5C5C",
//            "#4682B4", "#9ACD32", "#8FBC8F", "#483D8B", "#E9967A",
//            "#8B4513", "#5F9EA0", "#556B2F", "#6A5ACD", "#98FB98",
//            "#DB7093", "#BC8F8F", "#8470FF", "#B8860B", "#C71585",
//            "#708090", "#00BFFF", "#66CDAA", "#0000CD", "#FA8072",
//            "#191970", "#7B68EE", "#48D1CC", "#DDA0DD", "#87CEFA"
//        };

//            var colors = new Scalar[80];
//            for (int i = 0; i < 80; i++)
//            {
//                colors[i] = HexToScalar(hexColors[i % hexColors.Length]);
//            }
//            return colors;
//        }

//        /// <summary>
//        /// Generates ADE20K semantic segmentation standard color palette.
//        /// 生成ADE20K语义分割标准配色。
//        /// </summary>
//        /// <returns>Array of Scalar colors / Scalar颜色数组</returns>
//        /// <remarks>
//        /// ADE20K dataset contains 150 semantic categories.
//        /// ADE20K数据集包含150个语义类别。
//        /// </remarks>
//        private static Scalar[] GenerateAde20kPalette()
//        {
//            string[] hexColors =
//            {
//            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231",
//            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB",
//            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC",
//            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7",
//            "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#33FFF5",
//            "#F5FF33", "#FF1493", "#00FF00", "#FF4500", "#9400D3",
//            "#4B0082", "#008080", "#800000", "#000080", "#8A2BE2",
//            "#7CFC00", "#FFD700", "#4169E1", "#32CD32", "#BA55D3",
//            "#FF00FF", "#FF8C00", "#9932CC", "#00FA9A", "#FF6347",
//            "#9370DB", "#2E8B57", "#DA70D6", "#D2691E", "#B22222",
//            "#20B2AA", "#6495ED", "#778899", "#FF69B4", "#CD5C5C",
//            "#4682B4", "#9ACD32", "#8FBC8F", "#483D8B", "#E9967A",
//            "#8B4513", "#5F9EA0", "#556B2F", "#6A5ACD", "#98FB98",
//            "#DB7093", "#BC8F8F", "#8470FF", "#B8860B", "#C71585",
//            "#708090", "#00BFFF", "#66CDAA", "#0000CD", "#FA8072",
//            "#191970", "#7B68EE", "#48D1CC", "#DDA0DD", "#87CEFA"
//        };

//            var colors = new Scalar[hexColors.Length];
//            for (int i = 0; i < hexColors.Length; i++)
//            {
//                colors[i] = HexToScalar(hexColors[i]);
//            }
//            return colors;
//        }

//        //------------------------- Helper Methods / 辅助方法 -------------------------

//        /// <summary>
//        /// Converts hexadecimal color string to OpenCV Scalar (BGR format).
//        /// 将十六进制颜色字符串转换为OpenCV Scalar(BGR格式)。
//        /// </summary>
//        /// <param name="hexColor">Hex color string (e.g., "#FF3838") / 十六进制颜色字符串(如"#FF3838")</param>
//        /// <returns>OpenCV Scalar in BGR format / BGR格式的OpenCV Scalar</returns>
//        /// <exception cref="ArgumentException">Thrown when hex format is invalid / 当十六进制格式无效时抛出</exception>
//        /// <remarks>
//        /// OpenCV uses BGR color order by default.
//        /// OpenCV默认使用BGR颜色顺序。
//        /// </remarks>
//        private static Scalar HexToScalar(string hexColor)
//        {
//            // Remove possible # prefix
//            if (hexColor.StartsWith("#"))
//            {
//                hexColor = hexColor.Substring(1);
//            }

//            // Parse RGB components
//            byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
//            byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
//            byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

//            // OpenCV defaults to BGR order
//            return new Scalar(b, g, r);
//        }

//        //------------------------- Safe Boundary Handling / 安全边界处理 -------------------------
//        /// <summary>
//        /// Safely clamps class ID to valid range.
//        /// 安全地将类别ID钳制到有效范围。
//        /// </summary>
//        /// <param name="classId">Input class ID / 输入类别ID</param>
//        /// <param name="max">Maximum valid value / 最大有效值</param>
//        /// <returns>Clamped class ID / 钳制后的类别ID</returns>
//        private static int SafeClassId(int classId, int max)
//        {
//            // Manual Clamp implementation
//            if (classId < 0) return 0;
//            if (classId > max) return max - 1;
//            return classId;
//        }
//    }
//}



using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeploySharp.Data
{
    /// <summary>
    /// Computer vision color provider (supports automatic color assignment with customizable palettes).
    /// 计算机视觉颜色提供器（支持自定义配色方案）。
    /// </summary>
    public class VisionColors
    {
        //------------------------- Fields / 字段 -------------------------
        private readonly Scalar[] _boundingBoxPalette;
        private readonly Scalar[] _maskPalette;
        private readonly int _maxClasses;

        //------------------------- Static Default Palettes / 静态默认配色 -------------------------
        private static readonly Lazy<Scalar[]> _defaultCocoPalette = new Lazy<Scalar[]>(GenerateCocoPalette);
        private static readonly Lazy<Scalar[]> _defaultAde20kPalette = new Lazy<Scalar[]>(GenerateAde20kPalette);

        //------------------------- Constructors / 构造函数 -------------------------

        /// <summary>
        /// Initializes with default COCO and ADE20K palettes.
        /// 使用默认的COCO和ADE20K配色初始化。
        /// </summary>
        public VisionColors() : this(null, null, 80)
        {
        }

        /// <summary>
        /// Initializes with custom palettes.
        /// 使用自定义配色初始化。
        /// </summary>
        /// <param name="boundingBoxPalette">Custom bounding box color palette / 自定义边界框配色</param>
        /// <param name="maskPalette">Custom mask color palette / 自定义掩膜配色</param>
        /// <param name="maxClasses">Maximum number of classes (default: 80) / 最大类别数（默认：80）</param>
        public VisionColors(Scalar[] boundingBoxPalette, Scalar[] maskPalette, int maxClasses = 80)
        {
            _maxClasses = maxClasses;
            _boundingBoxPalette = boundingBoxPalette ?? _defaultCocoPalette.Value;
            _maskPalette = maskPalette ?? _defaultAde20kPalette.Value;
        }

        //------------------------- Public API / 公共API -------------------------

        /// <summary>
        /// Gets bounding box color.
        /// 获取边界框颜色。
        /// </summary>
        public Scalar GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, _boundingBoxPalette.Length);
            var color = _boundingBoxPalette[classId];
            return new Scalar(color[0], color[1], color[2], alpha);
        }

        /// <summary>
        /// Gets semantic segmentation mask color.
        /// 获取语义分割掩膜颜色。
        /// </summary>
        public Scalar GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _maskPalette.Length);
            return _maskPalette[classId];
        }

        /// <summary>
        /// Gets instance segmentation fill color.
        /// 获取实例分割填充色。
        /// </summary>
        public Scalar GetInstanceColor(int instanceId, byte alpha = 128)
        {
            return GetBoundingBoxColor(instanceId % _maxClasses, alpha);
        }

        /// <summary>
        /// Gets random color from bounding box palette.
        /// 从边界框配色中获取随机颜色。
        /// </summary>
        public Scalar GetRandomColor(byte alpha = 255)
        {
            var random = new Random();
            var color = _boundingBoxPalette[random.Next(_boundingBoxPalette.Length)];
            return new Scalar(color[0], color[1], color[2], alpha);
        }

        /// <summary>
        /// Updates bounding box color for a specific class.
        /// 更新特定类别的边界框颜色。
        /// </summary>
        public void SetBoundingBoxColor(int classId, Scalar color)
        {
            if (classId >= 0 && classId < _boundingBoxPalette.Length && _boundingBoxPalette != _defaultCocoPalette.Value)
            {
                _boundingBoxPalette[classId] = color;
            }
        }

        //------------------------- Static Color Generators / 静态配色生成器 -------------------------

        /// <summary>
        /// Generates COCO dataset 80-category standard color palette.
        /// 生成COCO数据集80类别标准配色。
        /// </summary>
        public static Scalar[] GenerateCocoPalette()
        {
            string[] hexColors = GetStandardCocoColors();
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
        public static Scalar[] GenerateAde20kPalette()
        {
            string[] hexColors = GetStandardAde20kColors();
            var colors = new Scalar[hexColors.Length];
            for (int i = 0; i < hexColors.Length; i++)
            {
                colors[i] = HexToScalar(hexColors[i]);
            }
            return colors;
        }

        /// <summary>
        /// Generates a high-contrast color palette dynamically.
        /// 动态生成高对比度配色。
        /// </summary>
        /// <param name="count">Number of colors to generate / 生成颜色数量</param>
        /// <param name="saturation">Saturation (0-1) / 饱和度</param>
        /// <param name="value">Value/Brightness (0-1) / 明度</param>
        public static Scalar[] GenerateHighContrastPalette(int count, double saturation = 0.8, double value = 0.9)
        {
            var colors = new Scalar[count];
            var random = new Random(42); // Fixed seed for consistency

            for (int i = 0; i < count; i++)
            {
                // Use golden ratio for even distribution
                double hue = (i * 0.618033988749895) % 1.0;
                var rgb = HsvToRgb(hue, saturation, value);
                colors[i] = new Scalar(rgb[2], rgb[1], rgb[0]); // BGR order
            }

            return colors;
        }

        /// <summary>
        /// Creates a grayscale palette.
        /// 创建灰度配色。
        /// </summary>
        public static Scalar[] GenerateGrayscalePalette(int count, byte minBrightness = 64, byte maxBrightness = 224)
        {
            var colors = new Scalar[count];
            for (int i = 0; i < count; i++)
            {
                byte intensity = (byte)(minBrightness + (i % (maxBrightness - minBrightness)));
                colors[i] = new Scalar(intensity, intensity, intensity);
            }
            return colors;
        }

        //------------------------- Helper Methods / 辅助方法 -------------------------

        private static string[] GetStandardCocoColors()
        {
            return new[]
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
        }

        private static string[] GetStandardAde20kColors()
        {
            // ADE20K has 150 classes, here we provide first 80
            return GetStandardCocoColors();
        }

        private static Scalar HexToScalar(string hexColor)
        {
            if (hexColor.StartsWith("#"))
                hexColor = hexColor.Substring(1);

            byte r = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return new Scalar(b, g, r);
        }

        private static byte[] HsvToRgb(double hue, double saturation, double value)
        {
            int hi = (int)(hue * 6) % 6;
            double f = hue * 6 - hi;
            double p = value * (1 - saturation);
            double q = value * (1 - f * saturation);
            double t = value * (1 - (1 - f) * saturation);

            double r = 0, g = 0, b = 0;
            switch (hi)
            {
                case 0: r = value; g = t; b = p; break;
                case 1: r = q; g = value; b = p; break;
                case 2: r = p; g = value; b = t; break;
                case 3: r = p; g = q; b = value; break;
                case 4: r = t; g = p; b = value; break;
                case 5: r = value; g = p; b = q; break;
            }

            return new[] { (byte)(r * 255), (byte)(g * 255), (byte)(b * 255) };
        }

        private static int SafeClassId(int classId, int maxLength)
        {
            if (classId < 0) return 0;
            if (classId >= maxLength) return classId % maxLength;
            return classId;
        }
    }

    /// <summary>
    /// Predefined color palette presets.
    /// 预定义的配色预设。
    /// </summary>
    public static class VisionColorPresets
    {
        /// <summary>
        /// Creates a preset with COCO dataset colors.
        /// 使用COCO数据集颜色创建预设。
        /// </summary>
        public static VisionColors CocoPreset => new VisionColors();

        /// <summary>
        /// Creates a preset with pastel colors (soft, less saturated).
        /// 使用柔和色彩创建预设（低饱和度）。
        /// </summary>
        public static VisionColors PastelPreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateHighContrastPalette(classCount, 0.5, 0.85);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Creates a preset with vibrant colors.
        /// 使用鲜艳色彩创建预设。
        /// </summary>
        public static VisionColors VibrantPreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateHighContrastPalette(classCount, 0.9, 0.95);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Creates a preset with grayscale colors.
        /// 使用灰度色彩创建预设。
        /// </summary>
        public static VisionColors GrayscalePreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateGrayscalePalette(classCount);
            return new VisionColors(palette, palette, classCount);
        }
    }
}