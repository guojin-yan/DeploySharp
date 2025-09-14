using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    public struct ModelInferenceTimeRecord
    {
        public double PreprocessTime { get; }
        public double InferenceTime { get; }
        public double PostprocessTime { get; }
        public double TotalTime => PreprocessTime + InferenceTime + PostprocessTime;

        public ModelInferenceTimeRecord(double preprocessTime, double inferenceTime, double postprocessTime)
        {
            PreprocessTime = preprocessTime;
            InferenceTime = inferenceTime;
            PostprocessTime = postprocessTime;
        }

        public override string ToString()
        {
            return $"Input Data Preprocess: {PreprocessTime.ToString("0.00")} ms," +
                   $"\tModel Inference: {InferenceTime.ToString("0.00")} ms," +
                   $"\tResult Postprocess: {PostprocessTime.ToString("0.00")} ms";
        }

    }

    public class ModelInferenceProfiler
    {
        private readonly int _maxRecordCount;
        private readonly Queue<ModelInferenceTimeRecord> _records = new Queue<ModelInferenceTimeRecord>();


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxRecordCount">最大记录数，默认50</param>
        public ModelInferenceProfiler(int maxRecordCount = 50)
        {
            _maxRecordCount = maxRecordCount;
        }

        /// <summary>
        /// 记录一次推理耗时
        /// </summary>
        public void Record(float preprocessTime, float inferenceTime, float postprocessTime)
        {
            var record = new ModelInferenceTimeRecord(preprocessTime, inferenceTime, postprocessTime);

            if (_records.Count >= _maxRecordCount)
            {
                _records.Dequeue();
            }

            _records.Enqueue(record);
        }

        /// <summary>
        /// 记录一次推理耗时
        /// </summary>
        public void Record(ModelInferenceTimeRecord record)
        {
            if (_records.Count >= _maxRecordCount)
            {
                _records.Dequeue();
            }

            _records.Enqueue(record);
        }

        /// <summary>
        /// 打印并返回所有记录的详细时间信息
        /// </summary>
        /// <returns>格式化后的记录字符串</returns>
        public string PrintAllRecords()
        {
            if (_records.Count == 0)
            {
                var message = "No inference records available.";
                Console.WriteLine(message);
                return message;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Inference Time Records:");
            builder.AppendLine("Index\tPreprocess(ms)\tInference(ms)\tPostprocess(ms)\tTotal(ms)");

            int index = 1;
            foreach (var record in _records)
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
        /// 打印并返回统计信息（次数和平均时间）
        /// </summary>
        /// <returns>格式化后的统计信息字符串</returns>
        public string PrintStatistics()
        {
            if (_records.Count == 0)
            {
                var message = "No inference records available for statistics.";
                Console.WriteLine(message);
                return message;
            }

            var avgPreprocess = GetAveragePreprocessTime();
            var avgInference = GetAverageInferenceTime();
            var avgPostprocess = GetAveragePostprocessTime();
            var avgTotal = GetAverageTotalTime();
            var fps = GetAverageFPS();

            var builder = new StringBuilder();
            builder.AppendLine("Inference Statistics:");
            builder.AppendLine($"Record Count: {_records.Count}");
            builder.AppendLine($"Average Preprocess Time: {avgPreprocess:F2} ms");
            builder.AppendLine($"Average Inference Time: {avgInference:F2} ms");
            builder.AppendLine($"Average Postprocess Time: {avgPostprocess:F2} ms");
            builder.AppendLine($"Average Total Time: {avgTotal:F2} ms");
            builder.AppendLine($"Throughput: {fps:F2} FPS");

            var result = builder.ToString();
            Console.WriteLine(result);
            return result;
        }
        // 以下为各个统计指标的获取方法

        /// <summary>
        /// 获取平均预处理时间(ms)，移除第一次记录(如果有多次记录)
        /// </summary>
        public double GetAveragePreprocessTime()
        {
            if (_records.Count <= 1)
                return _records.Count == 1 ? _records.First().PreprocessTime : 0;

            return _records.Skip(1).Average(r => r.PreprocessTime);
        }

        /// <summary>
        /// 获取平均推理时间(ms)，移除第一次记录(如果有多次记录)
        /// </summary>
        public double GetAverageInferenceTime()
        {
            if (_records.Count <= 1)
                return _records.Count == 1 ? _records.First().InferenceTime : 0;

            return _records.Skip(1).Average(r => r.InferenceTime);
        }

        /// <summary>
        /// 获取平均后处理时间(ms)，移除第一次记录(如果有多次记录)
        /// </summary>
        public double GetAveragePostprocessTime()
        {
            if (_records.Count <= 1)
                return _records.Count == 1 ? _records.First().PostprocessTime : 0;

            return _records.Skip(1).Average(r => r.PostprocessTime);
        }

        /// <summary>
        /// 获取平均总时间(ms)，移除第一次记录(如果有多次记录)
        /// </summary>
        public double GetAverageTotalTime()
        {
            if (_records.Count <= 1)
                return _records.Count == 1 ? _records.First().TotalTime : 0;

            return _records.Skip(1).Average(r => r.TotalTime);
        }

        /// <summary>
        /// 获取平均FPS，移除第一次记录(如果有多次记录)
        /// </summary>
        public double GetAverageFPS()
        {
            if (_records.Count <= 1)
                return _records.Count == 1 ? 1000f / _records.First().TotalTime : 0;

            var avgTotalSeconds = _records.Skip(1).Average(r => r.TotalTime) / 1000f;
            return 1f / avgTotalSeconds;
        }

    }

}
