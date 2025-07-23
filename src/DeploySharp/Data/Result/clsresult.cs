using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Data
{
    /// <summary>
    /// Object detection result data.
    /// </summary>
    public class ClsData : IResultData
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
        /// Default constructor.
        /// </summary>
        public ClsData() { }
        /// <summary>
        /// Parameter construction.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="lable">Identification result label.</param>
        /// <param name="score">Identification result score.</param>

        public ClsData(int index, string lable, float score)
        {
            this.index = index;
            this.lable = lable;
            this.score = score;
        }
        /// <summary>
        /// Parameter construction.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="score">Identification result score.</param>
        public ClsData(int index, float score)
        {
            this.index = index;
            this.score = score;
            this.lable = index.ToString();
        }
        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public ClsData UpdateLable(List<string> lables)
        {
            this.lable = lables[this.index];
            return this;
        }
        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public ClsData UpdateLable(string[] lables)
        {
            this.lable = lables[this.index];
            return this;
        }
        /// <summary>
        /// Converts the numeric value of this instance to its equivalent string representation.
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        /// <returns>DetData string.</returns>
        public string ToString(string format = "0.00")
        {
            string msg = "";
            msg += ("index: " + index.ToString() + "\t");
            if (lable != null)
                msg += ("lable: " + lable.ToString() + "\t");
            msg += ("score: " + score.ToString(format) + "\t");
            return msg;
        }
    };
    public class ClsResult : Result<ClsData>
    {
        /// <summary>
        /// Add data.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public override void Add(int index, float score)
        {
            ClsData data = new ClsData(index, score);
            this.Add(data);
        }
        /// <summary>
        /// Add data.
        /// </summary>
        /// <param name="index">Identification result number.</param>
        /// <param name="lable">Identification result label.</param>
        /// <param name="score">Identification result score.</param>
        /// <param name="box">Identification result box.</param>
        public override void Add(int index, string lable, float score)
        {
            ClsData data = new ClsData(index, lable, score);
            this.Add(data);
        }

        /// <summary>
        /// Update lable.
        /// </summary>
        /// <param name="lables">Lable array.</param>
        /// <returns>DetData class.</returns>
        public override void UpdateLable(List<string> lables)
        {
            foreach (ClsData data in this.datas)
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
            foreach (ClsData data in this.datas)
            {
                data.UpdateLable(lables);
            }
        }
        /// <summary>
        /// Print the inference results.
        /// </summary>
        /// <param name="format">A numeric format string.</param>
        public override void Print(string format = "0.00")
        {
            INFO(string.Format("\n Classification Top {0} result : \n", count));
            INFO("classid probability");
            INFO("------- -----------");
            foreach (ClsData data in this.datas)
            {
                INFO(data.ToString(format));
            }
        }
    }
}
