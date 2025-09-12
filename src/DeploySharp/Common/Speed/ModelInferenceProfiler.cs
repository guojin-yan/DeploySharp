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
        /// 打印所有记录的详细时间信息
        /// </summary>
        public void PrintAllRecords()
        {
            if (_records.Count == 0)
            {
                Console.WriteLine("No inference records available.");
                return;
            }

            Console.WriteLine("Inference Time Records:");
            Console.WriteLine("Index\tPreprocess(ms)\tInference(ms)\tPostprocess(ms)\tTotal(ms)");

            int index = 1;
            foreach (var record in _records)
            {
                Console.WriteLine($"{index}\t{record.PreprocessTime:F2}\t\t{record.InferenceTime:F2}\t\t" +
                                 $"{record.PostprocessTime:F2}\t\t{record.TotalTime:F2}");
                index++;
            }
        }

        /// <summary>
        /// 打印统计信息（次数和平均时间）
        /// </summary>
        public void PrintStatistics()
        {
            if (_records.Count == 0)
            {
                Console.WriteLine("No inference records available for statistics.");
                return;
            }

            var avgPreprocess = GetAveragePreprocessTime();
            var avgInference = GetAverageInferenceTime();
            var avgPostprocess = GetAveragePostprocessTime();
            var avgTotal = GetAverageTotalTime();

            Console.WriteLine("Inference Statistics:");
            Console.WriteLine($"Record Count: {_records.Count}");
            Console.WriteLine($"Average Preprocess Time: {avgPreprocess:F2} ms");
            Console.WriteLine($"Average Inference Time: {avgInference:F2} ms");
            Console.WriteLine($"Average Postprocess Time: {avgPostprocess:F2} ms");
            Console.WriteLine($"Average Total Time: {avgTotal:F2} ms");
        }

        /// <summary>
        /// 打印汇总信息（平均时间和FPS）
        /// </summary>
        public void PrintSummary()
        {
            if (_records.Count == 0)
            {
                Console.WriteLine("No inference records available for summary.");
                return;
            }

            var avgTotal = GetAverageTotalTime();
            var fps = GetAverageFPS();

            Console.WriteLine("Inference Summary:");
            Console.WriteLine($"Average Total Time: {avgTotal:F2} ms");
            Console.WriteLine($"Throughput: {fps:F2} FPS");
        }

        // 以下为各个统计指标的获取方法

        /// <summary>
        /// 获取平均预处理时间(ms)
        /// </summary>
        public double GetAveragePreprocessTime()
        {
            return _records.Average(r => r.PreprocessTime);
        }

        /// <summary>
        /// 获取平均推理时间(ms)
        /// </summary>
        public double GetAverageInferenceTime()
        {
            return _records.Average(r => r.InferenceTime);
        }

        /// <summary>
        /// 获取平均后处理时间(ms)
        /// </summary>
        public double GetAveragePostprocessTime()
        {
            return _records.Average(r => r.PostprocessTime);
        }

        /// <summary>
        /// 获取平均总时间(ms)
        /// </summary>
        public double GetAverageTotalTime()
        {
            return _records.Average(r => r.TotalTime);
        }

        /// <summary>
        /// 获取平均FPS
        /// </summary>
        public double GetAverageFPS()
        {
            var avgTotalSeconds = GetAverageTotalTime() / 1000f; // 转换为秒
            return 1f / avgTotalSeconds; // FPS = 1 / 每帧耗时(秒)
        }
    }

}
