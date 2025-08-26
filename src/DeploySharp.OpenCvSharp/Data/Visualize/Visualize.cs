
using Clipper2Lib;
using OpenCvSharp;
using OpenVinoSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static log4net.Appender.ColoredConsoleAppender;

namespace DeploySharp.Data
{
    public static class Visualize
    {

        /// <summary>
        /// Result drawing
        /// </summary>
        /// <param name="bresult">recognition result</param>
        /// <param name="image">image</param>
        /// <returns></returns>
        public static Mat DrawDetResult(Result[] bresult, Mat image, VisualizeOptions options)
        {
            DetResult[] result = bresult as DetResult[];
            // Draw recognition results on the image
            for (int i = 0; i < result.Length; i++)
            {
                var box = result[i].Bounds;
                //Console.WriteLine(result.rects[i]);
                Cv2.Rectangle(image, 
                    CvDataExtensions.ToRect(box), 
                    options.Colors.GetBoundingBoxColor(result[i].Id), 
                    (int)options.BorderThickness,
                    LineTypes.Link8);
                Cv2.Rectangle(image, 
                    new OpenCvSharp.Point(box.TopLeft.X, box.TopLeft.Y + options.FontHeight),
                    new OpenCvSharp.Point(box.BottomRight.X, box.TopLeft.Y), 
                    new Scalar(0, 255, 255), 
                    -1);
                Cv2.PutText(image, 
                    result[i].Category + "-" + result[i].Confidence.ToString("0.00"),
                    new OpenCvSharp.Point(box.X, box.Y + options.FontHeight - 5),
                    options.FontType, 
                    options.FontSize, 
                    new Scalar(0, 0, 0), 
                    1);
            }
            return image;
        }

        /// <summary>
        /// Result drawing
        /// </summary>
        /// <param name="bresult">recognition result</param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Mat DrawObbResult(Result[] bresult, Mat image, VisualizeOptions options)
        {
            ObbResult[] result = bresult as ObbResult[];
            // Draw recognition results on the image
            for (int i = 0; i < result.Length; i++)
            {
                var box = result[i].Bounds.BoundingRect();
                Point2f[] points = CvDataExtensions.ToRotatedRect(result[i].Bounds).Points();
                for (int j = 0; j < 4; j++)
                {
                    Cv2.Line(image, (OpenCvSharp.Point)points[j], (OpenCvSharp.Point)points[(j + 1) % 4], options.Colors.GetBoundingBoxColor(result[i].Id), (int)options.BorderThickness);
                }

                Cv2.Rectangle(image,
                   new OpenCvSharp.Point(box.TopLeft.X, box.TopLeft.Y + options.FontHeight),
                   new OpenCvSharp.Point(box.BottomRight.X, box.TopLeft.Y),
                   new Scalar(0, 255, 255),
                   -1);
                Cv2.PutText(image,
                    result[i].Category + "-" + result[i].Confidence.ToString("0.00"),
                    new OpenCvSharp.Point(box.TopLeft.X, box.TopLeft.Y + options.FontHeight - 5),
                    options.FontType,
                    options.FontSize,
                    new Scalar(0, 0, 0),
                    1);
            }
            return image;
        }


        /// <summary>
        /// 将矩形调整到图像范围内
        /// </summary>
        private static OpenCvSharp.Rect GetSafeRectangle(OpenCvSharp.Rect rect, int imgWidth, int imgHeight)
        {
            // 检查rect参数有效性
            if (rect.Width <= 0 || rect.Height <= 0)
                return new OpenCvSharp.Rect(0, 0, 0, 0);

            // 计算安全的矩形边界
            int x = Math.Max(0, Math.Min(rect.X, imgWidth - 1));
            int y = Math.Max(0, Math.Min(rect.Y, imgHeight - 1));
            int width = Math.Min(rect.Width, imgWidth - x);
            int height = Math.Min(rect.Height, imgHeight - y);

            return new OpenCvSharp.Rect(x, y, width, height);
        }

        /// <summary>
        /// Result drawing
        /// </summary>
        /// <param name="bresult">recognition result</param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Mat DrawSegResult(Result[] bresult, Mat img, VisualizeOptions options)
        {
            SegResult[] result = bresult as SegResult[];


            Mat image = img.Clone();
            // 将原始图像转换为BGRA格式（如果还不是）
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, image, ColorConversionCodes.BGR2BGRA);
            }

            // Draw recognition results on the image
            for (int i = 0; i < result.Length; i++)
            {
                var box = result[i].Bounds;
                var mask = result[i].Mask;
                OpenCvSharp.Rect rect = GetSafeRectangle(
                    new OpenCvSharp.Rect(
                    box.TopLeft.X,
                    box.TopLeft.Y,
                    box.BottomRight.X - box.TopLeft.X,
                    box.BottomRight.Y - box.TopLeft.Y), 
                    image.Width, image.Height);

                // 创建掩膜图层
                using Mat maskLayer = new Mat(box.Height, box.Width, MatType.CV_8UC4, Scalar.All(0));

                Scalar color = options.Colors.GetMaskColor(result[i].Id);

                for (var x = 0; x < box.Width; x++)
                {
                    for (var y = 0; y < box.Height; y++)
                    {
                        var value = mask[0, y, x];

                        if (value > options.MaskMinConfidence)
                        {
                            maskLayer.Set(y, x, color.ToVec3b());
                        }
                    }
                }
                // 创建ROI（目标区域）
                using Mat roi = new Mat(image, rect);

                // 混合掩膜到输出图像
                Cv2.AddWeighted(
                    roi, 1.0,
                    new Mat(maskLayer, new OpenCvSharp.Rect(0, 0, rect.Width, rect.Height)), options.MaskAlpha,
                    0.0, roi);

                Cv2.Rectangle(image, CvDataExtensions.ToRect(box), options.Colors.GetBoundingBoxColor(result[i].Id), 2, LineTypes.Link8);
                Cv2.Rectangle(image, new OpenCvSharp.Point(box.TopLeft.X, box.TopLeft.Y + options.FontHeight),
                    new OpenCvSharp.Point(box.BottomRight.X, box.TopLeft.Y), Scalar.Yellow, -1);
                Cv2.PutText(image, result[i].Category + "-" + result[i].Confidence.ToString("0.00"),
                    new OpenCvSharp.Point(box.X, box.Y + 25),
                    HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 2);
            }

            // 转回BGR格式（如果原始是3通道）
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, image, ColorConversionCodes.BGRA2BGR);
            }

            return image;
        }

        /// <summary>
        /// Key point result drawing
        /// </summary>
        /// <param name="bresult">Key point data</param>
        /// <param name="img">image</param>
        public static Mat DrawPoses(Result[] bresult, Mat img, VisualizeOptions options)
        {
            KeyPointResult[] result = bresult as KeyPointResult[];
            Mat image = img.Clone();
            // Connection point relationship
            int[,] edgs = new int[17, 2] { { 0, 1 }, { 0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8},
                 {7, 9}, {8, 10}, {5, 11}, {6, 12}, {11, 13}, {12, 14},{13, 15 }, {14, 16 }, {11, 12 } };
            // Color Library
            Scalar[] colors = new Scalar[18] { new Scalar(255, 0, 0), new Scalar(255, 85, 0), new Scalar(255, 170, 0),
                new Scalar(255, 255, 0), new Scalar(170, 255, 0), new Scalar(85, 255, 0), new Scalar(0, 255, 0),
                new Scalar(0, 255, 85), new Scalar(0, 255, 170), new Scalar(0, 255, 255), new Scalar(0, 170, 255),
                new Scalar(0, 85, 255), new Scalar(0, 0, 255), new Scalar(85, 0, 255), new Scalar(170, 0, 255),
                new Scalar(255, 0, 255), new Scalar(255, 0, 170), new Scalar(255, 0, 85) };
            string[] point_str = new string[] { "Nose", "Left Eye", "Right Eye", "Left Ear", "Right Ear",
                "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow", "Left Wrist", "Right Wrist",
                "Left Hip", "Right Hip", "Left Knee", "Right Knee", "Left Ankle", "Right Ankle" };
            for (int i = 0; i < result.Length; ++i)
            {
                var box = result[i].Bounds;
                // Draw Keys
                for (int p = 0; p < 17; p++)
                {
                    if (result[i].KeyPoints[p].Confidence < options.KeyPointMinConfidence)
                    {
                        continue;
                    }

                    Cv2.Circle(image, CvDataExtensions.ToPoint(result[i].KeyPoints[p].Point), 2, colors[p], -1);
                    //Console.WriteLine(pose.point[p]);
                }
                // draw

                Cv2.Rectangle(image, CvDataExtensions.ToRect(box), options.Colors.GetBoundingBoxColor(result[i].Id), 2, LineTypes.Link8);
                Cv2.Rectangle(image, new OpenCvSharp.Point(box.TopLeft.X, box.TopLeft.Y + options.FontHeight),
                    new OpenCvSharp.Point(box.BottomRight.X, box.TopLeft.Y), Scalar.Yellow, -1);
                Cv2.PutText(image, result[i].Category + "-" + result[i].Confidence.ToString("0.00"),
                    new OpenCvSharp.Point(box.X, box.Y + 25),
                    HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 0), 2);

                for (int p = 0; p < 17; p++)
                {
                    if (result[i].KeyPoints[edgs[p, 0]].Confidence < options.KeyPointMinConfidence ||
                        result[i].KeyPoints[edgs[p, 1]].Confidence < options.KeyPointMinConfidence)
                    {
                        continue;
                    }

                    float[] point_x = new float[] { result[i].KeyPoints[edgs[p, 0]].Point.X,
                        result[i].KeyPoints[edgs[p, 1]].Point.X };
                    float[] point_y = new float[] { result[i].KeyPoints[edgs[p, 0]].Point.Y,
                        result[i].KeyPoints[edgs[p, 1]].Point.Y };

                    OpenCvSharp.Point center_point = new OpenCvSharp.Point((int)((point_x[0] + point_x[1]) / 2), (int)((point_y[0] + point_y[1]) / 2));
                    double length = Math.Sqrt(Math.Pow((double)(point_x[0] - point_x[1]), 2.0) + Math.Pow((double)(point_y[0] - point_y[1]), 2.0));
                    int stick_width = 2;
                    OpenCvSharp.Size axis = new OpenCvSharp.Size(length / 2, stick_width);
                    double angle = (Math.Atan2((double)(point_y[0] - point_y[1]), (double)(point_x[0] - point_x[1]))) * 180 / Math.PI;
                    OpenCvSharp.Point[] polygon = Cv2.Ellipse2Poly(center_point, axis, (int)angle, 0, 360, 1);
                    Cv2.FillConvexPoly(image, polygon, colors[p]);

                }
            }
            return image;

        }

    }
}
