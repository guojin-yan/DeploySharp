using DeploySharp.Data;
using DeploySharp.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Engine
{
    /// <summary>
    /// Defines the core interface for model inference engines
    /// </summary>
    public interface IModelInferEngine
    {
        /// <summary>
        /// Performs model prediction/inference on the input tensor
        /// </summary>
        /// <param name="input">Input data tensor containing model inputs</param>
        /// <returns>Output data tensor containing model predictions</returns>
        public DataTensor Predict(DataTensor inputs);

        /// <summary>
        /// Loads and initializes the model with specified configuration
        /// </summary>
        /// <param name="config">Reference to model configuration object</param>
        /// <remarks>
        /// The configuration object may be modified during loading to reflect actual model parameters
        /// </remarks>
        public void LoadModel(ref IConfig config);

        /// <summary>
        /// Releases all resources used by the inference engine
        /// </summary>
        /// <remarks>
        /// Implementations should ensure proper cleanup of model resources and memory
        /// </remarks>
        public void Dispose();
    }

}
