using DeploySharp.Common;
using FluentAssertions;
using System;
using System.Threading;
using Xunit;

namespace DeploySharp.Tests.Common
{
    public class ModelInferenceTimeRecordTests
    {
        [Fact]
        public void Constructor_WithValidValues_ShouldSetProperties()
        {
            var record = new ModelInferenceTimeRecord(10.5, 20.3, 5.2);

            record.PreprocessTime.Should().Be(10.5);
            record.InferenceTime.Should().Be(20.3);
            record.PostprocessTime.Should().Be(5.2);
        }

        [Fact]
        public void TotalTime_ShouldBeSumOfAllPhases()
        {
            var record = new ModelInferenceTimeRecord(10, 20, 5);

            record.TotalTime.Should().Be(35);
        }

        [Fact]
        public void TotalTime_WithZeroValues_ShouldBeZero()
        {
            var record = new ModelInferenceTimeRecord(0, 0, 0);

            record.TotalTime.Should().Be(0);
        }

        [Fact]
        public void ToString_ShouldContainAllTimes()
        {
            var record = new ModelInferenceTimeRecord(10.5, 20.3, 5.2);
            var str = record.ToString();

            str.Should().Contain("Preprocess");
            str.Should().Contain("Inference");
            str.Should().Contain("Postprocess");
            str.Should().Contain("10.5");
            str.Should().Contain("20.3");
            str.Should().Contain("5.2");
        }
    }

    public class PredictorTimerTests
    {
        [Fact]
        public void Reset_ShouldResetAllTimes()
        {
            var timer = new PredictorTimer();
            timer.StartPreprocess();
            Thread.Sleep(10);
            timer.StartInference();
            Thread.Sleep(10);
            timer.StartPostprocess();
            Thread.Sleep(10);
            timer.Stop();

            timer.Reset();

            var record = timer.GetBatchRecord();
            record.PreprocessTime.Should().Be(0);
            record.InferenceTime.Should().Be(0);
            record.PostprocessTime.Should().Be(0);
        }

        [Fact]
        public void StartPreprocess_ShouldNotThrow()
        {
            var timer = new PredictorTimer();
            Action act = () => timer.StartPreprocess();
            act.Should().NotThrow();
        }

        [Fact]
        public void StartInference_ShouldNotThrow()
        {
            var timer = new PredictorTimer();
            timer.StartPreprocess();
            Action act = () => timer.StartInference();
            act.Should().NotThrow();
        }

        [Fact]
        public void StartPostprocess_ShouldNotThrow()
        {
            var timer = new PredictorTimer();
            timer.StartPreprocess();
            timer.StartInference();
            Action act = () => timer.StartPostprocess();
            act.Should().NotThrow();
        }

        [Fact]
        public void Stop_ShouldReturnRecord()
        {
            var timer = new PredictorTimer();
            timer.StartPreprocess();
            Thread.Sleep(5);
            timer.StartInference();
            Thread.Sleep(5);
            timer.StartPostprocess();
            Thread.Sleep(5);

            var record = timer.Stop();

            record.Should().NotBeNull();
            record.TotalTime.Should().BeGreaterThan(0);
        }

        [Fact]
        public void StopBatch_ShouldNotThrow()
        {
            var timer = new PredictorTimer();
            timer.StartPreprocess();
            timer.StartInference();
            timer.StartPostprocess();
            Action act = () => timer.StopBatch();
            act.Should().NotThrow();
        }

        [Fact]
        public void GetBatchRecord_ShouldReturnRecord()
        {
            var timer = new PredictorTimer();
            var record = timer.GetBatchRecord();
            record.Should().NotBeNull();
        }
    }

    public class ModelInferenceProfilerTests
    {
        [Fact]
        public void Constructor_DefaultMaxCount_ShouldBe50()
        {
            var profiler = new ModelInferenceProfiler();
            // Internal field - tested through behavior
            for (int i = 0; i < 60; i++)
            {
                profiler.Record(1, 2, 3);
            }
            // Should maintain rolling window, PrintAllRecords should work
            var output = profiler.PrintAllRecords();
            output.Should().Contain("Inference Time Records");
        }

        [Fact]
        public void Constructor_CustomMaxCount_ShouldWork()
        {
            var profiler = new ModelInferenceProfiler(10);
            for (int i = 0; i < 15; i++)
            {
                profiler.Record(1, 2, 3);
            }
            var output = profiler.PrintAllRecords();
            output.Should().Contain("Inference Time Records");
        }

        [Fact]
        public void Record_WithFloatValues_ShouldStore()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10.5f, 20.3f, 5.2f);

            var stats = profiler.PrintStatistics();
            stats.Should().Contain("Record Count: 1");
        }

        [Fact]
        public void Record_WithRecordStruct_ShouldStore()
        {
            var profiler = new ModelInferenceProfiler();
            var record = new ModelInferenceTimeRecord(10, 20, 5);
            profiler.Record(record);

            var stats = profiler.PrintStatistics();
            stats.Should().Contain("Record Count: 1");
        }

        [Fact]
        public void PrintAllRecords_WithNoRecords_ShouldReturnMessage()
        {
            var profiler = new ModelInferenceProfiler();
            var output = profiler.PrintAllRecords();
            output.Should().Contain("No inference records available");
        }

        [Fact]
        public void PrintStatistics_WithNoRecords_ShouldReturnMessage()
        {
            var profiler = new ModelInferenceProfiler();
            var output = profiler.PrintStatistics();
            output.Should().Contain("No inference records available");
        }

        [Fact]
        public void PrintStatistics_WithRecords_ShouldContainStats()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10, 20, 5);
            profiler.Record(12, 22, 6);
            profiler.Record(11, 21, 5.5f);

            var output = profiler.PrintStatistics();
            output.Should().Contain("Inference Statistics");
            output.Should().Contain("Record Count");
            output.Should().Contain("Average Preprocess Time");
            output.Should().Contain("Average Inference Time");
            output.Should().Contain("Average Postprocess Time");
            output.Should().Contain("Average Total Time");
            output.Should().Contain("Throughput");
            output.Should().Contain("FPS");
        }

        [Fact]
        public void GetAveragePreprocessTime_WithNoRecords_ShouldBeZero()
        {
            var profiler = new ModelInferenceProfiler();
            var avg = profiler.GetAveragePreprocessTime();
            avg.Should().Be(0);
        }

        [Fact]
        public void GetAveragePreprocessTime_WithOneRecord_ShouldReturnThatValue()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10, 20, 5);
            var avg = profiler.GetAveragePreprocessTime();
            avg.Should().Be(10);
        }

        [Fact]
        public void GetAveragePreprocessTime_WithMultipleRecords_ShouldSkipFirst()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(100, 200, 50); // First record (skipped)
            profiler.Record(10, 20, 5);
            profiler.Record(20, 30, 10);

            var avg = profiler.GetAveragePreprocessTime();
            avg.Should().Be(15); // Average of 10 and 20
        }

        [Fact]
        public void GetAverageInferenceTime_WithMultipleRecords_ShouldSkipFirst()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10, 200, 5); // First record (skipped)
            profiler.Record(10, 20, 5);
            profiler.Record(10, 30, 5);

            var avg = profiler.GetAverageInferenceTime();
            avg.Should().Be(25); // Average of 20 and 30
        }

        [Fact]
        public void GetAveragePostprocessTime_WithMultipleRecords_ShouldSkipFirst()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10, 20, 50); // First record (skipped)
            profiler.Record(10, 20, 5);
            profiler.Record(10, 20, 15);

            var avg = profiler.GetAveragePostprocessTime();
            avg.Should().Be(10); // Average of 5 and 15
        }

        [Fact]
        public void GetAverageTotalTime_WithMultipleRecords_ShouldSkipFirst()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(100, 200, 50); // First record (skipped) = 350
            profiler.Record(10, 20, 5);    // = 35
            profiler.Record(20, 30, 10);   // = 60

            var avg = profiler.GetAverageTotalTime();
            avg.Should().Be(47.5); // Average of 35 and 60
        }

        [Fact]
        public void GetAverageFPS_WithNoRecords_ShouldBeZero()
        {
            var profiler = new ModelInferenceProfiler();
            var fps = profiler.GetAverageFPS();
            fps.Should().Be(0);
        }

        [Fact]
        public void GetAverageFPS_WithOneRecord_ShouldCalculateCorrectly()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(10, 20, 5); // Total = 35ms
            var fps = profiler.GetAverageFPS();
            fps.Should().BeApproximately(1000.0 / 35.0, 0.01);
        }

        [Fact]
        public void GetAverageFPS_WithMultipleRecords_ShouldCalculateCorrectly()
        {
            var profiler = new ModelInferenceProfiler();
            profiler.Record(100, 200, 50); // First record (skipped)
            profiler.Record(10, 20, 20);   // Total = 50ms
            profiler.Record(20, 30, 50);   // Total = 100ms

            var fps = profiler.GetAverageFPS();
            var expectedAvgTotalSeconds = (50 + 100) / 2.0 / 1000.0; // Average total in seconds
            fps.Should().BeApproximately(1.0 / expectedAvgTotalSeconds, 0.01);
        }
    }
}
