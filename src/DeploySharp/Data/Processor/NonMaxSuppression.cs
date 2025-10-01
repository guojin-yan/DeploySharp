using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clipper2Lib;


namespace DeploySharp.Data
{
    //public class NonMaxSuppression
    //{
    //    public BoundingBox[] Run(BoundingBox[] candidateBoxes, float iouThreshold) => Run(new List<BoundingBox>(candidateBoxes), iouThreshold);
    //    public BoundingBox[] Run(List<BoundingBox> candidateBoxes, float iouThreshold)
    //    {
    //        if (candidateBoxes.Count == 0)
    //        {
    //            return Array.Empty<BoundingBox>();
    //        }

    //        // Sort boxes by confidence score in descending order
    //        candidateBoxes.Sort((boxA, boxB) => boxB.CompareTo(boxA));

    //        // Initialize selected boxes with the highest confidence box
    //        var selectedBoxes = new List<BoundingBox>(capacity: 8)
    //        {
    //            candidateBoxes[0]
    //        };

    //        // Process remaining candidate boxes
    //        for (var candidateIndex = 1; candidateIndex < candidateBoxes.Count; candidateIndex++)
    //        {
    //            var currentBox = candidateBoxes[candidateIndex];
    //            var shouldKeepBox = true;

    //            // Check against all selected boxes
    //            for (var selectedIndex = 0; selectedIndex < selectedBoxes.Count; selectedIndex++)
    //            {
    //                var selectedBox = selectedBoxes[selectedIndex];

    //                // Skip boxes with different class labels
    //                if (currentBox.NameIndex != selectedBox.NameIndex)
    //                {
    //                    continue;
    //                }

    //                // Exclude this box if it overlaps too much with an already selected box
    //                if (CalculateIntersectionOverUnion(currentBox, selectedBox) > iouThreshold)
    //                {
    //                    shouldKeepBox = false;
    //                    break;
    //                }
    //            }

    //            if (shouldKeepBox)
    //            {
    //                selectedBoxes.Add(currentBox);
    //            }
    //        }

    //        return selectedBoxes.ToArray();
    //    }

    //    protected virtual float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
    //public class RectNonMaxSuppression : NonMaxSuppression
    //{

    //    protected override float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
    //    {
    //        var rectangleA = boxA.Box;
    //        var rectangleB = boxB.Box;

    //        var areaA = rectangleA.Width * rectangleA.Height;
    //        if (areaA <= 0f) return 0f;

    //        var areaB = rectangleB.Width * rectangleB.Height;
    //        if (areaB <= 0f) return 0f;

    //        var intersectionArea = RectF.Intersect(rectangleA, rectangleB);
    //        var intersectionSize = intersectionArea.Width * intersectionArea.Height;

    //        return (float)intersectionSize / (areaA + areaB - intersectionSize);
    //    }
    //}

    //public class ObbNonMaxSuppression : NonMaxSuppression
    //{
    //    protected override float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
    //    {
    //        var rectangleA = boxA.Box;
    //        var rectangleB = boxB.Box;

    //        // 计算边界矩形的面积
    //        var boundingAreaA = rectangleA.Width * rectangleA.Height;
    //        if (boundingAreaA <= 0f) return 0f;

    //        var boundingAreaB = rectangleB.Width * rectangleB.Height;
    //        if (boundingAreaB <= 0f) return 0f;

    //        // 创建旋转矩形并获取顶点
    //        var rotatedRectA = RotatedRect.FromAxisAlignedRect(boxA.Box, boxA.Angle);
    //        var rotatedRectB = RotatedRect.FromAxisAlignedRect(boxB.Box, boxB.Angle);

    //        var verticesA = rotatedRectA.Points();
    //        var verticesB = rotatedRectB.Points();

    //        // 转换为Clipper库使用的路径格式
    //        var pathA = new Path64(verticesA.Select(v => new Point64(v.X, v.Y)));
    //        var pathB = new Path64(verticesB.Select(v => new Point64(v.X, v.Y)));

    //        // 准备Clipper操作
    //        var subjectPaths = new Paths64([pathA]);
    //        var clipPaths = new Paths64([pathB]);

    //        // 计算交集和并集
    //        var intersectionResult = Clipper.Intersect(subjectPaths, clipPaths, FillRule.EvenOdd);
    //        var unionResult = Clipper.Union(subjectPaths, clipPaths, FillRule.EvenOdd);

    //        // 检查计算结果是否有效
    //        if (intersectionResult.Count == 0 || unionResult.Count == 0)
    //        {
    //            return 0f;
    //        }

    //        // 计算实际相交面积和并集面积
    //        var actualIntersectionArea = Clipper.Area(intersectionResult[0]);
    //        var actualUnionArea = Clipper.Area(unionResult[0]);

    //        return (float)(actualIntersectionArea / actualUnionArea);
    //    }

    //}

    /// <summary>
    /// Performs Non-Maximum Suppression (NMS) to filter overlapping bounding boxes
    /// 执行非极大值抑制(NMS)以过滤重叠的边界框
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard post-processing step for object detection pipelines that removes
    /// redundant detections while preserving the most confident predictions.
    /// </para>
    /// <para>
    /// 目标检测管线的标准后处理步骤，可去除冗余检测同时保留最有把握的预测结果。
    /// </para>
    /// </remarks>
    public class NonMaxSuppression
    {
        /// <summary>
        /// Applies NMS to an array of candidate bounding boxes
        /// 对候选边界框数组应用NMS
        /// </summary>
        /// <param name="candidateBoxes">Input bounding boxes 输入的边界框数组</param>
        /// <param name="iouThreshold">
        /// Intersection-over-Union threshold for suppression
        /// 用于抑制的重叠度(IoU)阈值
        /// </param>
        /// <returns>Filtered boxes 过滤后的边界框</returns>
        /// <remarks>
        /// Higher thresholds retain more boxes, lower thresholds are more strict
        /// 阈值越高保留的框越多，阈值越低标准越严格
        /// </remarks>
        public BoundingBox[] Run(BoundingBox[] candidateBoxes, float iouThreshold) =>
            Run(new List<BoundingBox>(candidateBoxes), iouThreshold);

        /// <summary>
        /// Core NMS implementation that processes a list of bounding boxes
        /// 处理边界框列表的核心NMS实现
        /// </summary>
        /// <param name="candidateBoxes">Input bounding boxes 输入的边界框列表</param>
        /// <param name="iouThreshold">IoU suppression threshold 重叠度抑制阈值</param>
        /// <returns>Filtered boxes 过滤后的边界框</returns>
        public BoundingBox[] Run(List<BoundingBox> candidateBoxes, float iouThreshold)
        {
            if (candidateBoxes.Count == 0)
            {
                return Array.Empty<BoundingBox>();
            }

            // Sort boxes by confidence score in descending order
            // 按置信度分数降序排序边界框
            candidateBoxes.Sort((boxA, boxB) => boxB.CompareTo(boxA));

            // Initialize selected boxes with the highest confidence box
            // 用置信度最高的边界框初始化选中框集合
            var selectedBoxes = new List<BoundingBox>(capacity: 8)
        {
            candidateBoxes[0]
        };

            // Process remaining candidate boxes
            // 处理剩余的候选边界框
            for (var candidateIndex = 1; candidateIndex < candidateBoxes.Count; candidateIndex++)
            {
                var currentBox = candidateBoxes[candidateIndex];
                var shouldKeepBox = true;

                // Check against all selected boxes
                // 与所有已选中的框进行比较
                for (var selectedIndex = 0; selectedIndex < selectedBoxes.Count; selectedIndex++)
                {
                    var selectedBox = selectedBoxes[selectedIndex];

                    // Skip boxes with different class labels
                    // 跳过类别标签不同的框
                    if (currentBox.NameIndex != selectedBox.NameIndex)
                    {
                        continue;
                    }

                    // Exclude this box if it overlaps too much with an already selected box
                    // 如果与已选中框重叠度过高则排除此框
                    if (CalculateIntersectionOverUnion(currentBox, selectedBox) > iouThreshold)
                    {
                        shouldKeepBox = false;
                        break;
                    }
                }

                if (shouldKeepBox)
                {
                    selectedBoxes.Add(currentBox);
                }
            }

            return selectedBoxes.ToArray();
        }

        /// <summary>
        /// Calculates Intersection-over-Union metric for two bounding boxes
        /// 计算两个边界框的交并比(IoU)
        /// </summary>
        /// <param name="boxA">First bounding box 第一个边界框</param>
        /// <param name="boxB">Second bounding box 第二个边界框</param>
        /// <returns>
        /// IoU value between 0 (no overlap) and 1 (perfect overlap)
        /// 0到1之间的IoU值（0表示无重叠，1表示完全重叠）
        /// </returns>
        /// <exception cref="NotImplementedException">
        /// Thrown in base class - must be implemented by derived classes
        /// 基类中抛出 - 必须由派生类实现
        /// </exception>
        protected virtual float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Axis-aligned rectangle implementation of NMS
    /// 轴对齐矩形的NMS实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard NMS for rectangular bounding boxes without rotation.
    /// Uses simple rectangle intersection calculations.
    /// </para>
    /// <para>
    /// 适用于无旋转矩形边界框的标准NMS。
    /// 使用简单的矩形相交计算。
    /// </para>
    /// </remarks>
    public class RectNonMaxSuppression : NonMaxSuppression
    {
        /// <summary>
        /// Calculates IoU for axis-aligned rectangles
        /// 计算轴对齐矩形的IoU
        /// </summary>
        protected override float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
        {
            var rectangleA = boxA.Box;
            var rectangleB = boxB.Box;

            var areaA = rectangleA.Width * rectangleA.Height;
            if (areaA <= 0f) return 0f;

            var areaB = rectangleB.Width * rectangleB.Height;
            if (areaB <= 0f) return 0f;

            var intersectionArea = RectF.Intersect(rectangleA, rectangleB);
            var intersectionSize = intersectionArea.Width * intersectionArea.Height;

            return (float)intersectionSize / (areaA + areaB - intersectionSize);
        }
    }

    /// <summary>
    /// Oriented bounding box implementation of NMS
    /// 定向边界框(OBB)的NMS实现
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles rotated rectangles using polygon intersection calculations.
    /// More computationally expensive than axis-aligned version.
    /// </para>
    /// <para>
    /// 使用多边形相交计算处理旋转矩形。
    /// 比轴对齐版本计算量更大。
    /// </para>
    /// </remarks>
    public class ObbNonMaxSuppression : NonMaxSuppression
    {
        /// <summary>
        /// Calculates IoU for rotated rectangles using polygon clipping
        /// 使用多边形裁剪计算旋转矩形的IoU
        /// </summary>
        protected override float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
        {
            var rectangleA = boxA.Box;
            var rectangleB = boxB.Box;

            // Calculate bounding rectangle areas
            // 计算边界矩形的面积
            var boundingAreaA = rectangleA.Width * rectangleA.Height;
            if (boundingAreaA <= 0f) return 0f;

            var boundingAreaB = rectangleB.Width * rectangleB.Height;
            if (boundingAreaB <= 0f) return 0f;

            // Create rotated rectangles and get vertices
            // 创建旋转矩形并获取顶点
            var rotatedRectA = RotatedRect.FromAxisAlignedRect(boxA.Box, boxA.Angle);
            var rotatedRectB = RotatedRect.FromAxisAlignedRect(boxB.Box, boxB.Angle);

            var verticesA = rotatedRectA.Points();
            var verticesB = rotatedRectB.Points();

            // Convert to Clipper library format
            // 转换为Clipper库使用的路径格式
            var pathA = new Path64(verticesA.Select(v => new Point64(v.X, v.Y)));
            var pathB = new Path64(verticesB.Select(v => new Point64(v.X, v.Y)));

            // Prepare Clipper operations
            // 准备Clipper操作
            var subjectPaths = new Paths64([pathA]);
            var clipPaths = new Paths64([pathB]);

            // 计算交集和并集
            var intersectionResult = Clipper.Intersect(subjectPaths, clipPaths, FillRule.EvenOdd);
            var unionResult = Clipper.Union(subjectPaths, clipPaths, FillRule.EvenOdd);

            // 检查计算结果是否有效
            if (intersectionResult.Count == 0 || unionResult.Count == 0)
            {
                return 0f;
            }

            // 计算实际相交面积和并集面积
            var actualIntersectionArea = Clipper.Area(intersectionResult[0]);
            var actualUnionArea = Clipper.Area(unionResult[0]);

            return (float)(actualIntersectionArea / actualUnionArea);
        }

    }
}
