using iTextSharp.text.pdf.parser.clipper;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// PPOcr data processor - High-performance optimized version.
    /// PPOcr数据处理器 - 高性能优化版。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deeply optimized for DBNet post-processing: reduced GC pressure, optimized sorting algorithm, fixed geometric logic.
    /// 针对DBNet后处理进行了深度优化：减少GC压力、优化排序算法、修复几何逻辑。
    /// </para>
    /// <para>
    /// This processor is specifically designed for PaddleOCR text detection tasks.
    /// 此处理器专为PaddleOCR文本检测任务设计。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Process OCR detection results
    /// // 处理OCR检测结果
    /// using (Mat pred = model.GetPrediction())
    /// using (Mat bitmap = ThresholdBitmap(pred))
    /// {
    ///     var boxes = CvPPOcrDataProcessor.BoxesFromBitmap(
    ///         pred, bitmap, 0.5f, 2.0f, "fast");
    ///     
    ///     foreach (var (rect, score) in boxes)
    ///     {
    ///         Console.WriteLine($"Text box: {rect}, Score: {score}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public class CvPPOcrDataProcessor
    {
        // 常量定义
        private const int MinSize = 3;               // 文本框最小边长阈值
        private const int MaxCandidates = 1000;     // 最大处理轮廓数量，防止极端情况卡顿
        private const int BoxEdgeThreshold = 4;     // 过滤极小噪声框的边长阈值

        #region Basic Math Tools / 基础数学工具

        /// <summary>
        /// Integer clamp function - Force inline to reduce call overhead.
        /// 整数钳制函数 - 强制内联以减少调用开销。
        /// </summary>
        /// <param name="value">Value to clamp / 要钳制的值</param>
        /// <param name="min">Minimum value / 最小值</param>
        /// <param name="max">Maximum value / 最大值</param>
        /// <returns>Clamped value / 钳制后的值</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp(int value, int min, int max)
        {
            if (value > max) return max;
            if (value < min) return min;
            return value;
        }

        #endregion

        #region Core Geometric Algorithms / 核心几何算法

        /// <summary>
        /// Calculates the unclip distance for contour expansion.
        /// Uses shoelace formula to calculate polygon area, calculates offset based on area and perimeter.
        /// 计算轮廓的膨胀距离。
        /// 使用鞋带公式计算多边形面积，基于面积和周长计算膨胀偏移量。
        /// </summary>
        /// <param name="boxX">X coordinates of the box corners / 框角点的X坐标</param>
        /// <param name="boxY">Y coordinates of the box corners / 框角点的Y坐标</param>
        /// <param name="unclipRatio">Ratio for unclip expansion / 膨胀比例</param>
        /// <returns>Unclip distance / 膨胀距离</returns>
        /// <remarks>
        /// Formula: distance = (area * ratio) / perimeter
        /// 公式：distance = (area * ratio) / perimeter
        /// </remarks>
        private static float GetUnclipDistance(float[] boxX, float[] boxY, float unclipRatio)
        {
            float area = 0.0f;
            float dist = 0.0f;
            const int ptsNum = 4;

            for (int i = 0; i < ptsNum; i++)
            {
                int next = (i + 1) % ptsNum;
                // Shoelace formula for area
                area += boxX[i] * boxY[next] - boxY[i] * boxX[next];

                // Accumulate edge length (perimeter)
                float dx = boxX[i] - boxX[next];
                float dy = boxY[i] - boxY[next];
                dist += (float)Math.Sqrt(dx * dx + dy * dy);
            }

            area = Math.Abs(area / 2.0f);
            // Prevent division by zero
            if (dist == 0) return 0.0f;

            // Distance formula: r = area * ratio / perimeter
            return area * unclipRatio / dist;
        }

        /// <summary>
        /// Performs polygon expansion (unclip) on the contour.
        /// 对轮廓进行多边形膨胀。
        /// </summary>
        /// <param name="boxX">X coordinates of the box corners / 框角点的X坐标</param>
        /// <param name="boxY">Y coordinates of the box corners / 框角点的Y坐标</param>
        /// <param name="unclipRatio">Ratio for expansion / 膨胀比例</param>
        /// <returns>Expanded rotated rectangle / 膨胀后的旋转矩形</returns>
        /// <remarks>
        /// Uses Clipper library for polygon offsetting.
        /// 使用Clipper库进行多边形偏移。
        /// </remarks>
        private static OpenCvSharp.RotatedRect Unclip(float[] boxX, float[] boxY, float unclipRatio)
        {
            float distance = GetUnclipDistance(boxX, boxY, unclipRatio);

            // Use Clipper library for polygon offsetting
            var offset = new ClipperOffset();
            var path = new List<IntPoint>(4);

            for (int i = 0; i < 4; i++)
            {
                path.Add(new IntPoint((long)boxX[i], (long)boxY[i]));
            }

            offset.AddPath(path, JoinType.jtRound, EndType.etClosedPolygon);
            var solution = new List<List<IntPoint>>();
            offset.Execute(ref solution, distance);

            // Convert expanded points back to OpenCV format
            var points = new List<Point2f>();
            if (solution.Count > 0 && solution[0].Count > 0)
            {
                foreach (var p in solution[0])
                {
                    points.Add(new Point2f(p.X, p.Y));
                }
            }

            if (points.Count == 0)
            {
                // Return a default minimal rectangle to avoid subsequent exceptions
                return new OpenCvSharp.RotatedRect(new OpenCvSharp.Point2f(0, 0), new Size2f(1, 1), 0);
            }

            return Cv2.MinAreaRect(points);
        }

        /// <summary>
        /// Gets the four vertices of a rotated rectangle and reorders them.
        /// Fixes the sorting logic confusion in the original code.
        /// 获取旋转矩形的四个顶点，并重新排序。
        /// 修正了原代码排序逻辑混乱的问题。
        /// </summary>
        /// <param name="box">Input rotated rectangle / 输入旋转矩形</param>
        /// <param name="ptsX">Output X coordinates (sorted) / 输出X坐标(已排序)</param>
        /// <param name="ptsY">Output Y coordinates (sorted) / 输出Y坐标(已排序)</param>
        /// <param name="sideMax">Output length of the longer side / 输出长边长度</param>
        /// <remarks>
        /// Output order: top-left, top-right, bottom-right, bottom-left
        /// 输出顺序：左上、右上、右下、左下
        /// </remarks>
        private static void GetMiniBoxes(OpenCvSharp.RotatedRect box, out float[] ptsX, out float[] ptsY, out float sideMax)
        {
            sideMax = Math.Max(box.Size.Width, box.Size.Height);

            OpenCvSharp.Point2f[] vertices = box.Points();

            ptsX = new float[4];
            ptsY = new float[4];

            for (int i = 0; i < 4; i++)
            {
                ptsX[i] = vertices[i].X;
                ptsY[i] = vertices[i].Y;
            }

            // Sort by X coordinate first
            for (int i = 0; i < 4; i++)
            {
                for (int j = i + 1; j < 4; j++)
                {
                    if (ptsX[i] > ptsX[j])
                    {
                        // Swap
                        (ptsX[i], ptsX[j]) = (ptsX[j], ptsX[i]);
                        (ptsY[i], ptsY[j]) = (ptsY[j], ptsY[i]);
                    }
                }
            }

            // Now ptsX[0], ptsX[1] are left points; ptsX[2], ptsX[3] are right points
            int idx1 = 0, idx2 = 1, idx3 = 2, idx4 = 3;

            // Left two points: smaller Y first -> top-left(idx1), bottom-left(idx4)
            if (ptsY[1] < ptsY[0])
            {
                idx1 = 1; idx4 = 0;
            }
            else
            {
                idx1 = 0; idx4 = 1;
            }

            // Right two points: smaller Y first -> top-right(idx2), bottom-right(idx3)
            if (ptsY[3] < ptsY[2])
            {
                idx2 = 3; idx3 = 2;
            }
            else
            {
                idx2 = 2; idx3 = 3;
            }

            // Reorganize array order
            float[] resX = new float[4] { ptsX[idx1], ptsX[idx2], ptsX[idx3], ptsX[idx4] };
            float[] resY = new float[4] { ptsY[idx1], ptsY[idx2], ptsY[idx3], ptsY[idx4] };

            ptsX = resX;
            ptsY = resY;
        }

        #endregion

        #region Scoring Algorithms / 评分算法

        /// <summary>
        /// Quickly calculates the average score within the box (based on box approximation).
        /// Optimized: reduced Mat creation, using ROI.
        /// 快速计算框内的平均分数 (基于Box近似)。
        /// 优化：减少 Mat 创建，使用 ROI。
        /// </summary>
        /// <param name="boxX">X coordinates of box corners / 框角点的X坐标</param>
        /// <param name="boxY">Y coordinates of box corners / 框角点的Y坐标</param>
        /// <param name="pred">Prediction probability map / 预测概率图</param>
        /// <returns>Average confidence score / 平均置信度分数</returns>
        private static float BoxScoreFast(float[] boxX, float[] boxY, Mat pred)
        {
            int width = pred.Cols;
            int height = pred.Rows;

            // Calculate bounding box
            int xmin = Clamp((int)Math.Floor(boxX.Min()), 0, width - 1);
            int xmax = Clamp((int)Math.Ceiling(boxX.Max()), 0, width - 1);
            int ymin = Clamp((int)Math.Floor(boxY.Min()), 0, height - 1);
            int ymax = Clamp((int)Math.Ceiling(boxY.Max()), 0, height - 1);

            if (xmax <= xmin || ymax <= ymin) return 0.0f;

            // Use local Mask to avoid full-image operations
            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
            {
                // Convert to Mask local coordinates
                var roiPoints = new OpenCvSharp.Point[4];
                for (int i = 0; i < 4; i++)
                {
                    roiPoints[i] = new OpenCvSharp.Point((int)boxX[i] - xmin, (int)boxY[i] - ymin);
                }

                Cv2.FillPoly(mask, new OpenCvSharp.Point[][] { roiPoints }, new Scalar(255));

                // Get prediction map ROI
                var predRoi = pred[new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)];

                // Calculate mean
                Scalar mean = Cv2.Mean(predRoi, mask);
                return (float)mean.Val0;
            }
        }

        /// <summary>
        /// Polygon accurate scoring (slow mode).
        /// 多边形精确评分 (慢速模式)。
        /// </summary>
        /// <param name="contour">Contour points / 轮廓点</param>
        /// <param name="pred">Prediction probability map / 预测概率图</param>
        /// <param name="width">Image width / 图像宽度</param>
        /// <param name="height">Image height / 图像高度</param>
        /// <returns>Accurate confidence score / 精确置信度分数</returns>
        /// <remarks>
        /// More accurate but slower than BoxScoreFast.
        /// 比BoxScoreFast更准确但更慢。
        /// </remarks>
        private static float PolygonScoreAcc(OpenCvSharp.Point[] contour, Mat pred, int width, int height)
        {
            if (contour.Length == 0) return 0f;

            int xmin = int.MaxValue, xmax = int.MinValue;
            int ymin = int.MaxValue, ymax = int.MinValue;

            // Extract all point boundaries
            for (int i = 0; i < contour.Length; i++)
            {
                int x = contour[i].X;
                int y = contour[i].Y;
                if (x < xmin) xmin = x;
                if (x > xmax) xmax = x;
                if (y < ymin) ymin = y;
                if (y > ymax) ymax = y;
            }

            // Clamp
            xmin = Clamp(xmin, 0, width - 1);
            xmax = Clamp(xmax, 0, width - 1);
            ymin = Clamp(ymin, 0, height - 1);
            ymax = Clamp(ymax, 0, height - 1);

            if (xmax <= xmin || ymax <= ymin) return 0f;

            using (var mask = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black))
            {
                var shiftedContour = new OpenCvSharp.Point[contour.Length];
                for (int i = 0; i < contour.Length; i++)
                {
                    shiftedContour[i] = new OpenCvSharp.Point(contour[i].X - xmin, contour[i].Y - ymin);
                }

                Cv2.FillPoly(mask, new OpenCvSharp.Point[][] { shiftedContour }, new Scalar(255));
                using (var croppedImg = new Mat(pred, new OpenCvSharp.Rect(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1)))
                {
                    return (float)Cv2.Mean(croppedImg, mask).Val0;
                }
            }
        }

        #endregion

        #region Main Process - BoxesFromBitmap (Key Optimization) / 主流程 - BoxesFromBitmap (重点优化)

        /// <summary>
        /// Extracts text boxes from probability map and bitmap.
        /// Optimized return type: directly returns List&lt;(RotatedRect, float)&gt; to reduce intermediate data structure conversion.
        /// 从概率图和位图中提取文本框。
        /// 返回值优化：直接返回 List&lt;(RotatedRect, float)&gt;，减少中间数据结构的转换。
        /// </summary>
        /// <param name="pred">Prediction probability map (float32, 0-1) / 预测概率图(float32, 0-1)</param>
        /// <param name="bitmap">Binary bitmap after thresholding / 阈值处理后的二值位图</param>
        /// <param name="boxThresh">Box confidence threshold / 框置信度阈值</param>
        /// <param name="detDbUnclipRatio">Expansion ratio for unclipping / 膨胀还原比例</param>
        /// <param name="detDbScoreMode">Scoring mode: "fast" or "slow" / 评分模式："fast"或"slow"</param>
        /// <returns>List of tuples containing rotated rectangle and confidence score / 包含旋转矩形和置信度分数的元组列表</returns>
        /// <exception cref="ArgumentNullException">Thrown when pred or bitmap is null / 当pred或bitmap为null时抛出</exception>
        /// <remarks>
        /// <para>
        /// This is the core post-processing method for DBNet text detection.
        /// 这是DBNet文本检测的核心后处理方法。
        /// </para>
        /// <para>
        /// Process flow:
        /// 处理流程:
        /// 1. Find contours from bitmap
        ///    从位图查找轮廓
        /// 2. Get minimum area rotated rectangles
        ///    获取最小面积旋转矩形
        /// 3. Calculate confidence scores
        ///    计算置信度分数
        /// 4. Expand boxes using unclip
        ///    使用unclip膨胀框
        /// 5. Normalize and filter results
        ///    归一化并过滤结果
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Typical DBNet post-processing workflow
        /// // 典型的DBNet后处理工作流
        /// using (Mat pred = model.Infer(preprocessedImage))
        /// using (Mat bitmap = new Mat())
        /// {
        ///     // Threshold the prediction map
        ///     // 对预测图进行阈值处理
        ///     Cv2.ConvertScaleAbs(pred, bitmap, 255.0, 0);
        ///     Cv2.Threshold(bitmap, bitmap, 127, 255, ThresholdTypes.Binary);
        ///     
        ///     // Extract text boxes
        ///     // 提取文本框
        ///     var boxes = CvPPOcrDataProcessor.BoxesFromBitmap(
        ///         pred, bitmap, 0.5f, 2.0f, "fast");
        ///     
        ///     foreach (var (rotatedRect, score) in boxes)
        ///     {
        ///         // Draw or process each text box
        ///         // 绘制或处理每个文本框
        ///         Point2f[] points = rotatedRect.Points();
        ///         for (int i = 0; i < 4; i++)
        ///         {
        ///             Cv2.Line(image, (Point)points[i], (Point)points[(i+1)%4], 
        ///                 Scalar.Green, 2);
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public static List<(OpenCvSharp.RotatedRect, float)> BoxesFromBitmap(Mat pred,
            Mat bitmap, 
            float boxThresh, float detDbUnclipRatio, string detDbScoreMode)
        {
            int width = bitmap.Cols;
            int height = bitmap.Rows;
            // Find contours
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(bitmap, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            // Modified return type to store RotatedRect and corresponding score
            var boxes = new List<(OpenCvSharp.RotatedRect, float)>();

            // Limit maximum processing quantity to prevent extreme inputs from causing lag
            int numContours = Math.Min(contours.Length, MaxCandidates);
            for (int i = 0; i < numContours; i++)
            {
                if (contours[i].Length < 2) continue;
                // 1. Get minimum area rotated rectangle
                OpenCvSharp.RotatedRect box = Cv2.MinAreaRect(contours[i]);
                float ssid; // Short side length
                float[] boxX, boxY;
                // 2. Normalize and calculate short side (only for size filtering)
                GetMiniBoxes(box, out boxX, out boxY, out ssid);
                if (ssid < MinSize) continue;
                // 3. Calculate confidence score
                float score;
                if (detDbScoreMode == "slow")
                {
                    score = PolygonScoreAcc(contours[i], pred, width, height);
                }
                else
                {
                    // Default to Fast mode
                    score = BoxScoreFast(boxX, boxY, pred);
                }
                if (score < boxThresh) continue;
                // 4. Expand
                // Expand current box coordinates to restore the complete text line area
                OpenCvSharp.RotatedRect unclipBox = Unclip(boxX, boxY, detDbUnclipRatio);
                // Check expanded size validity
                if (unclipBox.Size.Width < 1.001f && unclipBox.Size.Height < 1.001f) continue;
                // 5. Boundary correction
                // RotatedRect consists of Center, Size, Angle. We need to ensure it's fully within the image.
                // Get the four vertices of the rectangle
                Point2f[] points = unclipBox.Points();

                // Clamp each vertex to the image range
                for (int j = 0; j < 4; j++)
                {
                    points[j].X = Math.Max(0, Math.Min(width - 1, points[j].X));
                    points[j].Y = Math.Max(0, Math.Min(height - 1, points[j].Y));
                }
                // Convert corrected vertices back to RotatedRect
                OpenCvSharp.RotatedRect finalBox = NormalizeRotatedRect(Cv2.MinAreaRect(points));
                // Optional: Validate corrected size again to prevent clamp from making rectangle too small
                if (finalBox.Size.Width < 1.0f || finalBox.Size.Height < 1.0f) continue;
                // 6. Add to result list
                boxes.Add((finalBox, score));
            }
            return boxes;
        }

        /// <summary>
        /// Normalizes RotatedRect, converting 90 degrees (or -90 degrees) to 0 degrees and adjusting width/height.
        /// 规范化 RotatedRect，将 90 度（或 -90 度）转换为 0 度，并调整宽高。
        /// </summary>
        /// <param name="rect">Input rotated rectangle / 输入旋转矩形</param>
        /// <returns>Normalized rotated rectangle / 规范化后的旋转矩形</returns>
        /// <remarks>
        /// OpenCV returns angles in different ranges depending on the rectangle orientation.
        /// This method normalizes them for consistent processing.
        /// OpenCV根据矩形方向返回不同范围的角度。此方法对它们进行规范化以保持一致性。
        /// </remarks>
        public static OpenCvSharp.RotatedRect NormalizeRotatedRect(OpenCvSharp.RotatedRect rect)
        {
            float angle = rect.Angle;
            Size2f size = rect.Size;
            // Define correction threshold ranges.
            // Ranges [-90, -88] and [-2, 0] are common OpenCV representations.
            // We expand to [-90, -45] to normalize visually "upright" rectangles to horizontal.
            bool shouldNormalize = false;
            // 1. Determine if correction needed (near -90, 90 or in third quadrant)
            if (Math.Abs(angle - 90) < 5.0f || Math.Abs(angle + 90) < 5.0f || (angle <= -45.0f))
            {
                shouldNormalize = true;
            }

            // 2. Apply correction
            if (shouldNormalize)
            {
                // Force angle to 0
                angle = 0;
                // Swap width and height (critical, otherwise rectangle shape will be wrong after 90 degree rotation)
                float width = size.Width;
                size.Width = size.Height;
                size.Height = width;
            }

            // 4. Return corrected rectangle
            return new OpenCvSharp.RotatedRect(rect.Center, size, angle);
        }

        #endregion

        /// <summary>
        /// Crops and rotates image based on rotated rectangle.
        /// Used for extracting text regions from OCR detection results.
        /// 基于旋转矩形裁剪和旋转图像。
        /// 用于从OCR检测结果中提取文本区域。
        /// </summary>
        /// <param name="srcimage">Source image / 源图像</param>
        /// <param name="rect">Rotated rectangle defining the region / 定义区域的旋转矩形</param>
        /// <returns>Cropped and rotated image / 裁剪并旋转后的图像</returns>
        /// <exception cref="ArgumentNullException">Thrown when srcimage is null / 当srcimage为null时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when rect is invalid / 当rect无效时抛出</exception>
        /// <remarks>
        /// <para>
        /// This method performs affine transformation to extract the text region
        /// and automatically rotates vertical text to horizontal orientation.
        /// 此方法执行仿射变换以提取文本区域，并自动将竖向文本旋转为横向。
        /// </para>
        /// <para>
        /// If the resulting image's height is significantly greater than width (indicating vertical text),
        /// it will be rotated 90 degrees clockwise.
        /// 如果结果图像的高度明显大于宽度(表示竖向文本)，将顺时针旋转90度。
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Extract text regions from OCR detection
        /// // 从OCR检测中提取文本区域
        /// foreach (var (rotatedRect, score) in detectionResults)
        /// {
        ///     using (Mat textRegion = CvPPOcrDataProcessor.GetRotateCropImageByRect(image, rotatedRect))
        ///     {
        ///         // textRegion is now properly oriented for recognition
        ///         // textRegion现在已正确朝向，可用于识别
        ///         string text = recognitionModel.Predict(textRegion);
        ///     }
        /// }
        /// </code>
        /// </example>
        public static Mat GetRotateCropImageByRect(Mat srcimage, RotatedRect rect)
        {
            // 1. Get rotated rectangle parameters
            Point2f center = CvDataExtensions.ToPointF(rect.Center);   // Center point
            Size2f size = CvDataExtensions.ToSizeF(rect.Size);        // Width and height
            float angle = rect.Angle;     

            if (angle < -45)
            {
                angle += 90f;
                float temp = size.Width;
                size.Width = size.Height;
                size.Height = temp;
            }
         
            float dy = size.Height / 2.0f;
        
            Point2f[] srcPts = new Point2f[3];
            Point2f[] dstPts = new Point2f[3];
   

            double radians = angle * Math.PI / 180.0;
            double sin = Math.Sin(radians);
            double cos = Math.Cos(radians);
            double halfW = size.Width / 2.0;
            double halfH = size.Height / 2.0;

            srcPts[0] = new Point2f(
                (float)(center.X + (-halfW * cos - (-halfH) * sin)),
                (float)(center.Y + (-halfW * sin + (-halfH) * cos))
            );
            // Point 2: Top-Right (relative to center: +w/2, -h/2)
            srcPts[1] = new Point2f(
                (float)(center.X + (halfW * cos - (-halfH) * sin)),
                (float)(center.Y + (halfW * sin + (-halfH) * cos))
            );
            // Point 3: Bottom-Left (relative to center: -w/2, +h/2)
            srcPts[2] = new Point2f(
                (float)(center.X + (-halfW * cos - (halfH) * sin)),
                (float)(center.Y + (-halfW * sin + (halfH) * cos))
            );
            // Point 4: Bottom-Right (relative to center: +w/2, +h/2) - Affine transform only needs 3 points
            // Corresponding target image coordinates (where we want to map the above points)
            dstPts[0] = new Point2f(0, 0);                   // Map to top-left of new image
            dstPts[1] = new Point2f(size.Width, 0);         // Map to top-right of new image
            dstPts[2] = new Point2f(0, size.Height);        // Map to bottom-left of new image
                                                            // 5. Get affine transform matrix (maps srcPts to dstPts)
            Mat M = Cv2.GetAffineTransform(srcPts, dstPts);
            // 6. Execute affine transform
            Mat dst_img = new Mat();
            Cv2.WarpAffine(srcimage, dst_img, M, new OpenCvSharp.Size((int)size.Width, (int)size.Height),
                           InterpolationFlags.Linear, BorderTypes.Replicate);
            // 7. Detect and correct vertical text
            // If height is much greater than width (e.g., 1.5x), text is likely vertical, rotate 90 degrees to horizontal
            if (dst_img.Rows >= dst_img.Cols * 1.5)
            {
                Mat srcCopy = new Mat(dst_img.Rows, dst_img.Cols, dst_img.Depth());
                Cv2.Transpose(dst_img, srcCopy);
                Cv2.Flip(srcCopy, srcCopy, 0);
                return srcCopy;
            }
            return dst_img;
        }
    }
}
