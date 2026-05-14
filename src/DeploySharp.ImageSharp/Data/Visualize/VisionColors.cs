//using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.PixelFormats;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DeploySharp.Data
//{
//    /// <summary>
//    /// Computer vision color provider supporting automatic coloring for up to 80 class IDs
//    /// 计算机视觉颜色提供器，支持80个类别ID的自动配色
//    /// </summary>
//    /// <remarks>
//    /// <para>
//    /// Provides distinctive colors for different visualization purposes in computer vision tasks:
//    /// 为计算机视觉任务中的不同可视化目的提供独特的颜色:
//    /// - High-contrast bounding box colors for object detection
//    ///   用于目标检测的高对比度边界框颜色
//    /// - Semantic segmentation mask colors for pixel-level classification
//    ///   用于像素级分类的语义分割掩膜颜色
//    /// - Semi-transparent instance segmentation fill colors for overlapping objects
//    ///   用于重叠对象的半透明实例分割填充色
//    /// </para>
//    /// <para>
//    /// Color schemes follow common dataset standards:
//    /// 配色方案遵循常见的数据集标准:
//    /// - COCO (Common Objects in Context): 80-class palette optimized for object detection
//    ///   COCO (上下文中的常见对象): 为对象检测优化的80类调色板
//    /// - ADE20K: Semantic segmentation palette with distinct colors for scene parsing
//    ///   ADE20K: 用于场景解析的具有明显颜色的语义分割调色板
//    /// </para>
//    /// <para>
//    /// All colors are carefully selected for maximum distinguishability and visibility
//    /// across different backgrounds and lighting conditions.
//    /// 所有颜色都经过精心选择，以在不同背景和光照条件下实现最大的可区分性和可见性。
//    /// </para>
//    /// </remarks>
//    /// <example>
//    /// <code language="csharp">
//    /// var colors = new VisionColors();
//    /// 
//    /// // Get color for class 0 (person in COCO)
//    /// // 获取类别0的颜色(COCO中的person)
//    /// var personColor = colors.GetBoundingBoxColor(0);
//    /// 
//    /// // Get semi-transparent color for instance segmentation
//    /// // 获取用于实例分割的半透明颜色
//    /// var instanceColor = colors.GetInstanceColor(5, alpha: 128);
//    /// 
//    /// // Get semantic segmentation color
//    /// // 获取语义分割颜色
//    /// var skyColor = colors.GetMaskColor(2);
//    /// </code>
//    /// </example>
//    /// <seealso cref="VisualizeOptions"/>
//    /// <seealso cref="Visualize"/>
//    public class VisionColors
//    {
//        //------------------------- Base Color Schemes -------------------------
//        //------------------------- 基础配色方案 -------------------------

//        /// <summary>
//        /// COCO dataset 80-class standard color palette
//        /// COCO数据集80类别标准调色板
//        /// </summary>
//        /// <remarks>
//        /// Pre-generated palette optimized for high contrast and visibility.
//        /// Used for object detection bounding boxes and instance segmentation.
//        /// 预生成的针对高对比度和可见性优化的调色板。
//        /// 用于对象检测边界框和实例分割。
//        /// </remarks>
//        private readonly Rgba32[] _cocoPalette = GenerateCocoPalette();

//        /// <summary>
//        /// ADE20K dataset color palette for semantic segmentation
//        /// ADE20K数据集语义分割调色板
//        /// </summary>
//        /// <remarks>
//        /// Extended palette supporting more classes for scene parsing tasks.
//        /// 扩展调色板，支持更多类别用于场景解析任务。
//        /// </remarks>
//        private readonly Rgba32[] _ade20kPalette = GenerateAde20kPalette();

//        //------------------------- Public API -------------------------
//        //------------------------- 公共API -------------------------

//        /// <summary>
//        /// Gets bounding box color from COCO standard high-contrast palette
//        /// 从COCO标准高对比度调色板获取边界框颜色
//        /// </summary>
//        /// <param name="classId">
//        /// Class ID (0-79 for COCO dataset) / 类别ID (COCO数据集为0-79)
//        /// </param>
//        /// <param name="alpha">
//        /// Transparency value (0-255), default 255 (fully opaque) / 
//        /// 透明度值(0-255)，默认255(完全不透明)
//        /// </param>
//        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
//        /// <exception cref="ArgumentOutOfRangeException">
//        /// Thrown when alpha is not in range [0, 255]
//        /// 当alpha不在[0, 255]范围内时抛出
//        /// </exception>
//        /// <remarks>
//        /// Class IDs are automatically clamped to valid range using modulo operation.
//        /// 类别ID使用取模操作自动钳制到有效范围。
//        /// </remarks>
//        /// <example>
//        /// <code language="csharp">
//        /// var colors = new VisionColors();
//        /// 
//        /// // Get fully opaque color for person (class 0)
//        /// var personColor = colors.GetBoundingBoxColor(0);
//        /// 
//        /// // Get semi-transparent color
//        /// var transparentColor = colors.GetBoundingBoxColor(5, alpha: 128);
//        /// 
//        /// // Class ID wraps around for values > 79
//        /// var wrappedColor = colors.GetBoundingBoxColor(80); // Same as class 0
//        /// </code>
//        /// </example>
//        /// <seealso cref="GetInstanceColor"/>
//        public Color GetBoundingBoxColor(int classId, byte alpha = 255)
//        {
//            classId = SafeClassId(classId, 80);
//            Rgba32 color = _cocoPalette[classId];

//            // Create new color with specified alpha
//            // 构造带透明度的新颜色
//            return Color.FromRgba(color.R, color.G, color.B, alpha);
//        }

//        /// <summary>
//        /// Gets semantic segmentation mask color from ADE20K standard palette
//        /// 从ADE20K标准调色板获取语义分割掩膜颜色
//        /// </summary>
//        /// <param name="classId">Class ID for semantic class / 语义类别的类别ID</param>
//        /// <returns>RGBA color / RGBA颜色</returns>
//        /// <exception cref="ArgumentOutOfRangeException">
//        /// Thrown when classId is negative
//        /// 当classId为负数时抛出
//        /// </exception>
//        /// <remarks>
//        /// Uses ADE20K palette which supports more classes than COCO.
//        /// 使用比COCO支持更多类别的ADE20K调色板。
//        /// </remarks>
//        /// <example>
//        /// <code language="csharp">
//        /// var colors = new VisionColors();
//        /// 
//        /// // Common ADE20K class IDs:
//        /// // 0: background/wall
//        /// // 1: building
//        /// // 2: sky
//        /// // 3: floor
//        /// // 4: tree
//        /// var skyColor = colors.GetMaskColor(2);
//        /// </code>
//        /// </example>
//        public Color GetMaskColor(int classId)
//        {
//            classId = SafeClassId(classId, _ade20kPalette.Length - 1);
//            return _ade20kPalette[classId];
//        }

//        /// <summary>
//        /// Gets instance segmentation fill color (semi-transparent version of bounding box color)
//        /// 获取实例分割填充色（边界框颜色的半透明版本）
//        /// </summary>
//        /// <param name="instanceId">Instance ID (can be any integer) / 实例ID（可以是任意整数）</param>
//        /// <param name="alpha">
//        /// Transparency value (0-255), default 128 (50% transparent) / 
//        /// 透明度值(0-255)，默认128(50%透明)
//        /// </param>
//        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
//        /// <exception cref="ArgumentOutOfRangeException">
//        /// Thrown when alpha is not in range [0, 255]
//        /// 当alpha不在[0, 255]范围内时抛出
//        /// </exception>
//        /// <remarks>
//        /// Instance ID is wrapped to COCO palette range (0-79) using modulo.
//        /// This ensures consistent coloring for the same instance across frames.
//        /// 实例ID使用取模映射到COCO调色板范围(0-79)。
//        /// 这确保了同一实例在跨帧时颜色一致。
//        /// </remarks>
//        /// <example>
//        /// <code language="csharp">
//        /// var colors = new VisionColors();
//        /// 
//        /// // Get semi-transparent color for instance 5
//        /// var instance5Color = colors.GetInstanceColor(5);
//        /// 
//        /// // More transparent
//        /// var veryTransparent = colors.GetInstanceColor(5, alpha: 64);
//        /// 
//        /// // Less transparent
//        /// var barelyTransparent = colors.GetInstanceColor(5, alpha: 200);
//        /// </code>
//        /// </example>
//        /// <seealso cref="GetBoundingBoxColor"/>
//        public Color GetInstanceColor(int instanceId, byte alpha = 128)
//        {
//            return GetBoundingBoxColor(instanceId % 80, alpha);
//        }

//        //------------------------- Palette Generators -------------------------
//        //------------------------- 配色生成器 -------------------------

//        /// <summary>
//        /// Generates COCO dataset 80-class standard color palette
//        /// 生成COCO数据集80类别标准配色
//        /// </summary>
//        /// <returns>Array of 80 Rgba32 colors / 80个Rgba32颜色的数组</returns>
//        /// <remarks>
//        /// <para>
//        /// Colors are optimized for high visibility and contrast against various backgrounds.
//        /// The palette includes colors from different hues to maximize distinguishability.
//        /// 颜色针对各种背景的高可见性和对比度进行了优化。
//        /// 调色板包含不同色调的颜色以最大化可区分性。
//        /// </para>
//        /// <para>
//        /// Color distribution:
//        /// 颜色分布:
//        /// - Red-Yellow range (indices 0-4): #FF3838, #FF9D97, #FF701F, #FFB21D, #CFD231
//        ///   红-黄范围 (索引0-4)
//        /// - Green-Cyan range (indices 5-9): #48F90A, #92CC17, #3DDB86, #1A9334, #00D4BB
//        ///   绿-青范围 (索引5-9)
//        /// - Blue range (indices 10-14): #2C99A8, #00C2FF, #344593, #6473FF, #0018EC
//        ///   蓝色范围 (索引10-14)
//        /// - Purple-Pink range (indices 15-19): #8438FF, #520085, #CB38FF, #FF95C8, #FF37C7
//        ///   紫-粉范围 (索引15-19)
//        /// - Extended colors (indices 20-79): Additional high-contrast colors
//        ///   扩展颜色 (索引20-79): 额外的高对比度颜色
//        /// </para>
//        /// </remarks>
//        private static Rgba32[] GenerateCocoPalette()
//        {
//            string[] hexColors =
//            {
//            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow/红-黄
//            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan/绿-青
//            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue/蓝
//            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink/紫-粉
//            // Extended colors (ensures 80 distinct high-contrast colors)
//            // 扩展颜色（确保80个不重复的高对比色）
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

//            var colors = new Rgba32[80];
//            for (int i = 0; i < 80; i++)
//            {
//                colors[i] = Rgba32.ParseHex(hexColors[i % hexColors.Length]);
//            }
//            return colors;
//        }

//        /// <summary>
//        /// Generates ADE20K semantic segmentation standard color palette
//        /// 生成ADE20K语义分割标准配色
//        /// </summary>
//        /// <returns>Array of Rgba32 colors / Rgba32颜色数组</returns>
//        /// <remarks>
//        /// Similar to COCO palette but with extended range for semantic segmentation tasks.
//        /// 与COCO调色板类似，但为语义分割任务扩展了范围。
//        /// </remarks>
//        private static Rgba32[] GenerateAde20kPalette()
//        {
//            string[] hexColors =
//            {
//            "#FF3838", "#FF9D97", "#FF701F", "#FFB21D", "#CFD231", // Red-Yellow/红-黄
//            "#48F90A", "#92CC17", "#3DDB86", "#1A9334", "#00D4BB", // Green-Cyan/绿-青
//            "#2C99A8", "#00C2FF", "#344593", "#6473FF", "#0018EC", // Blue/蓝
//            "#8438FF", "#520085", "#CB38FF", "#FF95C8", "#FF37C7", // Purple-Pink/紫-粉
//            // Extended colors (high-contrast colors)
//            // 扩展颜色（高对比色）
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

//            var colors = new Rgba32[hexColors.Length];
//            for (int i = 0; i < hexColors.Length; i++)
//            {
//                colors[i] = Rgba32.ParseHex(hexColors[i]);
//            }
//            return colors;
//        }

//        //------------------------- Safety Boundary Handling -------------------------
//        //------------------------- 安全边界处理 -------------------------

//        /// <summary>
//        /// Ensures class ID falls within valid range [0, max]
//        /// 确保类别ID在有效范围[0, max]内
//        /// </summary>
//        /// <param name="classId">Input class ID (can be any integer) / 输入类别ID（可以是任意整数）</param>
//        /// <param name="max">Maximum allowed value (exclusive) / 允许的最大值（不包含）</param>
//        /// <returns>Clamped class ID in range [0, max-1] / 钳制后的类别ID，范围[0, max-1]</returns>
//        /// <remarks>
//        /// Manual implementation of Clamp functionality to avoid dependency on Math.Clamp (.NET Core 2.0+).
//        /// Negative values are clamped to 0, values >= max are wrapped using modulo.
//        /// Math.Clamp的手动实现，避免依赖.NET Core 2.0+的Math.Clamp。
//        /// 负值钳制为0，值>=max使用取模环绕。
//        /// </remarks>
//        private static int SafeClassId(int classId, int max)
//        {
//            // Manual implementation of Clamp functionality
//            // 手动实现Clamp功能
//            if (classId < 0) return 0;
//            if (classId > max) return max - 1;
//            return classId;
//        }
//    }

//}


using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeploySharp.Data
{
    /// <summary>
    /// Computer vision color provider supporting automatic coloring with customizable palettes
    /// 计算机视觉颜色提供器，支持自定义配色的自动配色
    /// </summary>
    public class VisionColors
    {
        //------------------------- Fields / 字段 -------------------------
        private readonly Rgba32[] _boundingBoxPalette;
        private readonly Rgba32[] _maskPalette;
        private readonly int _maxClasses;

        //------------------------- Static Default Palettes / 静态默认配色 -------------------------
        private static readonly Lazy<Rgba32[]> _defaultCocoPalette = new Lazy<Rgba32[]>(GenerateCocoPalette);
        private static readonly Lazy<Rgba32[]> _defaultAde20kPalette = new Lazy<Rgba32[]>(GenerateAde20kPalette);

        //------------------------- Constructors / 构造函数 -------------------------

        /// <summary>
        /// Initializes with default COCO and ADE20K palettes
        /// 使用默认的COCO和ADE20K配色初始化
        /// </summary>
        public VisionColors() : this(null, null, 80)
        {
        }

        /// <summary>
        /// Initializes with custom palettes
        /// 使用自定义配色初始化
        /// </summary>
        /// <param name="boundingBoxPalette">
        /// Custom bounding box color palette (null to use default COCO palette)
        /// 自定义边界框配色（null则使用默认COCO配色）
        /// </param>
        /// <param name="maskPalette">
        /// Custom mask color palette (null to use default ADE20K palette)
        /// 自定义掩膜配色（null则使用默认ADE20K配色）
        /// </param>
        /// <param name="maxClasses">
        /// Maximum number of classes for instance wrapping (default: 80)
        /// 实例映射的最大类别数（默认：80）
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when palettes are empty
        /// 当配色为空时抛出
        /// </exception>
        public VisionColors(Rgba32[] boundingBoxPalette, Rgba32[] maskPalette, int maxClasses = 80)
        {
            if (boundingBoxPalette != null && boundingBoxPalette.Length == 0)
                throw new ArgumentException("Bounding box palette cannot be empty", nameof(boundingBoxPalette));
            if (maskPalette != null && maskPalette.Length == 0)
                throw new ArgumentException("Mask palette cannot be empty", nameof(maskPalette));
            if (maxClasses <= 0)
                throw new ArgumentException("Max classes must be positive", nameof(maxClasses));

            _maxClasses = maxClasses;
            _boundingBoxPalette = boundingBoxPalette ?? _defaultCocoPalette.Value;
            _maskPalette = maskPalette ?? _defaultAde20kPalette.Value;
        }

        //------------------------- Public API / 公共API -------------------------

        /// <summary>
        /// Gets bounding box color from the palette
        /// 从调色板获取边界框颜色
        /// </summary>
        /// <param name="classId">Class ID (wrapped to palette range) / 类别ID（映射到调色板范围）</param>
        /// <param name="alpha">Transparency value (0-255), default 255 (opaque) / 透明度值(0-255)，默认255</param>
        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
        public Color GetBoundingBoxColor(int classId, byte alpha = 255)
        {
            classId = SafeClassId(classId, _boundingBoxPalette.Length);
            Rgba32 color = _boundingBoxPalette[classId];
            return Color.FromRgba(color.R, color.G, color.B, alpha);
        }

        /// <summary>
        /// Gets semantic segmentation mask color
        /// 获取语义分割掩膜颜色
        /// </summary>
        /// <param name="classId">Class ID for semantic class / 语义类别的类别ID</param>
        /// <returns>RGBA color / RGBA颜色</returns>
        public Color GetMaskColor(int classId)
        {
            classId = SafeClassId(classId, _maskPalette.Length);
            return _maskPalette[classId];
        }

        /// <summary>
        /// Gets instance segmentation fill color (semi-transparent version)
        /// 获取实例分割填充色（半透明版本）
        /// </summary>
        /// <param name="instanceId">Instance ID (can be any integer) / 实例ID（可以是任意整数）</param>
        /// <param name="alpha">Transparency value (0-255), default 128 (50% transparent) / 透明度值(0-255)，默认128</param>
        /// <returns>RGBA color with specified transparency / 指定透明度的RGBA颜色</returns>
        public Color GetInstanceColor(int instanceId, byte alpha = 128)
        {
            return GetBoundingBoxColor(instanceId % _maxClasses, alpha);
        }

        /// <summary>
        /// Gets random color from bounding box palette
        /// 从边界框配色中获取随机颜色
        /// </summary>
        /// <param name="alpha">Transparency value (0-255), default 255 / 透明度值(0-255)，默认255</param>
        /// <returns>Random RGBA color / 随机RGBA颜色</returns>
        public Color GetRandomColor(byte alpha = 255)
        {
            var random = new Random();
            var color = _boundingBoxPalette[random.Next(_boundingBoxPalette.Length)];
            return Color.FromRgba(color.R, color.G, color.B, alpha);
        }

        /// <summary>
        /// Updates bounding box color for a specific class
        /// 更新特定类别的边界框颜色
        /// </summary>
        /// <param name="classId">Class ID to update / 要更新的类别ID</param>
        /// <param name="color">New color / 新颜色</param>
        /// <returns>True if update succeeded, false if palette is read-only / 更新成功返回true，调色板只读返回false</returns>
        public bool SetBoundingBoxColor(int classId, Rgba32 color)
        {
            if (classId >= 0 && classId < _boundingBoxPalette.Length && !IsReadOnlyPalette(_boundingBoxPalette))
            {
                _boundingBoxPalette[classId] = color;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Updates mask color for a specific class
        /// 更新特定类别的掩膜颜色
        /// </summary>
        /// <param name="classId">Class ID to update / 要更新的类别ID</param>
        /// <param name="color">New color / 新颜色</param>
        /// <returns>True if update succeeded, false if palette is read-only / 更新成功返回true，调色板只读返回false</returns>
        public bool SetMaskColor(int classId, Rgba32 color)
        {
            if (classId >= 0 && classId < _maskPalette.Length && !IsReadOnlyPalette(_maskPalette))
            {
                _maskPalette[classId] = color;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current bounding box palette
        /// 获取当前边界框调色板
        /// </summary>
        /// <returns>Copy of the bounding box palette / 边界框调色板的副本</returns>
        public Rgba32[] GetBoundingBoxPalette()
        {
            return (Rgba32[])_boundingBoxPalette.Clone();
        }

        /// <summary>
        /// Gets the current mask palette
        /// 获取当前掩膜调色板
        /// </summary>
        /// <returns>Copy of the mask palette / 掩膜调色板的副本</returns>
        public Rgba32[] GetMaskPalette()
        {
            return (Rgba32[])_maskPalette.Clone();
        }

        /// <summary>
        /// Creates a new instance with modified bounding box palette
        /// 创建使用修改后的边界框调色板的新实例
        /// </summary>
        /// <param name="newPalette">New palette to use / 要使用的新调色板</param>
        /// <returns>New VisionColors instance / 新的VisionColors实例</returns>
        public VisionColors WithBoundingBoxPalette(Rgba32[] newPalette)
        {
            return new VisionColors(newPalette, _maskPalette, _maxClasses);
        }

        /// <summary>
        /// Creates a new instance with modified mask palette
        /// 创建使用修改后的掩膜调色板的新实例
        /// </summary>
        /// <param name="newPalette">New palette to use / 要使用的新调色板</param>
        /// <returns>New VisionColors instance / 新的VisionColors实例</returns>
        public VisionColors WithMaskPalette(Rgba32[] newPalette)
        {
            return new VisionColors(_boundingBoxPalette, newPalette, _maxClasses);
        }

        //------------------------- Static Color Generators / 静态配色生成器 -------------------------

        /// <summary>
        /// Generates COCO dataset 80-class standard color palette
        /// 生成COCO数据集80类别标准配色
        /// </summary>
        public static Rgba32[] GenerateCocoPalette()
        {
            string[] hexColors = GetStandardCocoColors();
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
        public static Rgba32[] GenerateAde20kPalette()
        {
            string[] hexColors = GetStandardAde20kColors();
            var colors = new Rgba32[hexColors.Length];
            for (int i = 0; i < hexColors.Length; i++)
            {
                colors[i] = Rgba32.ParseHex(hexColors[i]);
            }
            return colors;
        }

        /// <summary>
        /// Generates a uniform color palette with variations in hue
        /// 生成色调变化的均匀调色板
        /// </summary>
        /// <param name="count">Number of colors to generate / 生成颜色数量</param>
        /// <param name="saturation">Saturation (0-1) / 饱和度</param>
        /// <param name="lightness">Lightness (0-1) / 亮度</param>
        public static Rgba32[] GenerateHuePalette(int count, float saturation = 0.8f, float lightness = 0.6f)
        {
            var colors = new Rgba32[count];
            for (int i = 0; i < count; i++)
            {
                float hue = i / (float)count;
                var color = new Rgba32(
                    (byte)(hue * 255),
                    (byte)(saturation * 255),
                    (byte)(lightness * 255),
                    255
                );
                // Use HSV conversion for better color distribution
                colors[i] = HsvToRgba(hue, saturation, lightness);
            }
            return colors;
        }

        /// <summary>
        /// Generates a high-contrast color palette using golden ratio
        /// 使用黄金分割比例生成高对比度配色
        /// </summary>
        /// <param name="count">Number of colors to generate / 生成颜色数量</param>
        /// <param name="saturation">Saturation (0-1), default 0.8 / 饱和度</param>
        /// <param name="value">Value/Brightness (0-1), default 0.9 / 明度</param>
        public static Rgba32[] GenerateHighContrastPalette(int count, double saturation = 0.8, double value = 0.9)
        {
            var colors = new Rgba32[count];
            for (int i = 0; i < count; i++)
            {
                // Use golden ratio for even distribution
                double hue = (i * 0.618033988749895) % 1.0;
                colors[i] = HsvToRgba(hue, saturation, value);
            }
            return colors;
        }

        /// <summary>
        /// Creates a grayscale palette
        /// 创建灰度配色
        /// </summary>
        /// <param name="count">Number of colors to generate / 生成颜色数量</param>
        /// <param name="minBrightness">Minimum brightness (0-255), default 64 / 最小亮度</param>
        /// <param name="maxBrightness">Maximum brightness (0-255), default 224 / 最大亮度</param>
        public static Rgba32[] GenerateGrayscalePalette(int count, byte minBrightness = 64, byte maxBrightness = 224)
        {
            var colors = new Rgba32[count];
            for (int i = 0; i < count; i++)
            {
                byte intensity = (byte)(minBrightness + (i * (maxBrightness - minBrightness) / Math.Max(1, count - 1)));
                colors[i] = new Rgba32(intensity, intensity, intensity);
            }
            return colors;
        }

        /// <summary>
        /// Generates a categorical color palette optimized for maximum distinguishability
        /// 生成针对最大可区分性优化的分类调色板
        /// </summary>
        /// <param name="count">Number of colors needed / 所需颜色数量</param>
        public static Rgba32[] GenerateCategoricalPalette(int count)
        {
            // Use Paul Tol's vibrant color scheme for categorical data
            // 使用Paul Tol的鲜艳配色方案用于分类数据
            var baseColors = new[]
            {
                "#4477AA", "#66CCEE", "#228833", "#CCBB44", "#EE6677",
                "#AA3377", "#BBBBBB", "#000000", "#88CCEE", "#44AA99",
                "#999933", "#DDDDDD", "#661100", "#882255", "#AA4499"
            };

            var colors = new Rgba32[count];
            for (int i = 0; i < count; i++)
            {
                colors[i] = Rgba32.ParseHex(baseColors[i % baseColors.Length]);
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
            // ADE20K supports up to 150 classes, provide first 80 here
            return GetStandardCocoColors();
        }

        private static Rgba32 HsvToRgba(double hue, double saturation, double value)
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

            return new Rgba32(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255),
                255
            );
        }

        private static int SafeClassId(int classId, int maxLength)
        {
            if (classId < 0) return 0;
            if (classId >= maxLength) return classId % maxLength;
            return classId;
        }

        private static bool IsReadOnlyPalette(Rgba32[] palette)
        {
            // Check if this is one of the default palettes
            return palette == _defaultCocoPalette.Value || palette == _defaultAde20kPalette.Value;
        }
    }

    /// <summary>
    /// Predefined color palette presets for computer vision tasks
    /// 计算机视觉任务的预定义配色预设
    /// </summary>
    public static class VisionColorPresets
    {
        /// <summary>
        /// COCO dataset standard colors (80 classes)
        /// COCO数据集标准颜色（80类）
        /// </summary>
        public static VisionColors CocoPreset => new VisionColors();

        /// <summary>
        /// Pastel colors with reduced saturation for softer visualization
        /// 低饱和度的柔和色彩，用于更柔和的可视化
        /// </summary>
        public static VisionColors PastelPreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateHighContrastPalette(classCount, 0.5, 0.85);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Vibrant colors with high saturation for maximum contrast
        /// 高饱和度的鲜艳色彩，用于最大对比度
        /// </summary>
        public static VisionColors VibrantPreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateHighContrastPalette(classCount, 0.9, 0.95);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Grayscale colors from dark to light
        /// 从暗到亮的灰度色彩
        /// </summary>
        public static VisionColors GrayscalePreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateGrayscalePalette(classCount);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Categorical colors optimized for maximum distinguishability
        /// 为最大可区分性优化的分类颜色
        /// </summary>
        public static VisionColors CategoricalPreset(int classCount = 80)
        {
            var palette = VisionColors.GenerateCategoricalPalette(classCount);
            return new VisionColors(palette, palette, classCount);
        }

        /// <summary>
        /// Medical imaging friendly colors (distinguishable by color-blind individuals)
        /// 医学影像友好颜色（色盲人群可区分）
        /// </summary>
        public static VisionColors ColorBlindFriendlyPreset(int classCount = 80)
        {
            // Color-blind friendly palette (Okabe-Ito style)
            var colors = new Rgba32[]
            {
                Rgba32.ParseHex("#E69F00"), Rgba32.ParseHex("#56B4E9"),
                Rgba32.ParseHex("#009E73"), Rgba32.ParseHex("#F0E442"),
                Rgba32.ParseHex("#0072B2"), Rgba32.ParseHex("#D55E00"),
                Rgba32.ParseHex("#CC79A7"), Rgba32.ParseHex("#000000")
            };

            var palette = new Rgba32[classCount];
            for (int i = 0; i < classCount; i++)
            {
                palette[i] = colors[i % colors.Length];
            }
            return new VisionColors(palette, palette, classCount);
        }
    }
}