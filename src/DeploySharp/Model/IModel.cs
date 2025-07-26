using DeploySharp.Data;
using DeploySharp.Engine;
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
        protected Pair<float, float> scales = new Pair<float, float>();
         
        protected abstract DataTensor Preprocess(object img);
        protected abstract BaseResult Postprocess(DataTensor result);

        protected IModel(ModelConfig config) 
        {
            this.config = config;
            engine = InferEngineFactory.Create(this.config.TargetInferenceBackend);
            engine.LoadModel(ref this.config);
        }
        public BaseResult Predict(object img) 
        {
            return Postprocess(engine.Predict(Preprocess(img)));
        }

        public List<BaseResult> Predict(List<object> imgs)
        {
            List<BaseResult> results = new List<BaseResult>();
            foreach (object img in imgs) 
            {
                results.Add(Postprocess(engine.Predict(Preprocess(img))));
            }
            return results;
        }
    }
}
