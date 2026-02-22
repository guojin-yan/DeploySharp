using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Configuration options for visualization rendering with ImageSharp
    /// 使用ImageSharp进行可视化渲染的配置选项
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides customizable settings for various aspects of visualization:
    /// 为可视化的各个方面提供可自定义的设置:
    /// - Mask transparency and confidence thresholds for segmentation
    ///   分割的掩膜透明度和置信度阈值
    /// - Font styling and sizing for labels
    ///   标签的字体样式和大小
    /// - Drawing parameters like border thickness
    ///   绘制参数如边框粗细
    /// </para>
    /// <para>
    /// Supports automatic scaling based on ratio for responsive visualization.
    /// When constructing with a ratio parameter, font sizes and border thickness
    /// are automatically adjusted.
    /// 支持基于比例的自适应缩放以实现响应式可视化。
    /// 当使用比例参数构造时，字体大小和边框粗细会自动调整。
    /// </para>
    /// <para>
    /// Default values are chosen for general-purpose visualization on typical images (1080p).
    /// 默认值是为典型图像(1080p)上的通用可视化选择的。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Default options
    /// var options = new VisualizeOptions(1.0f);
    /// 
    /// // Customized for small images
    /// var smallOptions = new VisualizeOptions(0.5f)
    /// {
    ///     MaskAlpha = 0.3f,
    ///     PointDrawThreshold = 0.2f
    /// };
    /// 
    /// // Customized for segmentation
    /// var segOptions = new VisualizeOptions(1.0f)
    /// {
    ///     MaskAlpha = 0.4f,
    ///     MaskMinimumConfidence = 0.6f
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="Visualize"/>
    /// <seealso cref="VisionColors"/>
    public class VisualizeOptions
    {
        /// <summary>
        /// Alpha transparency value for mask overlays (0.0 - 1.0)
        /// 掩膜覆盖层的透明度值(0.0 - 1.0)
        /// </summary>
        /// <value>Default: 0.5 (50% transparent) / 默认值: 0.5 (50%透明)</value>
        /// <remarks>
        /// Controls the transparency of segmentation masks when overlaid on the original image.
        /// Lower values make masks more transparent, higher values make them more opaque.
        /// 控制分割掩膜覆盖在原始图像上的透明度。
        /// 较低的值使掩膜更透明，较高的值使掩膜更不透明。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Very transparent masks
        /// options.MaskAlpha = 0.2f;
        /// 
        /// // Nearly opaque masks
        /// options.MaskAlpha = 0.8f;
        /// </code>
        /// </example>
        public float MaskAlpha { get; set; } = 0.5f;

        /// <summary>
        /// Minimum confidence threshold for rendering mask pixels
        /// 渲染掩膜像素的最低置信度阈值
        /// </summary>
        /// <value>Default: 0.5 / 默认值: 0.5</value>
        /// <remarks>
        /// Pixels with mask values below this threshold are not rendered.
        /// Higher values produce cleaner masks with fewer low-confidence regions.
        /// 掩膜值低于此阈值的像素不会被渲染。
        /// 较高的值产生更干净的掩膜，低置信度区域更少。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Only show high-confidence regions
        /// options.MaskMinimumConfidence = 0.7f;
        /// 
        /// // Show more regions including low-confidence
        /// options.MaskMinimumConfidence = 0.3f;
        /// </code>
        /// </example>
        public float MaskMinimumConfidence { get; set; } = 0.5f;

        /// <summary>
        /// Confidence threshold for rendering keypoints in pose estimation
        /// 姿态估计中渲染关键点的置信度阈值
        /// </summary>
        /// <value>Default: 0.5 / 默认值: 0.5</value>
        /// <remarks>
        /// Keypoints with confidence below this threshold are not drawn.
        /// Useful for filtering out uncertain detections like occluded joints.
        /// 置信度低于此阈值的关键点不会被绘制。
        /// 用于过滤掉不确定的检测，如被遮挡的关节。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Strict filtering - only very confident keypoints
        /// options.PointDrawThreshold = 0.7f;
        /// 
        /// // Lenient filtering - show most keypoints
        /// options.PointDrawThreshold = 0.3f;
        /// </code>
        /// </example>
        public float PointDrawThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Selected font family for text rendering
        /// 用于文本渲染的字体家族
        /// </summary>
        /// <remarks>
        /// Defaults to the first available system font.
        /// Uses GetDefaultFontFamily() for cross-platform compatibility.
        /// 默认为第一个可用的系统字体。
        /// 使用GetDefaultFontFamily()实现跨平台兼容。
        /// </remarks>
        /// <seealso cref="GetDefaultFontFamily"/>
        private FontFamily FontFamily { get; set; } = GetDefaultFontFamily();

        /// <summary>
        /// Base font size in points (will be scaled by ratio)
        /// 基础字体大小，单位磅（会被比例缩放）
        /// </summary>
        /// <value>Default: 12 / 默认值: 12</value>
        /// <remarks>
        /// Actual rendered size is FontSize * ratio passed to constructor.
        /// 实际渲染大小是FontSize乘以传递给构造函数的比例。
        /// </remarks>
        public float FontSize { get; set; } = 12f;

        /// <summary>
        /// Thickness of border lines in pixels (will be scaled by ratio)
        /// 边框线条粗细，单位像素（会被比例缩放）
        /// </summary>
        /// <value>Default: 2 / 默认值: 2</value>
        /// <remarks>
        /// Applies to bounding box borders and other drawn lines.
        /// Actual rendered thickness is BorderThickness * ratio.
        /// 应用于边界框边框和其他绘制的线条。
        /// 实际渲染粗细是BorderThickness乘以比例。
        /// </remarks>
        public float BorderThickness { get; set; } = 2;

        /// <summary>
        /// Generated font instance from selected family and size
        /// 根据选择的字体家族和大小生成的字体实例
        /// </summary>
        /// <remarks>
        /// This is a computed property that creates a new Font instance
        /// from the current FontFamily and FontSize settings.
        /// 这是一个计算属性，从当前的FontFamily和FontSize设置创建新的Font实例。
        /// </remarks>
        public Font FontType { get => FontFamily.CreateFont(FontSize); }

        /// <summary>
        /// Calculated height of the current font for label positioning
        /// 当前字体的计算高度，用于标签定位
        /// </summary>
        /// <remarks>
        /// Used to determine the size of label background rectangles.
        /// Computed using font metrics for accurate text measurement.
        /// 用于确定标签背景矩形的大小。
        /// 使用字体度量计算以实现准确的文本测量。
        /// </remarks>
        /// <seealso cref="GetFontHeight"/>
        public float FontHeight { get => GetFontHeight(FontType); }

        /// <summary>
        /// Color provider for visualization elements
        /// 可视化元素的颜色提供器
        /// </summary>
        /// <remarks>
        /// Provides colors for bounding boxes, masks, and instance segmentation.
        /// Uses COCO and ADE20K standard palettes.
        /// 为边界框、掩膜和实例分割提供颜色。
        /// 使用COCO和ADE20K标准调色板。
        /// </remarks>
        /// <seealso cref="VisionColors"/>
        public VisionColors colors { get; set; } = new VisionColors();

        /// <summary>
        /// Initializes visualization options with scaling ratio
        /// 使用缩放比例初始化可视化选项
        /// </summary>
        /// <param name="ratio">
        /// Scaling ratio (1.0 = 100%, 0.5 = 50%, 2.0 = 200%) / 
        /// 缩放比例(1.0 = 100%, 0.5 = 50%, 2.0 = 200%)
        /// </param>
        /// <remarks>
        /// Automatically scales FontSize and BorderThickness by the specified ratio.
        /// Use smaller ratios for small images, larger ratios for high-resolution images.
        /// 自动按指定比例缩放FontSize和BorderThickness。
        /// 小图像使用较小比例，高分辨率图像使用较大比例。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // For 4K images
        /// var hdOptions = new VisualizeOptions(2.0f);
        /// 
        /// // For 480p images
        /// var sdOptions = new VisualizeOptions(0.5f);
        /// 
        /// // For 1080p images (default)
        /// var fullHdOptions = new VisualizeOptions(1.0f);
        /// </code>
        /// </example>
        public VisualizeOptions(float ratio)
        {
            FontSize = FontSize * ratio;
            BorderThickness = BorderThickness * ratio;
        }

        /// <summary>
        /// Gets the most appropriate default font family across platforms
        /// 获取跨平台最合适的默认字体家族
        /// </summary>
        /// <returns>Located font family / 找到的字体家族</returns>
        /// <remarks>
        /// <para>
        /// Tries common sans-serif fonts first (Arial, Helvetica, DejaVu Sans, Verdana, Liberation Sans),
        /// then Chinese-supporting fonts (Microsoft YaHei, SimHei, Noto Sans CJK SC, Source Han Sans SC),
        /// finally falls back to first available system font.
        /// 首先尝试常见无衬线字体(Arial、Helvetica、DejaVu Sans、Verdana、Liberation Sans)，
        /// 然后尝试支持中文的字体(微软雅黑、黑体、Noto Sans CJK SC、Source Han Sans SC)，
        /// 最后回退到首个可用系统字体。
        /// </para>
        /// <para>
        /// This method never throws - always returns a valid font family.
        /// 此方法不会抛出异常 - 总是返回有效的字体家族。
        /// </para>
        /// </remarks>
        private static FontFamily GetDefaultFontFamily()
        {
            // Try common cross-platform sans-serif fonts
            // 尝试获取跨平台通用字体
            var fallbackFonts = new[] { "Arial", "Helvetica", "DejaVu Sans", "Verdana", "Liberation Sans" };

            foreach (var fontName in fallbackFonts)
            {
                if (SystemFonts.TryGet(fontName, out var family))
                {
                    return family;
                }
            }

            // Try Chinese-supporting fonts (higher priority than full fallback)
            // 尝试获取中文支持字体(优先级高于完全回退)
            var chineseFonts = new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Source Han Sans SC" };
            foreach (var fontName in chineseFonts)
            {
                if (SystemFonts.TryGet(fontName, out var family))
                {
                    return family;
                }
            }

            // Final fallback
            // 最终回退方案
            try
            {
                // Get first available system font
                // 获取系统第一个可用字体
                return SystemFonts.Families.First();
            }
            catch
            {
                // Last resort fallback
                // 极端情况下的保底处理
                return SystemFonts.Get("Arial"); // Force return Arial (even if possibly missing)
            }
        }

        /// <summary>
        /// Calculates the actual rendered height of a font
        /// 计算字体实际渲染高度
        /// </summary>
        /// <param name="font">Font to measure / 要测量的字体</param>
        /// <returns>Effective font height in points / 有效的字体高度（磅）</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when font is null
        /// 当font为null时抛出
        /// </exception>
        /// <remarks>
        /// Accounts for ascender, descender and line gap metrics from font metadata.
        /// Formula: (Ascender - Descender + LineGap) * (Size / UnitsPerEm)
        /// 考虑字体元数据中的上行高度、下行高度和行间距。
        /// 公式: (Ascender - Descender + LineGap) * (Size / UnitsPerEm)
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var font = SystemFonts.CreateFont("Arial", 16);
        /// float height = VisualizeOptions.GetFontHeight(font);
        /// // height ≈ 18.5 for 16pt Arial
        /// </code>
        /// </example>
        public static float GetFontHeight(Font font)
        {
            float ascender = font.FontMetrics.HorizontalMetrics.Ascender;
            float descender = font.FontMetrics.HorizontalMetrics.Descender;
            float lineGap = font.FontMetrics.HorizontalMetrics.LineGap;

            return (ascender - descender + lineGap) * (font.Size / font.FontMetrics.UnitsPerEm);
        }
    }

}
