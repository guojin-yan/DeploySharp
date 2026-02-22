using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Configuration options for visualization of computer vision results.
    /// 计算机视觉结果可视化的配置选项。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provides customizable parameters for drawing detection results, segmentation masks,
    /// keypoints, and text annotations on images.
    /// 提供可定制的参数，用于在图像上绘制检测结果、分割掩膜、关键点和文本注释。
    /// </para>
    /// <para>
    /// All size-related properties are automatically scaled based on the ratio parameter
    /// provided in the constructor.
    /// 所有与尺寸相关的属性会根据构造函数中提供的ratio参数自动缩放。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create visualization options with 1.0x scale
    /// // 创建1.0倍缩放的可视化选项
    /// var options = new VisualizeOptions(1.0f);
    /// 
    /// // Customize specific options
    /// // 自定义特定选项
    /// options.MaskAlpha = 0.6f;        // More transparent masks / 更透明的掩膜
    /// options.BorderThickness = 3;     // Thicker borders / 更粗的边框
    /// options.FontSize = 0.7f;         // Larger text / 更大的文字
    /// 
    /// // Use for visualization
    /// // 用于可视化
    /// Mat result = Visualize.DrawDetResult(detections, image, options);
    /// </code>
    /// </example>
    /// <seealso cref="Visualize"/>
    /// <seealso cref="VisionColors"/>
    public class VisualizeOptions
    {
        //------------------------- Configurable Options / 可配置选项 -------------------------

        /// <summary>
        /// Alpha transparency for segmentation masks (0.0 - 1.0).
        /// 分割掩膜的Alpha透明度(0.0 - 1.0)。
        /// </summary>
        /// <remarks>
        /// Default value: 0.5 (50% transparent)
        /// 默认值：0.5（50%透明）
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new VisualizeOptions(1.0f);
        /// options.MaskAlpha = 0.3f;  // More transparent / 更透明
        /// </code>
        /// </example>
        public float MaskAlpha { get; set; } = 0.5f;

        /// <summary>
        /// Minimum confidence threshold for displaying segmentation masks.
        /// 显示分割掩膜的最小置信度阈值。
        /// </summary>
        /// <remarks>
        /// Pixels with mask confidence below this threshold will not be displayed.
        /// 掩膜置信度低于此阈值的像素将不显示。
        /// Default: 0.5
        /// 默认：0.5
        /// </remarks>
        public float MaskMinConfidence { get; set; } = 0.5f;

        /// <summary>
        /// Minimum confidence threshold for displaying keypoints.
        /// 显示关键点的最小置信度阈值。
        /// </summary>
        /// <remarks>
        /// Keypoints with confidence below this threshold will not be drawn.
        /// 置信度低于此阈值的关键点将不绘制。
        /// Default: 0.5
        /// 默认：0.5
        /// </remarks>
        public float KeyPointMinConfidence { get; set; } = 0.5f;

        /// <summary>
        /// Font size for text annotations.
        /// 文本注释的字体大小。
        /// </summary>
        /// <remarks>
        /// This is a relative scale factor for OpenCV font rendering.
        /// 这是OpenCV字体渲染的相对缩放因子。
        /// Default: 0.5
        /// 默认：0.5
        /// </remarks>
        public float FontSize { get; set; } = 0.5f;

        /// <summary>
        /// Border thickness for bounding boxes (in pixels).
        /// 边界框的边框粗细(像素)。
        /// </summary>
        /// <remarks>
        /// Automatically scaled by the ratio parameter in constructor.
        /// 自动根据构造函数中的ratio参数缩放。
        /// Default: 2
        /// 默认：2
        /// </remarks>
        public float BorderThickness { get; set; } = 2;

        /// <summary>
        /// Font type for text annotations.
        /// 文本注释的字体类型。
        /// </summary>
        /// <remarks>
        /// OpenCV font types. Default: HersheySimplex
        /// OpenCV字体类型。默认：HersheySimplex
        /// </remarks>
        /// <seealso cref="HersheyFonts"/>
        public HersheyFonts FontType { get; set; } = HersheyFonts.HersheySimplex;

        /// <summary>
        /// Color provider for visualization.
        /// 可视化的颜色提供器。
        /// </summary>
        /// <remarks>
        /// Provides colors for different classes and instances.
        /// 为不同类别和实例提供颜色。
        /// </remarks>
        /// <seealso cref="VisionColors"/>
        public VisionColors Colors { get; set; } = new VisionColors();

        /// <summary>
        /// Estimated font height in pixels (based on OpenCV font characteristics).
        /// 估算的字体高度(像素)(基于OpenCV字体特性)。
        /// </summary>
        /// <remarks>
        /// Calculated as FontSize * 40. Used for positioning text labels.
        /// 计算为FontSize * 40。用于定位文本标签。
        /// </remarks>
        public int FontHeight
        {
            get => (int)(FontSize * 40);
        }

        //------------------------- Constructors / 构造函数 -------------------------

        /// <summary>
        /// Creates visualization options with specified scale ratio.
        /// 使用指定的缩放比例创建可视化选项。
        /// </summary>
        /// <param name="ratio">Scale ratio for size-related properties / 尺寸相关属性的缩放比例</param>
        /// <remarks>
        /// The ratio parameter scales FontSize and BorderThickness proportionally.
        /// ratio参数按比例缩放FontSize和BorderThickness。
        /// </remarks>
        /// <example>
        /// <code>
        /// // For high-resolution images
        /// // 用于高分辨率图像
        /// var highResOptions = new VisualizeOptions(1.5f);
        /// 
        /// // For thumbnail-sized images
        /// // 用于缩略图大小的图像
        /// var smallOptions = new VisualizeOptions(0.5f);
        /// </code>
        /// </example>
        public VisualizeOptions(float ratio)
        {
            FontSize = FontSize * ratio;
            BorderThickness = BorderThickness * ratio;
        }
    }
}
