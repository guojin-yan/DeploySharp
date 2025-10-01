using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    /// <summary>
    /// Provides timing measurement functionality for predictor pipeline stages.
    /// 为预测器流水线阶段提供计时测量功能。
    /// </summary>
    /// <remarks>
    /// This class allows precise measurement of preprocessing, inference,
    /// and postprocessing phases using Stopwatch.
    /// 此类允许使用Stopwatch精确测量预处理、推理和后处理阶段。
    /// </remarks>
    public class PredictorTimer
    {
        /// <summary>
        /// Stopwatch instance used for precise timing measurements.
        /// 用于精确计时测量的Stopwatch实例。
        /// </summary>
        private Stopwatch stopwatch = new();

        /// <summary>
        /// Stores accumulated time spent in preprocessing phase.
        /// 存储预处理阶段累积的时间。
        /// </summary>
        private TimeSpan preprocess;

        /// <summary>
        /// Stores accumulated time spent in inference phase.
        /// 存储推理阶段累积的时间。
        /// </summary>
        private TimeSpan inference;

        /// <summary>
        /// Stores accumulated time spent in postprocessing phase.
        /// 存储后处理阶段累积的时间。
        /// </summary>
        private TimeSpan postprocess;

        /// <summary>
        /// Starts timing for preprocessing phase.
        /// 开始预处理阶段的计时。
        /// </summary>
        /// <remarks>
        /// Resets and starts the internal stopwatch.
        /// 重置并启动内部秒表。
        /// </remarks>
        public void StartPreprocess()
        {
            stopwatch.Restart();
        }

        /// <summary>
        /// Starts timing for inference phase and records preprocessing duration.
        /// 开始推理阶段的计时，并记录预处理持续时间。
        /// </summary>
        /// <remarks>
        /// Records elapsed time since last start as preprocessing time,
        /// then resets the stopwatch for inference timing.
        /// 将自上次启动以来的时间记录为预处理时间，然后重置秒表以进行推理计时。
        /// </remarks>
        public void StartInference()
        {
            preprocess = stopwatch.Elapsed;
            stopwatch.Restart();
        }

        /// <summary>
        /// Starts timing for postprocessing phase and records inference duration.
        /// 开始后处理阶段的计时，并记录推理持续时间。
        /// </summary>
        /// <remarks>
        /// Records elapsed time since last start as inference time,
        /// then resets the stopwatch for postprocessing timing.
        /// 将自上次启动以来的时间记录为推理时间，然后重置秒表以进行后处理计时。
        /// </remarks>
        public void StartPostprocess()
        {
            inference = stopwatch.Elapsed;
            stopwatch.Restart();
        }

        /// <summary>
        /// Stops timing and returns accumulated measurements.
        /// 停止计时并返回累积的测量结果。
        /// </summary>
        /// <returns>
        /// A ModelInferenceTimeRecord containing measurements for all phases.
        /// 包含所有阶段测量结果的ModelInferenceTimeRecord。
        /// </returns>
        /// <remarks>
        /// Records elapsed time since last start as postprocessing time,
        /// stops the stopwatch, and returns all collected timing measurements.
        /// 将自上次启动以来的时间记录为后处理时间，停止秒表，并返回所有收集的时间测量结果。
        /// </remarks>
        public ModelInferenceTimeRecord Stop()
        {
            postprocess = stopwatch.Elapsed;
            stopwatch.Stop();

            return new ModelInferenceTimeRecord(
                preprocess.TotalMilliseconds,
                inference.TotalMilliseconds,
                postprocess.TotalMilliseconds);
        }
    }

}
