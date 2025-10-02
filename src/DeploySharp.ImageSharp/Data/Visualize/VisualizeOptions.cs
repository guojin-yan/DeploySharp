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
    /// Configuration options for visualization rendering
    /// 可视化渲染的配置选项
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides customizable settings for:
    /// 提供可自定义的设置:
    /// - Mask transparency and confidence thresholds
    ///   掩膜透明度和置信度阈值
    /// - Font styling and sizing
    ///   字体样式和大小
    /// - Drawing parameters like borders
    ///   绘制参数如边框
    /// </para>
    /// <para>
    /// Supports automatic scaling based on ratio for responsive visualization
    /// 支持基于比例的自适应缩放
    /// </para>
    /// </remarks>
    public class VisualizeOptions
    {
        /// <summary>
        /// Alpha transparency value for masks (0-1)
        /// 掩膜的透明度值(0-1)
        /// </summary>
        /// <value>Default: 0.5</value>
        public float MaskAlpha { get; set; } = 0.5f;

        /// <summary>
        /// Minimum confidence threshold for rendering masks
        /// 渲染掩膜的最低置信度阈值
        /// </summary>
        /// <value>Default: 0.5</value>
        public float MaskMinimumConfidence { get; set; } = 0.5f;

        /// <summary>
        /// Confidence threshold for rendering points
        /// 渲染关键点的置信度阈值
        /// </summary>
        /// <value>Default: 0.5</value>
        public float PointDrawThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Selected font family for text rendering
        /// 用于文本渲染的字体家族
        /// </summary>
        private FontFamily FontFamily { get; set; } = GetDefaultFontFamily();

        /// <summary>
        /// Base font size (will be scaled by ratio)
        /// 基础字体大小(会被比例缩放)
        /// </summary>
        /// <value>Default: 12</value>
        public float FontSize { get; set; } = 12f;

        /// <summary>
        /// Thickness of border lines (will be scaled by ratio)
        /// 边框线条粗细(会被比例缩放)
        /// </summary>
        /// <value>Default: 2</value>
        public float BorderThickness { get; set; } = 2;

        /// <summary>
        /// Generated font instance from selected family and size
        /// 根据选择的字体家族和大小生成的字体实例
        /// </summary>
        public Font FontType { get => FontFamily.CreateFont(FontSize); }

        /// <summary>
        /// Calculated height of the current font
        /// 当前字体的计算高度
        /// </summary>
        public float FontHeight { get => GetFontHeight(FontType); }

        /// <summary>
        /// Color provider for visualization elements
        /// 可视化元素的颜色提供器
        /// </summary>
        public VisionColors colors { get; set; } = new VisionColors();

        /// <summary>
        /// Initializes visualization options with scaling ratio
        /// 使用缩放比例初始化可视化选项
        /// </summary>
        /// <param name="ratio">Scaling ratio (1.0 = 100%)/缩放比例(1.0 = 100%)</param>
        /// <remarks>
        /// Automatically scales font size and border thickness
        /// 自动缩放字体大小和边框粗细
        /// </remarks>
        public VisualizeOptions(float ratio)
        {
            FontSize = FontSize * ratio;
            BorderThickness = BorderThickness * ratio;
        }

        /// <summary>
        /// Gets the most appropriate default font family across platforms
        /// 获取跨平台最合适的默认字体家族
        /// </summary>
        /// <returns>Located font family/找到的字体家族</returns>
        /// <remarks>
        /// <para>
        /// Tries common sans-serif fonts first, then Chinese-supporting fonts,
        /// finally falls back to first available system font
        /// 首先尝试常见无衬线字体，然后尝试支持中文的字体，
        /// 最后回退到首个可用系统字体
        /// </para>
        /// <para>
        /// Throws no exceptions - always returns a font (possibly Arial)
        /// 不会抛出异常 - 总是返回一个字体(可能是Arial)
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
        /// <param name="font">Font to measure/要测量的字体</param>
        /// <returns>Effective font height in points/有效的字体高度(点数)</returns>
        /// <remarks>
        /// Accounts for ascender, descender and line gap metrics
        /// 考虑了上行高度、下行高度和行间距
        /// </remarks>
        public static float GetFontHeight(Font font)
        {
            float ascender = font.FontMetrics.HorizontalMetrics.Ascender;
            float descender = font.FontMetrics.HorizontalMetrics.Descender;
            float lineGap = font.FontMetrics.HorizontalMetrics.LineGap;

            return (ascender - descender + lineGap) * (font.Size / font.FontMetrics.UnitsPerEm);
        }
    }

}
