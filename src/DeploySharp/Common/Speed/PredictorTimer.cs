using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Common.Speed
{
    public class PredictorTimer
    {
        private Stopwatch stopwatch = new();

        private TimeSpan preprocess;
        private TimeSpan inference;
        private TimeSpan postprocess;

        public void StartPreprocess()
        {
            stopwatch.Restart();
        }

        public void StartInference()
        {
            preprocess = stopwatch.Elapsed;
            stopwatch.Restart();
        }

        public void StartPostprocess()
        {
            inference = stopwatch.Elapsed;
            stopwatch.Restart();
        }

        public RunSpeed Stop()
        {
            postprocess = stopwatch.Elapsed;
            stopwatch.Stop();

            return new RunSpeed(preprocess, inference, postprocess);
        }
    }
}
