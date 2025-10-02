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
    /// This class helps bridge between SixLabors.ImageSharp and DeploySharp CVData types
    /// 该类帮助在SixLabors.ImageSharp和DeploySharp CVData类型之间建立桥梁
    /// </para>
    /// <para>
    /// Supported conversions include:
    /// 支持的转换包括:
    /// - Point/PointF types  点/浮点类型
    /// - Size/SizeF types    尺寸/浮点尺寸类型  
    /// - Rectangle/Rect types 矩形类型
    /// - Image data conversion 图像数据转换
    /// </para>
    /// </remarks>
    public static class CvDataExtensions
    {
        /// <summary>
        /// Converts SixLabors.ImageSharp PointF to DeploySharp CVData PointF
        /// 将SixLabors.ImageSharp的PointF转换为DeploySharp CVData的PointF
        /// </summary>
        /// <param name="point">SixLabors.ImageSharp point/SixLabors.ImageSharp点</param>
        /// <returns>DeploySharp CVData point/DeploySharp CVData点</returns>
        public static PointF ToCvPointF(SixLabors.ImageSharp.PointF point)
        {
            return new PointF(point.X, point.Y);
        }

        /// <summary>
        /// Converts DeploySharp CVData PointF to SixLabors.ImageSharp PointF
        /// 将DeploySharp CVData的PointF转换为SixLabors.ImageSharp的PointF
        /// </summary>
        /// <param name="point">DeploySharp CVData point/DeploySharp CVData点</param>
        /// <returns>SixLabors.ImageSharp point/SixLabors.ImageSharp点</returns>
        public static SixLabors.ImageSharp.PointF ToPointF(PointF point)
        {
            return new SixLabors.ImageSharp.PointF(point.X, point.Y);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Point to DeploySharp CVData Point
        /// 将SixLabors.ImageSharp的Point转换为DeploySharp CVData的Point
        /// </summary>
        /// <param name="point">SixLabors.ImageSharp point/SixLabors.ImageSharp点</param>
        /// <returns>DeploySharp CVData point/DeploySharp CVData点</returns>
        public static Point ToCvPoint(SixLabors.ImageSharp.Point point)
        {
            return new Point(point.X, point.Y);
        }

        /// <summary>
        /// Converts DeploySharp CVData Point to SixLabors.ImageSharp Point
        /// 将DeploySharp CVData的Point转换为SixLabors.ImageSharp的Point
        /// </summary>
        /// <param name="point">DeploySharp CVData point/DeploySharp CVData点</param>
        /// <returns>SixLabors.ImageSharp point/SixLabors.ImageSharp点</returns>
        public static SixLabors.ImageSharp.Point ToPoint(Point point)
        {
            return new SixLabors.ImageSharp.Point(point.X, point.Y);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp SizeF to DeploySharp CVData SizeF
        /// 将SixLabors.ImageSharp的SizeF转换为DeploySharp CVData的SizeF
        /// </summary>
        /// <param name="size">SixLabors.ImageSharp size/SixLabors.ImageSharp尺寸</param>
        /// <returns>DeploySharp CVData size/DeploySharp CVData尺寸</returns>
        public static SizeF ToCvSizeF(SixLabors.ImageSharp.SizeF size)
        {
            return new SizeF(size.Width, size.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData SizeF to SixLabors.ImageSharp SizeF
        /// 将DeploySharp CVData的SizeF转换为SixLabors.ImageSharp的SizeF
        /// </summary>
        /// <param name="size">DeploySharp CVData size/DeploySharp CVData尺寸</param>
        /// <returns>SixLabors.ImageSharp size/SixLabors.ImageSharp尺寸</returns>
        public static SixLabors.ImageSharp.SizeF ToSizeF(SizeF size)
        {
            return new SixLabors.ImageSharp.SizeF(size.Width, size.Height);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Size to DeploySharp CVData Size
        /// 将SixLabors.ImageSharp的Size转换为DeploySharp CVData的Size
        /// </summary>
        /// <param name="size">SixLabors.ImageSharp size/SixLabors.ImageSharp尺寸</param>
        /// <returns>DeploySharp CVData size/DeploySharp CVData尺寸</returns>
        public static Size ToCvSize(SixLabors.ImageSharp.Size size)
        {
            return new Size(size.Width, size.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData Size to SixLabors.ImageSharp Size
        /// 将DeploySharp CVData的Size转换为SixLabors.ImageSharp的Size
        /// </summary>
        /// <param name="size">DeploySharp CVData size/DeploySharp CVData尺寸</param>
        /// <returns>SixLabors.ImageSharp size/SixLabors.ImageSharp尺寸</returns>
        public static SixLabors.ImageSharp.Size ToSize(Size size)
        {
            return new SixLabors.ImageSharp.Size(size.Width, size.Height);
        }

        /// <summary>
        /// Converts SixLabors.ImageSharp Rectangle to DeploySharp CVData Rect
        /// 将SixLabors.ImageSharp的Rectangle转换为DeploySharp CVData的Rect
        /// </summary>
        /// <param name="rect">SixLabors.ImageSharp rectangle/SixLabors.ImageSharp矩形</param>
        /// <returns>DeploySharp CVData rectangle/DeploySharp CVData矩形</returns>
        public static Rect ToCvRect(SixLabors.ImageSharp.Rectangle rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Converts DeploySharp CVData Rect to SixLabors.ImageSharp Rectangle
        /// 将DeploySharp CVData的Rect转换为SixLabors.ImageSharp的Rectangle
        /// </summary>
        /// <param name="rect">DeploySharp CVData rectangle/DeploySharp CVData矩形</param>
        /// <returns>SixLabors.ImageSharp rectangle/SixLabors.ImageSharp矩形</returns>
        public static SixLabors.ImageSharp.Rectangle ToRect(Rect rect)
        {
            return new SixLabors.ImageSharp.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Converts ImageDataB to ImageSharp Image&lt;Rgb24&gt;
        /// 将ImageDataB转换为ImageSharp的Image&lt;Rgb24&gt;
        /// </summary>
        /// <param name="imageData">Source image data/源图像数据</param>
        /// <returns>ImageSharp RGB image/ImageSharp RGB图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null/当imageData为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image dimensions are invalid/当图像尺寸无效时抛出</exception>
        /// <exception cref="NotSupportedException">Thrown when channel count is unsupported/当通道数不支持时抛出</exception>
        public static Image<Rgb24> ToImage(this ImageDataB imageData)
        {
            if (imageData == null)
                throw new ArgumentNullException(nameof(imageData));
            if (imageData.Width <= 0 || imageData.Height <= 0)
                throw new ArgumentException("Image dimensions must be positive");

            byte[] rawData = imageData.GetRawData();

            return imageData.Channels switch
            {
                1 => Image.WrapMemory<L8>(rawData, imageData.Width, imageData.Height).CloneAs<Rgb24>(),
                3 => Image.WrapMemory<Rgb24>(rawData, imageData.Width, imageData.Height),
                4 => Image.WrapMemory<Rgba32>(rawData, imageData.Width, imageData.Height).CloneAs<Rgb24>(),
                _ => throw new NotSupportedException($"Unsupported channel count: {imageData.Channels}")
            };
        }

        /// <summary>
        /// Converts ImageSharp Image&lt;Rgb24&gt; to ImageDataB
        /// 将ImageSharp的Image&lt;Rgb24&gt;转换为ImageDataB
        /// </summary>
        /// <param name="image">Source RGB image/源RGB图像</param>
        /// <returns>Image data buffer/图像数据缓冲区</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
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
