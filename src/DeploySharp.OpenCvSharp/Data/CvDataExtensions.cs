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
    public static class CvDataExtensions
    {

        public static PointF ToCvPointF(OpenCvSharp.Point2f point)
        {
            return new PointF(
                point.X,
                point.Y
            );
        }

        public static OpenCvSharp.Point2f ToPointF(PointF point)
        {
            return new OpenCvSharp.Point2f(
                point.X,
                point.Y
            );
        }


        public static Point ToCvPoint(OpenCvSharp.Point point)
        {
            return new Point(
                point.X,
                point.Y
            );
        }

        public static OpenCvSharp.Point ToPoint(Point point)
        {
            return new OpenCvSharp.Point(
                point.X,
                point.Y
            );
        }

        public static SizeF ToCvSizeF(OpenCvSharp.Size2f size)
        {
            return new SizeF(
                size.Width,
                size.Height
            );
        }

        public static OpenCvSharp.Size2f ToSizeF(SizeF size)
        {
            return new OpenCvSharp.Size2f(
                size.Width,
                size.Height
            );
        }

        public static Size ToCvSize(OpenCvSharp.Size size)
        {
            return new Size(
                size.Width,
                size.Height
            );
        }

        public static OpenCvSharp.Size ToSize(Size size)
        {
            return new OpenCvSharp.Size(
                size.Width,
                size.Height
            );
        }
        public static Rect ToCvRect(OpenCvSharp.Rect rect)
        {
            return new Rect(
                X: rect.X,
                Y: rect.Y,
                Width: rect.Width,
                Height: rect.Height
            );
        }

        public static OpenCvSharp.Rect ToRect(Rect rect)
        {
            return new OpenCvSharp.Rect(
                X: rect.X,
                Y: rect.Y,
                Width: rect.Width,
                Height: rect.Height
            );
        }

        public static RotatedRect ToCvRotatedRect(OpenCvSharp.RotatedRect rect)
        {
            return new RotatedRect(
                ToCvPointF(rect.Center),
                ToCvSizeF(rect.Size),
                rect.Angle
            );

        }

        public static OpenCvSharp.RotatedRect ToRotatedRect(RotatedRect rect)
        {
            return new OpenCvSharp.RotatedRect(
                ToPointF(rect.Center),
                ToSizeF(rect.Size),
                rect.Angle
            );
        }



        // 将ImageData转换为Mat
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

        // 从Mat创建ImageData
        public static ImageDataB ToImageDataB(this OpenCvSharp.Mat mat) 
        {
            if (mat.Empty()) throw new ArgumentException("输入Mat为空");

            // 获取原始字节数据
            byte[] byteData = new byte[mat.Total() * mat.Channels()];
            //mat.GetArray(out byteData);
            Marshal.Copy(mat.Ptr(0),  byteData, 0, byteData.Length);


            return new ImageDataB(byteData, mat.Width, mat.Height, mat.Channels());
        }



        // 将ImageData转换为Mat
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

        // 从Mat创建ImageData
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
