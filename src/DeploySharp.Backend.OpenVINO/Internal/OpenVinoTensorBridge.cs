using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Tensors;
using OpenVinoSharp;
using CoreElementType = JYPPX.DeploySharp.Tensors.TensorElementType;
using CoreModelMetadata = JYPPX.DeploySharp.Models.ModelMetadata;
using OvElementType = OpenVinoSharp.ElementType;
using OvTensor = OpenVinoSharp.Tensor;

namespace JYPPX.DeploySharp.Backends.OpenVINO.Internal
{
    internal static class OpenVinoTensorBridge
    {
        public static CoreModelMetadata CreateMetadata(ModelArtifact artifact, Model model, CompiledModel compiledModel, bool allowDynamicShapes)
        {
            if (model.InputCount != compiledModel.InputCount || model.OutputCount != compiledModel.OutputCount) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "OpenVINO changed the public model port count during compilation.", modelId: artifact.ModelId, operation: "metadata", technicalDetails: "modelInputs=" + model.InputCount + ";compiledInputs=" + compiledModel.InputCount + ";modelOutputs=" + model.OutputCount + ";compiledOutputs=" + compiledModel.OutputCount);
            var inputs = new List<TensorDescriptor>();
            for (ulong index = 0; index < compiledModel.InputCount; index++)
            {
                using (Input sourcePort = model.GetInput(index))
                using (Input compiledPort = compiledModel.GetInput(index)) inputs.Add(ToDescriptor(artifact, sourcePort.GetAnyName(), compiledPort.get_element_type().get_type(), compiledPort.get_partial_shape(), allowDynamicShapes));
            }
            var outputs = new List<TensorDescriptor>();
            for (ulong index = 0; index < compiledModel.OutputCount; index++)
            {
                using (Output sourcePort = model.GetOutput(index))
                using (Output compiledPort = compiledModel.GetOutput(index)) outputs.Add(ToDescriptor(artifact, sourcePort.GetAnyName(), compiledPort.get_element_type().get_type(), compiledPort.get_partial_shape(), allowDynamicShapes));
            }
            return new CoreModelMetadata(artifact.ModelId, artifact.Format, inputs, outputs);
        }

        public static OvTensor CreateInput(ModelArtifact artifact, string name, ITensor tensor, TensorDescriptor descriptor)
        {
            ValidateRuntimeTensor(artifact, name, tensor, descriptor);
            // Managed API 3.3.0 cannot allocate a writable rank-zero Tensor, so fail before native allocation. / Managed API 3.3.0 无法分配可写的零秩 Tensor，因此在原生分配前稳定失败。
            if (tensor.Shape.Rank == 0) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "Scalar tensor inputs are not supported by JYPPX.OpenVINO.CSharp.API 3.3.0.", modelId: artifact.ModelId, tensorName: name, operation: "bind-input", technicalDetails: "rank=0;managed-api=3.3.0");
            var shape = new Shape(tensor.Shape.ToArray());
            try
            {
                var native = new OvTensor(shape, ToOpenVinoType(artifact, name, tensor.ElementType));
                try
                {
                    CopyIntoNative(artifact, name, tensor, native);
                    return native;
                }
                catch { native.Dispose(); throw; }
            }
            finally { shape.Dispose(); }
        }

        public static InferenceOutputs CopyOutputs(ModelArtifact artifact, InferRequest request, IReadOnlyList<TensorDescriptor> descriptors)
        {
            var values = new List<NamedTensor>(descriptors.Count);
            for (int index = 0; index < descriptors.Count; index++)
            {
                TensorDescriptor descriptor = descriptors[index];
                using (OvTensor tensor = request.get_output_tensor((ulong)index)) values.Add(new NamedTensor(descriptor.Name, CopyOutput(artifact, descriptor.Name, tensor)));
            }
            return new InferenceOutputs(values);
        }

        private static TensorDescriptor ToDescriptor(ModelArtifact artifact, string name, OvElementType nativeType, PartialShape partialShape, bool allowDynamicShapes)
        {
            using (partialShape)
            {
                if (partialShape.rank.is_dynamic()) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "OpenVINO returned a tensor with dynamic rank, which Core cannot represent safely.", modelId: artifact.ModelId, tensorName: name, operation: "metadata");
                var dimensions = new long[partialShape.dims.Length];
                for (int index = 0; index < dimensions.Length; index++) dimensions[index] = partialShape.dims[index].is_dynamic() ? -1 : partialShape.dims[index].get_length();
                var shape = new TensorShape(dimensions);
                if (shape.IsDynamic && !allowDynamicShapes) throw new OpenVinoBackendException(OpenVinoErrorCodes.ConfigurationInvalid, "The model contains a dynamic tensor but dynamic shapes are disabled.", modelId: artifact.ModelId, tensorName: name, operation: "metadata");
                return new TensorDescriptor(name, ToCoreType(artifact, name, nativeType), shape);
            }
        }

        private static void CopyIntoNative(ModelArtifact artifact, string name, ITensor source, OvTensor destination)
        {
            switch (source.ElementType)
            {
                case CoreElementType.Boolean: destination.SetData(RequireBuffer<bool>(artifact, name, source)); break;
                case CoreElementType.Int8: destination.SetData(RequireBuffer<sbyte>(artifact, name, source)); break;
                case CoreElementType.UInt8: destination.SetData(RequireBuffer<byte>(artifact, name, source)); break;
                case CoreElementType.Int16: destination.SetData(RequireBuffer<short>(artifact, name, source)); break;
                case CoreElementType.UInt16: destination.SetData(RequireBuffer<ushort>(artifact, name, source)); break;
                case CoreElementType.Int32: destination.SetData(RequireBuffer<int>(artifact, name, source)); break;
                case CoreElementType.UInt32: destination.SetData(RequireBuffer<uint>(artifact, name, source)); break;
                case CoreElementType.Int64: destination.SetData(RequireBuffer<long>(artifact, name, source)); break;
                case CoreElementType.UInt64: destination.SetData(RequireBuffer<ulong>(artifact, name, source)); break;
                case CoreElementType.Float32: destination.SetData(RequireBuffer<float>(artifact, name, source)); break;
                case CoreElementType.Float64: destination.SetData(RequireBuffer<double>(artifact, name, source)); break;
                default: throw Unsupported(artifact, name, source.ElementType);
            }
        }

        private static ITensor CopyOutput(ModelArtifact artifact, string name, OvTensor native)
        {
            using (Shape nativeShape = native.shape)
            {
                var shape = new TensorShape(nativeShape.get_dims());
                int length = checked((int)shape.GetElementCount());
                switch (ToCoreType(artifact, name, native.element_type))
                {
                    case CoreElementType.Boolean: return Owned(shape, native.GetData<bool>(length));
                    case CoreElementType.Int8: return Owned(shape, native.GetData<sbyte>(length));
                    case CoreElementType.UInt8: return Owned(shape, native.GetData<byte>(length));
                    case CoreElementType.Int16: return Owned(shape, native.GetData<short>(length));
                    case CoreElementType.UInt16: return Owned(shape, native.GetData<ushort>(length));
                    case CoreElementType.Int32: return Owned(shape, native.GetData<int>(length));
                    case CoreElementType.UInt32: return Owned(shape, native.GetData<uint>(length));
                    case CoreElementType.Int64: return Owned(shape, native.GetData<long>(length));
                    case CoreElementType.UInt64: return Owned(shape, native.GetData<ulong>(length));
                    case CoreElementType.Float32: return Owned(shape, native.GetData<float>(length));
                    case CoreElementType.Float64: return Owned(shape, native.GetData<double>(length));
                    default: throw Unsupported(artifact, name, ToCoreType(artifact, name, native.element_type));
                }
            }
        }

        private static Tensor<T> Owned<T>(TensorShape shape, T[] values) => new Tensor<T>(shape, values, TensorBufferOwnership.Transfer);

        private static T[] RequireBuffer<T>(ModelArtifact artifact, string name, ITensor tensor)
        {
            T[]? values = tensor.Buffer as T[];
            if (values == null) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "The managed tensor buffer type does not match its declared element type.", modelId: artifact.ModelId, tensorName: name, operation: "bind-input");
            return values;
        }

        private static void ValidateRuntimeTensor(ModelArtifact artifact, string name, ITensor tensor, TensorDescriptor descriptor)
        {
            if (tensor.ElementType != descriptor.ElementType) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "Input tensor element type does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input", technicalDetails: tensor.ElementType + " != " + descriptor.ElementType);
            if (tensor.Shape.Rank != descriptor.Shape.Rank) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "Input tensor rank does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input");
            for (int index = 0; index < tensor.Shape.Rank; index++)
            {
                long expected = descriptor.Shape[index];
                if (expected >= 0 && tensor.Shape[index] != expected) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "Input tensor shape does not match model metadata.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input", technicalDetails: "dimension=" + index + ";expected=" + expected + ";actual=" + tensor.Shape[index]);
            }
            if (tensor.Length != tensor.Shape.GetElementCount()) throw new OpenVinoBackendException(OpenVinoErrorCodes.TensorInvalid, "Input tensor element count is invalid.", modelId: artifact.ModelId, tensorName: name, operation: "validate-input");
        }

        private static CoreElementType ToCoreType(ModelArtifact artifact, string name, OvElementType value)
        {
            switch (value)
            {
                case OvElementType.BOOLEAN: return CoreElementType.Boolean;
                case OvElementType.I8: return CoreElementType.Int8;
                case OvElementType.U8: return CoreElementType.UInt8;
                case OvElementType.I16: return CoreElementType.Int16;
                case OvElementType.U16: return CoreElementType.UInt16;
                case OvElementType.I32: return CoreElementType.Int32;
                case OvElementType.U32: return CoreElementType.UInt32;
                case OvElementType.I64: return CoreElementType.Int64;
                case OvElementType.U64: return CoreElementType.UInt64;
                case OvElementType.F32: return CoreElementType.Float32;
                case OvElementType.F64: return CoreElementType.Float64;
                case OvElementType.F16: throw Unsupported(artifact, name, CoreElementType.Float16);
                case OvElementType.BF16: throw Unsupported(artifact, name, CoreElementType.BFloat16);
                case OvElementType.STRING: throw Unsupported(artifact, name, CoreElementType.String);
                default: throw Unsupported(artifact, name, CoreElementType.Unknown);
            }
        }

        private static OvElementType ToOpenVinoType(ModelArtifact artifact, string name, CoreElementType value)
        {
            switch (value)
            {
                case CoreElementType.Boolean: return OvElementType.BOOLEAN;
                case CoreElementType.Int8: return OvElementType.I8;
                case CoreElementType.UInt8: return OvElementType.U8;
                case CoreElementType.Int16: return OvElementType.I16;
                case CoreElementType.UInt16: return OvElementType.U16;
                case CoreElementType.Int32: return OvElementType.I32;
                case CoreElementType.UInt32: return OvElementType.U32;
                case CoreElementType.Int64: return OvElementType.I64;
                case CoreElementType.UInt64: return OvElementType.U64;
                case CoreElementType.Float32: return OvElementType.F32;
                case CoreElementType.Float64: return OvElementType.F64;
                default: throw Unsupported(artifact, name, value);
            }
        }

        private static OpenVinoBackendException Unsupported(ModelArtifact artifact, string name, CoreElementType value)
        {
            return new OpenVinoBackendException(OpenVinoErrorCodes.ElementTypeUnsupported, "The OpenVINO adapter does not support this Core tensor element type.", modelId: artifact.ModelId, tensorName: name, operation: "tensor-type", technicalDetails: value.ToString());
        }
    }
}
