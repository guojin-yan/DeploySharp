using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    public struct KeyPoint
    {
        public float Confidence;
        public Point Point;
        public override string ToString()
        {
            return $"[Confidence: {Confidence.ToString("0.00")} Point: {Point.ToString()}]";
        }
    }

    public class KeyPointResult : DetResult, IEnumerable<KeyPoint>
    {
        public KeyPoint[] KeyPoints;

        //public KeyPointResult(KeyPoint[] keypoints)
        //{
        //    KeyPoints = keypoints ?? throw new ArgumentNullException(nameof(keypoints));
        //}

        public KeyPoint this[int index] => KeyPoints[index];



        public IEnumerator<KeyPoint> GetEnumerator()
        {
            return ((IEnumerable<KeyPoint>)KeyPoints).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString()
        {
            return base.ToString() + ": " + Bounds.ToString() + $"KeyPoints: {string.Join(", ", KeyPoints.Select(kp => kp.ToString()))}"; ;
        }
    }
}
