using DeploySharp.Data;
using DeploySharp.Engine;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Model
{
    public abstract class IModel
    {
        protected ModelConfig config;
        protected IModelInferEngine engine;
        protected Point2f scales = new Point2f();

        protected abstract DataTensor Preprocess(Mat img);
        protected abstract BaseResult Postprocess(DataTensor result);

        protected IModel(ModelConfig config) 
        {
            this.config = config;
            engine = InferEngineFactory.Create(this.config.TargetInferenceBackend);
            engine.LoadModel(ref this.config);
        }
        public BaseResult Predict(Mat img) 
        {
            return Postprocess(engine.Predict(Preprocess(img)));
        }

        public List<BaseResult> Predict(List<Mat> imgs)
        {
            List<BaseResult> results = new List<BaseResult>();
            foreach (Mat img in imgs) 
            {
                results.Add(Postprocess(engine.Predict(Preprocess(img))));
            }
            return results;
        }
    }
}
