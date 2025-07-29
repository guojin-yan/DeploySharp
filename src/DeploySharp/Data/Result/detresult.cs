using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Object detection result data.
    /// </summary>
    public class DetData : IResultData
    {
        /// <summary>
        /// Identification result class index.
        /// </summary>
        public int index;
        /// <summary>
        /// Identification result class lable.
        /// </summary>
        public string lable;
        /// <summary>
        /// Confidence value.
        /// </summary>
        public float score;
        /// <summary>
        /// Prediction box.
        /// </summary>
        public Rect box;
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DetData() { }
        /// <summary>
        /// Parameter construction.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="lable">Identification result label.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public DetData(int index, string lable, float score, Rect box) 
        { 
            this.index = index;
            this.lable = lable;
            this.score = score;
            this.box = box;
        }
        /// <summary>
        /// Parameter construction.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public DetData(int index, float score, Rect box) 
            :this(index, index.ToString(), score, box) 
        { }
        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public DetData UpdateLable(List<string> lables) 
        {
            this.lable = lables[this.index];
            return this;
        }
        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public DetData UpdateLable(string[] lables)
        {
            this.lable = lables[this.index];
            return this;
        }
        /// <summary>
        /// Converts the numeric value of this instance to its equivalent string representation.
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <returns>DetData string.</returns>
        public string ToString(string format = "0.00") {
            string msg = "";
            msg += ("index: " + index.ToString() + "\t");
            if(lable!=null)
                msg += ("lable: " + lable.ToString() + "\t");
            msg += ("score: " + score.ToString(format) + "\t");
            msg += ("box: " + box.ToString() + "\t");
            return msg;
        }
    };
    /// <summary>
    /// Object detection result class.
    /// </summary>
    public class DetResult : Result<DetData>
    {
        /// <summary>
        /// Add data.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public void Add(int index, float score, Rect box)
        {
            DetData data = new DetData(index, score, box);
            this.Add(data);
        }
        /// <summary>
        /// Add data.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="lable">Identification result label.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public void Add(int index, string lable, float score, Rect box)
        {
            DetData data = new DetData(index, lable, score, box);
            this.Add(data);
        }

        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public override void UpdateLable(List<string> lables)
        {
            foreach (DetData data in this.datas)
            {
                data.UpdateLable(lables);
            }
        }
        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public override void UpdateLable(string[] lables)
        {
            foreach (DetData data in this.datas)
            {
                data.UpdateLable(lables);
            }
        }
        /// <summary>
        /// Sorts the index elements in the entire inference results using the default comparer.
        /// </summary>
        /// <param name="flag"></param>
        public override void SortByIndex(bool flag = true)
        {
            if (flag)
                this.Sort((x, y) => x.index.CompareTo(y.index));
            else
                this.Sort((x, y) => y.index.CompareTo(x.index));
        }
        /// <summary>
        /// Sorts the score elements in the entire inference results using the default comparer.
        /// </summary>
        /// <param name="flag"></param>
        public override void SortByScore(bool flag = true)
        {
            if (flag)
                this.Sort((x, y) => x.score.CompareTo(y.score));
            else
                this.Sort((x, y) => y.score.CompareTo(x.score));
        }
        /// <summary>
        /// Sorts the box elements in the entire inference results using the default comparer.
        /// </summary>
        /// <param name="flag"></param>
        public override void SortByBbox(bool flag)
        {
            datas.OrderBy(t => t.box.Location.X).ThenBy(t => t.box.Location.Y).ToList();

        }

        /// <summary>
        /// Print the inference results.
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        public override void Print(string format = "0.00")
        {
            INFO("Detection results:");
            foreach (DetData data in this.datas)
            {
                INFO(data.ToString(format));
            }
        }
    }
}
