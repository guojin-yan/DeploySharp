using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    /// <summary>
    /// Represents timing measurements for different phases of model inference.
    /// 表示模型推理不同阶段的时间测量结果。
    /// </summary>
    /// <remarks>
    /// This structure records the time taken for preprocessing input data,
    /// the actual model inference, and postprocessing the results.
    /// 该结构体记录了输入数据预处理、模型推理和结果后处理所花费的时间。
    /// </remarks>
    public struct ModelInferenceTimeRecord
    {
        /// <summary>
        /// Gets the time taken for input data preprocessing in milliseconds.
        /// 获取输入数据预处理花费的时间(毫秒)。
        /// </summary>
        public double PreprocessTime { get; }

        /// <summary>
        /// Gets the time taken for model inference execution in milliseconds.
        /// 获取模型推理执行花费的时间(毫秒)。
        /// </summary>
        public double InferenceTime { get; }

        /// <summary>
        /// Gets the time taken for result postprocessing in milliseconds.
        /// 获取结果后处理花费的时间(毫秒)。
        /// </summary>
        public double PostprocessTime { get; }

        /// <summary>
        /// Gets the total time taken for the complete inference pipeline in milliseconds.
        /// 获取完整推理流程的总时间(毫秒)。
        /// </summary>
        /// <value>
        /// The sum of PreprocessTime, InferenceTime and PostprocessTime.
        /// 预处理时间、推理时间和后处理时间的总和。
        /// </value>
        public double TotalTime => PreprocessTime + InferenceTime + PostprocessTime;

        /// <summary>
        /// Initializes a new instance of ModelInferenceTimeRecord.
        /// 初始化ModelInferenceTimeRecord结构的新实例。
        /// </summary>
        /// <param name="preprocessTime">Preprocessing time in milliseconds.预处理时间(毫秒)</param>
        /// <param name="inferenceTime">Inference time in milliseconds.推理时间(毫秒)</param>
        /// <param name="postprocessTime">Postprocessing time in milliseconds.后处理时间(毫秒)</param>
        public ModelInferenceTimeRecord(double preprocessTime, double inferenceTime, double postprocessTime)
        {
            PreprocessTime = preprocessTime;
            InferenceTime = inferenceTime;
            PostprocessTime = postprocessTime;
        }

        /// <summary>
        /// Returns a formatted string representation of the timing measurements.
        /// 返回时间测量结果的格式化字符串表示。
        /// </summary>
        /// <returns>A formatted string showing all time components.</returns>
        public override string ToString()
        {
            return $"Input Data Preprocess: {PreprocessTime.ToString("0.00")} ms," +
                   $"\tModel Inference: {InferenceTime.ToString("0.00")} ms," +
                   $"\tResult Postprocess: {PostprocessTime.ToString("0.00")} ms";
        }
    }

    /// <summary>
    /// Provides performance profiling capabilities for model inference tasks.
    /// 为模型推理任务提供性能分析功能。
    /// </summary>
    /// <remarks>
    /// This class maintains a rolling window of inference time records and provides
    /// methods to analyze the performance characteristics over time.
    /// 此类维护一个滚动窗口的推理时间记录，并提供分析随时间变化的性能特征的方法。
    /// </remarks>
    public class ModelInferenceProfiler
    {
        private readonly int maxRecordCount;
        private readonly Queue<ModelInferenceTimeRecord> records = new Queue<ModelInferenceTimeRecord>();

        /// <summary>
        /// Initializes a new instance of ModelInferenceProfiler.
        /// 初始化ModelInferenceProfiler类的新实例。
        /// </summary>
        /// <param name="maxRecordCount">
        /// Maximum number of records to maintain (default: 50).
        /// 要维护的最大记录数(默认50)
        /// </param>
        /// <remarks>
        /// When the record count exceeds maxRecordCount, oldest records are removed.
        /// 当记录数超过maxRecordCount时，最早的记录将被移除。
        /// </remarks>
        public ModelInferenceProfiler(int maxRecordCount = 50)
        {
            // Validate and set the maximum record count
            // 验证并设置最大记录数
            this.maxRecordCount = maxRecordCount;
        }

        /// <summary>
        /// Records timing information for an inference operation.
        /// 记录一次推理操作的时间信息。
        /// </summary>
        /// <param name="preprocessTime">Preprocessing time in milliseconds.预处理时间(毫秒)</param>
        /// <param name="inferenceTime">Inference time in milliseconds.推理时间(毫秒)</param>
        /// <param name="postprocessTime">Postprocessing time in milliseconds.后处理时间(毫秒)</param>
        public void Record(float preprocessTime, float inferenceTime, float postprocessTime)
        {
            var record = new ModelInferenceTimeRecord(preprocessTime, inferenceTime, postprocessTime);

            // Maintain queue size by removing oldest record if necessary
            // 如果需要，通过移除最老的记录来维护队列大小
            if (records.Count >= maxRecordCount)
            {
                records.Dequeue();
            }

            records.Enqueue(record);
        }

        /// <summary>
        /// Records timing information using an existing ModelInferenceTimeRecord.
        /// 使用现有的ModelInferenceTimeRecord记录时间信息。
        /// </summary>
        /// <param name="record">The timing record to add.要添加的时间记录</param>
        public void Record(ModelInferenceTimeRecord record)
        {
            // Same queue size maintenance as above
            // 与上面相同的队列大小维护
            if (records.Count >= maxRecordCount)
            {
                records.Dequeue();
            }

            records.Enqueue(record);
        }

        /// <summary>
        /// Prints and returns detailed timing information for all records.
        /// 打印并返回所有记录的详细时间信息。
        /// </summary>
        /// <returns>Formatted string containing all records.包含所有记录的格式化字符串</returns>
        public string PrintAllRecords()
        {
            if (records.Count == 0)
            {
                var message = "No inference records available.";
                Console.WriteLine(message);
                return message;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Inference Time Records:");
            builder.AppendLine("Index\tPreprocess(ms)\tInference(ms)\tPostprocess(ms)\tTotal(ms)");

            int index = 1;
            foreach (var record in records)
            {
                builder.AppendLine($"{index}\t{record.PreprocessTime:F2}\t\t{record.InferenceTime:F2}\t\t" +
                                 $"{record.PostprocessTime:F2}\t\t{record.TotalTime:F2}");
                index++;
            }

            var result = builder.ToString();
            Console.WriteLine(result);
            return result;
        }

        /// <summary>
        /// Prints and returns statistical summary of recorded timings.
        /// 打印并返回记录时间的统计摘要。
        /// </summary>
        /// <returns>Formatted statistical information string.格式化的统计信息字符串</returns>
        public string PrintStatistics()
        {
            if (records.Count == 0)
            {
                var message = "No inference records available for statistics.";
                Console.WriteLine(message);
                return message;
            }

            // Calculate all statistics
            // 计算所有统计数据
            var avgPreprocess = GetAveragePreprocessTime();
            var avgInference = GetAverageInferenceTime();
            var avgPostprocess = GetAveragePostprocessTime();
            var avgTotal = GetAverageTotalTime();
            var fps = GetAverageFPS();

            var builder = new StringBuilder();
            builder.AppendLine("Inference Statistics:");
            builder.AppendLine($"Record Count: {records.Count}");
            builder.AppendLine($"Average Preprocess Time: {avgPreprocess:F2} ms");
            builder.AppendLine($"Average Inference Time: {avgInference:F2} ms");
            builder.AppendLine($"Average Postprocess Time: {avgPostprocess:F2} ms");
            builder.AppendLine($"Average Total Time: {avgTotal:F2} ms");
            builder.AppendLine($"Throughput: {fps:F2} FPS");

            var result = builder.ToString();
            Console.WriteLine(result);
            return result;
        }

        #region Statistics Calculation Methods

        /// <summary>
        /// Gets average preprocessing time, skipping the first record if multiple exist.
        /// 获取平均预处理时间，如果有多次记录则跳过第一次。
        /// </summary>
        /// <returns>
        /// Average preprocessing time in milliseconds.平均预处理时间(毫秒)
        /// </returns>
        /// <remarks>
        /// Skips first record to avoid counting cold start effects.
        /// 跳过第一次记录以避免计算冷启动影响。
        /// </remarks>
        public double GetAveragePreprocessTime()
        {
            if (records.Count <= 1)
                return records.Count == 1 ? records.First().PreprocessTime : 0;

            return records.Skip(1).Average(r => r.PreprocessTime);
        }

        /// <summary>
        /// Gets average inference time, skipping the first record if multiple exist.
        /// 获取平均推理时间，如果有多次记录则跳过第一次。
        /// </summary>
        /// <returns>Average inference time in milliseconds.平均推理时间(毫秒)</returns>
        public double GetAverageInferenceTime()
        {
            if (records.Count <= 1)
                return records.Count == 1 ? records.First().InferenceTime : 0;

            return records.Skip(1).Average(r => r.InferenceTime);
        }

        /// <summary>
        /// Gets average postprocessing time, skipping the first record if multiple exist.
        /// 获取平均后处理时间，如果有多次记录则跳过第一次。
        /// </summary>
        /// <returns>Average postprocessing time in milliseconds.平均后处理时间(毫秒)</returns>
        public double GetAveragePostprocessTime()
        {
            if (records.Count <= 1)
                return records.Count == 1 ? records.First().PostprocessTime : 0;

            return records.Skip(1).Average(r => r.PostprocessTime);
        }

        /// <summary>
        /// Gets average total processing time, skipping the first record if multiple exist.
        /// 获取平均总处理时间，如果有多次记录则跳过第一次。
        /// </summary>
        /// <returns>Average total time in milliseconds.平均总时间(毫秒)</returns>
        public double GetAverageTotalTime()
        {
            if (records.Count <= 1)
                return records.Count == 1 ? records.First().TotalTime : 0;

            return records.Skip(1).Average(r => r.TotalTime);
        }

        /// <summary>
        /// Gets average frames per second (FPS), skipping the first record if multiple exist.
        /// 获取平均每秒帧数(FPS)，如果有多次记录则跳过第一次。
        /// </summary>
        /// <returns>Calculated frames per second.计算出的每秒帧数</returns>
        public double GetAverageFPS()
        {
            if (records.Count <= 1)
                return records.Count == 1 ? 1000f / records.First().TotalTime : 0;

            var avgTotalSeconds = records.Skip(1).Average(r => r.TotalTime) / 1000f;
            return 1f / avgTotalSeconds;
        }

        #endregion
    }

}
