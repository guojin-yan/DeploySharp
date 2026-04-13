using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeploySharp.Data.ResultData
{
    public class ClsResult : Result
    {
        public ClsResult() : base()
        {
            Type = ResultType.Classification;
            SuspectedResults = new List<Result>();
        }

        public List<Result> SuspectedResults { get; set; }
        public override string ToString()
        {
            string baseInfo = base.ToString();

            if (SuspectedResults == null || SuspectedResults.Count == 0)
            {
                return $"{baseInfo}, SuspectedResults: []";
            }
            // 取前3个打印，防止日志过长
            var details = SuspectedResults.Take(3).Select(r => $"{r.Category}({r.Confidence:P0})");
            string detailStr = string.Join(", ", details);

            if (SuspectedResults.Count > 3)
            {
                detailStr += "...";
            }
            return $"{baseInfo}, SuspectedResults({SuspectedResults.Count}): [{detailStr}]";
        }
    }
}
