using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public class VisualizeOptions
    {
        public float MaskAlpha { get; set; } = 0.5f;
        public float MaskMinimumConfidence { get; set; } = 0.5f;
        public float PointDrawThreshold{ get; set; } = 0.5f;
        private FontFamily FontFamily { get; set; } = GetDefaultFontFamily();

        public float FontSize { get; set; } = 12f;

        public float BorderThickness { get; set; } = 2;

        public Font FontType { get =>  FontFamily.CreateFont(FontSize); }
        public float FontHeight { get => GetFontHeight(FontType); }

        public VisionColors colors { get; set; } = new VisionColors();

        public VisualizeOptions(float ratio)
        {
            FontSize = FontSize * ratio;
            BorderThickness = BorderThickness * ratio;
        }

        private static FontFamily GetDefaultFontFamily()
        {
            // 尝试获取跨平台通用字体
            var fallbackFonts = new[] { "Arial", "Helvetica", "DejaVu Sans", "Verdana", "Liberation Sans" };

            foreach (var fontName in fallbackFonts)
            {
                if (SystemFonts.TryGet(fontName, out var family))
                {
                    return family;
                }
            }

            // 尝试获取中文支持字体（优先级高于完全回退）
            var chineseFonts = new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Source Han Sans SC" };
            foreach (var fontName in chineseFonts)
            {
                if (SystemFonts.TryGet(fontName, out var family))
                {
                    return family;
                }
            }

            // 最终回退方案
            try
            {
                // 获取系统第一个可用字体
                return SystemFonts.Families.First();
            }
            catch
            {
                // 极端情况下的保底处理
                return SystemFonts.Get("Arial"); // 强制返回Arial（即使可能不存在）
            }
        }

        public static float GetFontHeight(Font font)
        {
            float ascender = font.FontMetrics.HorizontalMetrics.Ascender;
            float descender = font.FontMetrics.HorizontalMetrics.Descender;
            float lineGap = font.FontMetrics.HorizontalMetrics.LineGap;

            return (ascender - descender + lineGap) * (font.Size / font.FontMetrics.UnitsPerEm);
        }
    }
}
