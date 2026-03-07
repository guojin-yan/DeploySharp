
using Clipper2Lib;
using iTextSharp.awt.geom;
using OpenCvSharp;
using OpenCvSharp.Extensions;
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
    /// <summary>
    /// Provides visualization methods for computer vision detection results.
    /// 提供计算机视觉检测结果的可视化方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supports visualization of:
    /// 支持可视化:
    /// - Object detection (bounding boxes)
    ///   目标检测(边界框)
    /// - Oriented bounding boxes (OBB)
    ///   有向边界框(OBB)
    /// - Instance segmentation (masks)
    ///   实例分割(掩膜)
    /// - Keypoint detection (poses)
    ///   关键点检测(姿态)
    /// - OCR results
    ///   OCR结果
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Visualize detection results
    /// // 可视化检测结果
    /// using OpenCvSharp;
    /// 
    /// Mat image = Cv2.ImRead("input.jpg");
    /// var options = new VisualizeOptions(1.0f);
    /// 
    /// // Run detection
    /// DetResult[] results = model.Predict(image);
    /// 
    /// // Draw results
    /// Mat visualized = Visualize.DrawDetResult(results, image, options);
    /// Cv2.ImShow("Detections", visualized);
    /// </code>
    /// </example>
    public static class Visualize
    {
        /// <summary>
        /// Draws detection results (bounding boxes) on the image.
        /// 在图像上绘制检测结果(边界框)。
        /// </summary>
        /// <param name="bresult">Detection results array / 检测结果数组</param>
        /// <param name="image">Source image to draw on / 要绘制的源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn detection results / 带有绘制检测结果的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when image or options is null / 当图像或选项为null时抛出</exception>
        /// <remarks>
        /// Draws bounding boxes, class labels, and confidence scores.
        /// 绘制边界框、类别标签和置信度分数。
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = yoloModel.Predict(image);
        /// var options = new VisualizeOptions(1.0f);
        /// Mat output = Visualize.DrawDetResult(results, image.Clone(), options);
        /// Cv2.ImWrite("output.jpg", output);
        /// </code>
        /// </example>
        /// <seealso cref="DrawObbResult"/>
        /// <seealso cref="DrawSegResult"/>
        public static Mat DrawDetResult(Result[] bresult, Mat image, VisualizeOptions options)
        {
            return DrawDetResult(bresult as DetResult[], image, options);
        }

        /// <summary>
        /// Draws detection results (bounding boxes) on the image.
        /// 在图像上绘制检测结果(边界框)。
        /// </summary>
        /// <param name="result">Detection results array / 检测结果数组</param>
        /// <param name="image">Source image to draw on / 要绘制的源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn detection results / 带有绘制检测结果的图像</returns>
        public static Mat DrawDetResult(DetResult[] result, Mat image, VisualizeOptions options)
        {            
            // Draw recognition results on the image
            for (int i = 0; i < result.Length; i++)
            {
                var box = result[i].Bounds;
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
        /// Draws oriented bounding box (OBB) detection results on the image.
        /// 在图像上绘制有向边界框(OBB)检测结果。
        /// </summary>
        /// <param name="bresult">OBB detection results / OBB检测结果</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn OBB results / 带有绘制OBB结果的图像</returns>
        /// <remarks>
        /// Used for text detection and oriented object detection.
        /// 用于文本检测和有向目标检测。
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = obbModel.Predict(image);
        /// Mat output = Visualize.DrawObbResult(results, image.Clone(), options);
        /// </code>
        /// </example>
        public static Mat DrawObbResult(Result[] bresult, Mat image, VisualizeOptions options) 
        {
            return DrawObbResult(bresult as ObbResult[], image, options);
        }

        /// <summary>
        /// Draws oriented bounding box (OBB) detection results on the image.
        /// 在图像上绘制有向边界框(OBB)检测结果。
        /// </summary>
        /// <param name="result">OBB detection results array / OBB检测结果数组</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn OBB results / 带有绘制OBB结果的图像</returns>
        public static Mat DrawObbResult(ObbResult[] result, Mat image, VisualizeOptions options)
        {
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
        /// Clamps rectangle to image boundaries.
        /// 将矩形调整到图像范围内。
        /// </summary>
        /// <param name="rect">Rectangle to clamp / 要调整的矩形</param>
        /// <param name="imgWidth">Image width / 图像宽度</param>
        /// <param name="imgHeight">Image height / 图像高度</param>
        /// <returns>Safe rectangle within image bounds / 图像范围内的安全矩形</returns>
        /// <remarks>
        /// Internal helper method to prevent drawing outside image boundaries.
        /// 内部辅助方法，防止在图像边界外绘制。
        /// </remarks>
        private static OpenCvSharp.Rect GetSafeRectangle(OpenCvSharp.Rect rect, int imgWidth, int imgHeight)
        {
            // Check rect parameter validity
            if (rect.Width <= 0 || rect.Height <= 0)
                return new OpenCvSharp.Rect(0, 0, 0, 0);

            // Calculate safe rectangle boundaries
            int x = Math.Max(0, Math.Min(rect.X, imgWidth - 1));
            int y = Math.Max(0, Math.Min(rect.Y, imgHeight - 1));
            int width = Math.Min(rect.Width, imgWidth - x);
            int height = Math.Min(rect.Height, imgHeight - y);

            return new OpenCvSharp.Rect(x, y, width, height);
        }

        /// <summary>
        /// Draws segmentation results on the image.
        /// 在图像上绘制分割结果。
        /// </summary>
        /// <param name="bresult">Segmentation results / 分割结果</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn segmentation masks / 带有绘制分割掩膜的图像</returns>
        /// <remarks>
        /// Overlays semi-transparent masks on detected objects.
        /// 在检测到的物体上叠加半透明掩膜。
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = segModel.Predict(image);
        /// Mat output = Visualize.DrawSegResult(results, image.Clone(), options);
        /// </code>
        /// </example>
        public static Mat DrawSegResult(Result[] bresult, Mat img, VisualizeOptions options) 
        {
            return DrawSegResult(bresult as SegResult[], img, options);
        }

        /// <summary>
        /// Draws segmentation results on the image.
        /// 在图像上绘制分割结果。
        /// </summary>
        /// <param name="result">Segmentation results array / 分割结果数组</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn segmentation masks / 带有绘制分割掩膜的图像</returns>
        public static Mat DrawSegResult(SegResult[] result, Mat img, VisualizeOptions options)
        {
            Mat image = img.Clone();
            // Convert original image to BGRA format (if not already)
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

                // Create mask layer
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
                // Create ROI (region of interest)
                using Mat roi = new Mat(image, rect);

                // Blend mask into output image
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

            // Convert back to BGR format (if original was 3-channel)
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, image, ColorConversionCodes.BGRA2BGR);
            }

            return image;
        }

        /// <summary>
        /// Draws keypoint detection (pose) results on the image.
        /// 在图像上绘制关键点检测(姿态)结果。
        /// </summary>
        /// <param name="bresult">Keypoint detection results / 关键点检测结果</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn keypoints and skeleton / 带有绘制关键点和骨架的图像</returns>
        /// <remarks>
        /// Draws 17 COCO keypoints and connecting skeleton lines.
        /// 绘制17个COCO关键点和连接的骨架线。
        /// </remarks>
        /// <example>
        /// <code>
        /// var results = poseModel.Predict(image);
        /// Mat output = Visualize.DrawPoses(results, image.Clone(), options);
        /// </code>
        /// </example>
        public static Mat DrawPoses(Result[] bresult, Mat img, VisualizeOptions options) 
        {
            return DrawPoses(bresult as KeyPointResult[], img, options);
        }

        /// <summary>
        /// Draws keypoint detection (pose) results on the image.
        /// 在图像上绘制关键点检测(姿态)结果。
        /// </summary>
        /// <param name="result">Keypoint detection results array / 关键点检测结果数组</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn keypoints and skeleton / 带有绘制关键点和骨架的图像</returns>
        public static Mat DrawPoses(KeyPointResult[] result, Mat img, VisualizeOptions options)
        {
            Mat image = img.Clone();
            // Connection point relationships for COCO keypoints
            int[,] edgs = new int[17, 2] { { 0, 1 }, { 0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8},
                 {7, 9}, {8, 10}, {5, 11}, {6, 12}, {11, 13}, {12, 14},{13, 15 }, {14, 16 }, {11, 12 } };
            // Color library
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
                // Draw keys
                for (int p = 0; p < 17; p++)
                {
                    if (result[i].KeyPoints[p].Confidence < options.KeyPointMinConfidence)
                    {
                        continue;
                    }

                    Cv2.Circle(image, CvDataExtensions.ToPoint(result[i].KeyPoints[p].Point), 2, colors[p], -1);
                }

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

        /// <summary>
        /// Draws OCR results on the image.
        /// 在图像上绘制OCR结果。
        /// </summary>
        /// <param name="srcimg">Source image / 源图像</param>
        /// <param name="ocrResult">OCR recognition result / OCR识别结果</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with drawn text boxes and recognized text / 带有绘制文本框和识别文字的图像</returns>
        /// <remarks>
        /// Draws rotated text boxes and overlays recognized text.
        /// 绘制旋转的文本框并叠加识别的文字。
        /// </remarks>
        /// <example>
        /// <code>
        /// var ocrResult = ocrModel.Predict(image);
        /// Mat output = Visualize.DrawOcrResult(image.Clone(), ocrResult, options);
        /// </code>
        /// </example>
        public static Mat DrawOcrResult(Mat srcimg, OcrResult ocrResult, VisualizeOptions options)
        {
            // Draw recognition results on the image
            if (ocrResult.TextAreas != null)
            {
                for (int i = 0; i < ocrResult.TextAreas.Length; i++)
                {
                    var box = ocrResult.TextAreas[i].Bounds.BoundingRect();
                    Point2f[] points = CvDataExtensions.ToRotatedRect(ocrResult.TextAreas[i].Bounds).Points();
                    for (int j = 0; j < 4; j++)
                    {
                        Cv2.Line(srcimg, (OpenCvSharp.Point)points[j], (OpenCvSharp.Point)points[(j + 1) % 4],
                            options.Colors.GetBoundingBoxColor(5), (int)options.BorderThickness);
                    }
                }
            }

            if (ocrResult.TextContents != null)
            {
                System.Drawing.Image im = BitmapConverter.ToBitmap(srcimg) as System.Drawing.Image;
                Graphics graphics = Graphics.FromImage(im);

                SolidBrush brush = new SolidBrush(Color.Red);
                for (int n = 0; n < ocrResult.TextContents.Length; n++)
                {
                    if (ocrResult.TextContents[n].Confidence < 0.7) continue;
                    PointF[] points = ocrResult.TextAreas[n].Bounds.Points();
                    int w = (int)Math.Ceiling((double)(ocrResult.TextAreas[n].Bounds.Size.Width) / 3.0) + 1;
                    int h = (int)Math.Ceiling((double)(ocrResult.TextAreas[n].Bounds.Size.Height) / 3.0) + 1;
                    int min = w < h ? w : h;
                    System.Drawing.Font font = new System.Drawing.Font("Arial", min);
                    float y = (float)points[1].Y;
                    if (y > min * 1.5)
                    {
                        y -= (int)(min * 1.5);
                    }
                    // Set text position (top-left)
                    System.Drawing.PointF point = new System.Drawing.PointF(points[0].X, y);
                    string text = ocrResult.TextContents[n].Text;
                    // Draw text onto image
                    graphics.DrawString(text, font, brush, point);
                }

                srcimg = BitmapConverter.ToMat((Bitmap)im);
            }

            return srcimg;
        }
    }

    /// <summary>
    /// Handler class for visualization operations using strategy pattern.
    /// 使用策略模式的可视化操作处理类。
    /// </summary>
    /// <remarks>
    /// Encapsulates different visualization methods for different result types.
    /// 为不同结果类型封装不同的可视化方法。
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create handler for detection visualization
    /// var handler = new VisualizeHandler(Visualize.DrawDetResult);
    /// 
    /// // Execute visualization
    /// Mat result = handler.ExecuteDrawing(detections, image, options);
    /// </code>
    /// </example>
    public class VisualizeHandler
    {
        // Define drawing delegate type
        /// <summary>
        /// Delegate type for visualization methods.
        /// 可视化方法的委托类型。
        /// </summary>
        /// <param name="results">Detection results / 检测结果</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Visualized image / 可视化后的图像</returns>
        public delegate Mat VisualizeDelegate(Result[] results, Mat image, VisualizeOptions options);

        private readonly VisualizeDelegate _drawingMethod;

        /// <summary>
        /// Creates a new visualize handler with specified drawing method.
        /// 使用指定的绘制方法创建新的可视化处理程序。
        /// </summary>
        /// <param name="drawingMethod">Drawing method delegate / 绘制方法委托</param>
        /// <exception cref="ArgumentNullException">Thrown when drawingMethod is null / 当drawingMethod为null时抛出</exception>
        public VisualizeHandler(VisualizeDelegate drawingMethod)
        {
            _drawingMethod = drawingMethod ?? throw new ArgumentNullException(nameof(drawingMethod));
        }

        /// <summary>
        /// Executes the drawing operation.
        /// 执行绘制操作。
        /// </summary>
        /// <param name="results">Detection results to visualize / 要可视化的检测结果</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>Image with visualization drawn / 带有可视化的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null / 当任何参数为null时抛出</exception>
        public Mat ExecuteDrawing(Result[] results, Mat image, VisualizeOptions options)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return _drawingMethod(results, image, options);
        }
    }
}
