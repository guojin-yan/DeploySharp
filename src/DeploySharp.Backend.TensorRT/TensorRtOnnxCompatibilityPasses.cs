using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Onnx;

namespace JYPPX.DeploySharp.Backends.TensorRT
{
    /// <summary>Small, semantics-preserving ONNX fixes required by TensorRT's importer. / TensorRT 导入器所需的安全 ONNX 修复。</summary>
    internal static class TensorRtOnnxCompatibilityPasses
    {
        /// <summary>Repairs PaddleDetection's Gather-to-Squeeze export when axes was omitted by an opset-11 exporter. / 修复 opset-11 导出器遗漏 axes 时 PaddleDetection 的 Gather-to-Squeeze 路径。</summary>
        public static byte[] Normalize(byte[] source)
        {
            if (source == null || source.Length == 0) return source ?? Array.Empty<byte>();

            ModelProto model;
            try { model = ModelProto.Parser.ParseFrom(source); }
            catch (InvalidProtocolBufferException) { return source; }

            Dictionary<string, NodeProto> producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            foreach (NodeProto node in model.Graph.Node)
            {
                foreach (string output in node.Output)
                {
                    if (!string.IsNullOrWhiteSpace(output)) producers[output] = node;
                }
            }

            bool changed = false;
            foreach (NodeProto squeeze in model.Graph.Node.Where(node => string.Equals(node.OpType, "Squeeze", StringComparison.Ordinal)))
            {
                if (squeeze.Input.Count != 1 || HasAxesAttribute(squeeze)) continue;
                if (!producers.TryGetValue(squeeze.Input[0], out NodeProto? gather) ||
                    !string.Equals(gather.OpType, "Gather", StringComparison.Ordinal) ||
                    !HasIntegerAttribute(gather, "axis", 1) ||
                    gather.Input.Count < 2 ||
                    !producers.TryGetValue(gather.Input[1], out NodeProto? indexConstant) ||
                    !string.Equals(indexConstant.OpType, "Constant", StringComparison.Ordinal) ||
                    !HasSingleElementTensor(indexConstant)) continue;

                // Gather(axis=1) followed by Squeeze is the PaddleDetection NMS shape path:
                // [N, 1] -> [N]. Explicitly naming axis 1 avoids TensorRT's dynamic-shape ambiguity.
                var axes = new AttributeProto
                {
                    Name = "axes",
                    Type = AttributeProto.Types.AttributeType.Ints
                };
                axes.Ints.Add(1);
                squeeze.Attribute.Add(axes);
                changed = true;
            }

            return changed ? model.ToByteArray() : source;
        }

        private static bool HasAxesAttribute(NodeProto node)
        {
            return node.Attribute.Any(attribute => string.Equals(attribute.Name, "axes", StringComparison.Ordinal));
        }

        private static bool HasIntegerAttribute(NodeProto node, string name, long expected)
        {
            AttributeProto? attribute = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal));
            return attribute != null && attribute.Type == AttributeProto.Types.AttributeType.Int && attribute.I == expected;
        }

        private static bool HasSingleElementTensor(NodeProto constant)
        {
            AttributeProto? value = constant.Attribute.FirstOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal));
            if (value == null || value.Type != AttributeProto.Types.AttributeType.Tensor || value.T == null) return false;
            TensorProto tensor = value.T;
            if (tensor.Dims.Count > 1 || (tensor.Dims.Count == 1 && tensor.Dims[0] != 1)) return false;
            return tensor.Int64Data.Count == 1 || tensor.RawData.Length == sizeof(long);
        }
    }
}
