using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Provides extension methods for converting between OpenCvSharp and DeploySharp CVData data structures.
    /// 提供OpenCvSharp和DeploySharp CVData数据结构之间的转换扩展方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class helps bridge between OpenCvSharp and DeploySharp CVData types.
    /// 该类帮助在OpenCvSharp和DeploySharp CVData类型之间建立桥梁。
    /// </para>
    /// <para>
    /// Supported conversions include:
    /// 支持的转换包括:
    /// - Point/PointF types (点/浮点类型)
    /// - Size/SizeF types (尺寸/浮点尺寸类型)
    /// - Rectangle/Rect types (矩形类型)
    /// - Image data conversion (图像数据转换)
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Convert OpenCvSharp Mat to ImageDataB for model inference
    /// // 将OpenCvSharp Mat转换为ImageDataB用于模型推理
    /// using OpenCvSharp;
    /// 
    /// Mat image = Cv2.ImRead("input.jpg");
    /// ImageDataB imageData = image.ToImageDataB();
    /// 
    /// // Process with DeploySharp model
    /// // 使用DeploySharp模型处理
    /// var results = model.Predict(imageData);
    /// 
    /// // Convert back to Mat for visualization
    /// // 转换回Mat用于可视化
    /// Mat output = results[0].ToMat();
    /// Cv2.ImShow("Result", output);
    /// </code>
    /// </example>
    public static class CvDataExtensions
    {
        /// <summary>
        /// Converts OpenCvSharp Point2f to DeploySharp CVData PointF.
        /// 将OpenCvSharp的Point2f转换为DeploySharp CVData的PointF。
        /// </summary>
        /// <param name="point">OpenCvSharp Point2f to convert / 要转换的OpenCvSharp点</param>
        /// <returns>DeploySharp CVData PointF / DeploySharp CVData浮点坐标</returns>
        /// <remarks>
        /// This is a simple struct conversion with no data transformation.
        /// 这是一个简单的结构体转换，没有数据转换。
        /// </remarks>
        /// <seealso cref="ToPointF(PointF)"/>
        public static PointF ToCvPointF(OpenCvSharp.Point2f point)
        {
            return new PointF(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData PointF to OpenCvSharp Point2f.
        /// 将DeploySharp CVData的PointF转换为OpenCvSharp的Point2f。
        /// </summary>
        /// <param name="point">DeploySharp CVData PointF to convert / 要转换的DeploySharp CVData点</param>
        /// <returns>OpenCvSharp Point2f / OpenCvSharp浮点坐标</returns>
        /// <remarks>
        /// Used when converting detection results back to OpenCvSharp format for visualization.
        /// 在将检测结果转换回OpenCvSharp格式进行可视化时使用。
        /// </remarks>
        /// <seealso cref="ToCvPointF(OpenCvSharp.Point2f)"/>
        public static OpenCvSharp.Point2f ToPointF(PointF point)
        {
            return new OpenCvSharp.Point2f(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts OpenCvSharp Point to DeploySharp CVData Point.
        /// 将OpenCvSharp的Point转换为DeploySharp CVData的Point。
        /// </summary>
        /// <param name="point">OpenCvSharp Point to convert / 要转换的OpenCvSharp点</param>
        /// <returns>DeploySharp CVData Point / DeploySharp CVData整数坐标</returns>
        /// <remarks>
        /// Integer coordinates are used for pixel-level operations.
        /// 整数坐标用于像素级操作。
        /// </remarks>
        /// <seealso cref="ToPoint(Point)"/>
        public static Point ToCvPoint(OpenCvSharp.Point point)
        {
            return new Point(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData Point to OpenCvSharp Point.
        /// 将DeploySharp CVData的Point转换为OpenCvSharp的Point。
        /// </summary>
        /// <param name="point">DeploySharp CVData Point to convert / 要转换的DeploySharp CVData点</param>
        /// <returns>OpenCvSharp Point / OpenCvSharp整数坐标</returns>
        /// <seealso cref="ToCvPoint(OpenCvSharp.Point)"/>
        public static OpenCvSharp.Point ToPoint(Point point)
        {
            return new OpenCvSharp.Point(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts OpenCvSharp Size2f to DeploySharp CVData SizeF.
        /// 将OpenCvSharp的Size2f转换为DeploySharp CVData的SizeF。
        /// </summary>
        /// <param name="size">OpenCvSharp Size2f to convert / 要转换的OpenCvSharp尺寸</param>
        /// <returns>DeploySharp CVData SizeF / DeploySharp CVData浮点尺寸</returns>
        /// <remarks>
        /// Used for representing floating-point dimensions like model input sizes.
        /// 用于表示浮点尺寸，如模型输入尺寸。
        /// </remarks>
        /// <seealso cref="ToSizeF(SizeF)"/>
        public static SizeF ToCvSizeF(OpenCvSharp.Size2f size)
        {
            return new SizeF(
                size.Width,
                size.Height
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData SizeF to OpenCvSharp Size2f.
        /// 将DeploySharp CVData的SizeF转换为OpenCvSharp的Size2f。
        /// </summary>
        /// <param name="size">DeploySharp CVData SizeF to convert / 要转换的DeploySharp CVData尺寸</param>
        /// <returns>OpenCvSharp Size2f / OpenCvSharp浮点尺寸</returns>
        /// <seealso cref="ToCvSizeF(OpenCvSharp.Size2f)"/>
        public static OpenCvSharp.Size2f ToSizeF(SizeF size)
        {
            return new OpenCvSharp.Size2f(
                size.Width,
                size.Height
            );
        }

        /// <summary>
        /// Converts OpenCvSharp Size to DeploySharp CVData Size.
        /// 将OpenCvSharp的Size转换为DeploySharp CVData的Size。
        /// </summary>
        /// <param name="size">OpenCvSharp Size to convert / 要转换的OpenCvSharp尺寸</param>
        /// <returns>DeploySharp CVData Size / DeploySharp CVData整数尺寸</returns>
        /// <remarks>
        /// Integer sizes are commonly used for image dimensions.
        /// 整数尺寸通常用于图像尺寸。
        /// </remarks>
        /// <seealso cref="ToSize(Size)"/>
        public static Size ToCvSize(OpenCvSharp.Size size)
        {
            return new Size(
                size.Width,
                size.Height
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData Size to OpenCvSharp Size.
        /// 将DeploySharp CVData的Size转换为OpenCvSharp的Size。
        /// </summary>
        /// <param name="size">DeploySharp CVData Size to convert / 要转换的DeploySharp CVData尺寸</param>
        /// <returns>OpenCvSharp Size / OpenCvSharp整数尺寸</returns>
        /// <seealso cref="ToCvSize(OpenCvSharp.Size)"/>
        public static OpenCvSharp.Size ToSize(Size size)
        {
            return new OpenCvSharp.Size(
                size.Width,
                size.Height
            );
        }

        /// <summary>
        /// Converts OpenCvSharp Rect to DeploySharp CVData Rect.
        /// 将OpenCvSharp的Rect转换为DeploySharp CVData的Rect。
        /// </summary>
        /// <param name="rect">OpenCvSharp Rect to convert / 要转换的OpenCvSharp矩形</param>
        /// <returns>DeploySharp CVData Rect / DeploySharp CVData矩形</returns>
        /// <remarks>
        /// Bounding boxes from OpenCvSharp operations can be directly converted.
        /// 来自OpenCvSharp操作的边界框可以直接转换。
        /// </remarks>
        /// <seealso cref="ToRect(Rect)"/>
        public static Rect ToCvRect(OpenCvSharp.Rect rect)
        {
            return new Rect(
                X: rect.X,
                Y: rect.Y,
                Width: rect.Width,
                Height: rect.Height
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData Rect to OpenCvSharp Rect.
        /// 将DeploySharp CVData的Rect转换为OpenCvSharp的Rect。
        /// </summary>
        /// <param name="rect">DeploySharp CVData Rect to convert / 要转换的DeploySharp CVData矩形</param>
        /// <returns>OpenCvSharp Rect / OpenCvSharp矩形</returns>
        /// <remarks>
        /// Used for drawing detection results with OpenCvSharp.
        /// 用于使用OpenCvSharp绘制检测结果。
        /// </remarks>
        /// <seealso cref="ToCvRect(OpenCvSharp.Rect)"/>
        public static OpenCvSharp.Rect ToRect(Rect rect)
        {
            return new OpenCvSharp.Rect(
                X: rect.X,
                Y: rect.Y,
                Width: rect.Width,
                Height: rect.Height
            );
        }

        /// <summary>
        /// Converts OpenCvSharp RotatedRect to DeploySharp CVData RotatedRect.
        /// 将OpenCvSharp的RotatedRect转换为DeploySharp CVData的RotatedRect。
        /// </summary>
        /// <param name="rect">OpenCvSharp RotatedRect to convert / 要转换的OpenCvSharp旋转矩形</param>
        /// <returns>DeploySharp CVData RotatedRect / DeploySharp CVData旋转矩形</returns>
        /// <remarks>
        /// Used for oriented bounding box (OBB) detection results.
        /// 用于有向边界框(OBB)检测结果。
        /// </remarks>
        /// <seealso cref="ToRotatedRect(RotatedRect)"/>
        public static RotatedRect ToCvRotatedRect(OpenCvSharp.RotatedRect rect)
        {
            return new RotatedRect(
                ToCvPointF(rect.Center),
                ToCvSizeF(rect.Size),
                rect.Angle
            );
        }

        /// <summary>
        /// Converts DeploySharp CVData RotatedRect to OpenCvSharp RotatedRect.
        /// 将DeploySharp CVData的RotatedRect转换为OpenCvSharp的RotatedRect。
        /// </summary>
        /// <param name="rect">DeploySharp CVData RotatedRect to convert / 要转换的DeploySharp CVData旋转矩形</param>
        /// <returns>OpenCvSharp RotatedRect / OpenCvSharp旋转矩形</returns>
        /// <remarks>
        /// Used for drawing rotated bounding boxes in OCR and oriented object detection.
        /// 用于在OCR和有向目标检测中绘制旋转边界框。
        /// </remarks>
        /// <seealso cref="ToCvRotatedRect(OpenCvSharp.RotatedRect)"/>
        public static OpenCvSharp.RotatedRect ToRotatedRect(RotatedRect rect)
        {
            return new OpenCvSharp.RotatedRect(
                ToPointF(rect.Center),
                ToSizeF(rect.Size),
                rect.Angle
            );
        }

        /// <summary>
        /// Converts ImageDataB to OpenCvSharp Mat.
        /// 将ImageDataB转换为OpenCvSharp Mat。
        /// </summary>
        /// <param name="imageData">Source image data in byte format / 源图像数据(字节格式)</param>
        /// <returns>OpenCvSharp Mat image / OpenCvSharp Mat图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null / 当imageData为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image dimensions are invalid / 当图像尺寸无效时抛出</exception>
        /// <exception cref="NotSupportedException">Thrown when channel count is unsupported / 当通道数不支持时抛出</exception>
        /// <remarks>
        /// <para>
        /// Supports 1-channel (grayscale), 3-channel (BGR/RGB), and 4-channel (BGRA/RGBA) images.
        /// 支持1通道(灰度)、3通道(BGR/RGB)和4通道(BGRA/RGBA)图像。
        /// </para>
        /// <para>
        /// The resulting Mat shares data with the ImageDataB buffer. Dispose properly to avoid memory leaks.
        /// 返回的Mat与ImageDataB缓冲区共享数据。请正确释放以避免内存泄漏。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Convert model output to Mat for visualization
        /// // 将模型输出转换为Mat用于可视化
        /// ImageDataB outputData = model.GetOutput();
        /// using (Mat result = outputData.ToMat())
        /// {
        ///     Cv2.ImShow("Output", result);
        ///     Cv2.WaitKey(0);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ToImageDataB(Mat)"/>
        public static OpenCvSharp.Mat ToMat(this ImageDataB imageData)
        {
            if (imageData == null)
                throw new ArgumentNullException(nameof(imageData));
            if (imageData.Width <= 0 || imageData.Height <= 0)
                throw new ArgumentException("Image dimensions must be positive");

            // 获取原始数据(避免多次调用GetRawData)
            byte[] rawData = imageData.GetRawData();

            // 根据通道数创建适当类型的Mat
            OpenCvSharp.MatType matType = imageData.Channels switch
            {
                1 => OpenCvSharp.MatType.CV_8UC1,
                3 => OpenCvSharp.MatType.CV_8UC3,
                4 => OpenCvSharp.MatType.CV_8UC4,
                _ => throw new NotSupportedException($"Unsupported channel count: {imageData.Channels}")
            };

            // 一次性创建正确通道数的Mat(避免Reshape操作)
            // 直接使用指针创建Mat，避免数据拷贝
            Mat mat = new OpenCvSharp.Mat(
                imageData.Height,
                imageData.Width,
                matType);
            mat.SetArray(rawData);
            return mat;
        }

        /// <summary>
        /// Converts OpenCvSharp Mat to ImageDataB.
        /// 将OpenCvSharp Mat转换为ImageDataB。
        /// </summary>
        /// <param name="mat">Source OpenCvSharp Mat / 源OpenCvSharp Mat</param>
        /// <returns>ImageDataB containing the image data / 包含图像数据的ImageDataB</returns>
        /// <exception cref="ArgumentException">Thrown when mat is empty / 当mat为空时抛出</exception>
        /// <remarks>
        /// <para>
        /// Supports CV_8UC1, CV_8UC3, and CV_8UC4 Mat types.
        /// 支持CV_8UC1、CV_8UC3和CV_8UC4 Mat类型。
        /// </para>
        /// <para>
        /// Data is copied from the Mat to the ImageDataB buffer.
        /// 数据从Mat复制到ImageDataB缓冲区。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Load image and convert for model inference
        /// // 加载图像并转换为模型推理格式
        /// using (Mat image = Cv2.ImRead("input.jpg"))
        /// {
        ///     ImageDataB imageData = image.ToImageDataB();
        ///     var results = model.Predict(imageData);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ToMat(ImageDataB)"/>
        public static ImageDataB ToImageDataB(this OpenCvSharp.Mat mat) 
        {
            if (mat.Empty()) throw new ArgumentException("输入Mat为空");

            // 获取原始字节数据
            byte[] byteData = new byte[mat.Total() * mat.Channels()];
            Marshal.Copy(mat.Ptr(0), byteData, 0, byteData.Length);

            return new ImageDataB(byteData, mat.Width, mat.Height, mat.Channels());
        }

        /// <summary>
        /// Converts ImageDataF to OpenCvSharp Mat.
        /// 将ImageDataF转换为OpenCvSharp Mat。
        /// </summary>
        /// <param name="imageData">Source image data in float format / 源图像数据(浮点格式)</param>
        /// <returns>OpenCvSharp Mat image (CV_32FC1/CV_32FC3/CV_32FC4) / OpenCvSharp Mat图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null / 当imageData为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image dimensions are invalid / 当图像尺寸无效时抛出</exception>
        /// <exception cref="NotSupportedException">Thrown when channel count is unsupported / 当通道数不支持时抛出</exception>
        /// <remarks>
        /// <para>
        /// Float images are commonly used for normalized model inputs and intermediate processing.
        /// 浮点图像通常用于归一化的模型输入和中间处理。
        /// </para>
        /// <para>
        /// The resulting Mat will have type CV_32FC1, CV_32FC3, or CV_32FC4 depending on channels.
        /// 返回的Mat类型将是CV_32FC1、CV_32FC3或CV_32FC4，取决于通道数。
        /// </para>
        /// </remarks>
        /// <seealso cref="ToImageDataF(Mat)"/>
        public static OpenCvSharp.Mat ToMat(this ImageDataF imageData)
        {
            if (imageData == null)
                throw new ArgumentNullException(nameof(imageData));
            if (imageData.Width <= 0 || imageData.Height <= 0)
                throw new ArgumentException("Image dimensions must be positive");

            // 获取原始数据(避免多次调用GetRawData)
            float[] rawData = imageData.GetRawData();

            // 根据通道数创建适当类型的Mat
            OpenCvSharp.MatType matType = imageData.Channels switch
            {
                1 => OpenCvSharp.MatType.CV_32FC1,
                3 => OpenCvSharp.MatType.CV_32FC3,
                4 => OpenCvSharp.MatType.CV_32FC4,
                _ => throw new NotSupportedException($"Unsupported channel count: {imageData.Channels}")
            };

            // 一次性创建正确通道数的Mat(避免Reshape操作)
            // 直接使用指针创建Mat，避免数据拷贝
            Mat mat = new OpenCvSharp.Mat(
                imageData.Height,
                imageData.Width,
                matType);
            mat.SetArray(rawData);
            return mat;
        }

        /// <summary>
        /// Converts OpenCvSharp Mat to ImageDataF.
        /// 将OpenCvSharp Mat转换为ImageDataF。
        /// </summary>
        /// <param name="mat">Source OpenCvSharp Mat (must be CV_32F type) / 源OpenCvSharp Mat(必须是CV_32F类型)</param>
        /// <returns>ImageDataF containing the image data / 包含图像数据的ImageDataF</returns>
        /// <exception cref="ArgumentException">Thrown when mat is empty / 当mat为空时抛出</exception>
        /// <remarks>
        /// <para>
        /// The Mat should be of type CV_32FC1, CV_32FC3, or CV_32FC4.
        /// Mat应该是CV_32FC1、CV_32FC3或CV_32FC4类型。
        /// </para>
        /// <para>
        /// Data is copied from the Mat to the ImageDataF buffer.
        /// 数据从Mat复制到ImageDataF缓冲区。
        /// </para>
        /// </remarks>
        /// <seealso cref="ToMat(ImageDataF)"/>
        public static ImageDataF ToImageDataF(this OpenCvSharp.Mat mat)
        {
            if (mat.Empty()) throw new ArgumentException("输入Mat为空");

            // 获取原始字节数据
            float[] byteData = new float[mat.Total() * mat.Channels()];
            Marshal.Copy(mat.Ptr(0), byteData, 0, byteData.Length);

            return new ImageDataF(byteData, mat.Width, mat.Height, mat.Channels());
        }
    }
}
