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
    /// Static class providing visualization methods for different computer vision results using ImageSharp
    /// 使用ImageSharp为不同计算机视觉结果提供可视化方法的静态类
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contains specialized rendering methods for various computer vision tasks:
    /// 包含针对各种计算机视觉任务的专门渲染方法:
    /// - Object detection (DetResult): Bounding boxes with labels and confidence scores
    ///   目标检测(DetResult): 带标签和置信度分数的边界框
    /// - Oriented bounding boxes (ObbResult): Rotated boxes for aerial/rotated object detection
    ///   定向边界框(ObbResult): 用于航拍/旋转目标检测的旋转框
    /// - Semantic segmentation (SegResult): Pixel-level masks with boundary boxes
    ///   语义分割(SegResult): 带边界框的像素级掩膜
    /// - Human pose estimation (KeyPointResult): Skeleton connections and keypoints
    ///   人体姿态估计(KeyPointResult): 骨架连接和关键点
    /// </para>
    /// <para>
    /// All methods follow these principles:
    /// 所有方法遵循以下原则:
    /// - Return new Image&lt;Rgb24&gt; instances, leaving original images unmodified
    ///   返回新的Image&lt;Rgb24&gt;实例，原始图像不被修改
    /// - Use consistent color schemes from VisionColors for class identification
    ///   使用VisionColors中的一致配色方案进行类别识别
    /// - Support customizable visualization through VisualizeOptions
    ///   通过VisualizeOptions支持可自定义的可视化
    /// - Handle edge cases like out-of-bounds coordinates gracefully
    ///   优雅地处理边界情况，如越界坐标
    /// </para>
    /// <para>
    /// Performance considerations:
    /// 性能考虑:
    /// - Uses ImageSharp's Mutate for efficient in-place drawing operations
    ///   使用ImageSharp的Mutate进行高效的就地绘制操作
    /// - Creates cloned images to avoid modifying inputs
    ///   创建克隆图像以避免修改输入
    /// - Leverages hardware-accelerated drawing where available
    ///   利用硬件加速绘制（如果可用）
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// // Load image and run detection
    /// using var image = Image.Load&lt;Rgb24&gt;("photo.jpg");
    /// var results = model.Predict(image);
    /// 
    /// // Visualize results
    /// var options = new VisualizeOptions(1.0f);
    /// using var visualized = Visualize.DrawDetResult(results, image, options);
    /// visualized.Save("output.jpg");
    /// </code>
    /// </example>
    /// <seealso cref="VisualizeOptions"/>
    /// <seealso cref="VisionColors"/>
    /// <seealso cref="DetResult"/>
    /// <seealso cref="ObbResult"/>
    /// <seealso cref="SegResult"/>
    /// <seealso cref="KeyPointResult"/>
    public static class Visualize
    {
        /// <summary>
        /// Draws detection results with bounding boxes, labels, and confidence scores
        /// 绘制带边界框、标签和置信度分数的检测结果
        /// </summary>
        /// <param name="bresult">Detection results array (polymorphic, will be cast to DetResult[]) / 检测结果数组（多态，将被转换为DetResult[]）</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options for colors, fonts, and sizes / 用于颜色、字体和大小的可视化选项</param>
        /// <returns>New image with rendered detections / 渲染了检测结果的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image or options is null
        /// 当image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// Each detection is rendered with:
        /// 每个检测结果渲染包含:
        /// - Colored bounding box (color based on class ID)
        ///   彩色边界框（颜色基于类别ID）
        /// - Yellow label background for text readability
        ///   黄色标签背景以提高文本可读性
        /// - Class name and confidence score (e.g., "person-0.95")
        ///   类别名称和置信度分数（例如 "person-0.95"）
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var results = model.Predict(image);
        /// var options = new VisualizeOptions(1.0f);
        /// using var visualized = Visualize.DrawDetResult(results, image, options);
        /// visualized.Save("detections.jpg");
        /// </code>
        /// </example>
        /// <seealso cref="DrawDetResult(DetResult[], Image{Rgb24}, VisualizeOptions)"/>
        public static Image<Rgb24> DrawDetResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options) 
        {
            return DrawDetResult(bresult as DetResult[], image, options);
        }

        /// <summary>
        /// Draws detection results with bounding boxes, labels, and confidence scores
        /// 绘制带边界框、标签和置信度分数的检测结果
        /// </summary>
        /// <param name="result">Detection results array / 检测结果数组</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered detections / 渲染了检测结果的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when result, image, or options is null
        /// 当result、image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// Internal implementation called by the polymorphic overload.
        /// 由多态重载调用的内部实现。
        /// </remarks>
        /// <seealso cref="DrawDetResult(Result[], Image{Rgb24}, VisualizeOptions)"/>
        public static Image<Rgb24> DrawDetResult(DetResult[] result, Image<Rgb24> image, VisualizeOptions options)
        {

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
        /// Draws oriented bounding box (OBB) results with rotated rectangles
        /// 使用旋转矩形绘制定向边界框(OBB)结果
        /// </summary>
        /// <param name="bresult">OBB results array (polymorphic, will be cast to ObbResult[]) / OBB结果数组（多态，将被转换为ObbResult[]）</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered OBBs / 渲染了OBB的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image or options is null
        /// 当image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Draws quadrilateral boxes with angle information.
        /// Unlike axis-aligned bounding boxes, OBBs have 4 corner points
        /// that can represent rotated objects like ships, vehicles in aerial images,
        /// or text at various angles.
        /// 绘制带角度信息的四边形框。
        /// 与轴对齐边界框不同，OBB有4个角点，可以表示航拍图像中的
        /// 旋转对象，如船只、车辆，或各种角度的文本。
        /// </para>
        /// <para>
        /// Lines are drawn between consecutive corner points to form the rotated box.
        /// 在连续角点之间绘制线条形成旋转框。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var results = obbModel.Predict(aerialImage);
        /// using var visualized = Visualize.DrawObbResult(results, aerialImage, options);
        /// visualized.Save("obb_detections.jpg");
        /// </code>
        /// </example>
        /// <seealso cref="DrawObbResult(ObbResult[], Image{Rgb24}, VisualizeOptions)"/>
        /// <seealso cref="ObbResult"/>
        public static Image<Rgb24> DrawObbResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options) 
        {
            return DrawObbResult(bresult as ObbResult[], image, options);
        }

        /// <summary>
        /// Draws oriented bounding box (OBB) results with rotated rectangles
        /// 使用旋转矩形绘制定向边界框(OBB)结果
        /// </summary>
        /// <param name="result">OBB results array / OBB结果数组</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered OBBs / 渲染了OBB的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when result, image, or options is null
        /// 当result、image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// Internal implementation that draws 4 lines connecting the corner points.
        /// 连接角点绘制4条线的内部实现。
        /// </remarks>
        /// <seealso cref="DrawObbResult(Result[], Image{Rgb24}, VisualizeOptions)"/>
        public static Image<Rgb24> DrawObbResult(ObbResult[] result, Image<Rgb24> image, VisualizeOptions options)
        {
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
        /// Draws segmentation results with masks and bounding boxes
        /// 绘制带掩膜和边界框的分割结果
        /// </summary>
        /// <param name="bresult">Segmentation results array (polymorphic, will be cast to SegResult[]) / 分割结果数组（多态，将被转换为SegResult[]）</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered masks / 渲染了掩膜的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when image or options is null
        /// 当image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Combines semi-transparent colored masks with bounding boxes for clear visualization.
        /// 结合半透明彩色掩膜与边界框以进行清晰可视化。
        /// </para>
        /// <para>
        /// Rendering process:
        /// 渲染过程:
        /// 1. Creates a temporary mask layer for each detection
        ///    为每个检测创建临时掩膜层
        /// 2. Pixels with mask value &gt; MaskMinimumConfidence are colored
        ///    掩膜值大于MaskMinimumConfidence的像素被着色
        /// 3. Mask layer is drawn with MaskAlpha transparency
        ///    掩膜层以MaskAlpha透明度绘制
        /// 4. Bounding box and label are drawn on top
        ///    边界框和标签绘制在顶部
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var results = segModel.Predict(image);
        /// var options = new VisualizeOptions(1.0f)
        /// {
        ///     MaskAlpha = 0.4f,  // More transparent
        ///     MaskMinimumConfidence = 0.5f
        /// };
        /// using var visualized = Visualize.DrawSegResult(results, image, options);
        /// </code>
        /// </example>
        /// <seealso cref="DrawSegResult(SegResult[], Image{Rgb24}, VisualizeOptions)"/>
        /// <seealso cref="SegResult"/>
        public static Image<Rgb24> DrawSegResult(Result[] bresult, Image<Rgb24> image, VisualizeOptions options) 
        {
            return DrawSegResult(bresult as SegResult[], image, options);
        }

        /// <summary>
        /// Draws segmentation results with masks and bounding boxes
        /// 绘制带掩膜和边界框的分割结果
        /// </summary>
        /// <param name="result">Segmentation results array / 分割结果数组</param>
        /// <param name="image">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered masks / 渲染了掩膜的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when result, image, or options is null
        /// 当result、image或options为null时抛出
        /// </exception>
        /// <remarks>
        /// Internal implementation that creates mask layers for each detection.
        /// 为每个检测创建掩膜层的内部实现。
        /// </remarks>
        /// <seealso cref="DrawSegResult(Result[], Image{Rgb24}, VisualizeOptions)"/>
        public static Image<Rgb24> DrawSegResult(SegResult[] result, Image<Rgb24> image, VisualizeOptions options)
        {
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
        /// Draws human pose estimation results with keypoints and skeleton connections
        /// 绘制带关键点和骨架连接的人体姿态估计结果
        /// </summary>
        /// <param name="bresult">Pose results array (polymorphic, will be cast to KeyPointResult[]) / 姿态结果数组（多态，将被转换为KeyPointResult[]）</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered poses / 渲染了姿态的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when img or options is null
        /// 当img或options为null时抛出
        /// </exception>
        /// <remarks>
        /// <para>
        /// Draws both keypoints (as filled circles) and skeleton connections (as colored lines).
        /// 绘制关键点（填充圆）和骨架连接（彩色线）。
        /// </para>
        /// <para>
        /// Keypoint indices (COCO format):
        /// 关键点索引（COCO格式）:
        /// 0: Nose, 1: Left Eye, 2: Right Eye, 3: Left Ear, 4: Right Ear,
        /// 5: Left Shoulder, 6: Right Shoulder, 7: Left Elbow, 8: Right Elbow,
        /// 9: Left Wrist, 10: Right Wrist, 11: Left Hip, 12: Right Hip,
        /// 13: Left Knee, 14: Right Knee, 15: Left Ankle, 16: Right Ankle
        /// </para>
        /// <para>
        /// Skeleton connections follow standard COCO pose format with 17 connections.
        /// 骨架连接遵循标准COCO姿态格式，共17个连接。
        /// </para>
        /// <para>
        /// Uses multi-color scheme for better visualization of different body parts.
        /// 使用多色方案以更好地区分不同身体部位。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var results = poseModel.Predict(image);
        /// var options = new VisualizeOptions(1.0f)
        /// {
        ///     PointDrawThreshold = 0.3f  // Lower threshold to show more keypoints
        /// };
        /// using var visualized = Visualize.DrawPoses(results, image, options);
        /// visualized.Save("poses.jpg");
        /// </code>
        /// </example>
        /// <seealso cref="DrawPoses(KeyPointResult[], Image{Rgb24}, VisualizeOptions)"/>
        /// <seealso cref="KeyPointResult"/>
        public static Image<Rgb24> DrawPoses(Result[] bresult, Image<Rgb24> img, VisualizeOptions options)
        {
            return DrawPoses(bresult as KeyPointResult[], img, options);
        }

        /// <summary>
        /// Draws human pose estimation results with keypoints and skeleton connections
        /// 绘制带关键点和骨架连接的人体姿态估计结果
        /// </summary>
        /// <param name="pose">Pose results array / 姿态结果数组</param>
        /// <param name="img">Source image / 源图像</param>
        /// <param name="options">Visualization options / 可视化选项</param>
        /// <returns>New image with rendered poses / 渲染了姿态的新图像</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when pose, img, or options is null
        /// 当pose、img或options为null时抛出
        /// </exception>
        /// <remarks>
        /// Internal implementation with hardcoded COCO skeleton connections and color palette.
        /// 带有硬编码COCO骨架连接和调色板的内部实现。
        /// </remarks>
        /// <seealso cref="DrawPoses(Result[], Image{Rgb24}, VisualizeOptions)"/>
        public static Image<Rgb24> DrawPoses(KeyPointResult[] pose, Image<Rgb24> img, VisualizeOptions options)
        {
            var output = img.Clone();

            // Keypoint connection relationships (COCO format)
            // 关节点连线关系（COCO格式）
            int[,] edges = new int[17, 2] { { 0, 1 }, { 0, 2}, {1, 3}, {2, 4}, {3, 5}, {4, 6}, {5, 7}, {6, 8},
             {7, 9}, {8, 10}, {5, 11}, {6, 12}, {11, 13}, {12, 14},{13, 15 }, {14, 16 }, {11, 12 } };

            // Color palette for different body parts
            // 不同身体部位的颜色库
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
        /// Handler class for polymorphic visualization operations using delegate pattern
        /// 使用委托模式的多态可视化操作处理程序类
        /// </summary>
        /// <remarks>
        /// <para>
        /// Provides a flexible way to select and execute visualization methods at runtime.
        /// This is used by the Pipeline class to automatically select the appropriate
        /// visualization method based on the model type.
        /// 提供在运行时选择和执行可视化方法的灵活方式。
        /// Pipeline类使用它根据模型类型自动选择适当的可视化方法。
        /// </para>
        /// <para>
        /// The delegate pattern allows different visualization methods to be treated uniformly,
        /// enabling easy extension for new result types.
        /// 委托模式允许不同的可视化方法被统一处理，便于扩展新的结果类型。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// // Create handler for detection visualization
        /// var detHandler = new VisualizeHandler(Visualize.DrawDetResult);
        /// 
        /// // Execute visualization
        /// var result = detHandler.ExecuteDrawing(detections, image, options);
        /// 
        /// // Can be assigned to Pipeline
        /// var pipeline = new Pipeline(ModelType.YOLOv8Det, "model.onnx");
        /// // pipeline internally uses VisualizeHandler
        /// </code>
        /// </example>
        /// <seealso cref="VisualizeDelegate"/>
        /// <seealso cref="Pipeline"/>
        public class VisualizeHandler
        {
            /// <summary>
            /// Delegate type for visualization methods
            /// 可视化方法的委托类型
            /// </summary>
            /// <param name="results">Detection results array / 检测结果数组</param>
            /// <param name="image">Source image / 源图像</param>
            /// <param name="options">Visualization options / 可视化选项</param>
            /// <returns>Visualized image / 可视化后的图像</returns>
            /// <remarks>
            /// Matches the signature of DrawDetResult, DrawObbResult, DrawSegResult, and DrawPoses methods.
            /// 与DrawDetResult、DrawObbResult、DrawSegResult和DrawPoses方法的签名匹配。
            /// </remarks>
            public delegate Image<Rgb24> VisualizeDelegate(Result[] results, Image<Rgb24> image, VisualizeOptions options);

            private readonly VisualizeDelegate _drawingMethod;

            /// <summary>
            /// Initializes handler with specific visualization method
            /// 使用特定的可视化方法初始化处理程序
            /// </summary>
            /// <param name="drawingMethod">Visualization method delegate to use / 要使用的可视化方法委托</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown when drawingMethod is null
            /// 当drawingMethod为null时抛出
            /// </exception>
            /// <example>
            /// <code language="csharp">
            /// var handler = new VisualizeHandler(Visualize.DrawDetResult);
            /// </code>
            /// </example>
            public VisualizeHandler(VisualizeDelegate drawingMethod)
            {
                _drawingMethod = drawingMethod;
            }

            /// <summary>
            /// Executes the configured visualization method
            /// 执行配置的可视化方法
            /// </summary>
            /// <param name="results">Detection results / 检测结果</param>
            /// <param name="image">Source image / 源图像</param>
            /// <param name="options">Visualization options / 可视化选项</param>
            /// <returns>Visualized image / 可视化后的图像</returns>
            /// <exception cref="ArgumentNullException">
            /// Thrown when results, image, or options is null
            /// 当results、image或options为null时抛出
            /// </exception>
            /// <example>
            /// <code language="csharp">
            /// var handler = new VisualizeHandler(Visualize.DrawSegResult);
            /// using var visualized = handler.ExecuteDrawing(segResults, image, options);
            /// </code>
            /// </example>
            public Image<Rgb24> ExecuteDrawing(Result[] results, Image<Rgb24> image, VisualizeOptions options)
            {
                return _drawingMethod(results, image, options);
            }
        }
    }

}
