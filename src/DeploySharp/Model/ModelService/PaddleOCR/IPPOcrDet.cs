using DeploySharp.Data;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace DeploySharp.Model
{
    public abstract class IPPOcrDet : IModel
    {
        public IPPOcrDet(IConfig config) : base(config)
        {
            MyLogger.Log.Info($"初始化 {this.GetType().Name}, \n {config.ToString()}");
        }
        public ObbResult[] Predict(object img)
        {
            return base.Predict(img) as ObbResult[];
        }




        //protected override List<Result[]> PostprocessBatch(DataTensor dataTensor, ImageAdjustmentParam[] imageAdjustmentParams)
        //{

        //}
    }
}
