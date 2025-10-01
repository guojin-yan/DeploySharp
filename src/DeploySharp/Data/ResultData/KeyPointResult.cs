using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents a single detected keypoint with location and confidence score
    /// 表示带有位置和置信度分数的单个检测关键点
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used in pose estimation, facial landmark detection, and other feature point detection tasks.
    /// Typically part of a larger <see cref="KeyPointResult"/> collection.
    /// </para>
    /// <para>
    /// 用于姿态估计、面部关键点检测和其他特征点检测任务。
    /// 通常是<see cref="KeyPointResult"/>集合的一部分。
    /// </para>
    /// </remarks>
    public struct KeyPoint
    {
        /// <summary>
        /// Detection confidence score (0-1 range)
        /// 检测置信度分数(0-1范围)
        /// </summary>
        /// <value>
        /// Values closer to 1 indicate higher reliability.
        /// Score below 0.5 typically indicates low-confidence detection.
        /// 接近1的值表示更高的可靠性。
        /// 低于0.5的分数通常表示低置信度检测。
        /// </value>
        public float Confidence { get; set; }

        /// <summary>
        /// Spatial coordinates of the keypoint
        /// 关键点的空间坐标
        /// </summary>
        /// <value>
        /// Pixel coordinates relative to the source image
        /// 相对于源图像的像素坐标
        /// </value>
        public Point Point { get; set; }

        /// <summary>
        /// Returns formatted string representation of the keypoint
        /// 返回关键点的格式化字符串表示
        /// </summary>
        /// <returns>
        /// String containing both confidence and coordinates
        /// 包含置信度和坐标的字符串
        /// </returns>
        public override string ToString()
        {
            return $"[Confidence: {Confidence.ToString("0.00")} Point: {Point}]";
        }

        /// <summary>
        /// Calculates distance to another keypoint
        /// 计算到另一个关键点的距离
        /// </summary>
        /// <param name="other">Target keypoint 目标关键点</param>
        /// <returns>Euclidean distance in pixels 像素为单位的欧几里得距离</returns>
        public float DistanceTo(KeyPoint other)
        {
            float dx = Point.X - other.Point.X;
            float dy = Point.Y - other.Point.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// Represents a collection of keypoints detected in an image
    /// 表示图像中检测到的关键点集合
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inherits from <see cref="DetResult"/> while adding keypoint-specific functionality.
    /// Used for human pose estimation, facial landmarks, and object part detection.
    /// </para>
    /// <para>
    /// 继承自<see cref="DetResult"/>同时增加了关键点特定功能。
    /// 用于人体姿态估计、面部关键点和物体部件检测。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var kpResult = new KeyPointResult {
    ///     Bounds = new Rect(100, 100, 200, 300),
    ///     KeyPoints = new KeyPoint[] {
    ///         new KeyPoint { Point = new Point(120, 130), Confidence = 0.95f },
    ///         new KeyPoint { Point = new Point(150, 140), Confidence = 0.92f }
    ///     }
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public class KeyPointResult : DetResult, IEnumerable<KeyPoint>
    {
        /// <summary>
        /// Array of detected keypoints
        /// 检测到的关键点数组
        /// </summary>
        /// <value>
        /// Ordered collection where index may correspond to specific semantic meaning
        /// (e.g., index 0 = nose, index 1 = left eye in facial landmarks)
        /// 有序集合，其中索引可能对应特定的语义含义
        /// (例如在面部关键点中，索引0=鼻子，索引1=左眼)
        /// </value>
        public KeyPoint[] KeyPoints { get; set; }

        /// <summary>
        /// Initializes a new empty keypoint result
        /// 初始化一个新的空关键点结果
        /// </summary>
        /// <remarks>
        /// Automatically sets <see cref="Result.Type"/> to <see cref="ResultType.KeyPoints"/>
        /// 自动将<see cref="Result.Type"/>设置为<see cref="ResultType.KeyPoints"/>
        /// </remarks>
        public KeyPointResult()
        {
            Type = ResultType.KeyPoints;
        }

        /// <summary>
        /// Provides indexed access to individual keypoints
        /// 提供对单个关键点的索引访问
        /// </summary>
        /// <param name="index">Zero-based keypoint index 从零开始的关键点索引</param>
        /// <returns>The keypoint at specified index 指定索引处的关键点</returns>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when index is outside valid range
        /// 当索引超出有效范围时抛出
        /// </exception>
        public KeyPoint this[int index] => KeyPoints[index];

        /// <summary>
        /// Returns the number of detected keypoints
        /// 返回检测到的关键点数量
        /// </summary>
        public int Count => KeyPoints?.Length ?? 0;

        /// <summary>
        /// Gets enumerator for iterating through keypoints
        /// 获取用于遍历关键点的枚举器
        /// </summary>
        /// <returns>Keypoint enumerator 关键点枚举器</returns>
        public IEnumerator<KeyPoint> GetEnumerator()
        {
            return ((IEnumerable<KeyPoint>)(KeyPoints ?? Array.Empty<KeyPoint>())).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns formatted string representation of all keypoints
        /// 返回所有关键点的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Combined string with base detection info and keypoint list
        /// 包含基础检测信息和关键点列表的组合字符串
        /// </returns>
        public override string ToString()
        {
            return $"{base.ToString()}, KeyPoints: [{string.Join("; ", KeyPoints?.Select(kp => kp.ToString()) ?? Enumerable.Empty<string>())}]";
        }

        /// <summary>
        /// Creates a deep copy of this keypoint result
        /// 创建此关键点结果的深拷贝
        /// </summary>
        public new KeyPointResult Clone()
        {
            return new KeyPointResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Category = Category,
                Bounds = Bounds,
                KeyPoints = KeyPoints?.Select(kp => new KeyPoint
                {
                    Confidence = kp.Confidence,
                    Point = kp.Point
                }).ToArray()
            };
        }

        /// <summary>
        /// Calculates average confidence score across all keypoints
        /// 计算所有关键点的平均置信度分数
        /// </summary>
        /// <returns>
        /// Mean confidence (0-1) or 0 if no keypoints exist
        /// 平均置信度(0-1)，如果没有关键点则返回0
        /// </returns>
        public float GetAverageConfidence()
        {
            if (KeyPoints == null || KeyPoints.Length == 0)
                return 0f;

            return KeyPoints.Average(kp => kp.Confidence);
        }
    }

}
