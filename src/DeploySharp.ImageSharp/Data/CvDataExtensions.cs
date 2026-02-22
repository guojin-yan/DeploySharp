using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides extension methods for converting between SixLabors.ImageSharp and DeploySharp CVData data structures
    /// 提供SixLabors.ImageSharp和DeploySharp CVData数据结构之间的转换扩展方法
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class helps bridge between SixLabors.ImageSharp and DeploySharp CVData types, enabling seamless integration
    /// of ImageSharp's powerful image processing capabilities with DeploySharp's computer vision data structures.
    /// 该类帮助在SixLabors.ImageSharp和DeploySharp CVData类型之间建立桥梁，实现ImageSharp强大的图像处理功能
    /// 与DeploySharp计算机视觉数据结构的无缝集成。
    /// </para>
    /// <para>
    /// Supported conversions include:
    /// 支持的转换包括:
    /// - Point/PointF types: Converting between ImageSharp and CVData point representations
    ///   点/浮点类型: 在ImageSharp和CVData点表示之间转换
    /// - Size/SizeF types: Converting dimensions and scales between formats
    ///   尺寸/浮点尺寸类型: 在格式之间转换尺寸和比例
    /// - Rectangle/Rect types: Converting bounding box representations
    ///   矩形类型: 转换边界框表示
    /// - Image data conversion: Converting between ImageSharp Image&lt;Rgb24&gt; and ImageDataB
    ///   图像数据转换: 在ImageSharp Image&lt;Rgb24&gt;和ImageDataB之间转换
    /// </para>
    /// <para>
    /// These conversions are essential for:
    /// 这些转换对于以下场景至关重要:
    /// - Loading images with ImageSharp and processing with DeploySharp models
    ///   使用ImageSharp加载图像并用DeploySharp模型处理
    /// - Visualizing model outputs using ImageSharp's drawing capabilities
    ///   使用ImageSharp的绘制功能可视化模型输出
    /// - Integrating with existing ImageSharp-based image processing pipelines
    ///   与现有的基于ImageSharp的图像处理流水线集成
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Load an image using ImageSharp
    /// // 使用ImageSharp加载图像
    /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
    /// 
    /// // Convert to ImageDataB for model input
    /// // 转换为ImageDataB作为模型输入
    /// var imageData = image.ToImageDataB();
    /// 
    //  // Process with model...
    /// // 用模型处理...
    /// 
    /// // Convert result back to ImageSharp for visualization
    /// // 将结果转换回ImageSharp用于可视化
    /// using var resultImage = imageData.ToImage();
    /// resultImage.Save("output.jpg");
    /// </code>
    /// </example>
    /// <seealso cref="ImageDataB"/>
    /// <seealso cref="Image{Rgb24}"/>
    public static class CvDataExtensions
    {
        /// <summary>
        /// Converts SixLabors.ImageSharp PointF to DeploySharp CVData PointF
        /// 将SixLabors.ImageSharp的PointF转换为DeploySharp CVData的PointF
        /// </summary>
        /// <param name="point">SixLabors.ImageSharp point to convert / 要转换的SixLabors.ImageSharp点</param>
        /// <returns>DeploySharp CVData point / DeploySharp CVData点</returns>
        /// <remarks>
        /// Performs direct coordinate mapping without any transformation.
        /// Both X and Y coordinates are preserved exactly.
        /// 执行直接坐标映射，不进行任何变换。
        /// X和Y坐标被精确保留。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var imageSharpPoint = new SixLabors.ImageSharp.PointF(100.5f, 200.75f);
        /// var cvPoint = CvDataExtensions.ToCvPointF(imageSharpPoint);
        /// // cvPoint.X = 100.5, cvPoint.Y = 200.75
        /// </code>
        /// </example>
        public static PointF ToCvPointF(SixLabors.ImageSharp.PointF point)
        {
            return new PointF(point.X, point.Y);
        }

        /// <summary>
        /// Converts DeploySharp CVData PointF to SixLabors.ImageSharp PointF
        /// 将DeploySharp CVData的PointF转换为SixLabors.ImageSharp的PointF
        /// </summary>
        /// <param name="point">DeploySharp CVData point to convert / 要转换的DeploySharp CVData点</param>
        /// <returns>SixLabors.ImageSharp point / SixLabors.ImageSharp点</returns>
        /// <remarks>
        /// Performs direct coordinate mapping without any transformation.
        /// Both X and Y coordinates are preserved exactly.
        /// 执行直接坐标映射，不进行任何变换。
        /// X和Y坐标被精确保留。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var cvPoint = new DeploySharp.Data.PointF(100.5f, 200.75f);
        /// var imageSharpPoint = CvDataExtensions.ToPointF(cvPoint);
        /// // imageSharpPoint.X = 100.5, imageSharpPoint.Y = 200.75
        /// </code>
        /// </example>
        public static SixLabors.ImageSharp.PointF ToPointF(PointF point)
        {
            return new SixLabors.ImageSharp.PointF(point.X, point.Y);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Point to DeploySharp CVData Point
        /// 将SixLabors.ImageSharp的Point转换为DeploySharp CVData的Point
        /// </summary>
        /// <param name="point">SixLabors.ImageSharp point to convert / 要转换的SixLabors.ImageSharp点</param>
        /// <returns>DeploySharp CVData point / DeploySharp CVData点</returns>
        /// <remarks>
        /// Performs direct integer coordinate mapping.
        /// Useful for pixel-perfect operations like drawing or mask manipulation.
        /// 执行直接整数坐标映射。
        /// 适用于像素级精确操作，如绘制或掩膜操作。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var imageSharpPoint = new SixLabors.ImageSharp.Point(100, 200);
        /// var cvPoint = CvDataExtensions.ToCvPoint(imageSharpPoint);
        /// // cvPoint.X = 100, cvPoint.Y = 200
        /// </code>
        /// </example>
        public static Point ToCvPoint(SixLabors.ImageSharp.Point point)
        {
            return new Point(point.X, point.Y);
        }

        /// <summary>
        /// Converts DeploySharp CVData Point to SixLabors.ImageSharp Point
        /// 将DeploySharp CVData的Point转换为SixLabors.ImageSharp的Point
        /// </summary>
        /// <param name="point">DeploySharp CVData point to convert / 要转换的DeploySharp CVData点</param>
        /// <returns>SixLabors.ImageSharp point / SixLabors.ImageSharp点</returns>
        /// <remarks>
        /// Performs direct integer coordinate mapping.
        /// Coordinates are truncated if they contain fractional parts.
        /// 执行直接整数坐标映射。
        /// 如果坐标包含小数部分，将被截断。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var cvPoint = new DeploySharp.Data.Point(100, 200);
        /// var imageSharpPoint = CvDataExtensions.ToPoint(cvPoint);
        /// // imageSharpPoint.X = 100, imageSharpPoint.Y = 200
        /// </code>
        /// </example>
        public static SixLabors.ImageSharp.Point ToPoint(Point point)
        {
            return new SixLabors.ImageSharp.Point(point.X, point.Y);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp SizeF to DeploySharp CVData SizeF
        /// 将SixLabors.ImageSharp的SizeF转换为DeploySharp CVData的SizeF
        /// </summary>
        /// <param name="size">SixLabors.ImageSharp size to convert / 要转换的SixLabors.ImageSharp尺寸</param>
        /// <returns>DeploySharp CVData size / DeploySharp CVData尺寸</returns>
        /// <remarks>
        /// Preserves both Width and Height as floating-point values.
        /// Commonly used when working with scaled or normalized dimensions.
        /// 将宽度和高度都保留为浮点值。
        /// 常用于处理缩放或归一化尺寸的场景。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var imageSharpSize = new SixLabors.ImageSharp.SizeF(1920.5f, 1080.25f);
        /// var cvSize = CvDataExtensions.ToCvSizeF(imageSharpSize);
        /// // cvSize.Width = 1920.5, cvSize.Height = 1080.25
        /// </code>
        /// </example>
        public static SizeF ToCvSizeF(SixLabors.ImageSharp.SizeF size)
        {
            return new SizeF(size.Width, size.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData SizeF to SixLabors.ImageSharp SizeF
        /// 将DeploySharp CVData的SizeF转换为SixLabors.ImageSharp的SizeF
        /// </summary>
        /// <param name="size">DeploySharp CVData size to convert / 要转换的DeploySharp CVData尺寸</param>
        /// <returns>SixLabors.ImageSharp size / SixLabors.ImageSharp尺寸</returns>
        /// <remarks>
        /// Preserves both Width and Height as floating-point values.
        /// Useful for specifying resize parameters or aspect ratios.
        /// 将宽度和高度都保留为浮点值。
        /// 适用于指定调整大小参数或宽高比。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var cvSize = new DeploySharp.Data.SizeF(1920.5f, 1080.25f);
        /// var imageSharpSize = CvDataExtensions.ToSizeF(cvSize);
        /// // imageSharpSize.Width = 1920.5, imageSharpSize.Height = 1080.25
        /// </code>
        /// </example>
        public static SixLabors.ImageSharp.SizeF ToSizeF(SizeF size)
        {
            return new SixLabors.ImageSharp.SizeF(size.Width, size.Height);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Size to DeploySharp CVData Size
        /// 将SixLabors.ImageSharp的Size转换为DeploySharp CVData的Size
        /// </summary>
        /// <param name="size">SixLabors.ImageSharp size to convert / 要转换的SixLabors.ImageSharp尺寸</param>
        /// <returns>DeploySharp CVData size / DeploySharp CVData尺寸</returns>
        /// <remarks>
        /// Preserves Width and Height as integer values.
        /// Used for exact pixel dimensions like image sizes or crop regions.
        /// 将宽度和高度保留为整数值。
        /// 用于精确的像素尺寸，如图像大小或裁剪区域。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var imageSharpSize = new SixLabors.ImageSharp.Size(1920, 1080);
        /// var cvSize = CvDataExtensions.ToCvSize(imageSharpSize);
        /// // cvSize.Width = 1920, cvSize.Height = 1080
        /// </code>
        /// </example>
        public static Size ToCvSize(SixLabors.ImageSharp.Size size)
        {
            return new Size(size.Width, size.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData Size to SixLabors.ImageSharp Size
        /// 将DeploySharp CVData的Size转换为SixLabors.ImageSharp的Size
        /// </summary>
        /// <param name="size">DeploySharp CVData size to convert / 要转换的DeploySharp CVData尺寸</param>
        /// <returns>SixLabors.ImageSharp size / SixLabors.ImageSharp尺寸</returns>
        /// <remarks>
        /// Preserves Width and Height as integer values.
        /// Commonly used when creating new ImageSharp images or specifying resize options.
        /// 将宽度和高度保留为整数值。
        /// 常用于创建新的ImageSharp图像或指定调整大小选项。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var cvSize = new DeploySharp.Data.Size(1920, 1080);
        /// var imageSharpSize = CvDataExtensions.ToSize(cvSize);
        /// // imageSharpSize.Width = 1920, imageSharpSize.Height = 1080
        /// </code>
        /// </example>
        public static SixLabors.ImageSharp.Size ToSize(Size size)
        {
            return new SixLabors.ImageSharp.Size(size.Width, size.Height);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Rectangle to DeploySharp CVData Rect
        /// 将SixLabors.ImageSharp的Rectangle转换为DeploySharp CVData的Rect
        /// </summary>
        /// <param name="rect">SixLabors.ImageSharp rectangle to convert / 要转换的SixLabors.ImageSharp矩形</param>
        /// <returns>DeploySharp CVData rectangle / DeploySharp CVData矩形</returns>
        /// <remarks>
        /// Maps Rectangle (X, Y, Width, Height) to Rect with calculated corners.
        /// Top-left corner is (X, Y), bottom-right is (X+Width, Y+Height).
        /// 将Rectangle (X, Y, Width, Height)映射到带计算角的Rect。
        /// 左上角为(X, Y)，右下角为(X+Width, Y+Height)。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var imageSharpRect = new SixLabors.ImageSharp.Rectangle(100, 100, 200, 150);
        /// var cvRect = CvDataExtensions.ToCvRect(imageSharpRect);
        /// // cvRect.TopLeft = (100, 100), cvRect.BottomRight = (300, 250)
        /// </code>
        /// </example>
        public static Rect ToCvRect(SixLabors.ImageSharp.Rectangle rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData Rect to SixLabors.ImageSharp Rectangle
        /// 将DeploySharp CVData的Rect转换为SixLabors.ImageSharp的Rectangle
        /// </summary>
        /// <param name="rect">DeploySharp CVData rectangle to convert / 要转换的DeploySharp CVData矩形</param>
        /// <returns>SixLabors.ImageSharp rectangle / SixLabors.ImageSharp矩形</returns>
        /// <remarks>
        /// Maps Rect with corners to Rectangle (X, Y, Width, Height).
        /// Width is calculated as BottomRight.X - TopLeft.X.
        /// Height is calculated as BottomRight.Y - TopLeft.Y.
        /// 将带角的Rect映射到Rectangle (X, Y, Width, Height)。
        /// 宽度计算为BottomRight.X - TopLeft.X。
        /// 高度计算为BottomRight.Y - TopLeft.Y。
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var cvRect = new DeploySharp.Data.Rect(100, 100, 200, 150);
        /// var imageSharpRect = CvDataExtensions.ToRect(cvRect);
        /// // imageSharpRect.X = 100, imageSharpRect.Y = 100
        /// // imageSharpRect.Width = 200, imageSharpRect.Height = 150
        /// </code>
        /// </example>
        public static SixLabors.ImageSharp.Rectangle ToRect(Rect rect)
        {
            return new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Converts ImageDataB to ImageSharp Image&lt;Rgb24&gt;
        /// 将ImageDataB转换为ImageSharp的Image&lt;Rgb24&gt;
        /// </summary>
        /// <param name="imageData">Source image data buffer / 源图像数据缓冲区</param>
        /// <returns>ImageSharp RGB image / ImageSharp RGB图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when imageData is null
        /// 当imageData为null时抛出
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when image dimensions are invalid (width or height less than or equal to 0)
        /// 当图像尺寸无效时抛出（宽度或高度小于等于0）
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when channel count is not 1, 3, or 4
        /// 当通道数不是1、3或4时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Supports the following channel configurations:
        /// 支持以下通道配置:
        /// - 1 channel (Grayscale): Converted to RGB by duplicating the single channel
        ///   1通道（灰度）: 通过复制单通道转换为RGB
        /// - 3 channels (RGB): Direct mapping to Rgb24 format
        ///   3通道（RGB）: 直接映射到Rgb24格式
        /// - 4 channels (RGBA): Alpha channel is discarded, RGB is preserved
        ///   4通道（RGBA）: 丢弃Alpha通道，保留RGB
        /// </para>
        /// <para>
        /// The returned image is a new instance; modifications do not affect the original ImageDataB.
        /// 返回的图像是新实例；修改不会影响原始的ImageDataB。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Convert from model output to displayable image
        /// // 从模型输出转换为可显示的图像
        /// ImageDataB resultData = model.Process(inputData);
        /// using var displayImage = resultData.ToImage();
        /// displayImage.Save("result.jpg");
        /// 
        /// // Handle grayscale output
        /// // 处理灰度输出
        /// var grayData = new ImageDataB(grayBytes, width, height, 1);
        /// using var rgbImage = grayData.ToImage(); // Converted to RGB
        /// </code>
        /// </example>
        /// <seealso cref="ImageDataB"/>
        /// <seealso cref="Image{Rgb24}"/>
        public static Image<Rgb24> ToImage(this ImageDataB imageData)
        {
            if (imageData == null)
                throw new ArgumentNullException(nameof(imageData));
            if (imageData.Width <= 0 || imageData.Height <= 0)
                throw new ArgumentException("Image dimensions must be positive");

            byte[] rawData = imageData.GetRawData();

            return imageData.Channels switch
            {
                1 => Image.LoadPixelData<L8>(rawData, imageData.Width, imageData.Height).CloneAs<Rgb24>(),
                3 => Image.LoadPixelData<Rgb24>(rawData, imageData.Width, imageData.Height),
                4 => Image.LoadPixelData<Rgba32>(rawData, imageData.Width, imageData.Height).CloneAs<Rgb24>(),
                _ => throw new NotSupportedException($"Unsupported channel count: {imageData.Channels}")
            };
        }

        /// <summary>
        /// Converts ImageSharp Image&lt;Rgb24&gt; to ImageDataB
        /// 将ImageSharp的Image&lt;Rgb24&gt;转换为ImageDataB
        /// </summary>
        /// <param name="image">Source RGB image / 源RGB图像</param>
        /// <returns>Image data buffer with 3 channels / 3通道的图像数据缓冲区</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image is null
        /// 当image为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Extracts raw pixel data from the ImageSharp image in RGB format.
        /// The returned ImageDataB contains a copy of the pixel data.
        /// 以RGB格式从ImageSharp图像提取原始像素数据。
        /// 返回的ImageDataB包含像素数据的副本。
        /// </para>
        /// <para>
        /// Data layout: [R0, G0, B0, R1, G1, B1, ...] where each pixel occupies 3 consecutive bytes.
        /// 数据布局: [R0, G0, B0, R1, G1, B1, ...]，每个像素占用3个连续字节。
        /// </para>
        /// <para>
        /// This is the primary method for preparing image data for model inference.
        /// 这是为模型推理准备图像数据的主要方法。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Load and prepare image for inference
        /// // 加载并准备图像进行推理
        /// using var image = Image.Load&lt;Rgb24&gt;("input.jpg");
        /// var imageData = image.ToImageDataB();
        /// 
        /// // Use with model
        /// // 与模型一起使用
        /// var results = model.Predict(imageData);
        /// 
        /// // Or convert to tensor
        /// // 或转换为张量
        /// float[] tensor = CvDataProcessor.ProcessToFloat(image, 
        ///     new Size(640, 640), processorConfig);
        /// </code>
        /// </example>
        /// <seealso cref="ImageDataB"/>
        /// <seealso cref="Image{Rgb24}"/>
        /// <seealso cref="CvDataProcessor.ProcessToFloat(Image{Rgb24}, Size, DataProcessorConfig)"/>
        public static ImageDataB ToImageDataB(this Image<Rgb24> image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            byte[] pixelData = new byte[image.Width * image.Height * 3];
            image.CopyPixelDataTo(pixelData);

            return new ImageDataB(pixelData, image.Width, image.Height, 3);
        }
    }

}
