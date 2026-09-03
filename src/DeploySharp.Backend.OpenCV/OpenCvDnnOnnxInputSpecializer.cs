using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    internal static class OpenCvDnnOnnxInputSpecializer
    {
        private const int ModelGraphField = 7;
        private const int GraphInputField = 11;
        private const int ValueInfoNameField = 1;
        private const int ValueInfoTypeField = 2;
        private const int TypeTensorField = 1;
        private const int TensorElementTypeField = 1;
        private const int TensorShapeField = 2;
        private const int ShapeDimensionField = 1;
        private const int DimensionValueField = 1;
        private const int DimensionParameterField = 2;
        private const ulong OnnxFloat32 = 1;
        private const ulong OnnxUInt8 = 2;
        private const ulong OnnxInt8 = 3;
        private const ulong OnnxInt32 = 6;
        private const ulong OnnxInt64 = 7;
        private const ulong OnnxFloat64 = 11;

        internal static byte[] Specialize(byte[] model, IReadOnlyList<TensorDescriptor> inputs, out bool changed)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            List<ProtoField> modelFields = Parse(model, "ModelProto");
            int graphIndex = FindSingleField(modelFields, ModelGraphField, required: true, "ModelProto.graph");
            ProtoField graphField = modelFields[graphIndex];
            RequireWireType(graphField, 2, "ModelProto.graph");

            var contracts = new Dictionary<string, TensorDescriptor>(StringComparer.Ordinal);
            foreach (TensorDescriptor input in inputs)
            {
                if (input == null) throw new InvalidDataException("The OpenCV DNN input contract contains a null descriptor.");
                contracts.Add(input.Name, input);
            }

            List<ProtoField> graphFields = Parse(graphField.Payload, "GraphProto");
            var matched = new HashSet<string>(StringComparer.Ordinal);
            bool graphChanged = false;
            for (int index = 0; index < graphFields.Count; index++)
            {
                ProtoField inputField = graphFields[index];
                if (inputField.Number != GraphInputField) continue;
                RequireWireType(inputField, 2, "GraphProto.input");

                List<ProtoField> valueInfoFields = Parse(inputField.Payload, "ValueInfoProto");
                string name = ReadString(valueInfoFields, ValueInfoNameField, "ValueInfoProto.name");
                if (!contracts.TryGetValue(name, out TensorDescriptor? descriptor)) continue;
                if (!matched.Add(name)) throw new InvalidDataException("The ONNX graph declares the contracted input more than once: " + name);

                int typeIndex = FindSingleField(valueInfoFields, ValueInfoTypeField, required: true, "ValueInfoProto.type");
                ProtoField typeField = valueInfoFields[typeIndex];
                RequireWireType(typeField, 2, "ValueInfoProto.type");
                byte[] specializedType = SpecializeType(typeField.Payload, descriptor, out bool typeChanged);
                if (typeChanged)
                {
                    valueInfoFields[typeIndex] = new ProtoField(typeField.Number, typeField.WireType, specializedType);
                    graphFields[index] = new ProtoField(inputField.Number, inputField.WireType, Serialize(valueInfoFields));
                    graphChanged = true;
                }
            }

            foreach (TensorDescriptor descriptor in inputs)
            {
                if (!matched.Contains(descriptor.Name)) throw new InvalidDataException("The ONNX graph does not declare the contracted input: " + descriptor.Name);
            }

            if (!graphChanged)
            {
                changed = false;
                return model;
            }

            modelFields[graphIndex] = new ProtoField(graphField.Number, graphField.WireType, Serialize(graphFields));
            changed = true;
            return Serialize(modelFields);
        }

        private static byte[] SpecializeType(byte[] payload, TensorDescriptor descriptor, out bool changed)
        {
            List<ProtoField> typeFields = Parse(payload, "TypeProto");
            int tensorIndex = FindSingleField(typeFields, TypeTensorField, required: true, "TypeProto.tensor_type");
            ProtoField tensorField = typeFields[tensorIndex];
            RequireWireType(tensorField, 2, "TypeProto.tensor_type");

            List<ProtoField> tensorFields = Parse(tensorField.Payload, "TypeProto.Tensor");
            ulong elementType = ReadVarint(tensorFields, TensorElementTypeField, "TypeProto.Tensor.elem_type");
            ulong expectedElementType = descriptor.ElementType switch
            {
                TensorElementType.Float32 => OnnxFloat32,
                TensorElementType.UInt8 => OnnxUInt8,
                TensorElementType.Int8 => OnnxInt8,
                TensorElementType.Int32 => OnnxInt32,
                TensorElementType.Int64 => OnnxInt64,
                TensorElementType.Float64 => OnnxFloat64,
                _ => throw new InvalidDataException("OpenCV DNN input specialization supports float32, float64, int8, uint8, int32, and int64 contracts: " + descriptor.Name)
            };
            if (elementType != expectedElementType) throw new InvalidDataException("The ONNX input element type differs from the OpenCV DNN contract: " + descriptor.Name + ";model=" + elementType + ";contract=" + descriptor.ElementType);

            int shapeIndex = FindSingleField(tensorFields, TensorShapeField, required: true, "TypeProto.Tensor.shape");
            ProtoField shapeField = tensorFields[shapeIndex];
            RequireWireType(shapeField, 2, "TypeProto.Tensor.shape");
            List<ProtoField> shapeFields = Parse(shapeField.Payload, "TensorShapeProto");
            var dimensionIndexes = new List<int>();
            for (int index = 0; index < shapeFields.Count; index++)
            {
                if (shapeFields[index].Number == ShapeDimensionField) dimensionIndexes.Add(index);
            }
            if (dimensionIndexes.Count != descriptor.Shape.Rank) throw new InvalidDataException("The ONNX input rank differs from the OpenCV DNN contract: " + descriptor.Name);

            bool shapeChanged = false;
            for (int dimension = 0; dimension < dimensionIndexes.Count; dimension++)
            {
                int fieldIndex = dimensionIndexes[dimension];
                ProtoField dimensionField = shapeFields[fieldIndex];
                RequireWireType(dimensionField, 2, "TensorShapeProto.dim");
                List<ProtoField> dimensionFields = Parse(dimensionField.Payload, "TensorShapeProto.Dimension");
                long contractValue = descriptor.Shape[dimension];
                if (contractValue <= 0) throw new InvalidDataException("OpenCV DNN input specialization requires a positive static contract dimension: " + descriptor.Name);

                int valueIndex = FindSingleField(dimensionFields, DimensionValueField, required: false, "TensorShapeProto.Dimension.dim_value");
                if (valueIndex >= 0)
                {
                    ProtoField valueField = dimensionFields[valueIndex];
                    RequireWireType(valueField, 0, "TensorShapeProto.Dimension.dim_value");
                    ulong modelValue = DecodeVarint(valueField.Payload, "TensorShapeProto.Dimension.dim_value");
                    if (modelValue > 0 && modelValue != checked((ulong)contractValue))
                    {
                        throw new InvalidDataException("The ONNX input dimension differs from the OpenCV DNN contract: " + descriptor.Name + "[" + dimension + "] model=" + modelValue + ";contract=" + contractValue);
                    }
                    if (modelValue == checked((ulong)contractValue) && FindSingleField(dimensionFields, DimensionParameterField, required: false, "TensorShapeProto.Dimension.dim_param") < 0) continue;
                }

                var specializedDimension = new List<ProtoField>();
                specializedDimension.Add(new ProtoField(DimensionValueField, 0, EncodeVarint(checked((ulong)contractValue))));
                foreach (ProtoField field in dimensionFields)
                {
                    if (field.Number != DimensionValueField && field.Number != DimensionParameterField) specializedDimension.Add(field);
                }
                shapeFields[fieldIndex] = new ProtoField(dimensionField.Number, dimensionField.WireType, Serialize(specializedDimension));
                shapeChanged = true;
            }

            if (!shapeChanged)
            {
                changed = false;
                return payload;
            }

            tensorFields[shapeIndex] = new ProtoField(shapeField.Number, shapeField.WireType, Serialize(shapeFields));
            typeFields[tensorIndex] = new ProtoField(tensorField.Number, tensorField.WireType, Serialize(tensorFields));
            changed = true;
            return Serialize(typeFields);
        }

        private static List<ProtoField> Parse(byte[] bytes, string messageName)
        {
            var fields = new List<ProtoField>();
            int offset = 0;
            while (offset < bytes.Length)
            {
                ulong key = ReadVarint(bytes, ref offset, messageName + " field key");
                int number = checked((int)(key >> 3));
                int wireType = checked((int)(key & 7));
                if (number <= 0) throw new InvalidDataException(messageName + " contains an invalid field number.");
                int payloadStart;
                int payloadLength;
                switch (wireType)
                {
                    case 0:
                        payloadStart = offset;
                        ReadVarint(bytes, ref offset, messageName + " varint");
                        payloadLength = offset - payloadStart;
                        break;
                    case 1:
                        payloadStart = offset;
                        payloadLength = 8;
                        offset = CheckedAdvance(offset, payloadLength, bytes.Length, messageName);
                        break;
                    case 2:
                        ulong length = ReadVarint(bytes, ref offset, messageName + " length");
                        if (length > int.MaxValue) throw new InvalidDataException(messageName + " contains an oversized field.");
                        payloadStart = offset;
                        payloadLength = checked((int)length);
                        offset = CheckedAdvance(offset, payloadLength, bytes.Length, messageName);
                        break;
                    case 5:
                        payloadStart = offset;
                        payloadLength = 4;
                        offset = CheckedAdvance(offset, payloadLength, bytes.Length, messageName);
                        break;
                    default:
                        throw new InvalidDataException(messageName + " contains an unsupported protobuf wire type: " + wireType);
                }
                var payload = new byte[payloadLength];
                Buffer.BlockCopy(bytes, payloadStart, payload, 0, payloadLength);
                fields.Add(new ProtoField(number, wireType, payload));
            }
            return fields;
        }

        private static byte[] Serialize(IReadOnlyList<ProtoField> fields)
        {
            using (var stream = new MemoryStream())
            {
                foreach (ProtoField field in fields)
                {
                    Write(stream, EncodeVarint(checked(((ulong)field.Number << 3) | (uint)field.WireType)));
                    if (field.WireType == 2) Write(stream, EncodeVarint(checked((ulong)field.Payload.Length)));
                    Write(stream, field.Payload);
                }
                return stream.ToArray();
            }
        }

        private static int FindSingleField(IReadOnlyList<ProtoField> fields, int number, bool required, string name)
        {
            int found = -1;
            for (int index = 0; index < fields.Count; index++)
            {
                if (fields[index].Number != number) continue;
                if (found >= 0) throw new InvalidDataException(name + " is declared more than once.");
                found = index;
            }
            if (required && found < 0) throw new InvalidDataException(name + " is missing.");
            return found;
        }

        private static string ReadString(IReadOnlyList<ProtoField> fields, int number, string name)
        {
            int index = FindSingleField(fields, number, required: true, name);
            ProtoField field = fields[index];
            RequireWireType(field, 2, name);
            return Encoding.UTF8.GetString(field.Payload);
        }

        private static ulong ReadVarint(IReadOnlyList<ProtoField> fields, int number, string name)
        {
            int index = FindSingleField(fields, number, required: true, name);
            ProtoField field = fields[index];
            RequireWireType(field, 0, name);
            return DecodeVarint(field.Payload, name);
        }

        private static ulong DecodeVarint(byte[] payload, string name)
        {
            int offset = 0;
            ulong value = ReadVarint(payload, ref offset, name);
            if (offset != payload.Length) throw new InvalidDataException(name + " contains trailing bytes.");
            return value;
        }

        private static ulong ReadVarint(byte[] bytes, ref int offset, string name)
        {
            ulong value = 0;
            for (int shift = 0; shift < 70; shift += 7)
            {
                if (offset >= bytes.Length) throw new InvalidDataException(name + " is truncated.");
                byte current = bytes[offset++];
                if (shift == 63 && (current & 0xfe) != 0) throw new InvalidDataException(name + " exceeds UInt64.");
                value |= (ulong)(current & 0x7f) << shift;
                if ((current & 0x80) == 0) return value;
            }
            throw new InvalidDataException(name + " exceeds the protobuf varint limit.");
        }

        private static byte[] EncodeVarint(ulong value)
        {
            var bytes = new byte[10];
            int length = 0;
            do
            {
                byte current = (byte)(value & 0x7f);
                value >>= 7;
                if (value != 0) current |= 0x80;
                bytes[length++] = current;
            }
            while (value != 0);
            if (length == bytes.Length) return bytes;
            var result = new byte[length];
            Buffer.BlockCopy(bytes, 0, result, 0, length);
            return result;
        }

        private static int CheckedAdvance(int offset, int length, int total, string name)
        {
            if (length < 0 || offset < 0 || offset > total - length) throw new InvalidDataException(name + " is truncated.");
            return offset + length;
        }

        private static void RequireWireType(ProtoField field, int expected, string name)
        {
            if (field.WireType != expected) throw new InvalidDataException(name + " uses an unexpected protobuf wire type.");
        }

        private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);

        private sealed class ProtoField
        {
            internal ProtoField(int number, int wireType, byte[] payload)
            {
                Number = number;
                WireType = wireType;
                Payload = payload;
            }

            internal int Number { get; }
            internal int WireType { get; }
            internal byte[] Payload { get; }
        }
    }
}
