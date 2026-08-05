using System;

namespace JYPPX.DeploySharp.Results
{
    /// <summary>
    /// Contains measured preprocessing, backend inference, and postprocessing durations. / 包含预处理、后端推理和后处理的测量时长。
    /// </summary>
    public sealed class InferenceTiming
    {
        /// <summary>Initializes timing measurements. / 初始化时长测量值。</summary>
        public InferenceTiming(TimeSpan preprocessing, TimeSpan inference, TimeSpan postprocessing)
        {
            if (preprocessing < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(preprocessing));
            if (inference < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(inference));
            if (postprocessing < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(postprocessing));
            Preprocessing = preprocessing;
            Inference = inference;
            Postprocessing = postprocessing;
        }

        /// <summary>Gets preprocessing time. / 获取预处理时长。</summary>
        public TimeSpan Preprocessing { get; }

        /// <summary>Gets backend inference time. / 获取后端推理时长。</summary>
        public TimeSpan Inference { get; }

        /// <summary>Gets postprocessing time. / 获取后处理时长。</summary>
        public TimeSpan Postprocessing { get; }

        /// <summary>Gets total measured time. / 获取测量总时长。</summary>
        public TimeSpan Total => Preprocessing + Inference + Postprocessing;

        /// <summary>Gets an all-zero timing value. / 获取全部为零的时长值。</summary>
        public static InferenceTiming Zero { get; } =
            new InferenceTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
    }
}
