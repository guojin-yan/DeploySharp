using DeploySharp.Data;
using DeploySharp.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{

    public class PPOcrDetConfig : IImgConfig
    {
        public float det_db_thresh = 0.3f;
        public float det_db_box_thresh = 0.5f;
        public int limit_side_len = 960;
        public string limit_type = "max";
        public string db_score_mode = "slow";
        public float db_unclip_ratio = 2.0f;


        //public override string ToString()
        //{
        //    var sb = new StringBuilder();
        //    AppendIfSet(sb, "Confidence Threshold", ConfidenceThreshold, 0.5f);
        //    AppendIfSet(sb, "NMS Threshold", NmsThreshold, 0.5f);
        //    return base.ToString() + sb.ToString();
        //}
    }
}
