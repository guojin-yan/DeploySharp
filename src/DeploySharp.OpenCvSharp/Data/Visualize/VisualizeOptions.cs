using OpenCvSharp;
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
        //------------------------- 可配置选项 -------------------------
        public float MaskAlpha { get; set; } = 0.5f;
        public float MaskMinimumConfidence { get; set; } = 0.5f;
        public float FontSize { get; set; } = 0.5f;
        public float BorderThickness { get; set; } = 2;
        public HersheyFonts FontType { get; set; } = HersheyFonts.HersheySimplex; // OpenCV的字体类型
        public VisionColors Colors { get; set; } = new VisionColors();

        // 字体高度估算值 (基于OpenCV字体特性)
        public int FontHeight
        {
            get => (int)(FontSize * 40); // 经验值近似计算
        }

        //------------------------- 构造函数 -------------------------
        public VisualizeOptions(float ratio)
        {
            FontSize = FontSize * ratio;
            BorderThickness = BorderThickness * ratio;
        }


    }
}
