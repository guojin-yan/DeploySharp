using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using Microsoft.ML.OnnxRuntime;
using CoreElementType = JYPPX.DeploySharp.Tensors.TensorElementType;
using CoreModelMetadata = JYPPX.DeploySharp.Models.ModelMetadata;
using OrtElementType = Microsoft.ML.OnnxRuntime.Tensors.TensorElementType;

namespace JYPPX.DeploySharp.Backends.OnnxRuntime.Internal
{
    internal static class OnnxTensorBridge
    {
        public static CoreModelMetadata CreateMetadata(ModelArtifact artifact, InferenceSession session)
        {
            var inputs = new List<TensorDescriptor>();
            foreach (KeyValuePair<string, NodeMetadata> pair in session.InputMetadata) inputs.Add(ToDescriptor(artifact, pair.Key, pair.Value));
            var outputs = new List<TensorDescriptor>();
            foreach (KeyValuePair<string, NodeMetadata> pair in session.OutputMetadata) outputs.Add(ToDescriptor(artifact, pair.Key, pair.Value));
            return new CoreModelMetadata(artifact.ModelId, "onnx", inputs, outputs);
        }

        public static OrtValue CreateInput(ModelArtifact artifact, string name, ITensor tensor, TensorDescriptor descriptor)
        {
            ValidateRuntimeTensor(artifact, name, tensor, descriptor);
            long[] shape = tensor.Shape.ToArray();
            try
            {
                switch (tensor.ElementType)
                {
                    case CoreElementType.Boolean: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<bool>(artifact, name, tensor), shape);
                    case CoreElementType.Int8: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<sbyte>(artifact, name, tensor), shape);
                    case CoreElementType.UInt8: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<byte>(artifact, name, tensor), shape);
                    case CoreElementType.Int16: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<short>(artifact, name, tensor), shape);
                    case CoreElementType.UInt16: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<ushort>(artifact, name, tensor), shape);
                    case CoreElementType.Int32: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<int>(artifact, name, tensor), shape);
                    case CoreElementType.UInt32: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<uint>(artifact, name, tensor), shape);
                    case CoreElementType.Int64: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<long>(artifact, name, tensor), shape);
                    case CoreElementType.UInt64: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<ulong>(artifact, name, tensor), shape);
                    case CoreElementType.Float32: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<float>(artifact, name, tensor), shape);
                    case CoreElementType.Float64: return OrtValue.CreateTensorValueFromMemory(RequireBuffer<double>(artifact, name, tensor), shape);
                    default: throw Unsupported(artifact, name, tensor.ElementType);
                }
            }
            catch (OnnxRuntimeBackendException) { throw; }
            catch (Exception exception) { throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "The Core tensor could not be adapted to an ONNX Runtime input.", exception, artifact.ModelId, name, "bind-input", exception.ToString()); }
        }

        public static InferenceOutputs CopyOutputs(ModelArtifact artifact, IReadOnlyList<string> names, IEnumerable<OrtValue> values)
        {
            var tensors = new List<NamedTensor>();
            int index = 0;
            foreach (OrtValue value in values)
            {
                if (index >= names.Count) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "ONNX Runtime returned more outputs than requested.", modelId: artifact.ModelId, operation: "copy-output");
                tensors.Add(new NamedTensor(names[index], CopyOutput(artifact, names[index], value)));
                index++;
            }
            if (index != names.Count) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "ONNX Runtime returned fewer outputs than requested.", modelId: artifact.ModelId, operation: "copy-output");
            return new InferenceOutputs(tensors);
        }

        public static OrtValue AllocateOutput(ModelArtifact artifact, TensorDescriptor descriptor)
        {
            if (descriptor.Shape.IsDynamic) throw new InvalidOperationException("A dynamic output cannot be preallocated.");
            OrtElementType type = ToOrtType(artifact, descriptor.Name, descriptor.ElementType);
            try { return OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, type, descriptor.Shape.ToArray()); }
            catch (Exception exception) { throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "The ONNX Runtime output buffer could not be allocated.", exception, artifact.ModelId, descriptor.Name, "allocate-output", exception.ToString()); }
        }

        private static TensorDescriptor ToDescriptor(ModelArtifact artifact, string name, NodeMetadata metadata)
        {
            try
            {
                CoreElementType elementType = ToCoreType(metadata.ElementDataType);
                var dimensions = new long[metadata.Dimensions.Length];
                for (int index = 0; index < dimensions.Length; index++) dimensions[index] = metadata.Dimensions[index] < 0 ? -1 : metadata.Dimensions[index];
                return new TensorDescriptor(name, elementType, new TensorShape(dimensions));
            }
            catch (OnnxRuntimeBackendException) { throw; }
            catch (Exception exception) { throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ElementTypeUnsupported, "The ONNX model contains a non-tensor or unsupported metadata type.", exception, artifact.ModelId, name, "metadata", exception.ToString()); }
        }

        private static ITensor CopyOutput(ModelArtifact artifact, string name, OrtValue value)
        {
            try
            {
                OrtTensorTypeAndShapeInfo info = value.GetTensorTypeAndShape();
                {
                    var shape = new TensorShape(info.Shape);
                    switch (ToCoreType(info.ElementDataType))
                    {
                        case CoreElementType.Boolean: return new Tensor<bool>(shape, value.GetTensorDataAsSpan<bool>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Int8: return new Tensor<sbyte>(shape, value.GetTensorDataAsSpan<sbyte>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.UInt8: return new Tensor<byte>(shape, value.GetTensorDataAsSpan<byte>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Int16: return new Tensor<short>(shape, value.GetTensorDataAsSpan<short>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.UInt16: return new Tensor<ushort>(shape, value.GetTensorDataAsSpan<ushort>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Int32: return new Tensor<int>(shape, value.GetTensorDataAsSpan<int>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.UInt32: return new Tensor<uint>(shape, value.GetTensorDataAsSpan<uint>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Int64: return new Tensor<long>(shape, value.GetTensorDataAsSpan<long>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.UInt64: return new Tensor<ulong>(shape, value.GetTensorDataAsSpan<ulong>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Float32: return new Tensor<float>(shape, value.GetTensorDataAsSpan<float>().ToArray(), TensorBufferOwnership.Transfer);
                        case CoreElementType.Float64: return new Tensor<double>(shape, value.GetTensorDataAsSpan<double>().ToArray(), TensorBufferOwnership.Transfer);
                        default: throw Unsupported(artifact, name, ToCoreType(info.ElementDataType));
                    }
                }
            }
            catch (OnnxRuntimeBackendException) { throw; }
            catch (Exception exception) { throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "The ONNX Runtime output could not be copied into a Core tensor.", exception, artifact.ModelId, name, "copy-output", exception.ToString()); }
        }

        private static void ValidateRuntimeTensor(ModelArtifact artifact, string name, ITensor tensor, TensorDescriptor descriptor)
        {
            if (tensor.ElementType != descriptor.ElementType) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "Input tensor element type does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input", technicalDetails: tensor.ElementType + " != " + descriptor.ElementType);
            if (tensor.Shape.Rank != descriptor.Shape.Rank) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "Input tensor rank does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input");
            for (int index = 0; index < tensor.Shape.Rank; index++) if (descriptor.Shape[index] >= 0 && descriptor.Shape[index] != tensor.Shape[index]) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "Input tensor shape does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input", technicalDetails: tensor.Shape + " != " + descriptor.Shape);
            if (tensor.Shape.GetElementCount() != tensor.Length) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "Input tensor element count is inconsistent with its shape.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input");
            if (tensor.ElementType == CoreElementType.Float16 || tensor.ElementType == CoreElementType.BFloat16 || tensor.ElementType == CoreElementType.String || tensor.ElementType == CoreElementType.Unknown) throw Unsupported(artifact, name, tensor.ElementType);
        }

        private static T[] RequireBuffer<T>(ModelArtifact artifact, string name, ITensor tensor)
        {
            T[]? values = tensor.Buffer as T[];
            if (values == null) throw new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.TensorInvalid, "Input tensor buffer CLR type is inconsistent with its declared element type.", modelId: artifact.ModelId, tensorName: name, operation: "bind-input", technicalDetails: tensor.Buffer.GetType().FullName);
            return values;
        }

        private static CoreElementType ToCoreType(OrtElementType type)
        {
            switch (type)
            {
                case OrtElementType.Bool: return CoreElementType.Boolean;
                case OrtElementType.Int8: return CoreElementType.Int8;
                case OrtElementType.UInt8: return CoreElementType.UInt8;
                case OrtElementType.Int16: return CoreElementType.Int16;
                case OrtElementType.UInt16: return CoreElementType.UInt16;
                case OrtElementType.Int32: return CoreElementType.Int32;
                case OrtElementType.UInt32: return CoreElementType.UInt32;
                case OrtElementType.Int64: return CoreElementType.Int64;
                case OrtElementType.UInt64: return CoreElementType.UInt64;
                case OrtElementType.Float: return CoreElementType.Float32;
                case OrtElementType.Double: return CoreElementType.Float64;
                case OrtElementType.Float16: return CoreElementType.Float16;
                case OrtElementType.BFloat16: return CoreElementType.BFloat16;
                case OrtElementType.String: return CoreElementType.String;
                default: return CoreElementType.Unknown;
            }
        }

        private static OrtElementType ToOrtType(ModelArtifact artifact, string name, CoreElementType type)
        {
            switch (type)
            {
                case CoreElementType.Boolean: return OrtElementType.Bool;
                case CoreElementType.Int8: return OrtElementType.Int8;
                case CoreElementType.UInt8: return OrtElementType.UInt8;
                case CoreElementType.Int16: return OrtElementType.Int16;
                case CoreElementType.UInt16: return OrtElementType.UInt16;
                case CoreElementType.Int32: return OrtElementType.Int32;
                case CoreElementType.UInt32: return OrtElementType.UInt32;
                case CoreElementType.Int64: return OrtElementType.Int64;
                case CoreElementType.UInt64: return OrtElementType.UInt64;
                case CoreElementType.Float32: return OrtElementType.Float;
                case CoreElementType.Float64: return OrtElementType.Double;
                default: throw Unsupported(artifact, name, type);
            }
        }

        private static OnnxRuntimeBackendException Unsupported(ModelArtifact artifact, string name, CoreElementType type)
        {
            return new OnnxRuntimeBackendException(OnnxRuntimeErrorCodes.ElementTypeUnsupported, "The ONNX tensor element type is not supported by the stable Core array bridge.", modelId: artifact.ModelId, tensorName: name, operation: "tensor-type", technicalDetails: type.ToString());
        }
    }
}
