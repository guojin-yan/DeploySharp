using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DeploySharp.Data
{ 
    public struct PosePoint
    {
        /// <summary>
        /// Key point prediction score
        /// </summary>
        public float[] score;
        /// <summary>
        /// Key point prediction results.
        /// </summary>
        public List<Point> point;
        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="data">Key point prediction results.</param>
        /// <param name="scales">Image scaling ratio.</param>
        public PosePoint(float[] data, float scales)
        {
            score = new float[data.Length];
            point = new List<Point>();
            for (int i = 0; i < 17; i++)
            {
                Point p = new Point((int)(data[3 * i] * scales), (int)(data[3 * i + 1] * scales));
                this.point.Add(p);
                this.score[i] = data[3 * i + 2];
            }
        }
        /// <summary>
        /// Convert PoseData to string.
        /// </summary>
        /// <returns>PoseData string.</returns>
        public string ToString(string format = "0.00")
        {
            string[] point_str = new string[] { "Nose", "Left Eye", "Right Eye", "Left Ear", "Right Ear",
                "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow", "Left Wrist", "Right Wrist",
                "Left Hip", "Right Hip", "Left Knee", "Right Knee", "Left Ankle", "Right Ankle" };
            string ss = "";
            for (int i = 0; i < point.Count; i++)
            {
                ss += point_str[i] + ": (" + point[i].X.ToString(format) + " ," + point[i].Y.ToString(format)
                    + " ," + score[i].ToString(format) + ") ";
            }
            return ss;
        }
    }

    /// <summary>
    /// Represents pose detection data including class index, label, confidence score, bounding box and pose points
    /// </summary>
    public class PoseData : IResultData
    {
        /// <summary>
        /// Class index of the detection result (default: 1)
        /// </summary>
        public int index = 1;

        /// <summary>
        /// Class label of the detection result (default: "human")
        /// </summary>
        public string lable = "human";

        /// <summary>
        /// Confidence score of the detection (0-1)
        /// </summary>
        public float score;

        /// <summary>
        /// Bounding box coordinates of the detection
        /// </summary>
        public Rect box;

        /// <summary>
        /// Detected pose keypoints
        /// </summary>
        public PosePoint pose_point;

        /// <summary>
        /// Default constructor
        /// </summary>
        public PoseData() { }

        /// <summary>
        /// Initializes a new instance with specified parameters
        /// </summary>
        /// <param name="index">Class index</param>
        /// <param name="lable">Class label</param>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose">Pose keypoints</param>
        public PoseData(int index, string lable, float score, Rect box, PosePoint pose)
        {
            this.index = index;
            this.lable = lable;
            this.score = score;
            this.box = box;
            this.pose_point = pose;
        }

        /// <summary>
        /// Initializes a new instance with pose data array
        /// </summary>
        /// <param name="index">Class index</param>
        /// <param name="lable">Class label</param>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose_data">Pose data array</param>
        /// <param name="scales">Scale factor for pose data</param>
        public PoseData(int index, string lable, float score, Rect box, float[] pose_data, float scales)
        {
            this.index = index;
            this.lable = lable;
            this.score = score;
            this.box = box;
            PosePoint pose = new PosePoint(pose_data, scales);
            this.pose_point = pose;
        }

        /// <summary>
        /// Initializes a new instance with default class index and label
        /// </summary>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose">Pose keypoints</param>
        public PoseData(float score, Rect box, PosePoint pose)
        {
            this.index = 1;
            this.lable = "human";
            this.score = score;
            this.box = box;
            this.pose_point = pose;
        }

        /// <summary>
        /// Initializes a new instance with pose data array and default class index/label
        /// </summary>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose_data">Pose data array</param>
        /// <param name="scales">Scale factor for pose data</param>
        public PoseData(float score, Rect box, float[] pose_data, float scales)
        {
            this.index = 1;
            this.lable = "human";
            this.score = score;
            this.box = box;
            PosePoint pose = new PosePoint(pose_data, scales);
            this.pose_point = pose;
        }

        /// <summary>
        /// Converts the detection data to formatted string representation
        /// </summary>
        /// <param name="format">Numeric format string (default: "0.00")</param>
        /// <returns>Formatted string containing all detection data</returns>
        public string ToString(string format = "0.00")
        {
            string msg = "";
            msg += ("index: " + index.ToString() + "\t");
            if (lable != null)
                msg += ("lable: " + lable.ToString() + "\t");
            msg += ("score: " + score.ToString(format) + "\t");
            msg += ("box: " + box.ToString() + "\t");
            msg += ("pose: " + pose_point.ToString(format));
            return msg;
        }
    }

     /// <summary>
    /// Represents a collection of pose detection results with sorting and output capabilities
    /// </summary>
    public class PoseResult : Result<PoseData>
    {
        /// <summary>
        /// Adds a new pose detection result with specified parameters
        /// </summary>
        /// <param name="index">Class index</param>
        /// <param name="lable">Class label</param>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="point">Pose keypoints</param>
        public void Add(int index, string lable, float score, Rect box, PosePoint point)
        {
            PoseData data = new PoseData(index, lable, score, box, point);
            this.Add(data);
        }

        /// <summary>
        /// Adds a new pose detection result with pose data array
        /// </summary>
        /// <param name="index">Class index</param>
        /// <param name="lable">Class label</param>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose_data">Pose data array</param>
        /// <param name="scales">Scale factor for pose data</param>
        public  void Add(int index, string lable, float score, Rect box, float[] pose_data, float scales)
        {
            PoseData data = new PoseData(index, lable, score, box, pose_data, scales);
            this.Add(data);
        }

        /// <summary>
        /// Adds a new pose detection result with default class index/label
        /// </summary>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="point">Pose keypoints</param>
        public void Add(float score, Rect box, PosePoint point)
        {
            PoseData data = new PoseData(score, box, point);
            this.Add(data);
        }

        /// <summary>
        /// Adds a new pose detection result with pose data array and default class index/label
        /// </summary>
        /// <param name="score">Confidence score</param>
        /// <param name="box">Bounding box</param>
        /// <param name="pose_data">Pose data array</param>
        /// <param name="scales">Scale factor for pose data</param>
        public void Add(float score, Rect box, float[] pose_data, float scales)
        {
            PoseData data = new PoseData(score, box, pose_data, scales);
            this.Add(data);
        }

        /// <summary>
        /// Sorts the detection results by confidence score
        /// </summary>
        /// <param name="flag">True for ascending order, false for descending (default: true)</param>
        public override void SortByScore(bool flag = true)
        {
            if (flag)
                this.Sort((x, y) => x.score.CompareTo(y.score));
            else
                this.Sort((x, y) => y.score.CompareTo(x.score));
        }

        /// <summary>
        /// Sorts the detection results by bounding box position
        /// </summary>
        /// <param name="flag">Sorting criteria flag</param>
        public override void SortByBbox(bool flag)
        {
            datas.OrderBy(t => t.box.Location.X).ThenBy(t => t.box.Location.Y).ToList();
        }

        /// <summary>
        /// Prints all detection results to console
        /// </summary>
        /// <param name="format">Numeric format string (default: "0.00")</param>
        public override void Print(string format = "0.00")
        {
            INFO("Detection results:");
            foreach (PoseData data in this.datas)
            {
                INFO(data.ToString(format));
            }
        }
    }

}
