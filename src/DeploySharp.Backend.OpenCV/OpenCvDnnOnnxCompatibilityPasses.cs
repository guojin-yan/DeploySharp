using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Onnx;

namespace JYPPX.DeploySharp.Backends.OpenCV
{
    /// <summary>Normalizes constant-input forms of common ONNX operators for the OpenCV DNN importer. / 将常量输入形式的常见 ONNX 算子规范化为 OpenCV DNN 导入器兼容的属性形式。</summary>
    internal static class OpenCvDnnOnnxCompatibilityPasses
    {
        internal static bool IsNativeImporterHazard(byte[] source)
        {
            if (source == null || source.Length == 0) return false;
            ModelProto model;
            try { model = ModelProto.Parser.ParseFrom(source); }
            catch (InvalidProtocolBufferException) { return false; }

            bool hasShape = model.Graph.Node.Any(node => node.OpType == "Shape");
            if (!hasShape) return false;
            bool hasRfDetrTransformer = model.Graph.Node.Any(node =>
                node.Name.IndexOf("/transformer/", StringComparison.OrdinalIgnoreCase) >= 0
                && node.OpType == "Split");
            bool hasDynamicInput = model.Graph.Input.Any(input => input.Type?.TensorType?.Shape?.Dim.Any(dimension => dimension.DimValue <= 0 && !string.IsNullOrEmpty(dimension.DimParam)) == true);
            bool hasDeimDecoderShape = model.Graph.Node.Any(node =>
                node.Name.IndexOf("/model/decoder/", StringComparison.OrdinalIgnoreCase) >= 0
                && (node.OpType == "Shape" || node.OpType == "Equal"));
            return hasRfDetrTransformer || (hasDynamicInput && hasDeimDecoderShape);
        }

        internal static byte[] Normalize(byte[] source, out bool changed)
        {
            changed = false;
            if (source == null || source.Length == 0) return source ?? Array.Empty<byte>();
            ModelProto model;
            try { model = ModelProto.Parser.ParseFrom(source); }
            catch (InvalidProtocolBufferException) { return source; }

            changed |= FoldConstantIntegerSplits(model.Graph);

            var constants = new Dictionary<string, long[]>(StringComparer.Ordinal);
            var floatConstants = new Dictionary<string, float[]>(StringComparer.Ordinal);
            foreach (TensorProto initializer in model.Graph.Initializer)
            {
                long[]? values = ReadIntegerValues(initializer);
                if (values != null) constants[initializer.Name] = values;
                float[]? floatValues = ReadFloatValues(initializer);
                if (floatValues != null) floatConstants[initializer.Name] = floatValues;
            }
            foreach (NodeProto node in model.Graph.Node)
            {
                if (!string.Equals(node.OpType, "Constant", StringComparison.Ordinal) || node.Output.Count == 0) continue;
                AttributeProto? value = node.Attribute.FirstOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal));
                long[]? values = value?.Type == AttributeProto.Types.AttributeType.Tensor && value.T != null ? ReadIntegerValues(value.T) : null;
                if (values != null) constants[node.Output[0]] = values;
                float[]? floatValues = value?.Type == AttributeProto.Types.AttributeType.Tensor && value.T != null ? ReadFloatValues(value.T) : null;
                if (floatValues != null) floatConstants[node.Output[0]] = floatValues;
            }

            changed |= NormalizeIdentityMultiplications(model.Graph, floatConstants);
            changed |= NormalizeScalarConstantExpands(model.Graph);
            changed |= NormalizeShapeFloatCasts(model.Graph);
            changed |= NormalizeShapeArithmetic(model.Graph);
            changed |= NormalizeShapeCasts(model.Graph);
            changed |= NormalizeShapeConcatInputs(model.Graph);
            changed |= NormalizeMixedIntegerShapeInputs(model.Graph);
            changed |= NormalizeReshapedShapeComparisons(model.Graph);

            for (int nodeIndex = 0; nodeIndex < model.Graph.Node.Count; nodeIndex++)
            {
                NodeProto node = model.Graph.Node[nodeIndex];
                changed |= NormalizePoolDefaults(node);
                changed |= NormalizeGridSampleDefaults(node);
                changed |= NormalizeConcatAxis(node);
                changed |= NormalizeGatherAxis(node);
                changed |= NormalizeSplitAxis(node);
                changed |= NormalizeModDefaults(node);
                changed |= NormalizeReshapeDefaults(node);
                switch (node.OpType)
                {
                    case "Unsqueeze":
                    case "Squeeze":
                    case "ReduceMean":
                    case "ReduceSum":
                    case "ReduceMax":
                    case "ReduceMin":
                    case "ReduceProd":
                    case "ReduceL1":
                    case "ReduceL2":
                    case "ReduceLogSum":
                    case "ReduceLogSumExp":
                    case "ReduceSumSquare":
                        changed |= NormalizeSingleIntegerInput(node, constants);
                        changed |= NormalizeReduceDefaults(node, model.Graph.Node, nodeIndex);
                        break;
                    case "Split":
                        changed |= NormalizeSingleIntegerInput(node, constants);
                        changed |= NormalizeSingleOutputSplit(node);
                        break;
                    case "TopK":
                        changed |= NormalizeTopKInput(node, constants);
                        break;
                    case "Slice":
                        changed |= NormalizeSliceInputs(node, constants);
                        break;
                }
                if (node.OpType == "Unsqueeze") changed |= NormalizeUnsqueezeChain(node, model.Graph.Node, nodeIndex);
            }

            // Dynamic detector exports sometimes encode the reshape sentinel as
            // Shape -> Slice(0:0) -> Concat([-1]). OpenCV rejects the empty Slice
            // bounds even though its ONNX result is an empty shape vector. Remove
            // only this shape-only, single-consumer form; data slices remain intact.
            changed |= NormalizeEmptyShapeSlices(model.Graph);
            changed |= NormalizeFloatingGridShapeFills(model.Graph);

            return changed ? model.ToByteArray() : source;
        }

        private static bool HasAttribute(NodeProto node, string name)
            => node.Attribute.Any(attribute => string.Equals(attribute.Name, name, StringComparison.Ordinal));

        private static bool NormalizeScalarConstantExpands(GraphProto graph)
        {
            var constants = new Dictionary<string, TensorProto>(StringComparer.Ordinal);
            foreach (TensorProto initializer in graph.Initializer)
            {
                if (!string.IsNullOrEmpty(initializer.Name)) constants[initializer.Name] = initializer;
            }
            foreach (NodeProto node in graph.Node)
            {
                if (!string.Equals(node.OpType, "Constant", StringComparison.Ordinal) || node.Output.Count != 1) continue;
                AttributeProto? value = node.Attribute.FirstOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
                if (value?.T != null) constants[node.Output[0]] = value.T;
            }

            bool changed = false;
            foreach (NodeProto node in graph.Node)
            {
                if (!string.Equals(node.OpType, "Expand", StringComparison.Ordinal) || node.Input.Count != 2 || node.Output.Count != 1) continue;
                if (!constants.TryGetValue(node.Input[0], out TensorProto? source) || source.DataType != (int)TensorProto.Types.DataType.Float) continue;
                float[]? values = ReadFloatValues(source);
                if (values == null || values.Length != 1) continue;

                TensorProto fill = source.Clone();
                fill.Name = string.Empty;
                fill.Dims.Clear();
                fill.Dims.Add(1);
                string shapeInput = node.Input[1];
                node.OpType = "ConstantOfShape";
                node.Input.Clear();
                node.Input.Add(shapeInput);
                node.Attribute.Clear();
                node.Attribute.Add(new AttributeProto { Name = "value", Type = AttributeProto.Types.AttributeType.Tensor, T = fill });
                changed = true;
            }
            return changed;
        }

        private static bool FoldConstantIntegerSplits(GraphProto graph)
        {
            var known = new Dictionary<string, TensorProto>(StringComparer.Ordinal);
            foreach (TensorProto initializer in graph.Initializer)
            {
                if (!string.IsNullOrEmpty(initializer.Name) && ReadIntegerValues(initializer) != null) known[initializer.Name] = initializer;
            }

            bool changed = false;
            for (int nodeIndex = 0; nodeIndex < graph.Node.Count; nodeIndex++)
            {
                NodeProto node = graph.Node[nodeIndex];
                if (TryEvaluateIntegerShapeNode(node, known, out TensorProto[] outputs))
                {
                    for (int outputIndex = 0; outputIndex < outputs.Length; outputIndex++) known[node.Output[outputIndex]] = outputs[outputIndex];
                }
                if (!string.Equals(node.OpType, "Split", StringComparison.Ordinal) || outputs.Length != node.Output.Count || outputs.Length == 0) continue;

                string[] outputNames = node.Output.ToArray();
                string baseName = node.Name;
                for (int outputIndex = 0; outputIndex < outputs.Length; outputIndex++)
                {
                    NodeProto constant = outputIndex == 0 ? node : new NodeProto();
                    constant.OpType = "Constant";
                    constant.Name = string.IsNullOrEmpty(baseName) ? string.Empty : baseName + "__deploysharp_fold" + outputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    constant.Input.Clear();
                    constant.Output.Clear();
                    constant.Output.Add(outputNames[outputIndex]);
                    constant.Attribute.Clear();
                    TensorProto value = outputs[outputIndex].Clone();
                    value.Name = string.Empty;
                    constant.Attribute.Add(new AttributeProto { Name = "value", Type = AttributeProto.Types.AttributeType.Tensor, T = value });
                    if (outputIndex > 0) graph.Node.Insert(nodeIndex + outputIndex, constant);
                }
                nodeIndex += outputs.Length - 1;
                changed = true;
            }
            return changed;
        }

        private static bool TryEvaluateIntegerShapeNode(NodeProto node, IReadOnlyDictionary<string, TensorProto> known, out TensorProto[] outputs)
        {
            outputs = Array.Empty<TensorProto>();
            if (node.Output.Count == 0) return false;
            if (string.Equals(node.OpType, "Constant", StringComparison.Ordinal))
            {
                AttributeProto? value = node.Attribute.FirstOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
                if (value?.T == null || ReadIntegerValues(value.T) == null || node.Output.Count != 1) return false;
                TensorProto constant = value.T.Clone();
                constant.Name = node.Output[0];
                outputs = new[] { constant };
                return true;
            }
            if (node.Input.Count == 0 || !known.TryGetValue(node.Input[0], out TensorProto? input) || ReadIntegerValues(input) == null) return false;
            if (string.Equals(node.OpType, "Identity", StringComparison.Ordinal) && node.Output.Count == 1)
            {
                TensorProto identity = input.Clone();
                identity.Name = node.Output[0];
                outputs = new[] { identity };
                return true;
            }
            if ((string.Equals(node.OpType, "Squeeze", StringComparison.Ordinal) || string.Equals(node.OpType, "Unsqueeze", StringComparison.Ordinal)) && node.Output.Count == 1)
            {
                long[]? axes = ResolveIntegerParameter(node, "axes", 1, known);
                if (axes == null || !TryReshapeIntegerTensor(input, node.Output[0], axes, string.Equals(node.OpType, "Unsqueeze", StringComparison.Ordinal), out TensorProto? reshaped)) return false;
                outputs = new[] { reshaped! };
                return true;
            }
            if (!string.Equals(node.OpType, "Split", StringComparison.Ordinal)) return false;
            long[]? split = ResolveIntegerParameter(node, "split", 1, known);
            if (!TrySplitIntegerTensor(input, node.Output.ToArray(), split, ReadIntegerAxis(node), out outputs))
            {
                outputs = Array.Empty<TensorProto>();
                return false;
            }
            return true;
        }

        private static long[]? ResolveIntegerParameter(NodeProto node, string attributeName, int inputIndex, IReadOnlyDictionary<string, TensorProto> known)
        {
            if (node.Input.Count > inputIndex && known.TryGetValue(node.Input[inputIndex], out TensorProto? tensor)) return ReadIntegerValues(tensor);
            AttributeProto? attribute = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, attributeName, StringComparison.Ordinal));
            if (attribute?.Type == AttributeProto.Types.AttributeType.Ints) return attribute.Ints.ToArray();
            if (attribute?.Type == AttributeProto.Types.AttributeType.Int) return new[] { attribute.I };
            return null;
        }

        private static long ReadIntegerAxis(NodeProto node)
        {
            AttributeProto? axis = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, "axis", StringComparison.Ordinal));
            if (axis?.Type == AttributeProto.Types.AttributeType.Ints && axis.Ints.Count == 1) return axis.Ints[0];
            return axis?.Type == AttributeProto.Types.AttributeType.Int ? axis.I : 0L;
        }

        private static bool TryReshapeIntegerTensor(TensorProto source, string outputName, IReadOnlyList<long> axes, bool unsqueeze, out TensorProto? result)
        {
            result = null;
            var dimensions = source.Dims.ToList();
            if (unsqueeze)
            {
                int outputRank = dimensions.Count + axes.Count;
                var normalized = new List<int>(axes.Count);
                foreach (long axisValue in axes)
                {
                    long axis = axisValue < 0 ? axisValue + outputRank : axisValue;
                    if (axis < 0 || axis >= outputRank || normalized.Contains((int)axis)) return false;
                    normalized.Add((int)axis);
                }
                normalized.Sort();
                foreach (int axis in normalized) dimensions.Insert(axis, 1);
            }
            else
            {
                IEnumerable<long> selected = axes.Count == 0 ? Enumerable.Range(0, dimensions.Count).Where(index => dimensions[index] == 1).Select(index => (long)index) : axes;
                var normalized = new List<int>();
                foreach (long axisValue in selected)
                {
                    long axis = axisValue < 0 ? axisValue + dimensions.Count : axisValue;
                    if (axis < 0 || axis >= dimensions.Count || dimensions[(int)axis] != 1 || normalized.Contains((int)axis)) return false;
                    normalized.Add((int)axis);
                }
                normalized.Sort();
                normalized.Reverse();
                foreach (int axis in normalized) dimensions.RemoveAt(axis);
            }
            result = CloneIntegerTensorWithValues(source, outputName, dimensions, ReadIntegerValues(source)!);
            return true;
        }

        private static bool TrySplitIntegerTensor(TensorProto source, IReadOnlyList<string> outputNames, long[]? requestedSizes, long axisValue, out TensorProto[] outputs)
        {
            outputs = Array.Empty<TensorProto>();
            long[]? values = ReadIntegerValues(source);
            if (values == null || outputNames.Count == 0 || source.Dims.Count == 0 || source.Dims.Any(dimension => dimension <= 0 || dimension > int.MaxValue)) return false;
            int rank = source.Dims.Count;
            long normalizedAxis = axisValue < 0 ? axisValue + rank : axisValue;
            if (normalizedAxis < 0 || normalizedAxis >= rank) return false;
            int axis = (int)normalizedAxis;
            int axisLength = checked((int)source.Dims[axis]);
            long[] sizes = requestedSizes ?? (axisLength % outputNames.Count == 0 ? Enumerable.Repeat((long)(axisLength / outputNames.Count), outputNames.Count).ToArray() : Array.Empty<long>());
            if (sizes.Length != outputNames.Count || sizes.Any(size => size <= 0 || size > int.MaxValue) || sizes.Sum() != axisLength) return false;
            int inner = 1;
            for (int index = axis + 1; index < rank; index++) inner = checked(inner * (int)source.Dims[index]);
            int outer = values.Length / checked(axisLength * inner);
            var result = new TensorProto[outputNames.Count];
            int axisOffset = 0;
            for (int outputIndex = 0; outputIndex < outputNames.Count; outputIndex++)
            {
                int size = checked((int)sizes[outputIndex]);
                var outputValues = new long[checked(outer * size * inner)];
                for (int outerIndex = 0; outerIndex < outer; outerIndex++)
                {
                    int sourceOffset = checked((outerIndex * axisLength + axisOffset) * inner);
                    int destinationOffset = checked(outerIndex * size * inner);
                    Array.Copy(values, sourceOffset, outputValues, destinationOffset, checked(size * inner));
                }
                var dimensions = source.Dims.ToList();
                dimensions[axis] = size;
                result[outputIndex] = CloneIntegerTensorWithValues(source, outputNames[outputIndex], dimensions, outputValues);
                axisOffset += size;
            }
            outputs = result;
            return true;
        }

        private static TensorProto CloneIntegerTensorWithValues(TensorProto source, string name, IEnumerable<long> dimensions, IReadOnlyList<long> values)
        {
            var result = new TensorProto { Name = name, DataType = source.DataType };
            result.Dims.Add(dimensions);
            if (source.DataType == (int)TensorProto.Types.DataType.Int64) result.Int64Data.Add(values);
            else result.Int32Data.Add(values.Select(value => checked((int)value)));
            return result;
        }

        private static bool NormalizeShapeCasts(GraphProto graph)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node)
            {
                foreach (string output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output)) producers[output] = node;
                }
            }
            var memo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            foreach (NodeProto node in graph.Node)
            {
                if (!string.Equals(node.OpType, "Cast", StringComparison.Ordinal) || node.Input.Count != 1) continue;
                AttributeProto? target = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, "to", StringComparison.Ordinal) && value.Type == AttributeProto.Types.AttributeType.Int);
                if (target == null || target.I != (int)TensorProto.Types.DataType.Int64 || !IsShapeValue(node.Input[0], graph, producers, memo, new HashSet<string>(StringComparer.Ordinal))) continue;
                target.I = (int)TensorProto.Types.DataType.Int32;
                changed = true;
            }
            return changed;
        }

        private static bool NormalizeShapeConcatInputs(GraphProto graph)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            var consumers = new Dictionary<string, List<NodeProto>>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node)
            {
                foreach (string output in node.Output) if (!string.IsNullOrEmpty(output)) producers[output] = node;
                foreach (string input in node.Input)
                {
                    if (string.IsNullOrEmpty(input)) continue;
                    if (!consumers.TryGetValue(input, out List<NodeProto>? users)) consumers[input] = users = new List<NodeProto>();
                    users.Add(node);
                }
            }

            var shapeMemo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            for (int nodeIndex = 0; nodeIndex < graph.Node.Count; nodeIndex++)
            {
                NodeProto concat = graph.Node[nodeIndex];
                if (!string.Equals(concat.OpType, "Concat", StringComparison.Ordinal) || concat.Input.Count == 0 || concat.Output.Count != 1) continue;
                if (!concat.Input.All(input => IsShapeValue(input, graph, producers, shapeMemo, new HashSet<string>(StringComparer.Ordinal)))) continue;
                if (!consumers.TryGetValue(concat.Output[0], out List<NodeProto>? users) || users.Count == 0 || users.Any(user => !IsShapeConsumer(user))) continue;

                int inserted = 0;
                for (int inputIndex = 0; inputIndex < concat.Input.Count; inputIndex++)
                {
                    string originalInput = concat.Input[inputIndex];
                    string castOutput = (string.IsNullOrEmpty(concat.Name) ? concat.Output[0] : concat.Name) + "__deploysharp_i32_" + inputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var cast = new NodeProto { OpType = "Cast", Name = castOutput };
                    cast.Input.Add(originalInput);
                    cast.Output.Add(castOutput);
                    cast.Attribute.Add(new AttributeProto { Name = "to", Type = AttributeProto.Types.AttributeType.Int, I = (int)TensorProto.Types.DataType.Int32 });
                    graph.Node.Insert(nodeIndex + inserted, cast);
                    concat.Input[inputIndex] = castOutput;
                    inserted++;
                }
                nodeIndex += inserted;
                changed = true;
            }
            return changed;
        }

        private static bool IsShapeConsumer(NodeProto node)
            => string.Equals(node.OpType, "Reshape", StringComparison.Ordinal)
                || string.Equals(node.OpType, "Expand", StringComparison.Ordinal)
                || string.Equals(node.OpType, "Tile", StringComparison.Ordinal)
                || string.Equals(node.OpType, "ConstantOfShape", StringComparison.Ordinal)
                || string.Equals(node.OpType, "Range", StringComparison.Ordinal);

        private static bool NormalizeMixedIntegerShapeInputs(GraphProto graph)
        {
            var producers = BuildProducers(graph);
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node)
            {
                usedNames.UnionWith(node.Input);
                usedNames.UnionWith(node.Output);
            }
            foreach (TensorProto initializer in graph.Initializer) usedNames.Add(initializer.Name);

            var shapeMemo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            for (int nodeIndex = 0; nodeIndex < graph.Node.Count; nodeIndex++)
            {
                NodeProto node = graph.Node[nodeIndex];
                if (!IsMixedIntegerShapeOperator(node) || node.Input.Count < 2 || node.Output.Count == 0) continue;
                int firstValueInput = node.OpType == "Where" ? 1 : 0;
                string[] inputs = node.Input.Skip(firstValueInput).Where(input => !string.IsNullOrEmpty(input)).ToArray();
                if (inputs.Length != node.Input.Count - firstValueInput || !inputs.All(input => IsIntegerShapeValue(input, graph, producers, new Dictionary<string, bool>(shapeMemo), new HashSet<string>(StringComparer.Ordinal)))) continue;

                int inserted = 0;
                for (int inputIndex = firstValueInput; inputIndex < node.Input.Count; inputIndex++)
                {
                    string input = node.Input[inputIndex];
                    string baseName = string.IsNullOrEmpty(node.Name) ? node.Output[0] : node.Name;
                    string castOutput = baseName + "__deploysharp_i32_input_" + inputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    int suffix = 0;
                    while (!usedNames.Add(castOutput)) castOutput = baseName + "__deploysharp_i32_input_" + inputIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var cast = new NodeProto { OpType = "Cast", Name = castOutput };
                    cast.Input.Add(input);
                    cast.Output.Add(castOutput);
                    // OpenCV represents Shape/Gather values as CV_64S and its
                    // Cast(Int32) importer may preserve that depth. Keep shape
                    // arithmetic/comparison inputs at Int64 so every operand
                    // reaches the native n-ary layer with one type.
                    cast.Attribute.Add(new AttributeProto { Name = "to", Type = AttributeProto.Types.AttributeType.Int, I = (int)TensorProto.Types.DataType.Int64 });
                    graph.Node.Insert(nodeIndex + inserted, cast);
                    node.Input[inputIndex] = castOutput;
                    producers[castOutput] = cast;
                    inserted++;
                }
                if (inserted > 0)
                {
                    nodeIndex += inserted;
                    changed = true;
                }
            }
            return changed;
        }

        private static bool IsMixedIntegerShapeOperator(NodeProto node)
            => node.OpType == "Equal" || node.OpType == "Less" || node.OpType == "Greater" || node.OpType == "LessOrEqual" || node.OpType == "GreaterOrEqual"
                || node.OpType == "Add" || node.OpType == "Sub" || node.OpType == "Mul" || node.OpType == "Div" || node.OpType == "Max" || node.OpType == "Min" || node.OpType == "Where";

        private static Dictionary<string, NodeProto> BuildProducers(GraphProto graph)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node)
            {
                foreach (string output in node.Output)
                {
                    if (!string.IsNullOrEmpty(output)) producers[output] = node;
                }
            }
            return producers;
        }

        private static bool IsShapeValue(string value, GraphProto graph, IReadOnlyDictionary<string, NodeProto> producers, IDictionary<string, bool> memo, ISet<string> visiting)
        {
            if (memo.TryGetValue(value, out bool known)) return known;
            if (!visiting.Add(value)) return false;
            TensorProto? initializer = graph.Initializer.FirstOrDefault(tensor => string.Equals(tensor.Name, value, StringComparison.Ordinal));
            if (initializer != null)
            {
                bool integer = IsIntegerDataType(initializer.DataType);
                memo[value] = integer;
                return integer;
            }
            if (!producers.TryGetValue(value, out NodeProto? producer))
            {
                memo[value] = false;
                return false;
            }
            bool result = producer.OpType switch
            {
                "Shape" => true,
                // Constant nodes may carry float values in shape-construction
                // subgraphs (for example scale factors later cast to INT64).
                // Keep the broad semantic shape classification here; the
                // arithmetic type rewrite below uses IsIntegerShapeValue and
                // therefore remains strict about integer-only paths.
                "Constant" => true,
                "Cast" or "Gather" or "Concat" or "Unsqueeze" or "Squeeze" or "Reshape" or "Slice" or "Expand" or "Range" or "Add" or "Sub" or "Mul" or "Div" or "Max" or "Min"
                    => producer.Input.Count > 0 && producer.Input.Where(input => !string.IsNullOrEmpty(input)).All(input => IsShapeValue(input, graph, producers, memo, visiting)),
                "Where" => producer.Input.Count == 3 && IsShapeValue(producer.Input[1], graph, producers, memo, visiting) && IsShapeValue(producer.Input[2], graph, producers, memo, visiting),
                _ => false
            };
            visiting.Remove(value);
            memo[value] = result;
            return result;
        }

        private static bool NormalizeEmptyShapeSlices(GraphProto graph)
        {
            bool changed = false;
            for (int nodeIndex = 0; nodeIndex < graph.Node.Count; nodeIndex++)
            {
                NodeProto slice = graph.Node[nodeIndex];
                if (!string.Equals(slice.OpType, "Slice", StringComparison.Ordinal) || slice.Input.Count != 1 || slice.Output.Count != 1) continue;
                if (!TryReadIntAttribute(slice, "starts", out long[] starts) || !TryReadIntAttribute(slice, "ends", out long[] ends) || !TryReadIntAttribute(slice, "axes", out long[] axes) || starts.Length != ends.Length || starts.Length != axes.Length || starts.Length == 0) continue;
                if (!starts.Zip(ends, (start, end) => start == end).All(value => value)) continue;

                var producers = BuildProducers(graph);
                if (!IsShapeValue(slice.Input[0], graph, producers, new Dictionary<string, bool>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal))) continue;

                var removable = new List<NodeProto> { slice };
                string currentOutput = slice.Output[0];
                NodeProto? consumer = null;
                int consumerInput = -1;
                while (FindSingleConsumer(graph, currentOutput, out NodeProto? candidate, out int inputIndex))
                {
                    if (string.Equals(candidate!.OpType, "Cast", StringComparison.Ordinal) && candidate.Input.Count == 1 && candidate.Output.Count == 1)
                    {
                        removable.Add(candidate);
                        currentOutput = candidate.Output[0];
                        continue;
                    }
                    if (string.Equals(candidate.OpType, "Concat", StringComparison.Ordinal))
                    {
                        consumer = candidate;
                        consumerInput = inputIndex;
                    }
                    break;
                }

                if (consumer == null || consumerInput < 0 || consumer.Input.Count <= 1) continue;
                consumer.Input.RemoveAt(consumerInput);
                if (consumer.Input.Count == 1)
                {
                    string remaining = consumer.Input[0];
                    consumer.OpType = "Identity";
                    consumer.Input.Clear();
                    consumer.Input.Add(remaining);
                    consumer.Attribute.Clear();
                }
                for (int removeIndex = graph.Node.Count - 1; removeIndex >= 0; removeIndex--)
                {
                    if (removable.Contains(graph.Node[removeIndex])) graph.Node.RemoveAt(removeIndex);
                }
                nodeIndex = Math.Max(-1, nodeIndex - 1);
                changed = true;
                continue;
            }
            return changed;
        }

        private static bool FindSingleConsumer(GraphProto graph, string output, out NodeProto? consumer, out int inputIndex)
        {
            consumer = null;
            inputIndex = -1;
            foreach (NodeProto candidate in graph.Node)
            {
                for (int index = 0; index < candidate.Input.Count; index++)
                {
                    if (!string.Equals(candidate.Input[index], output, StringComparison.Ordinal)) continue;
                    if (consumer != null) return false;
                    consumer = candidate;
                    inputIndex = index;
                }
            }
            return consumer != null;
        }

        private static bool TryReadIntAttribute(NodeProto node, string name, out long[] values)
        {
            AttributeProto? attribute = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal) && value.Type == AttributeProto.Types.AttributeType.Ints);
            if (attribute == null)
            {
                values = Array.Empty<long>();
                return false;
            }
            values = attribute.Ints.ToArray();
            return values.Length > 0;
        }

        private static bool NormalizeFloatingGridShapeFills(GraphProto graph)
        {
            bool changed = false;
            for (int nodeIndex = 0; nodeIndex < graph.Node.Count; nodeIndex++)
            {
                NodeProto shape = graph.Node[nodeIndex];
                if (!string.Equals(shape.OpType, "Shape", StringComparison.Ordinal) || shape.Input.Count != 1 || shape.Output.Count != 1) continue;
                var producers = BuildProducers(graph);
                if (!IsFloatingGrid(producers, shape.Input[0])) continue;
                if (!FindSingleConsumer(graph, shape.Output[0], out NodeProto? fill, out int fillInput) || fillInput != 0 || fill!.Input.Count != 1 || fill.Output.Count != 1 || !IsUnitFloatConstantOfShape(fill)) continue;
                if (!FindSingleConsumer(graph, fill.Output[0], out NodeProto? scale, out _)
                    || !string.Equals(scale!.OpType, "Mul", StringComparison.Ordinal) || scale.Input.Count != 2) continue;

                string zeroName = shape.Output[0] + "__deploysharp_zero";
                string oneName = fill.Output[0] + "__deploysharp_one";
                if (graph.Initializer.Any(value => string.Equals(value.Name, zeroName, StringComparison.Ordinal) || string.Equals(value.Name, oneName, StringComparison.Ordinal))) continue;
                var zero = new TensorProto { Name = zeroName, DataType = (int)TensorProto.Types.DataType.Float };
                zero.FloatData.Add(0f);
                var one = new TensorProto { Name = oneName, DataType = (int)TensorProto.Types.DataType.Float };
                one.FloatData.Add(1f);
                graph.Initializer.Add(zero);
                graph.Initializer.Add(one);

                shape.OpType = "Mul";
                shape.Input.Add(zeroName);
                shape.Attribute.Clear();
                fill.OpType = "Add";
                fill.Input.Clear();
                fill.Input.Add(shape.Output[0]);
                fill.Input.Add(oneName);
                fill.Attribute.Clear();
                nodeIndex = Math.Max(-1, nodeIndex - 1);
                changed = true;
            }
            return changed;
        }

        private static bool IsFloatingGrid(IReadOnlyDictionary<string, NodeProto> producers, string value)
        {
            if (!producers.TryGetValue(value, out NodeProto? div) || !string.Equals(div.OpType, "Div", StringComparison.Ordinal) || div.Input.Count != 2) return false;
            if (!producers.TryGetValue(div.Input[0], out NodeProto? numerator) || !string.Equals(numerator.OpType, "Add", StringComparison.Ordinal) || numerator.Input.Count != 2) return false;
            if (!producers.TryGetValue(div.Input[1], out NodeProto? denominator) || !IsCastToFloat(denominator)) return false;
            return numerator.Input.Any(input => producers.TryGetValue(input, out NodeProto? producer) && IsCastToFloat(producer));
        }

        private static bool IsCastToFloat(NodeProto node)
            => string.Equals(node.OpType, "Cast", StringComparison.Ordinal)
                && node.Attribute.Any(attribute => string.Equals(attribute.Name, "to", StringComparison.Ordinal)
                    && attribute.Type == AttributeProto.Types.AttributeType.Int
                    && attribute.I == (int)TensorProto.Types.DataType.Float);

        private static bool IsUnitFloatConstantOfShape(NodeProto node)
        {
            if (!string.Equals(node.OpType, "ConstantOfShape", StringComparison.Ordinal)) return false;
            AttributeProto? value = node.Attribute.SingleOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
            if (value?.T == null || value.T.DataType != (int)TensorProto.Types.DataType.Float) return false;
            if (value.T.Dims.Count != 0 && (value.T.Dims.Count != 1 || value.T.Dims[0] != 1)) return false;
            if (value.T.FloatData.Count == 1) return value.T.FloatData[0] == 1f;
            return value.T.RawData.Length == sizeof(float) && BitConverter.ToSingle(value.T.RawData.ToByteArray(), 0) == 1f;
        }

        private static bool NormalizeReshapedShapeComparisons(GraphProto graph)
        {
            var producers = BuildProducers(graph);
            bool changed = false;
            foreach (NodeProto comparison in graph.Node)
            {
                if (!string.Equals(comparison.OpType, "Equal", StringComparison.Ordinal) || comparison.Input.Count != 2) continue;
                if (!TryGetInsertedIntegerCast(producers, comparison.Input[0], out NodeProto? firstCast, out NodeProto? firstSource)
                    || !TryGetInsertedIntegerCast(producers, comparison.Input[1], out NodeProto? secondCast, out NodeProto? secondSource)) continue;

                NodeProto? reshapeCast;
                NodeProto? shapeFillCast;
                TensorProto? shapeFill;
                TensorProto? shapeMultiplier;
                if (string.Equals(firstSource!.OpType, "Reshape", StringComparison.Ordinal) && TryGetIntegerShapeFill(producers, secondSource!, out shapeFill, out shapeMultiplier))
                {
                    reshapeCast = firstCast;
                    shapeFillCast = secondCast;
                }
                else if (string.Equals(secondSource!.OpType, "Reshape", StringComparison.Ordinal) && TryGetIntegerShapeFill(producers, firstSource, out shapeFill, out shapeMultiplier))
                {
                    reshapeCast = secondCast;
                    shapeFillCast = firstCast;
                }
                else continue;

                bool localChanged = ConvertIntegerTensorToInt32(shapeFill!) | ConvertIntegerTensorToInt32(shapeMultiplier!);
                localChanged |= SetCastTarget(reshapeCast!, TensorProto.Types.DataType.Int32);
                localChanged |= SetCastTarget(shapeFillCast!, TensorProto.Types.DataType.Int32);
                changed |= localChanged;
            }
            return changed;
        }

        private static bool TryGetInsertedIntegerCast(IReadOnlyDictionary<string, NodeProto> producers, string input, out NodeProto? cast, out NodeProto? source)
        {
            source = null;
            if (!producers.TryGetValue(input, out cast) || !string.Equals(cast.OpType, "Cast", StringComparison.Ordinal) || cast.Input.Count != 1) return false;
            AttributeProto? target = cast.Attribute.SingleOrDefault(attribute => string.Equals(attribute.Name, "to", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Int);
            if (target == null || !IsIntegerDataType((int)target.I)) return false;
            return producers.TryGetValue(cast.Input[0], out source);
        }

        private static bool TryGetIntegerShapeFill(IReadOnlyDictionary<string, NodeProto> producers, NodeProto source, out TensorProto? fill, out TensorProto? multiplier)
        {
            fill = null;
            multiplier = null;
            if (!string.Equals(source.OpType, "Mul", StringComparison.Ordinal) || source.Input.Count != 2) return false;
            NodeProto? constantOfShape = null;
            TensorProto? candidateMultiplier = null;
            foreach (string input in source.Input)
            {
                if (!producers.TryGetValue(input, out NodeProto? producer)) continue;
                if (string.Equals(producer.OpType, "Cast", StringComparison.Ordinal) && producer.Input.Count == 1 && producers.TryGetValue(producer.Input[0], out NodeProto? unwrapped)) producer = unwrapped;
                if (string.Equals(producer.OpType, "ConstantOfShape", StringComparison.Ordinal)) constantOfShape = producer;
                else candidateMultiplier = ReadConstantTensor(producer);
            }
            if (constantOfShape == null || candidateMultiplier == null || constantOfShape.Input.Count != 1 || !producers.TryGetValue(constantOfShape.Input[0], out NodeProto? shape) || !string.Equals(shape.OpType, "Shape", StringComparison.Ordinal)) return false;
            AttributeProto? value = constantOfShape.Attribute.SingleOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
            if (value?.T == null || ReadIntegerValues(value.T) is not long[] fillValues || fillValues.Any(item => item != 1)) return false;
            if (ReadIntegerValues(candidateMultiplier) is not long[] multiplierValues || multiplierValues.Any(item => item != -1)) return false;
            fill = value.T;
            multiplier = candidateMultiplier;
            return true;
        }

        private static TensorProto? ReadConstantTensor(NodeProto node)
            => string.Equals(node.OpType, "Constant", StringComparison.Ordinal)
                ? node.Attribute.SingleOrDefault(attribute => string.Equals(attribute.Name, "value", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Tensor)?.T
                : null;

        private static bool ConvertIntegerTensorToInt32(TensorProto tensor)
        {
            long[]? values = ReadIntegerValues(tensor);
            if (values == null || !AreInt32(values)) return false;
            if (tensor.DataType == (int)TensorProto.Types.DataType.Int32) return true;
            tensor.DataType = (int)TensorProto.Types.DataType.Int32;
            tensor.Int64Data.Clear();
            tensor.RawData = ByteString.Empty;
            tensor.Int32Data.Add(values.Select(value => checked((int)value)));
            return true;
        }

        private static bool SetCastTarget(NodeProto cast, TensorProto.Types.DataType target)
        {
            AttributeProto attribute = cast.Attribute.Single(value => string.Equals(value.Name, "to", StringComparison.Ordinal) && value.Type == AttributeProto.Types.AttributeType.Int);
            if (attribute.I == (int)target) return false;
            attribute.I = (int)target;
            return true;
        }

        private static bool NormalizePoolDefaults(NodeProto node)
        {
            if (!string.Equals(node.OpType, "MaxPool", StringComparison.Ordinal) && !string.Equals(node.OpType, "AveragePool", StringComparison.Ordinal)) return false;
            bool changed = false;
            for (int index = node.Attribute.Count - 1; index >= 0; index--)
            {
                AttributeProto attribute = node.Attribute[index];
                // OpenCV 5's ONNX importer rejects these default integer attributes,
                // although both ONNX and the native layer default them to zero.
                if ((string.Equals(attribute.Name, "ceil_mode", StringComparison.Ordinal) || string.Equals(attribute.Name, "storage_order", StringComparison.Ordinal) || string.Equals(attribute.Name, "count_include_pad", StringComparison.Ordinal))
                    && attribute.Type == AttributeProto.Types.AttributeType.Int && attribute.I == 0)
                {
                    node.Attribute.RemoveAt(index);
                    changed = true;
                }
            }
            return changed;
        }

        private static bool NormalizeGridSampleDefaults(NodeProto node)
        {
            if (!string.Equals(node.OpType, "GridSample", StringComparison.Ordinal)) return false;
            for (int index = node.Attribute.Count - 1; index >= 0; index--)
            {
                AttributeProto attribute = node.Attribute[index];
                // OpenCV DNN 5.0 uses false as the GridSample default but rejects
                // an explicit integer false attribute during ONNX import.
                if (string.Equals(attribute.Name, "align_corners", StringComparison.Ordinal)
                    && attribute.Type == AttributeProto.Types.AttributeType.Int && attribute.I == 0)
                {
                    node.Attribute.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        private static bool NormalizeConcatAxis(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Concat", StringComparison.Ordinal)) return false;
            for (int index = 0; index < node.Attribute.Count; index++)
            {
                AttributeProto attribute = node.Attribute[index];
                if (!string.Equals(attribute.Name, "axis", StringComparison.Ordinal) || attribute.Type != AttributeProto.Types.AttributeType.Int) continue;
                var replacement = new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Ints };
                replacement.Ints.Add(attribute.I);
                node.Attribute[index] = replacement;
                return true;
            }
            return false;
        }

        private static bool NormalizeGatherAxis(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Gather", StringComparison.Ordinal)) return false;
            for (int index = 0; index < node.Attribute.Count; index++)
            {
                AttributeProto attribute = node.Attribute[index];
                if (!string.Equals(attribute.Name, "axis", StringComparison.Ordinal) || attribute.Type != AttributeProto.Types.AttributeType.Int) continue;
                var replacement = new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Ints };
                replacement.Ints.Add(attribute.I);
                node.Attribute[index] = replacement;
                return true;
            }
            return false;
        }

        private static bool NormalizeSplitAxis(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Split", StringComparison.Ordinal)) return false;
            for (int index = 0; index < node.Attribute.Count; index++)
            {
                AttributeProto attribute = node.Attribute[index];
                if (!string.Equals(attribute.Name, "axis", StringComparison.Ordinal) || attribute.Type != AttributeProto.Types.AttributeType.Int) continue;
                var replacement = new AttributeProto { Name = "axis", Type = AttributeProto.Types.AttributeType.Ints };
                replacement.Ints.Add(attribute.I);
                node.Attribute[index] = replacement;
                return true;
            }
            return false;
        }

        private static bool NormalizeModDefaults(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Mod", StringComparison.Ordinal)) return false;
            for (int index = node.Attribute.Count - 1; index >= 0; index--)
            {
                AttributeProto attribute = node.Attribute[index];
                // OpenCV DNN 5.0 rejects the optional integer form although
                // ONNX defines fmod=0 as the default integer remainder mode.
                if (string.Equals(attribute.Name, "fmod", StringComparison.Ordinal)
                    && attribute.Type == AttributeProto.Types.AttributeType.Int && attribute.I == 0)
                {
                    node.Attribute.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        private static bool NormalizeReshapeDefaults(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Reshape", StringComparison.Ordinal)) return false;
            for (int index = node.Attribute.Count - 1; index >= 0; index--)
            {
                AttributeProto attribute = node.Attribute[index];
                if (string.Equals(attribute.Name, "allowzero", StringComparison.Ordinal)
                    && attribute.Type == AttributeProto.Types.AttributeType.Int && attribute.I == 0)
                {
                    node.Attribute.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        private static bool NormalizeReduceDefaults(NodeProto node, Google.Protobuf.Collections.RepeatedField<NodeProto> graphNodes, int nodeIndex)
        {
            if (!node.OpType.StartsWith("Reduce", StringComparison.Ordinal)) return false;
            bool changed = false;
            for (int index = node.Attribute.Count - 1; index >= 0; index--)
            {
                AttributeProto attribute = node.Attribute[index];
                bool isKeepDims = string.Equals(attribute.Name, "keepdims", StringComparison.Ordinal) && attribute.Type == AttributeProto.Types.AttributeType.Int;
                bool remove = (isKeepDims && attribute.I == 1)
                    || (string.Equals(attribute.Name, "noop_with_empty_axes", StringComparison.Ordinal) && attribute.I == 0);
                if (remove && attribute.Type == AttributeProto.Types.AttributeType.Int)
                {
                    node.Attribute.RemoveAt(index);
                    changed = true;
                }
                else if (isKeepDims && attribute.I == 0)
                {
                    AttributeProto? axes = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, "axes", StringComparison.Ordinal) && value.Type == AttributeProto.Types.AttributeType.Ints && value.Ints.Count > 0);
                    if (axes == null || node.Output.Count != 1 || string.IsNullOrEmpty(node.Output[0])) continue;
                    string originalOutput = node.Output[0];
                    string squeezedInput = originalOutput + "__deploysharp_keepdims";
                    var occupied = new HashSet<string>(StringComparer.Ordinal);
                    foreach (NodeProto graphNode in graphNodes)
                    {
                        occupied.UnionWith(graphNode.Input);
                        occupied.UnionWith(graphNode.Output);
                    }
                    int suffix = 0;
                    string candidate = squeezedInput;
                    while (!occupied.Add(candidate)) candidate = squeezedInput + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    node.Output[0] = candidate;
                    var squeeze = new NodeProto { OpType = "Squeeze", Name = string.IsNullOrEmpty(node.Name) ? string.Empty : node.Name + "__deploysharp_keepdims" };
                    squeeze.Input.Add(candidate);
                    squeeze.Output.Add(originalOutput);
                    var squeezeAxes = new AttributeProto { Name = "axes", Type = AttributeProto.Types.AttributeType.Ints };
                    squeezeAxes.Ints.Add(axes.Ints);
                    squeeze.Attribute.Add(squeezeAxes);
                    graphNodes.Insert(nodeIndex + 1, squeeze);
                    node.Attribute.RemoveAt(index);
                    changed = true;
                }
            }
            return changed;
        }

        private static bool NormalizeSingleIntegerInput(NodeProto node, IReadOnlyDictionary<string, long[]> constants)
        {
            string attributeName = node.OpType == "Split" ? "split" : "axes";
            if (node.Input.Count != 2 || HasAttribute(node, attributeName)) return false;
            if (!constants.TryGetValue(node.Input[1], out long[]? values) || !AreInt32(values)) return false;
            var attribute = new AttributeProto { Name = attributeName, Type = AttributeProto.Types.AttributeType.Ints };
            attribute.Ints.Add(values);
            node.Attribute.Add(attribute);
            node.Input.RemoveAt(1);
            return true;
        }

        private static bool NormalizeSingleOutputSplit(NodeProto node)
        {
            if (!string.Equals(node.OpType, "Split", StringComparison.Ordinal) || node.Input.Count != 1 || node.Output.Count != 1) return false;
            // A valid one-output Split preserves every element. OpenCV's importer
            // turns Split into a Slice layer, but constant-folded data leaves that
            // layer with no runtime input. Identity preserves the ONNX semantics
            // and lets OpenCV fold or execute the node without that invalid path.
            node.OpType = "Identity";
            node.Attribute.Clear();
            return true;
        }

        private static bool NormalizeIdentityMultiplications(GraphProto graph, IReadOnlyDictionary<string, float[]> constants)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node) foreach (string output in node.Output) if (!string.IsNullOrEmpty(output)) producers[output] = node;
            var memo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            foreach (NodeProto node in graph.Node)
            {
                if (!string.Equals(node.OpType, "Mul", StringComparison.Ordinal) || node.Input.Count != 2 || node.Output.Count != 1) continue;
                int constantIndex = constants.TryGetValue(node.Input[0], out float[]? first) && IsAllOnes(first) ? 0 : constants.TryGetValue(node.Input[1], out float[]? second) && IsAllOnes(second) ? 1 : -1;
                if (constantIndex < 0) continue;
                string valueInput = node.Input[1 - constantIndex];
                if (!IsShapeValue(valueInput, graph, producers, memo, new HashSet<string>(StringComparer.Ordinal))) continue;
                node.OpType = "Identity";
                node.Input.RemoveAt(constantIndex);
                changed = true;
            }
            return changed;
        }

        private static bool NormalizeShapeArithmetic(GraphProto graph)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            var constantTensors = new Dictionary<string, TensorProto>(StringComparer.Ordinal);
            var consumers = new Dictionary<string, List<NodeProto>>(StringComparer.Ordinal);
            foreach (TensorProto tensor in graph.Initializer)
            {
                if (!string.IsNullOrEmpty(tensor.Name)) constantTensors[tensor.Name] = tensor;
            }
            foreach (NodeProto node in graph.Node)
            {
                foreach (string output in node.Output) if (!string.IsNullOrEmpty(output)) producers[output] = node;
                if (node.OpType == "Constant" && node.Output.Count > 0)
                {
                    AttributeProto? value = node.Attribute.FirstOrDefault(attribute => attribute.Name == "value" && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
                    if (value?.T != null) constantTensors[node.Output[0]] = value.T;
                }
                foreach (string input in node.Input)
                {
                    if (string.IsNullOrEmpty(input)) continue;
                    if (!consumers.TryGetValue(input, out List<NodeProto>? list)) consumers[input] = list = new List<NodeProto>();
                    list.Add(node);
                }
            }
            var memo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            foreach (NodeProto node in graph.Node)
            {
                if ((node.OpType != "Add" && node.OpType != "Sub" && node.OpType != "Mul" && node.OpType != "Div") || node.Input.Count < 2) continue;
                string[] arithmeticInputs = node.Input.Where(input => !string.IsNullOrEmpty(input)).ToArray();
                bool hasShapeInput = arithmeticInputs.Any(input => IsIntegerShapeValue(input, graph, producers, memo, new HashSet<string>(StringComparer.Ordinal)));
                if (!hasShapeInput || !arithmeticInputs.All(input => IsIntegerShapeValue(input, graph, producers, memo, new HashSet<string>(StringComparer.Ordinal)) || IsIntegralFloatConstant(input, constantTensors))) continue;
                foreach (string input in node.Input)
                {
                    if (!constantTensors.TryGetValue(input, out TensorProto? tensor) || tensor.DataType != (int)TensorProto.Types.DataType.Float) continue;
                    if (!consumers.TryGetValue(input, out List<NodeProto>? users) || users.Any(user => !IsShapeArithmeticNode(user))) continue;
                    float[]? values = ReadFloatValues(tensor);
                    if (values == null || values.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value != MathF.Truncate(value) || value < int.MinValue || value > int.MaxValue)) continue;
                    tensor.DataType = (int)TensorProto.Types.DataType.Int32;
                    tensor.FloatData.Clear();
                    tensor.RawData = ByteString.Empty;
                    tensor.Int32Data.Add(values.Select(value => (int)value));
                    changed = true;
                }
            }
            return changed;
        }

        private static bool NormalizeShapeFloatCasts(GraphProto graph)
        {
            var producers = new Dictionary<string, NodeProto>(StringComparer.Ordinal);
            var consumers = new Dictionary<string, List<NodeProto>>(StringComparer.Ordinal);
            foreach (NodeProto node in graph.Node)
            {
                foreach (string output in node.Output) if (!string.IsNullOrEmpty(output)) producers[output] = node;
                foreach (string input in node.Input)
                {
                    if (string.IsNullOrEmpty(input)) continue;
                    if (!consumers.TryGetValue(input, out List<NodeProto>? users)) consumers[input] = users = new List<NodeProto>();
                    users.Add(node);
                }
            }
            var memo = new Dictionary<string, bool>(StringComparer.Ordinal);
            var shapeMemo = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool changed = false;
            foreach (NodeProto node in graph.Node)
            {
                if (node.OpType != "Cast" || node.Input.Count != 1 || node.Output.Count != 1) continue;
                AttributeProto? target = node.Attribute.FirstOrDefault(attribute => attribute.Name == "to" && attribute.Type == AttributeProto.Types.AttributeType.Int);
                if (target == null || target.I != (int)TensorProto.Types.DataType.Float || !IsShapeValue(node.Input[0], graph, producers, shapeMemo, new HashSet<string>(StringComparer.Ordinal))) continue;
                if (!IsIntegerConsumerPath(node.Output[0], consumers, memo, new HashSet<string>(StringComparer.Ordinal))) continue;
                target.I = (int)TensorProto.Types.DataType.Int32;
                changed = true;
            }
            return changed;
        }

        private static bool IsIntegerConsumerPath(string value, IReadOnlyDictionary<string, List<NodeProto>> consumers, IDictionary<string, bool> memo, ISet<string> visiting)
        {
            if (memo.TryGetValue("consumer:" + value, out bool known)) return known;
            if (!visiting.Add(value)) return false;
            if (!consumers.TryGetValue(value, out List<NodeProto>? users) || users.Count == 0)
            {
                memo["consumer:" + value] = false;
                visiting.Remove(value);
                return false;
            }
            bool result = true;
            foreach (NodeProto user in users)
            {
                if (user.OpType == "Cast")
                {
                    AttributeProto? target = user.Attribute.FirstOrDefault(attribute => attribute.Name == "to" && attribute.Type == AttributeProto.Types.AttributeType.Int);
                    if (target == null || !IsIntegerDataType((int)target.I)) { result = false; break; }
                    continue;
                }
                if (user.OpType != "Add" && user.OpType != "Sub" && user.OpType != "Mul" && user.OpType != "Div" && user.OpType != "Max" && user.OpType != "Min" && user.OpType != "Concat" && user.OpType != "Reshape" && user.OpType != "Slice" && user.OpType != "Gather" && user.OpType != "Unsqueeze" && user.OpType != "Squeeze") { result = false; break; }
                if (user.Output.Count == 0 || user.Output.Any(output => !IsIntegerConsumerPath(output, consumers, memo, visiting))) { result = false; break; }
            }
            visiting.Remove(value);
            memo["consumer:" + value] = result;
            return result;
        }

        private static bool IsShapeArithmeticNode(NodeProto node)
            => node.OpType == "Add" || node.OpType == "Sub" || node.OpType == "Mul" || node.OpType == "Div" || node.OpType == "Cast" || node.OpType == "Concat" || node.OpType == "Reshape" || node.OpType == "Slice";

        private static bool IsIntegralFloatConstant(string value, IReadOnlyDictionary<string, TensorProto> constants)
        {
            if (!constants.TryGetValue(value, out TensorProto? tensor) || tensor.DataType != (int)TensorProto.Types.DataType.Float) return false;
            float[]? values = ReadFloatValues(tensor);
            return values != null && values.All(item => !float.IsNaN(item) && !float.IsInfinity(item) && item == MathF.Truncate(item) && item >= int.MinValue && item <= int.MaxValue);
        }

        private static bool IsIntegerShapeValue(string value, GraphProto graph, IReadOnlyDictionary<string, NodeProto> producers, IDictionary<string, bool> memo, ISet<string> visiting)
        {
            if (memo.TryGetValue("int:" + value, out bool known)) return known;
            if (!visiting.Add("int:" + value)) return false;
            TensorProto? initializer = graph.Initializer.FirstOrDefault(tensor => string.Equals(tensor.Name, value, StringComparison.Ordinal));
            if (initializer != null)
            {
                bool integer = IsIntegerDataType(initializer.DataType);
                memo["int:" + value] = integer;
                visiting.Remove("int:" + value);
                return integer;
            }
            if (!producers.TryGetValue(value, out NodeProto? producer))
            {
                memo["int:" + value] = false;
                visiting.Remove("int:" + value);
                return false;
            }
            bool result;
            if (producer.OpType == "Shape") result = true;
            else if (producer.OpType == "Constant") result = IsIntegerConstant(producer);
            else if (producer.OpType == "ConstantOfShape") result = IsIntegerConstantOfShape(producer) && producer.Input.Count == 1 && IsIntegerShapeValue(producer.Input[0], graph, producers, memo, visiting);
            else if (producer.OpType == "Cast")
            {
                AttributeProto? target = producer.Attribute.FirstOrDefault(attribute => attribute.Name == "to" && attribute.Type == AttributeProto.Types.AttributeType.Int);
                result = target != null && IsIntegerDataType((int)target.I) && producer.Input.Count == 1 && IsIntegerShapeValue(producer.Input[0], graph, producers, memo, visiting);
            }
            else if (producer.OpType == "Gather" || producer.OpType == "Concat" || producer.OpType == "Unsqueeze" || producer.OpType == "Squeeze" || producer.OpType == "Reshape" || producer.OpType == "Slice" || producer.OpType == "Expand" || producer.OpType == "Range" || producer.OpType == "Add" || producer.OpType == "Sub" || producer.OpType == "Mul" || producer.OpType == "Div" || producer.OpType == "Max" || producer.OpType == "Min")
            {
                result = producer.Input.Count > 0 && producer.Input.Where(input => !string.IsNullOrEmpty(input)).All(input => IsIntegerShapeValue(input, graph, producers, memo, visiting));
            }
            else if (producer.OpType == "Where")
            {
                result = producer.Input.Count == 3 && IsIntegerShapeValue(producer.Input[1], graph, producers, memo, visiting) && IsIntegerShapeValue(producer.Input[2], graph, producers, memo, visiting);
            }
            else result = false;
            visiting.Remove("int:" + value);
            memo["int:" + value] = result;
            return result;
        }

        private static bool IsIntegerConstant(NodeProto node)
        {
            AttributeProto? value = node.Attribute.FirstOrDefault(attribute => attribute.Name == "value" && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
            return value?.T != null && IsIntegerDataType(value.T.DataType);
        }

        private static bool IsIntegerConstantOfShape(NodeProto node)
        {
            AttributeProto? value = node.Attribute.FirstOrDefault(attribute => attribute.Name == "value" && attribute.Type == AttributeProto.Types.AttributeType.Tensor);
            return value?.T != null && IsIntegerDataType(value.T.DataType);
        }

        private static bool IsIntegerDataType(int dataType)
            => dataType == (int)TensorProto.Types.DataType.Int8
                || dataType == (int)TensorProto.Types.DataType.Uint8
                || dataType == (int)TensorProto.Types.DataType.Int16
                || dataType == (int)TensorProto.Types.DataType.Uint16
                || dataType == (int)TensorProto.Types.DataType.Int32
                || dataType == (int)TensorProto.Types.DataType.Int64
                || dataType == (int)TensorProto.Types.DataType.Uint32
                || dataType == (int)TensorProto.Types.DataType.Uint64;

        private static bool IsAllOnes(IReadOnlyList<float> values)
            => values.Count > 0 && values.All(value => value == 1f);

        private static bool NormalizeUnsqueezeChain(NodeProto node, Google.Protobuf.Collections.RepeatedField<NodeProto> graphNodes, int nodeIndex)
        {
            AttributeProto? axes = node.Attribute.FirstOrDefault(value => string.Equals(value.Name, "axes", StringComparison.Ordinal) && value.Type == AttributeProto.Types.AttributeType.Ints);
            if (axes == null || axes.Ints.Count <= 1 || node.Input.Count != 1 || node.Output.Count != 1 || string.IsNullOrEmpty(node.Output[0])) return false;
            if (axes.Ints.Any(value => value < int.MinValue || value > int.MaxValue)) return false;
            var orderedAxes = axes.Ints.Select(value => checked((int)value)).ToArray();
            if (orderedAxes.Any(value => value < 0) || orderedAxes.Distinct().Count() != orderedAxes.Length) return false;
            Array.Sort(orderedAxes);

            string originalOutput = node.Output[0];
            string baseName = originalOutput + "__deploysharp_unsqueeze";
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (NodeProto graphNode in graphNodes)
            {
                occupied.UnionWith(graphNode.Input);
                occupied.UnionWith(graphNode.Output);
            }
            var intermediateNames = new string[orderedAxes.Length - 1];
            for (int index = 0; index < intermediateNames.Length; index++)
            {
                string candidate = baseName + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int suffix = 0;
                while (!occupied.Add(candidate)) candidate = baseName + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                intermediateNames[index] = candidate;
            }

            node.Output[0] = intermediateNames[0];
            axes.Ints.Clear();
            axes.Ints.Add(orderedAxes[0]);
            string previous = intermediateNames[0];
            for (int index = 1; index < orderedAxes.Length; index++)
            {
                var next = new NodeProto { OpType = "Unsqueeze", Name = string.IsNullOrEmpty(node.Name) ? string.Empty : node.Name + "__deploysharp_axis" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                next.Input.Add(previous);
                string output = index == orderedAxes.Length - 1 ? originalOutput : intermediateNames[index];
                next.Output.Add(output);
                var nextAxes = new AttributeProto { Name = "axes", Type = AttributeProto.Types.AttributeType.Ints };
                nextAxes.Ints.Add(orderedAxes[index]);
                next.Attribute.Add(nextAxes);
                graphNodes.Insert(nodeIndex + index, next);
                previous = output;
            }
            return true;
        }

        private static bool NormalizeTopKInput(NodeProto node, IReadOnlyDictionary<string, long[]> constants)
        {
            if (node.Input.Count != 2 || HasAttribute(node, "k")) return false;
            if (!constants.TryGetValue(node.Input[1], out long[]? values) || values.Length != 1 || !AreInt32(values) || values[0] <= 0) return false;
            node.Attribute.Add(new AttributeProto { Name = "k", Type = AttributeProto.Types.AttributeType.Int, I = values[0] });
            node.Input.RemoveAt(1);
            return true;
        }

        private static bool NormalizeSliceInputs(NodeProto node, IReadOnlyDictionary<string, long[]> constants)
        {
            // Slice parameter inputs are positional. Convert only when every supplied
            // parameter is constant, so removing the tail cannot shift a dynamic input.
            if (node.Input.Count < 3 || HasAttribute(node, "starts") || HasAttribute(node, "ends")) return false;
            var parameters = new (int Index, string AttributeName)[]
            {
                (1, "starts"), (2, "ends"), (3, "axes"), (4, "steps")
            };
            var values = new Dictionary<string, long[]>(StringComparer.Ordinal);
            int lastParameter = node.Input.Count - 1;
            for (int index = 1; index <= lastParameter; index++)
            {
                string input = node.Input[index];
                if (string.IsNullOrEmpty(input)) continue;
                if (index >= parameters.Length + 1) return false;
                var parameter = parameters[index - 1];
                if (!constants.TryGetValue(input, out long[]? constant) || !AreInt32(constant)) return false;
                values[parameter.AttributeName] = constant;
            }
            if (!values.ContainsKey("starts") || !values.ContainsKey("ends")) return false;
            foreach ((int _, string attributeName) in parameters)
            {
                if (!values.TryGetValue(attributeName, out long[]? value)) continue;
                var attribute = new AttributeProto { Name = attributeName, Type = AttributeProto.Types.AttributeType.Ints };
                attribute.Ints.Add(value);
                node.Attribute.Add(attribute);
            }
            while (node.Input.Count > 1) node.Input.RemoveAt(node.Input.Count - 1);
            return true;
        }

        private static bool AreInt32(IReadOnlyList<long> values)
            => values.Count > 0 && values.All(value => value >= int.MinValue && value <= int.MaxValue);

        private static float[]? ReadFloatValues(TensorProto tensor)
        {
            if (tensor == null || tensor.DataType != (int)TensorProto.Types.DataType.Float) return null;
            int count = tensor.Dims.Count == 0 ? 1 : checked((int)tensor.Dims.Aggregate(1L, (current, value) => checked(current * value)));
            if (tensor.FloatData.Count == count) return tensor.FloatData.ToArray();
            if (tensor.RawData.Length == checked(count * sizeof(float)))
            {
                var values = new float[count];
                byte[] raw = tensor.RawData.ToByteArray();
                Buffer.BlockCopy(raw, 0, values, 0, raw.Length);
                return values;
            }
            return null;
        }

        private static long[]? ReadIntegerValues(TensorProto tensor)
        {
            if (tensor == null || (tensor.DataType != (int)TensorProto.Types.DataType.Int32 && tensor.DataType != (int)TensorProto.Types.DataType.Int64)) return null;
            int count = tensor.Dims.Count == 0 ? 1 : checked((int)tensor.Dims.Aggregate(1L, (current, value) => checked(current * value)));
            var values = new long[count];
            if (tensor.DataType == (int)TensorProto.Types.DataType.Int64)
            {
                if (tensor.Int64Data.Count == count) { for (int index = 0; index < count; index++) values[index] = tensor.Int64Data[index]; return values; }
                if (tensor.RawData.Length == checked(count * sizeof(long))) { Buffer.BlockCopy(tensor.RawData.ToByteArray(), 0, values, 0, tensor.RawData.Length); return values; }
            }
            else
            {
                if (tensor.Int32Data.Count == count) { for (int index = 0; index < count; index++) values[index] = tensor.Int32Data[index]; return values; }
                if (tensor.RawData.Length == checked(count * sizeof(int))) { var ints = new int[count]; Buffer.BlockCopy(tensor.RawData.ToByteArray(), 0, ints, 0, tensor.RawData.Length); for (int index = 0; index < count; index++) values[index] = ints[index]; return values; }
            }
            return null;
        }
    }
}
