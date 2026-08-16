using System;
using JYPPX.TensorRtSharp.Shared.Interop;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    internal static class TensorRtApiLineMapper
    {
        public static TensorRtApiLine Map(TensorRtApiVersion version)
        {
            return version switch
            {
                TensorRtApiVersion.TensorRt8 => TensorRtApiLine.TensorRt8,
                TensorRtApiVersion.TensorRt10 => TensorRtApiLine.TensorRt10,
                TensorRtApiVersion.TensorRt11 => TensorRtApiLine.TensorRt11,
                _ => throw new ArgumentOutOfRangeException(nameof(version))
            };
        }
    }
}
