using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace DeploySharp.Data
{
    public static class CvDataExtensions
    {
        public static PointF ToCvPointF(SixLabors.ImageSharp.PointF point)
        {
            return new PointF(
                point.X,
                point.Y
            );
        }

        public static SixLabors.ImageSharp.PointF ToPointF(PointF point)
        {
            return new SixLabors.ImageSharp.PointF(
                point.X,
                point.Y
            );
        }


        public static Point ToCvPoint(SixLabors.ImageSharp.Point point)
        {
            return new Point(
                point.X,
                point.Y
            );
        }

        public static SixLabors.ImageSharp.Point ToPoint(Point point)
        {
            return new SixLabors.ImageSharp.Point(
                point.X,
                point.Y
            );
        }

        public static SizeF ToCvSizeF(SixLabors.ImageSharp.SizeF size)
        {
            return new SizeF(
                size.Width,
                size.Height
            );
        }

        public static SixLabors.ImageSharp.SizeF ToSizeF(SizeF size)
        {
            return new SixLabors.ImageSharp.SizeF(
                size.Width,
                size.Height
            );
        }

        public static Size ToCvSize(SixLabors.ImageSharp.Size size)
        {
            return new Size(
                size.Width,
                size.Height
            );
        }

        public static SixLabors.ImageSharp.Size ToSize(Size size)
        {
            return new SixLabors.ImageSharp.Size(
                size.Width,
                size.Height
            );
        }
        public static Rect ToCvRect(SixLabors.ImageSharp.Rectangle rect)
        {
            return new Rect(
                X: rect.X,
                Y: rect.Y,
                Width: rect.Width,
                Height: rect.Height
            );
        }

        public static SixLabors.ImageSharp.Rectangle ToRect(Rect rect)
        {
            return new SixLabors.ImageSharp.Rectangle(
                x: rect.X,
                y: rect.Y,
                width: rect.Width,
                height: rect.Height
            );
        }

        //public static RotatedRect ToCvRotatedRect(SixLabors.ImageSharp..RotatedRect rect)
        //{
        //    return new RotatedRect(
        //        ToCvPointF(rect.Center),
        //        ToCvSizeF(rect.Size),
        //        rect.Angle
        //    );

        //}

        //public static OpenCvSharp.RotatedRect ToRotatedRect(RotatedRect rect)
        //{
        //    return new OpenCvSharp.RotatedRect(
        //        ToPointF(rect.Center),
        //        ToSizeF(rect.Size),
        //        rect.Angle
        //    );
        //}


        // 将ImageDataB转换为Image<Rgb24>
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

        // 将Image<Rgb24>转换为ImageDataB
        public static ImageDataB ToImageDataB(this Image<Rgb24> image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            byte[] pixelData = new byte[image.Width * image.Height * 3];
            image.CopyPixelDataTo(pixelData);

            return new ImageDataB(pixelData, image.Width, image.Height, 3);
        }

        //// 辅助方法：将旋转矩形转换为点数组
        //public static PointF[] ToPointFArray(this RotatedRect rect)
        //{
        //    var points = new System.Drawing.PointF[4];
        //    rect.Points(points);
        //    return Array.ConvertAll(points, p => p.ToImageSharpPointF());
        //}
    }
}
