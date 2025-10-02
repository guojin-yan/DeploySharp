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
    /// Provides extension methods for converting between OpenCvSharp and DeploySharp CVData data structures
    /// 提供OpenCvSharp和DeploySharp CVData数据结构之间的转换扩展方法
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class helps bridge between OpenCvSharp and DeploySharp CVData types
    /// 该类帮助在OpenCvSharp和DeploySharp CVData类型之间建立桥梁
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
        /// Converts OpenCvSharp PointF to DeploySharp CVData PointF
        /// 将OpenCvSharp的PointF转换为DeploySharp CVData的PointF
        /// </summary>
        /// <param name="point">OpenCvSharp point/OpenCvSharp点</param>
        /// <returns>DeploySharp CVData point/DeploySharp CVData点</returns>
        public static PointF ToCvPointF(OpenCvSharp.Point2f point)
        {
            return new PointF(
                point.X,
                point.Y
            );
        }
        /// <summary>
        /// Converts DeploySharp CVData PointF to OpenCvSharp PointF
        /// 将DeploySharp CVData的PointF转换为OpenCvSharp的PointF
        /// </summary>
        /// <param name="point">DeploySharp CVData point/DeploySharp CVData点</param>
        /// <returns>OpenCvSharp point/OpenCvSharp点</returns>
        public static OpenCvSharp.Point2f ToPointF(PointF point)
        {
            return new OpenCvSharp.Point2f(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts OpenCvSharp Point to DeploySharp CVData Point
        /// 将OpenCvSharp的Point转换为DeploySharp CVData的Point
        /// </summary>
        /// <param name="point">OpenCvSharp point/OpenCvSharp点</param>
        /// <returns>DeploySharp CVData point/DeploySharp CVData点</returns>
        public static Point ToCvPoint(OpenCvSharp.Point point)
        {
            return new Point(
                point.X,
                point.Y
            );
        }
        /// <summary>
        /// Converts DeploySharp CVData Point to OpenCvSharp Point
        /// 将DeploySharp CVData的Point转换为OpenCvSharp的Point
        /// </summary>
        /// <param name="point">DeploySharp CVData point/DeploySharp CVData点</param>
        /// <returns>OpenCvSharp point/OpenCvSharp点</returns>
        public static OpenCvSharp.Point ToPoint(Point point)
        {
            return new OpenCvSharp.Point(
                point.X,
                point.Y
            );
        }

        /// <summary>
        /// Converts OpenCvSharp SizeF to DeploySharp CVData SizeF
        /// 将OpenCvSharp的SizeF转换为DeploySharp CVData的SizeF
        /// </summary>
        /// <param name="size">OpenCvSharp size/OpenCvSharp尺寸</param>
        /// <returns>DeploySharp CVData size/DeploySharp CVData尺寸</returns>
        public static SizeF ToCvSizeF(OpenCvSharp.Size2f size)
        {
            return new SizeF(
                size.Width,
                size.Height
            );
        }
        /// <summary>
        /// Converts DeploySharp CVData SizeF to OpenCvSharp SizeF
        /// 将DeploySharp CVData的SizeF转换为OpenCvSharp的SizeF
        /// </summary>
        /// <param name="size">DeploySharp CVData size/DeploySharp CVData尺寸</param>
        /// <returns>OpenCvSharp size/OpenCvSharp尺寸</returns>
        public static OpenCvSharp.Size2f ToSizeF(SizeF size)
        {
            return new OpenCvSharp.Size2f(
                size.Width,
                size.Height
            );
        }
        /// <summary>
        /// Converts OpenCvSharp Size to DeploySharp CVData Size
        /// 将OpenCvSharp的Size转换为DeploySharp CVData的Size
        /// </summary>
        /// <param name="size">OpenCvSharp size/OpenCvSharp尺寸</param>
        /// <returns>DeploySharp CVData size/DeploySharp CVData尺寸</returns>
        public static Size ToCvSize(OpenCvSharp.Size size)
        {
            return new Size(
                size.Width,
                size.Height
            );
        }
        /// <summary>
        /// Converts DeploySharp CVData Size to OpenCvSharp Size
        /// 将DeploySharp CVData的Size转换为OpenCvSharp的Size
        /// </summary>
        /// <param name="size">DeploySharp CVData size/DeploySharp CVData尺寸</param>
        /// <returns>OpenCvSharp size/OpenCvSharp尺寸</returns>
        public static OpenCvSharp.Size ToSize(Size size)
        {
            return new OpenCvSharp.Size(
                size.Width,
                size.Height
            );
        }
        /// <summary>
        /// Converts OpenCvSharp Rectangle to DeploySharp CVData Rect
        /// 将OpenCvSharp的Rectangle转换为DeploySharp CVData的Rect
        /// </summary>
        /// <param name="rect">OpenCvSharp rectangle/OpenCvSharp矩形</param>
        /// <returns>DeploySharp CVData rectangle/DeploySharp CVData矩形</returns>
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
        /// Converts DeploySharp CVData Rect to OpenCvSharp Rectangle
        /// 将DeploySharp CVData的Rect转换为OpenCvSharp的Rectangle
        /// </summary>
        /// <param name="rect">DeploySharp CVData rectangle/DeploySharp CVData矩形</param>
        /// <returns>OpenCvSharp rectangle/OpenCvSharp矩形</returns>
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
        /// Converts OpenCvSharp RotatedRect to DeploySharp CVData RotatedRect
        /// 将OpenCvSharp的RotatedRect转换为DeploySharp CVData的RotatedRect
        /// </summary>
        /// <param name="rect">OpenCvSharp RotatedRect/OpenCvSharp旋转矩形</param>
        /// <returns>DeploySharp CVData RotatedRect/DeploySharp CVData旋转矩形</returns>
        public static RotatedRect ToCvRotatedRect(OpenCvSharp.RotatedRect rect)
        {
            return new RotatedRect(
                ToCvPointF(rect.Center),
                ToCvSizeF(rect.Size),
                rect.Angle
            );

        }
        /// <summary>
        /// Converts DeploySharp CVData RotatedRect to OpenCvSharp RotatedRect
        /// 将DeploySharp CVData的RotatedRect转换为OpenCvSharp的RotatedRect
        /// </summary>
        /// <param name="rect">DeploySharp CVData RotatedRect/DeploySharp CVData 旋转矩形</param>
        /// <returns>OpenCvSharp RotatedRect/OpenCvSharp 旋转矩形</returns>
        public static OpenCvSharp.RotatedRect ToRotatedRect(RotatedRect rect)
        {
            return new OpenCvSharp.RotatedRect(
                ToPointF(rect.Center),
                ToSizeF(rect.Size),
                rect.Angle
            );
        }



        /// <summary>
        /// Converts ImageDataB to OpenCvSharp Mat;
        /// 将ImageDataB转换为 OpenCvSharp Mat;
        /// </summary>
        /// <param name="imageData">Source image data/源图像数据</param>
        /// <returns> OpenCvSharp Mat/ OpenCvSharp Mat图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null/当imageData为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image dimensions are invalid/当图像尺寸无效时抛出</exception>
        /// <exception cref="NotSupportedException">Thrown when channel count is unsupported/当通道数不支持时抛出</exception>
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
        /// Converts  OpenCvSharp Mat to ImageDataB
        /// 将 OpenCvSharp Mat转换为ImageDataB
        /// </summary>
        /// <param name="mat">Source RGB image/源RGB图像</param>
        /// <returns>Image data buffer/图像数据缓冲区</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
        public static ImageDataB ToImageDataB(this OpenCvSharp.Mat mat) 
        {
            if (mat.Empty()) throw new ArgumentException("输入Mat为空");

            // 获取原始字节数据
            byte[] byteData = new byte[mat.Total() * mat.Channels()];
            //mat.GetArray(out byteData);
            Marshal.Copy(mat.Ptr(0),  byteData, 0, byteData.Length);


            return new ImageDataB(byteData, mat.Width, mat.Height, mat.Channels());
        }



        /// <summary>
        /// Converts ImageDataB to OpenCvSharp Mat;
        /// 将ImageDataB转换为 OpenCvSharp Mat;
        /// </summary>
        /// <param name="imageData">Source image data/源图像数据</param>
        /// <returns> OpenCvSharp Mat/ OpenCvSharp Mat图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when imageData is null/当imageData为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when image dimensions are invalid/当图像尺寸无效时抛出</exception>
        /// <exception cref="NotSupportedException">Thrown when channel count is unsupported/当通道数不支持时抛出</exception>
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
        /// Converts  OpenCvSharp Mat to ImageDataB
        /// 将 OpenCvSharp Mat转换为ImageDataB
        /// </summary>
        /// <param name="mat">Source RGB image/源RGB图像</param>
        /// <returns>Image data buffer/图像数据缓冲区</returns>
        /// <exception cref="ArgumentNullException">Thrown when image is null/当image为null时抛出</exception>
        public static ImageDataF ToImageDataF(this OpenCvSharp.Mat mat)
        {
            if (mat.Empty()) throw new ArgumentException("输入Mat为空");

            // 获取原始字节数据
            float[] byteData = new float[mat.Total() * mat.Channels()];
            //mat.GetArray(out byteData);
            Marshal.Copy(mat.Ptr(0), byteData, 0, byteData.Length);


            return new ImageDataF(byteData, mat.Width, mat.Height, mat.Channels());
        }

    }
}
