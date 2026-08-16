using System;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using JYPPX.TensorRtSharp;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    internal static class TensorRtBindingContract
    {
        public static void ValidateForSession(TensorRtEngineTensorBinding binding, ModelId modelId)
        {
            if (binding.IOMode != TensorRtIOMode.Input && binding.IOMode != TensorRtIOMode.Output)
            {
                throw Invalid(binding, modelId, "TensorRT exposed an unknown tensor I/O mode.");
            }
            if (binding.Location != TensorRtTensorLocation.Device)
            {
                throw Invalid(binding, modelId, "Host-located TensorRT I/O tensors are not supported by this managed adapter.");
            }
            if (binding.IsShapeInferenceIO)
            {
                throw Invalid(binding, modelId, "TensorRT shape-inference I/O tensors require an explicit shape-tensor contract.");
            }
            if (binding.Format != TensorRtTensorFormat.Linear || binding.VectorizedDimension >= 0 || binding.EffectiveComponentsPerElement != 1)
            {
                throw Invalid(binding, modelId, "Only non-vectorized linear TensorRT tensor layouts are supported.");
            }
            if (binding.EngineShape.Values.Length == 0)
            {
                throw Invalid(binding, modelId, "Scalar TensorRT bindings are not supported by the published managed buffer API.");
            }
            foreach (int dimension in binding.EngineShape.Values)
            {
                if (dimension == 0 || dimension < -1)
                {
                    throw Invalid(binding, modelId, "TensorRT exposed an invalid engine dimension.");
                }
            }
        }

        public static void ValidateInputShape(TensorRtEngineTensorBinding binding, TensorRtDims runtimeShape, ModelId modelId)
        {
            int[] engine = binding.EngineShape.Values;
            int[] actual = runtimeShape.Values;
            if (actual.Length != engine.Length)
            {
                throw Invalid(binding, modelId, "The runtime tensor rank does not match the TensorRT engine binding.");
            }

            for (int index = 0; index < actual.Length; index++)
            {
                if (engine[index] > 0 && actual[index] != engine[index])
                {
                    throw Invalid(binding, modelId, "The runtime tensor shape does not match the static TensorRT engine shape.");
                }
            }

            ValidateProfileBound(binding, actual, binding.ProfileMinShape, isMinimum: true, modelId);
            ValidateProfileBound(binding, actual, binding.ProfileMaxShape, isMinimum: false, modelId);
        }

        public static void ValidateOutputBuffer(TensorRtEngineTensorBinding binding, TensorRtDims shape, int actualBytes, ModelId modelId)
        {
            int expectedBytes = binding.EstimateByteSize(shape);
            if (actualBytes != expectedBytes)
            {
                throw Invalid(binding, modelId, "The TensorRT output buffer size does not match its concrete shape and element layout.");
            }
        }

        private static void ValidateProfileBound(
            TensorRtEngineTensorBinding binding,
            int[] actual,
            TensorRtDims? bound,
            bool isMinimum,
            ModelId modelId)
        {
            if (bound == null || bound.Values.Length != actual.Length || Array.Exists(bound.Values, value => value <= 0)) return;
            for (int index = 0; index < actual.Length; index++)
            {
                if ((isMinimum && actual[index] < bound.Values[index]) || (!isMinimum && actual[index] > bound.Values[index]))
                {
                    throw Invalid(binding, modelId, "The runtime tensor shape is outside the selected TensorRT optimization profile.");
                }
            }
        }

        private static TensorRtBackendException Invalid(TensorRtEngineTensorBinding binding, ModelId modelId, string message)
        {
            return new TensorRtBackendException(
                TensorRtErrorCodes.TensorInvalid,
                message,
                modelId: modelId,
                tensorName: binding.Name,
                operation: "binding-contract",
                technicalDetails: "format=" + binding.Format + ";location=" + binding.Location + ";shape=" + binding.EngineShape);
        }
    }
}
