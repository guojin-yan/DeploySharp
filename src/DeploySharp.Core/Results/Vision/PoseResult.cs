using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Results.Vision
{
    /// <summary>
    /// Represents one scored keypoint. / 表示一个带分数的关键点。
    /// </summary>
    public sealed class Keypoint
    {
        /// <summary>Initializes a keypoint. / 初始化关键点。</summary>
        public Keypoint(int index, PointF point, float score)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (float.IsNaN(score) || float.IsInfinity(score)) throw new ArgumentOutOfRangeException(nameof(score));
            Index = index;
            Point = point;
            Score = score;
        }

        /// <summary>Gets the model-defined keypoint index. / 获取模型定义的关键点索引。</summary>
        public int Index { get; }

        /// <summary>Gets the source-image coordinate. / 获取源图像坐标。</summary>
        public PointF Point { get; }

        /// <summary>Gets the keypoint score. / 获取关键点分数。</summary>
        public float Score { get; }
    }

    /// <summary>
    /// Represents one detected pose and its keypoints. / 表示一个检测到的姿态及其关键点。
    /// </summary>
    public sealed class Pose
    {
        private readonly IReadOnlyList<Keypoint> _keypoints;

        /// <summary>Initializes a pose. / 初始化姿态。</summary>
        public Pose(Detection detection, IEnumerable<Keypoint> keypoints)
        {
            Detection = detection ?? throw new ArgumentNullException(nameof(detection));
            if (keypoints == null) throw new ArgumentNullException(nameof(keypoints));
            var values = new List<Keypoint>();
            foreach (Keypoint keypoint in keypoints)
            {
                if (keypoint == null) throw new ArgumentException("Keypoints cannot contain null values.", nameof(keypoints));
                values.Add(keypoint);
            }

            _keypoints = values.AsReadOnly();
        }

        /// <summary>Gets the person or object detection associated with the pose. / 获取与姿态关联的人体或对象检测结果。</summary>
        public Detection Detection { get; }

        /// <summary>Gets ordered keypoints. / 获取有序关键点。</summary>
        public IReadOnlyList<Keypoint> Keypoints => _keypoints;
    }

    /// <summary>
    /// Contains detected poses for one input. / 包含一个输入的检测姿态。
    /// </summary>
    public sealed class PoseResult
    {
        private readonly IReadOnlyList<Pose> _poses;

        /// <summary>Initializes a pose result. / 初始化姿态结果。</summary>
        public PoseResult(IEnumerable<Pose> poses)
        {
            if (poses == null) throw new ArgumentNullException(nameof(poses));
            var values = new List<Pose>();
            foreach (Pose pose in poses)
            {
                if (pose == null) throw new ArgumentException("Poses cannot contain null values.", nameof(poses));
                values.Add(pose);
            }

            _poses = values.AsReadOnly();
        }

        /// <summary>Gets detected poses. / 获取检测到的姿态。</summary>
        public IReadOnlyList<Pose> Poses => _poses;
    }
}
