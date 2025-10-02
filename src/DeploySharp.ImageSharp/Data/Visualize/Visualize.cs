using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DeploySharp.Data
{
    /// <summary>
    /// Static class providing visualization methods for different computer vision results
    /// 提供不同计算机视觉结果可视化方法的静态类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains specialized rendering methods for:
    /// 包含专门的渲染方法用于:
    /// - Object detection (bounding boxes)
    ///   目标检测(边界框)
    /// - Oriented bounding boxes (OBB)
    ///   定向边界框(OBB)
    /// - Semantic segmentation (masks)
    ///   语义分割(掩膜)
    /// - Human pose estimation (keypoints)
    ///   人体姿态估计(关键点)
    /// </para>
    /// <para>
    /// All methods return new Image&lt;Rgb24&gt; instances leaving original images unmodified
    /// 所有方法都返回新的Image&lt;Rgb24&gt;实例，原始图像不被修改
    /// </para>
    /// </remarks>
    public static class Visualize
    {
        /// <summary>
        /// Draws detection results (bounding boxes) on the image
        /// 在图像上绘制检测结果(边界框)
        /// </summary>
        /// <param name="bresult">Detection results array/检测结果数组</param>
        /// <param name="image">Source image/源图像</param>
        /// <param name="options">Visualization options/可视化选项</param>
        /// <returns>New image with rendered detections/渲染了检测结果的新图像</returns>
        /// <example>
        /// <code>
        /// var results = model.Predict(image);
        /// var visualized = Visualize.DrawDetResult(results, image, options);
        /// </code>
        /// </example>
        public static Image<Rgb24> DrawDetResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options)
        {
            DetResult[] result = bresult as DetResult[];

            // Create image copy to avoid modifying original
            // 创建图像副本以避免修改原始图像
            var output = image.Clone();

            for (int i = 0; i < result.Length; i++)
            {
                // Draw bounding box
                // 绘制边界框
                var box = result[i].Bounds;
                var rect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                        box.BottomRight.X - box.TopLeft.X,
                                        box.BottomRight.Y - box.TopLeft.Y);

                output.Mutate(ctx =>
                {
                    // Red bounding box
                    // 红色边界框
                    ctx.Draw(Pens.Solid(options.colors.GetBoundingBoxColor(result[i].Id), options.BorderThickness), rect);

                    // Yellow label background
                    // 黄色标签背景
                    var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                 box.BottomRight.X - box.TopLeft.X, options.FontHeight);
                    ctx.Fill(Color.Yellow, labelRect);

                    // Black text
                    // 黑色文本
                    var text = $"{result[i].Category}-{result[i].Confidence:0.00}";
                    ctx.DrawText(text, options.FontType, Color.Black,
                                new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y));
                });
            }

            return output;
        }

        /// <summary>
        /// Draws oriented bounding box (OBB) results
        /// 绘制定向边界框(OBB)结果
        /// </summary>
        /// <param name="bresult">OBB results array/OBB结果数组</param>
        /// <param name="image">Source image/源图像</param>
        /// <param name="options">Visualization options/可视化选项</param>
        /// <returns>New image with rendered OBBs/渲染了OBB的新图像</returns>
        /// <remarks>
        /// Draws quadrilateral boxes with angle information
        /// 绘制带有角度信息的四边形框
        /// </remarks>
        public static Image<Rgb24> DrawObbResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options)
        {
            ObbResult[] result = bresult as ObbResult[];
            var output = image.Clone();

            for (int i = 0; i < result.Length; i++)
            {
                PointF[] points = result[i].Bounds.Points();

                output.Mutate(ctx =>
                {
                    // Draw four edges of rotated box
                    // 绘制旋转框的四条边
                    for (int j = 0; j < 4; j++)
                    {
                        var start = CvDataExtensions.ToPointF(points[j]);
                        var end = CvDataExtensions.ToPointF(points[(j + 1) % 4]);
                        ctx.DrawLine(Color.FromRgb(255, 100, 200), 2, start, end);
                    }

                    // Text label
                    // 文本标签
                    var text = $"{result[i].Category}-{result[i].Confidence:0.00}";
                    ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black, CvDataExtensions.ToPointF(points[0]));
                });
            }

            return output;
        }

        /// <summary>
        /// Draws segmentation results
        /// 绘制分割结果
        /// </summary>
        /// <param name="bresult">Segmentation results array/分割结果数组</param>
        /// <param name="image">Source image/源图像</param>
        /// <param name="options">Visualization options/可视化选项</param>
        /// <returns>New image with rendered masks/渲染了掩膜的新图像</returns>
        /// <remarks>
        /// Combines semi-transparent colored masks with bounding boxes
        /// 将半透明彩色掩膜与边界框结合起来
        /// </remarks>
        public static Image<Rgb24> DrawSegResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options)
        {
            SegResult[] result = bresult as SegResult[];
            var output = image.Clone();

            for (int i = 0; i < result.Length; i++)
            {
                var box = result[i].Bounds;
                var mask = result[i].Mask;
                var rect = new Rectangle(box.TopLeft.X, box.TopLeft.Y,
                        box.BottomRight.X - box.TopLeft.X,
                        box.BottomRight.Y - box.TopLeft.Y);

                var color = options.colors.GetMaskColor(result[i].Id);

                using var maskLayer = new Image<Rgba32>(box.Width, box.Height);

                for (var x = 0; x < box.Width; x++)
                {
                    for (var y = 0; y < box.Height; y++)
                    {
                        var value = mask[0, y, x];

                        if (value > options.MaskMinimumConfidence)
                        {
                            maskLayer[x, y] = color;
                        }
                    }
                }

                output.Mutate(ctx =>
                {
                    // Draw bounding box
                    // 绘制边界框
                    ctx.Draw(Pens.Solid(options.colors.GetBoundingBoxColor(result[i].Id), options.BorderThickness), rect);

                    // Draw label background
                    // 绘制标签背景
                    var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                 box.BottomRight.X - box.TopLeft.X, options.FontHeight);
                    ctx.Fill(Color.Yellow, labelRect);

                    // Draw text
                    // 绘制文本
                    var text = $"{result[i].Category}-{result[i].Confidence:0.00}";
                    ctx.DrawText(text, options.FontType, Color.Black,
                                new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y));

                    // Semi-transparent mask overlay
                    // 50%透明度的蒙版覆盖
                    ctx.DrawImage(maskLayer, rect.Location, options.MaskAlpha);
                });
            }

            return output;
        }

        /// <summary>
        /// Draws human pose estimation results
        /// 绘制人体姿态估计结果
        /// </summary>
        /// <param name="bresult">Pose results array/姿态结果数组</param>
        /// <param name="img">Source image/源图像</param>
        /// <param name="options">Visualization options/可视化选项</param>
        /// <returns>New image with rendered poses/渲染了姿态的新图像</returns>
        /// <remarks>
        /// <para>
        /// Draws both keypoints (as circles) and skeleton connections (as lines)
        /// 绘制关键点(圆形)和骨架连接(线条)
        /// </para>
        /// <para>
        /// Uses multi-color scheme for better visualization
        /// 使用多色方案以获得更好的可视化效果
        /// </para>
        /// </remarks>
        public static Image<Rgb24> DrawPoses(Result[] bresult, Image<Rgb24> img, VisualizeOptions options)
        {
            KeyPointResult[] pose = bresult as KeyPointResult[];
            var output = img.Clone();

            // Keypoint connection relationships
            // 关节点连线关系
            int[,] edges = new int[17, 2] { { 0, 1 }, { 0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8},
             {7, 9}, {8, 10}, {5, 11}, {6, 12}, {11, 13}, {12, 14},{13, 15 }, {14, 16 }, {11, 12 } };

            // Color palette
            // 颜色库
            Color[] colors = new Color[18] {
            Color.FromRgb(255, 0, 0), Color.FromRgb(255, 85, 0), Color.FromRgb(255, 170, 0),
            Color.FromRgb(255, 255, 0), Color.FromRgb(170, 255, 0), Color.FromRgb(85, 255, 0),
            Color.FromRgb(0, 255, 0), Color.FromRgb(0, 255, 85), Color.FromRgb(0, 255, 170),
            Color.FromRgb(0, 255, 255), Color.FromRgb(0, 170, 255), Color.FromRgb(0, 85, 255),
            Color.FromRgb(0, 0, 255), Color.FromRgb(85, 0, 255), Color.FromRgb(170, 0, 255),
            Color.FromRgb(255, 0, 255), Color.FromRgb(255, 0, 170), Color.FromRgb(255, 0, 85)
        };

            string[] point_str = new string[] {
            "Nose", "Left Eye", "Right Eye", "Left Ear", "Right Ear",
            "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow",
            "Left Wrist", "Right Wrist", "Left Hip", "Right Hip",
            "Left Knee", "Right Knee", "Left Ankle", "Right Ankle"
        };

            for (int i = 0; i < pose.Length; ++i)
            {
                output.Mutate(ctx =>
                {
                    // Draw keypoints
                    // 绘制关节点
                    for (int p = 0; p < 17; p++)
                    {
                        if (pose[i].KeyPoints[p].Confidence < options.PointDrawThreshold) continue;

                        var point = pose[i].KeyPoints[p].Point;
                        ctx.Fill(colors[p], new EllipsePolygon(new SixLabors.ImageSharp.PointF(point.X, point.Y), 2));
                    }

                    // Draw bounding box
                    // 绘制边界框
                    var box = pose[i].Bounds;
                    var rect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                            box.BottomRight.X - box.TopLeft.X,
                                            box.BottomRight.Y - box.TopLeft.Y);
                    ctx.Draw(Pens.Solid(Color.Red, 2), rect);

                    // Draw label background
                    // 绘制标签背景
                    var labelRect = new RectangleF(box.TopLeft.X, box.TopLeft.Y,
                                                    box.BottomRight.X - box.TopLeft.X, 30);
                    ctx.Fill(Color.Yellow, labelRect);

                    // Draw text
                    // 绘制文本
                    var text = $"{pose[i].Category}-{pose[i].Confidence:0.00}";
                    ctx.DrawText(text, SystemFonts.CreateFont("Arial", 16), Color.Black,
                                new SixLabors.ImageSharp.PointF(box.TopLeft.X, box.TopLeft.Y + 5));

                    // Draw skeletal connections
                    // 绘制关节点连线
                    for (int p = 0; p < 17; p++)
                    {
                        if (pose[i].KeyPoints[edges[p, 0]].Confidence < options.PointDrawThreshold ||
                            pose[i].KeyPoints[edges[p, 1]].Confidence < options.PointDrawThreshold)
                        {
                            continue;
                        }

                        var start = pose[i].KeyPoints[edges[p, 0]].Point;
                        var end = pose[i].KeyPoints[edges[p, 1]].Point;

                        // Draw elliptical connections (thicker lines)
                        // 绘制椭圆形的连线(更粗的线)
                        var path = new PathBuilder().AddLine(start.X, start.Y, end.X, end.Y).Build();
                        ctx.Draw(colors[p], 3, path);
                    }
                });
            }

            return output;
        }

        /// <summary>
        /// Handler class for polymorphic visualization operations
        /// 多态可视化操作的处理程序类
        /// </summary>
        /// <remarks>
        /// Uses delegate pattern to provide flexible visualization method selection
        /// 使用委托模式提供灵活的可视化方法选择
        /// </remarks>
        public class VisualizeHandler
        {
            /// <summary>
            /// Delegate type for visualization methods
            /// 可视化方法的委托类型
            /// </summary>
            /// <param name="results">Detection results/检测结果</param>
            /// <param name="image">Source image/源图像</param>
            /// <param name="options">Visualization options/可视化选项</param>
            /// <returns>Visualized image/可视化后的图像</returns>
            public delegate Image<Rgb24> VisualizeDelegate(Result[] results, Image<Rgb24> image, VisualizeOptions options);

            private readonly VisualizeDelegate _drawingMethod;

            /// <summary>
            /// Initializes handler with specific visualization method
            /// 使用特定的可视化方法初始化处理程序
            /// </summary>
            /// <param name="drawingMethod">Visualization method to use/要使用的可视化方法</param>
            public VisualizeHandler(VisualizeDelegate drawingMethod)
            {
                _drawingMethod = drawingMethod;
            }

            /// <summary>
            /// Executes the configured visualization method
            /// 执行配置的可视化方法
            /// </summary>
            /// <param name="results">Detection results/检测结果</param>
            /// <param name="image">Source image/源图像</param>
            /// <param name="options">Visualization options/可视化选项</param>
            /// <returns>Visualized image/可视化后的图像</returns>
            public Image<Rgb24> ExecuteDrawing(Result[] results, Image<Rgb24> image, VisualizeOptions options)
            {
                return _drawingMethod(results, image, options);
            }
        }
    }

}
