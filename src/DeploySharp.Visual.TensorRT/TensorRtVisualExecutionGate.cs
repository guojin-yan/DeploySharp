using System;

namespace JYPPX.DeploySharp.Visual.TensorRT
{
    // TensorRT device buffers and output slots are owned by one pipeline and cannot overlap.
    internal sealed class TensorRtVisualExecutionGate
    {
        public object SyncRoot { get; } = new object();
    }
}
