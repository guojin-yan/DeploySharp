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
    public class NonMaxSuppression
    {

        public BoundingBox[] Run(BoundingBox[] candidateBoxes, float iouThreshold) => Run(new List<BoundingBox>(candidateBoxes), iouThreshold);
        public BoundingBox[] Run(List<BoundingBox> candidateBoxes, float iouThreshold)
        {
            if (candidateBoxes.Count == 0)
            {
                return Array.Empty<BoundingBox>();
            }

            // Sort boxes by confidence score in descending order
            candidateBoxes.Sort((boxA, boxB) => boxB.CompareTo(boxA));

            // Initialize selected boxes with the highest confidence box
            var selectedBoxes = new List<BoundingBox>(capacity: 8)
            {
                candidateBoxes[0]
            };

            // Process remaining candidate boxes
            for (var candidateIndex = 1; candidateIndex < candidateBoxes.Count; candidateIndex++)
            {
                var currentBox = candidateBoxes[candidateIndex];
                var shouldKeepBox = true;

                // Check against all selected boxes
                for (var selectedIndex = 0; selectedIndex < selectedBoxes.Count; selectedIndex++)
                {
                    var selectedBox = selectedBoxes[selectedIndex];

                    // Skip boxes with different class labels
                    if (currentBox.NameIndex != selectedBox.NameIndex)
                    {
                        continue;
                    }

                    // Exclude this box if it overlaps too much with an already selected box
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

        protected virtual float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
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

    public class ObbNonMaxSuppression : NonMaxSuppression
    {
        protected override float CalculateIntersectionOverUnion(BoundingBox boxA, BoundingBox boxB)
        {
            var rectangleA = boxA.Box;
            var rectangleB = boxB.Box;

            // 计算边界矩形的面积
            var boundingAreaA = rectangleA.Width * rectangleA.Height;
            if (boundingAreaA <= 0f) return 0f;

            var boundingAreaB = rectangleB.Width * rectangleB.Height;
            if (boundingAreaB <= 0f) return 0f;

            // 创建旋转矩形并获取顶点
            var rotatedRectA = RotatedRect.FromAxisAlignedRect(boxA.Box, boxA.Angle);
            var rotatedRectB = RotatedRect.FromAxisAlignedRect(boxB.Box, boxB.Angle);

            var verticesA = rotatedRectA.Points();
            var verticesB = rotatedRectB.Points();

            // 转换为Clipper库使用的路径格式
            var pathA = new Path64(verticesA.Select(v => new Point64(v.X, v.Y)));
            var pathB = new Path64(verticesB.Select(v => new Point64(v.X, v.Y)));

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
