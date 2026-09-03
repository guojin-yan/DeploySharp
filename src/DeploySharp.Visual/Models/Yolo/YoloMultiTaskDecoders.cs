using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Results.Vision;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual.Models.Yolo
{
    /// <summary>Decodes packed YOLO detection rows and prototype masks into canonical instance segmentation. / 将打包 YOLO 检测行和原型掩码解码为规范实例分割结果。</summary>
    public sealed class YoloInstanceSegmentationDecoder : IVisualDecoder
    {
        /// <summary>Initializes an exact packed YOLO instance-segmentation decoder. / 初始化精确的打包 YOLO 实例分割解码器。</summary>
        public YoloInstanceSegmentationDecoder(YoloInstanceSegmentationOutputContract contract, YoloPackedDecoderOptions? options = null)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            Options = options ?? new YoloPackedDecoderOptions();
            if (Contract.CandidateCount > Options.MaximumCandidates) throw new ArgumentException("The output candidate count exceeds the decoder bound.", nameof(contract));
        }

        /// <summary>Gets the instance-segmentation task. / 获取实例分割任务。</summary>
        public VisualTaskId Task => VisualTaskId.InstanceSegmentation;
        /// <summary>Gets the exact packed/prototype contract. / 获取精确的打包和原型合同。</summary>
        public YoloInstanceSegmentationOutputContract Contract { get; }
        /// <summary>Gets bounded decoding options. / 获取有界解码选项。</summary>
        public YoloPackedDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Only the retained masks are materialized; backend tensors are borrowed for this synchronous call and never retained. / 仅实体化保留的掩码；后端张量只在本次同步调用中借用且绝不保留。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Input.BatchSize > 1)
            {
                ITensor packed = YoloPackedTensor.Required(context, Contract.OutputName);
                ITensor prototypes = YoloPackedTensor.Required(context, Contract.PrototypeOutputName);
                int batch = YoloPackedTensor.ResolveBatch(context, packed, Contract.Layout, Contract.CandidateCount, Contract.FieldCount);
                if (prototypes.Shape.Rank != 4 || (prototypes.Shape[0] != 1 && prototypes.Shape[0] != batch) || prototypes.Shape[1] != Contract.MaskCoefficientCount)
                    throw YoloPackedTensor.Failure(context, "Batched YOLO prototypes must be [1,C,H/4,W/4] or [B,C,H/4,W/4].", Contract.PrototypeOutputName, prototypes.Shape.ToString());
                float[] packedValues = VisualTensorReader.ReadFiniteScores(packed, context.Profile.ProfileId, Contract.OutputName);
                float[] prototypeValues = VisualTensorReader.ReadFiniteScores(prototypes, context.Profile.ProfileId, Contract.PrototypeOutputName);
                var results = new List<InstanceSegmentationResult>(batch);
                for (int row = 0; row < batch; row++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    VisualInputFrame frame = context.Input.BatchFrames[row];
                    PreparedVisualInput rowInput = YoloPackedTensor.CreateRowInput(context, frame, row);
                    int packedLength = checked(Contract.CandidateCount * Contract.FieldCount);
                    int packedOffset = checked(row * packedLength);
                    int prototypePlane = checked((int)(prototypes.Length / prototypes.Shape[0]));
                    int prototypeRow = prototypes.Shape[0] == 1 ? 0 : row;
                    int prototypeOffset = checked(prototypeRow * prototypePlane);
                    var rowOutputs = new InferenceOutputs(new[]
                    {
                        new NamedTensor(Contract.OutputName, new Tensor<float>(PackedShape(Contract.Layout, Contract.CandidateCount, Contract.FieldCount), CopySlice(packedValues, packedOffset, packedLength), TensorBufferOwnership.Transfer)),
                        new NamedTensor(Contract.PrototypeOutputName, new Tensor<float>(new TensorShape(1, (int)prototypes.Shape[1], (int)prototypes.Shape[2], (int)prototypes.Shape[3]), CopySlice(prototypeValues, prototypeOffset, prototypePlane), TensorBufferOwnership.Transfer))
                    });
                    results.Add((InstanceSegmentationResult)DecodeSingle(new VisualDecodeContext(rowInput, context.Profile, rowOutputs, context.CancellationToken)));
                }
                return new InstanceSegmentationBatchResult(results);
            }
            return DecodeSingle(context);
        }

        private object DecodeSingle(VisualDecodeContext context)
        {
            YoloCudaInstanceSegmentationPlan plan = PrepareCudaPlan(context);
            ITensor prototypesTensor = YoloPackedTensor.Required(context, Contract.PrototypeOutputName);
            float[] prototypes = VisualTensorReader.ReadFiniteScores(prototypesTensor, context.Profile.ProfileId, Contract.PrototypeOutputName);
            int plane = checked(plan.PrototypeWidth * plan.PrototypeHeight);
            float[] combined = VisualArrayPool<float>.Rent(plane);
            var instances = new List<InstanceSegmentationInstance>(plan.Candidates.Count);
            try
            {
                for (int instanceIndex = 0; instanceIndex < plan.Candidates.Count; instanceIndex++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    VisualDetectionCandidate candidate = plan.Candidates[instanceIndex];
                    CombinePrototypes(plan.Coefficients, prototypes, combined, instanceIndex, plan.MaskCoefficientCount, plane, context);
                    InstanceBinaryMask mask = InstanceMaskRestorer.Restore(
                        combined, 0, plan.PrototypeWidth, plan.PrototypeHeight, context.Input, candidate.ModelBox,
                        InstanceMaskValueKind.Logits, InstanceMaskActivation.Sigmoid, InstanceMaskInterpolationMode.BilinearHalfPixel,
                        InstanceMaskThresholdOrder.AfterResize, InstanceMaskCropSpace.ModelInput, InstanceMaskCropOrder.BeforeResize,
                        Options.MaskThreshold, context.CancellationToken);
                    InstanceMaskRle? rle = InstanceSegmentationDecoding.EncodeRle(mask, plan.DecoderOptions, context, Contract.PrototypeOutputName);
                    instances.Add(CreateInstance(context, candidate, mask, rle));
                }

                return InstanceSegmentationDecoding.CreateResult(instances, context, plan.DecoderOptions);
            }
            finally
            {
                VisualArrayPool<float>.Return(combined);
            }
        }

        internal YoloCudaInstanceSegmentationPlan PrepareCudaPlan(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw YoloPackedTensor.Failure(context, "YOLO instance segmentation requires batch size one.", Contract.OutputName);
            if (context.Outputs.Count != 2) throw YoloPackedTensor.Failure(context, "YOLO instance segmentation requires exactly the packed and prototype outputs.", Contract.OutputName);
            ITensor packedTensor = YoloPackedTensor.Required(context, Contract.OutputName);
            float[] packed = YoloPackedTensor.Read(context, packedTensor, Contract.OutputName, Contract.Layout, Contract.CandidateCount, Contract.FieldCount);
            ITensor prototypesTensor = YoloPackedTensor.Required(context, Contract.PrototypeOutputName);
            ValidatePrototypeShape(context, prototypesTensor);

            int candidates = Contract.CandidateCount;
            int classOffset = Contract.HasObjectness ? 5 : 4;
            int coefficientOffset = classOffset + Contract.ClassCount;
            DetectionBoxFormat boxFormat = Contract.IsEndToEnd ? DetectionBoxFormat.Xyxy : DetectionBoxFormat.Cxcywh;
            var selected = new List<VisualDetectionCandidate>(Math.Min(candidates, Options.MaximumDetections * 4));
            for (int candidate = 0; candidate < candidates; candidate++)
            {
                if ((candidate & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                int selectedClass;
                float score;
                if (Contract.IsEndToEnd)
                {
                    score = YoloPackedTensor.Probability(YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 4, Contract.Layout), context, Contract.OutputName, candidate, "score");
                    selectedClass = YoloPackedTensor.ClassIndex(YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 5, Contract.Layout), Contract.ClassCount, context, Contract.OutputName, candidate);
                }
                else
                {
                    float objectness = Contract.HasObjectness ? YoloPackedTensor.Probability(YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 4, Contract.Layout), context, Contract.OutputName, candidate, "objectness") : 1f;
                    selectedClass = 0;
                    float classScore = YoloPackedTensor.Probability(YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, classOffset, Contract.Layout), context, Contract.OutputName, candidate, "class-score");
                    for (int classIndex = 1; classIndex < Contract.ClassCount; classIndex++)
                    {
                        float current = YoloPackedTensor.Probability(YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, classOffset + classIndex, Contract.Layout), context, Contract.OutputName, candidate, "class-score");
                        if (current > classScore) { classScore = current; selectedClass = classIndex; }
                    }
                    score = objectness * classScore;
                }

                // The previous adapter expanded every dense candidate into four managed
                // tensors before the generic decoder filtered them. Keep only candidates
                // that can survive the strict YOLO threshold, while retaining their original
                // indices so result identity and coefficient lookup remain unchanged.
                if (score <= Options.ScoreThreshold) continue;
                float first = YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 0, Contract.Layout);
                float second = YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 1, Contract.Layout);
                float third = YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 2, Contract.Layout);
                float fourth = YoloPackedTensor.Value(packed, candidates, Contract.FieldCount, candidate, 3, Contract.Layout);
                ValidateBox(first, second, third, fourth, boxFormat, context, candidate);
                RectangleF modelBox = DetectionPostprocessing.DecodeModelBox(boxFormat, false, context.Input.ModelSize, first, second, third, fourth);
                RectangleF sourceBox = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                selected.Add(new VisualDetectionCandidate(candidate, selectedClass, score, modelBox, sourceBox));
            }

            var genericOptions = new InstanceSegmentationDecoderOptions(
                Math.Max(Options.ScoreThreshold, float.Epsilon), Options.MaskThreshold, Contract.IsEndToEnd ? 1f : Options.IouThreshold, Options.NmsMode,
                InstanceMaskOverlapMode.Independent, Options.GenerateRle, candidates, Options.MaximumDetections, Contract.MaskCoefficientCount,
                maximumWorkspaceBytes: Options.MaximumWorkspaceBytes);

            selected.Sort(YoloPackedTensor.CompareCandidates);
            List<VisualDetectionCandidate> kept = DetectionPostprocessing.Suppress(selected, genericOptions.IouThreshold, genericOptions.NmsMode, genericOptions.MaximumInstances, context.CancellationToken);
            InstanceSegmentationDecoding.EnsureResultBound(context, kept.Count, genericOptions);
            int prototypeWidth = context.Input.ModelSize.Width / 4;
            int prototypeHeight = context.Input.ModelSize.Height / 4;
            int plane = checked(prototypeWidth * prototypeHeight);
            InstanceSegmentationDecoding.EnsureTensorMaskBound(context, checked((long)Contract.MaskCoefficientCount * plane), genericOptions, Contract.PrototypeOutputName);
            InstanceSegmentationDecoding.EnsureCombinedWorkspace(context, genericOptions, checked((long)plane * sizeof(float)), packedTensor, prototypesTensor);
            int coefficientFieldOffset = Contract.IsEndToEnd ? 6 : coefficientOffset;
            return new YoloCudaInstanceSegmentationPlan(packed, kept, genericOptions, coefficientFieldOffset, prototypeWidth, prototypeHeight, Contract);
        }

        internal YoloCudaInstanceSegmentationPlan PrepareCudaPlan(
            VisualDecodeContext context,
            byte[] selectedFlags,
            int[] classIndices,
            float[] scores,
            float[] boxes,
            float[] coefficients)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1 || context.Outputs.Count != 2) throw YoloPackedTensor.Failure(context, "YOLO instance segmentation requires batch one and exactly two outputs.", Contract.OutputName);
            ITensor packedTensor = YoloPackedTensor.Required(context, Contract.OutputName);
            YoloPackedTensor.ValidateShape(context, packedTensor, Contract.OutputName, Contract.Layout, Contract.CandidateCount, Contract.FieldCount);
            ITensor prototypesTensor = YoloPackedTensor.Required(context, Contract.PrototypeOutputName);
            ValidatePrototypeShape(context, prototypesTensor);
            int candidates = Contract.CandidateCount;
            if (selectedFlags == null || selectedFlags.Length != candidates || classIndices == null || classIndices.Length != candidates || scores == null || scores.Length != candidates
                || boxes == null || boxes.Length != checked(candidates * 4) || coefficients == null || coefficients.Length != checked(candidates * Contract.MaskCoefficientCount))
            {
                throw new ArgumentException("CUDA candidate buffers do not match the YOLO contract.", nameof(selectedFlags));
            }

            DetectionBoxFormat boxFormat = Contract.IsEndToEnd ? DetectionBoxFormat.Xyxy : DetectionBoxFormat.Cxcywh;
            var selected = new List<VisualDetectionCandidate>(Math.Min(candidates, Options.MaximumDetections * 4));
            for (int candidate = 0; candidate < candidates; candidate++)
            {
                if ((candidate & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                byte selectedFlag = selectedFlags[candidate];
                if (selectedFlag > 1) throw YoloPackedTensor.Failure(context, "CUDA candidate filter emitted a non-binary selection flag.", Contract.OutputName, "candidate=" + candidate);
                if (selectedFlag == 0) continue;
                int selectedClass = classIndices[candidate];
                float score = scores[candidate];
                if (selectedClass < 0 || selectedClass >= Contract.ClassCount || float.IsNaN(score) || float.IsInfinity(score) || score <= Options.ScoreThreshold || score > 1f)
                    throw YoloPackedTensor.Failure(context, "CUDA candidate filter emitted invalid class or score metadata.", Contract.OutputName, "candidate=" + candidate);
                int boxOffset = candidate * 4;
                float first = boxes[boxOffset];
                float second = boxes[boxOffset + 1];
                float third = boxes[boxOffset + 2];
                float fourth = boxes[boxOffset + 3];
                ValidateBox(first, second, third, fourth, boxFormat, context, candidate);
                RectangleF modelBox = DetectionPostprocessing.DecodeModelBox(boxFormat, false, context.Input.ModelSize, first, second, third, fourth);
                RectangleF sourceBox = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                selected.Add(new VisualDetectionCandidate(candidate, selectedClass, score, modelBox, sourceBox));
            }

            var genericOptions = new InstanceSegmentationDecoderOptions(
                Math.Max(Options.ScoreThreshold, float.Epsilon), Options.MaskThreshold, Contract.IsEndToEnd ? 1f : Options.IouThreshold, Options.NmsMode,
                InstanceMaskOverlapMode.Independent, Options.GenerateRle, candidates, Options.MaximumDetections, Contract.MaskCoefficientCount,
                maximumWorkspaceBytes: Options.MaximumWorkspaceBytes);
            selected.Sort(YoloPackedTensor.CompareCandidates);
            List<VisualDetectionCandidate> kept = DetectionPostprocessing.Suppress(selected, genericOptions.IouThreshold, genericOptions.NmsMode, genericOptions.MaximumInstances, context.CancellationToken);
            InstanceSegmentationDecoding.EnsureResultBound(context, kept.Count, genericOptions);
            int prototypeWidth = context.Input.ModelSize.Width / 4;
            int prototypeHeight = context.Input.ModelSize.Height / 4;
            int plane = checked(prototypeWidth * prototypeHeight);
            InstanceSegmentationDecoding.EnsureTensorMaskBound(context, checked((long)Contract.MaskCoefficientCount * plane), genericOptions, Contract.PrototypeOutputName);
            InstanceSegmentationDecoding.EnsureCombinedWorkspace(context, genericOptions, checked((long)plane * sizeof(float)), packedTensor, prototypesTensor);
            return new YoloCudaInstanceSegmentationPlan(coefficients, kept, genericOptions, prototypeWidth, prototypeHeight, Contract.MaskCoefficientCount);
        }

        internal object CreateCudaDecodedResult(VisualDecodeContext context, YoloCudaInstanceSegmentationPlan plan, byte[] masks, int[] foregroundPixelCounts)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (masks == null) throw new ArgumentNullException(nameof(masks));
            if (foregroundPixelCounts == null) throw new ArgumentNullException(nameof(foregroundPixelCounts));
            int instanceCount = plan.Candidates.Count;
            int pixels = checked(context.Input.SourceSize.Width * context.Input.SourceSize.Height);
            if (masks.Length != checked(instanceCount * pixels) || foregroundPixelCounts.Length != instanceCount) throw new ArgumentException("CUDA mask output does not match the retained instance count.", nameof(masks));
            var instances = new List<InstanceSegmentationInstance>(instanceCount);
            for (int index = 0; index < instanceCount; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var mask = new InstanceBinaryMask(context.Input.SourceSize.Width, context.Input.SourceSize.Height, masks, checked(index * pixels), InstanceMaskCoordinateSpace.SourceImage, 0, 0, foregroundPixelCounts[index]);
                InstanceMaskRle? rle = InstanceSegmentationDecoding.EncodeRle(mask, plan.DecoderOptions, context, Contract.PrototypeOutputName);
                instances.Add(CreateInstance(context, plan.Candidates[index], mask, rle));
            }
            return InstanceSegmentationDecoding.CreateResult(instances, context, plan.DecoderOptions);
        }

        private void ValidatePrototypeShape(VisualDecodeContext context, ITensor tensor)
        {
            TensorShape shape = tensor.Shape;
            int expectedHeight = context.Input.ModelSize.Height / 4;
            int expectedWidth = context.Input.ModelSize.Width / 4;
            if (shape.Rank != 4 || shape[0] != 1 || shape[1] != Contract.MaskCoefficientCount || shape[2] != expectedHeight || shape[3] != expectedWidth || tensor.Length != (long)Contract.MaskCoefficientCount * expectedHeight * expectedWidth)
                throw YoloPackedTensor.Failure(context, "YOLO prototypes do not match [1,C,H/4,W/4].", Contract.PrototypeOutputName, shape.ToString());
        }

        private static TensorShape PackedShape(YoloPackedTensorLayout layout, int candidates, int fields)
            => layout == YoloPackedTensorLayout.AttributeMajor ? new TensorShape(1, fields, candidates) : new TensorShape(1, candidates, fields);

        private static float[] CopySlice(float[] values, int offset, int length)
        {
            var copy = new float[length];
            Array.Copy(values, offset, copy, 0, length);
            return copy;
        }

        private static InstanceSegmentationInstance CreateInstance(VisualDecodeContext context, VisualDetectionCandidate candidate, InstanceBinaryMask mask, InstanceMaskRle? rle)
            => new InstanceSegmentationInstance(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, candidate.SourceBox, mask, rle);

        private void CombinePrototypes(float[] coefficients, float[] prototypes, float[] combined, int instanceIndex, int coefficientCount, int plane, VisualDecodeContext context)
        {
            Array.Clear(combined, 0, plane);
            for (int channel = 0; channel < coefficientCount; channel++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float coefficient = coefficients[(instanceIndex * coefficientCount) + channel];
                int prototypeOffset = checked(channel * plane);
                for (int position = 0; position < plane; position++) combined[position] += coefficient * prototypes[prototypeOffset + position];
            }
            for (int position = 0; position < plane; position++)
            {
                if (float.IsNaN(combined[position]) || float.IsInfinity(combined[position])) throw YoloPackedTensor.Failure(context, "Prototype linear combination produced a non-finite mask value.", Contract.PrototypeOutputName, "position=" + position);
            }
        }

        private static void ValidateBox(float first, float second, float third, float fourth, DetectionBoxFormat format, VisualDecodeContext context, int candidate)
        {
            float width = format == DetectionBoxFormat.Xyxy ? third - first : third;
            float height = format == DetectionBoxFormat.Xyxy ? fourth - second : fourth;
            if (width <= 0 || height <= 0) throw YoloPackedTensor.Failure(context, "A retained YOLO segmentation box has non-positive size.", technicalDetails: "candidate=" + candidate);
        }
    }

    internal sealed class YoloCudaInstanceSegmentationPlan
    {
        internal YoloCudaInstanceSegmentationPlan(float[] packed, List<VisualDetectionCandidate> candidates, InstanceSegmentationDecoderOptions decoderOptions, int coefficientFieldOffset, int prototypeWidth, int prototypeHeight, YoloInstanceSegmentationOutputContract contract)
        {
            Candidates = candidates;
            DecoderOptions = decoderOptions;
            PrototypeWidth = prototypeWidth;
            PrototypeHeight = prototypeHeight;
            MaskCoefficientCount = contract.MaskCoefficientCount;
            Coefficients = new float[checked(candidates.Count * MaskCoefficientCount)];
            for (int instance = 0; instance < candidates.Count; instance++)
            {
                int candidate = candidates[instance].SourceIndex;
                for (int channel = 0; channel < MaskCoefficientCount; channel++)
                {
                    Coefficients[(instance * MaskCoefficientCount) + channel] = YoloPackedTensor.Value(packed, contract.CandidateCount, contract.FieldCount, candidate, coefficientFieldOffset + channel, contract.Layout);
                }
            }
        }

        internal YoloCudaInstanceSegmentationPlan(float[] indexedCoefficients, List<VisualDetectionCandidate> candidates, InstanceSegmentationDecoderOptions decoderOptions, int prototypeWidth, int prototypeHeight, int maskCoefficientCount)
        {
            Candidates = candidates;
            DecoderOptions = decoderOptions;
            PrototypeWidth = prototypeWidth;
            PrototypeHeight = prototypeHeight;
            MaskCoefficientCount = maskCoefficientCount;
            Coefficients = new float[checked(candidates.Count * maskCoefficientCount)];
            for (int instance = 0; instance < candidates.Count; instance++)
            {
                Array.Copy(indexedCoefficients, checked(candidates[instance].SourceIndex * maskCoefficientCount), Coefficients, checked(instance * maskCoefficientCount), maskCoefficientCount);
            }
        }

        internal float[] Coefficients { get; }
        internal List<VisualDetectionCandidate> Candidates { get; }
        internal InstanceSegmentationDecoderOptions DecoderOptions { get; }
        internal int PrototypeWidth { get; }
        internal int PrototypeHeight { get; }
        internal int MaskCoefficientCount { get; }

        internal void FillCoefficientBuffer(float[] result)
        {
            if (result == null || result.Length != Coefficients.Length) throw new ArgumentException("Coefficient buffer length is incompatible.", nameof(result));
            Array.Copy(Coefficients, result, Coefficients.Length);
        }

        internal void FillModelBoxBuffer(float[] result)
        {
            if (result == null || result.Length != checked(Candidates.Count * 4)) throw new ArgumentException("Model-box buffer length is incompatible.", nameof(result));
            for (int instance = 0; instance < Candidates.Count; instance++)
            {
                RectangleF box = Candidates[instance].ModelBox;
                int offset = instance * 4;
                result[offset] = box.X;
                result[offset + 1] = box.Y;
                result[offset + 2] = box.Right;
                result[offset + 3] = box.Bottom;
            }
        }
    }

    /// <summary>Decodes packed YOLO boxes and decoded COCO keypoints into canonical Pose results. / 将打包 YOLO 边界框和已解码 COCO 关键点解码为规范 Pose 结果。</summary>
    public sealed class YoloPoseDecoder : IVisualDecoder
    {
        /// <summary>Initializes an exact packed YOLO Pose decoder. / 初始化精确的打包 YOLO Pose 解码器。</summary>
        public YoloPoseDecoder(YoloPoseOutputContract contract, PoseTopology topology, YoloPackedDecoderOptions? options = null)
        { Contract = contract ?? throw new ArgumentNullException(nameof(contract)); Topology = topology ?? throw new ArgumentNullException(nameof(topology)); Options = options ?? new YoloPackedDecoderOptions(); if (Contract.KeypointCount != Topology.Keypoints.Count) throw new ArgumentException("Pose contract and topology counts differ.", nameof(topology)); if (Contract.CandidateCount > Options.MaximumCandidates) throw new ArgumentException("The output candidate count exceeds the decoder bound.", nameof(contract)); }

        /// <summary>Gets the Pose task. / 获取 Pose 任务。</summary>
        public VisualTaskId Task => VisualTaskId.PoseEstimation;
        /// <summary>Gets the exact packed Pose contract. / 获取精确的打包 Pose 合同。</summary>
        public YoloPoseOutputContract Contract { get; }
        /// <summary>Gets immutable keypoint topology. / 获取不可变关键点拓扑。</summary>
        public PoseTopology Topology { get; }
        /// <summary>Gets bounded decoding options. / 获取有界解码选项。</summary>
        public YoloPackedDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Raw-head box NMS precedes keypoint materialization; end-to-end rows are never suppressed again. / 原始 Head 先执行边界框 NMS 再实体化关键点；端到端行绝不再次抑制。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Outputs.Count != 1) throw YoloPackedTensor.Failure(context, "YOLO Pose requires exactly one output.", Contract.OutputName);
            ITensor tensor = YoloPackedTensor.Required(context, Contract.OutputName);
            int batch = YoloPackedTensor.ResolveBatch(context, tensor, Contract.Layout, Contract.CandidateCount, Contract.FieldCount);
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Contract.OutputName);
            var results = new List<PoseEstimationResult>(batch);
            for (int batchIndex = 0; batchIndex < batch; batchIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                results.Add(DecodeSingle(context, tensor, values, batchIndex, batch));
            }
            return batch == 1 ? results[0] : new PoseEstimationBatchResult(results);
        }

        private PoseEstimationResult DecodeSingle(VisualDecodeContext context, ITensor tensor, float[] values, int batchIndex, int batch)
        {
            int baseOffset = checked(batchIndex * Contract.CandidateCount * Contract.FieldCount);
            VisualInputFrame frame = context.Input.BatchFrames[batchIndex];
            List<YoloSelectedCandidate> selected = SelectBoxes(context, values, baseOffset, frame);
            int count = selected.Count;
            var boxes = new float[checked(count * 4)];
            var scores = new float[count];
            var keypoints = new float[checked(count * Contract.KeypointCount * Contract.ComponentsPerKeypoint)];
            int extraOffset = Contract.IsEndToEnd ? 6 : 4 + Contract.ClassCount;
            for (int resultIndex = 0; resultIndex < count; resultIndex++)
            {
                YoloSelectedCandidate item = selected[resultIndex];
                boxes[(resultIndex * 4)] = item.ModelBox.X;
                boxes[(resultIndex * 4) + 1] = item.ModelBox.Y;
                boxes[(resultIndex * 4) + 2] = item.ModelBox.Right;
                boxes[(resultIndex * 4) + 3] = item.ModelBox.Bottom;
                scores[resultIndex] = item.Score;
                int destination = resultIndex * Contract.KeypointCount * Contract.ComponentsPerKeypoint;
                for (int field = 0; field < Contract.KeypointCount * Contract.ComponentsPerKeypoint; field++) keypoints[destination + field] = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, item.SourceIndex, extraOffset + field, Contract.Layout);
            }

            const string keypointsName = "__yolo_keypoints";
            const string boxesName = "__yolo_boxes";
            const string scoresName = "__yolo_scores";
            var schema = new DirectPoseOutputSchema(keypointsName, Contract.KeypointCount, Contract.ComponentsPerKeypoint, 0, 1, 2, -1, PoseCoordinateSpace.ModelPixels, boxesOutputName: boxesName, boxFormat: DetectionBoxFormat.Xyxy, instanceScoresOutputName: scoresName);
            int decodedCandidateLimit = Math.Max(1, count);
            var directOptions = new PoseDecoderOptions(0f, Options.KeypointThreshold, Options.KeypointThreshold, PoseBoundaryMode.Clip, maximumCandidates: decodedCandidateLimit, maximumInstances: Math.Min(Options.MaximumDetections, decodedCandidateLimit), maximumKeypoints: Contract.KeypointCount, maximumResultBytes: Options.MaximumWorkspaceBytes);
            var decoder = new DirectPoseDecoder(schema, Topology, directOptions);
            var unpacked = new InferenceOutputs(new[]
            {
                new NamedTensor(keypointsName, new Tensor<float>(new TensorShape(1, count, Contract.KeypointCount, Contract.ComponentsPerKeypoint), keypoints, TensorBufferOwnership.Transfer)),
                new NamedTensor(boxesName, new Tensor<float>(new TensorShape(1, count, 4), boxes, TensorBufferOwnership.Transfer)),
                new NamedTensor(scoresName, new Tensor<float>(new TensorShape(1, count), scores, TensorBufferOwnership.Transfer))
            });
            PreparedVisualInput rowInput = batch == 1 ? context.Input : YoloPackedTensor.CreateRowInput(context, frame, batchIndex);
            PoseEstimationResult decoded = (PoseEstimationResult)decoder.Decode(new VisualDecodeContext(rowInput, context.Profile, unpacked, context.CancellationToken));
            var remapped = new List<PoseInstance>(decoded.Instances.Count);
            for (int index = 0; index < decoded.Instances.Count; index++)
            {
                PoseInstance item = decoded.Instances[index];
                int sourceIndex = selected[item.SourceIndex].SourceIndex;
                remapped.Add(new PoseInstance(sourceIndex, item.Score, item.Keypoints, item.BoundingBox, selected[item.SourceIndex].ClassIndex, item.ExternalId));
            }
            return new PoseEstimationResult(Topology, remapped, frame.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private List<YoloSelectedCandidate> SelectBoxes(VisualDecodeContext context, float[] values, int baseOffset, VisualInputFrame frame)
        {
            var candidates = new List<VisualDetectionCandidate>(Math.Min(Contract.CandidateCount, Options.MaximumCandidates));
            for (int index = 0; index < Contract.CandidateCount; index++)
            {
                if ((index & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                float score;
                int classIndex;
                DetectionBoxFormat format;
                if (Contract.IsEndToEnd)
                {
                    score = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4, Contract.Layout), context, Contract.OutputName, index, "score");
                    classIndex = YoloPackedTensor.ClassIndex(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 5, Contract.Layout), Contract.ClassCount, context, Contract.OutputName, index);
                    format = DetectionBoxFormat.Xyxy;
                }
                else
                {
                    classIndex = 0;
                    score = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4, Contract.Layout), context, Contract.OutputName, index, "class-score");
                    for (int candidateClass = 1; candidateClass < Contract.ClassCount; candidateClass++)
                    {
                        float current = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4 + candidateClass, Contract.Layout), context, Contract.OutputName, index, "class-score");
                        if (current > score) { score = current; classIndex = candidateClass; }
                    }
                    format = DetectionBoxFormat.Cxcywh;
                }
                if (score <= Options.ScoreThreshold) continue;
                RectangleF modelBox = DetectionPostprocessing.DecodeModelBox(format, false, frame.ModelSize,
                    YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 0, Contract.Layout),
                    YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 1, Contract.Layout),
                    YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 2, Contract.Layout),
                    YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 3, Contract.Layout));
                if (modelBox.Width <= 0 || modelBox.Height <= 0) throw YoloPackedTensor.Failure(context, "A retained YOLO Pose box has non-positive size.", Contract.OutputName, "candidate=" + index);
                RectangleF sourceBox = frame.Transform.ClipToSource(frame.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                var candidate = new VisualDetectionCandidate(index, classIndex, score, modelBox, sourceBox);
                candidates.Add(candidate);
            }
            candidates.Sort(YoloPackedTensor.CompareCandidates);
            List<VisualDetectionCandidate> kept = Contract.IsEndToEnd ? candidates.GetRange(0, Math.Min(candidates.Count, Options.MaximumDetections)) : DetectionPostprocessing.Suppress(candidates, Options.IouThreshold, Options.NmsMode, Options.MaximumDetections, context.CancellationToken, true);
            var result = new List<YoloSelectedCandidate>(kept.Count);
            for (int index = 0; index < kept.Count; index++)
            {
                VisualDetectionCandidate item = kept[index];
                result.Add(new YoloSelectedCandidate(item.SourceIndex, item.ClassIndex, item.Score, item.ModelBox));
            }
            return result;
        }
    }

    /// <summary>Decodes packed YOLO xywhr outputs with official probabilistic-IoU rotated NMS. / 使用官方概率 IoU 旋转 NMS 解码打包 YOLO xywhr 输出。</summary>
    public sealed class YoloObbDecoder : IVisualDecoder
    {
        /// <summary>Initializes an exact packed YOLO OBB decoder. / 初始化精确的打包 YOLO OBB 解码器。</summary>
        public YoloObbDecoder(YoloObbOutputContract contract, YoloPackedDecoderOptions? options = null)
        { Contract = contract ?? throw new ArgumentNullException(nameof(contract)); Options = options ?? new YoloPackedDecoderOptions(); if (Contract.CandidateCount > Options.MaximumCandidates) throw new ArgumentException("The output candidate count exceeds the decoder bound.", nameof(contract)); }
        /// <summary>Gets the oriented-object-detection task. / 获取旋转目标检测任务。</summary>
        public VisualTaskId Task => VisualTaskId.OrientedObjectDetection;
        /// <summary>Gets the exact packed OBB contract. / 获取精确的打包 OBB 合同。</summary>
        public YoloObbOutputContract Contract { get; }
        /// <summary>Gets bounded decoding options. / 获取有界解码选项。</summary>
        public YoloPackedDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Raw exports use Ultralytics fast rotated NMS semantics; YOLO26 end-to-end rows are not suppressed twice. / 原始导出使用 Ultralytics 快速旋转 NMS 语义；YOLO26 端到端行不会重复抑制。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Outputs.Count != 1) throw YoloPackedTensor.Failure(context, "YOLO OBB requires exactly one output.", Contract.OutputName);
            ITensor tensor = YoloPackedTensor.Required(context, Contract.OutputName);
            int batch = YoloPackedTensor.ResolveBatch(context, tensor, Contract.Layout, Contract.CandidateCount, Contract.FieldCount);
            float[] values = VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, Contract.OutputName);
            var batchResults = new List<OrientedDetectionResult>(batch);
            for (int batchIndex = 0; batchIndex < batch; batchIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                batchResults.Add(DecodeSingle(context, values, batchIndex));
            }
            return batch == 1 ? batchResults[0] : new OrientedDetectionBatchResult(batchResults);
        }

        private OrientedDetectionResult DecodeSingle(VisualDecodeContext context, float[] values, int batchIndex)
        {
            int baseOffset = checked(batchIndex * Contract.CandidateCount * Contract.FieldCount);
            VisualInputFrame frame = context.Input.BatchFrames[batchIndex];
            var candidates = new List<YoloObbCandidate>(Math.Min(Contract.CandidateCount, Options.MaximumCandidates));
            for (int index = 0; index < Contract.CandidateCount; index++)
            {
                if ((index & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                int classIndex;
                float score;
                int angleIndex;
                if (Contract.IsEndToEnd)
                {
                    score = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4, Contract.Layout), context, Contract.OutputName, index, "score");
                    classIndex = YoloPackedTensor.ClassIndex(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 5, Contract.Layout), Contract.ClassCount, context, Contract.OutputName, index);
                    angleIndex = 6;
                }
                else
                {
                    classIndex = 0;
                    score = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4, Contract.Layout), context, Contract.OutputName, index, "class-score");
                    for (int candidateClass = 1; candidateClass < Contract.ClassCount; candidateClass++)
                    {
                        float current = YoloPackedTensor.Probability(YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 4 + candidateClass, Contract.Layout), context, Contract.OutputName, index, "class-score");
                        if (current > score) { score = current; classIndex = candidateClass; }
                    }
                    angleIndex = 4 + Contract.ClassCount;
                }
                if (score <= Options.ScoreThreshold) continue;
                float centerX = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 0, Contract.Layout);
                float centerY = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 1, Contract.Layout);
                float width = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 2, Contract.Layout);
                float height = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, 3, Contract.Layout);
                float angle = YoloPackedTensor.Value(values, baseOffset, Contract.CandidateCount, Contract.FieldCount, index, angleIndex, Contract.Layout);
                if (width <= 0 || height <= 0) throw YoloPackedTensor.Failure(context, "A retained YOLO OBB has non-positive size.", Contract.OutputName, "candidate=" + index);
                Regularize(ref width, ref height, ref angle);
                candidates.Add(new YoloObbCandidate(index, classIndex, score, centerX, centerY, width, height, angle));
            }
            candidates.Sort(YoloObbCandidate.Compare);
            List<YoloObbCandidate> kept = Contract.IsEndToEnd ? Take(candidates, Options.MaximumDetections) : FastRotatedNms(candidates, context.CancellationToken);
            var results = new List<OrientedDetection>(kept.Count);
            var schema = new CenterSizeAngleOutputSchema("boxes", "scores", "classes", angleDirection: OrientedAngleDirection.Clockwise, angleRange: OrientedAngleRange.ZeroToPi, widthConvention: OrientedWidthConvention.WidthAxis);
            for (int index = 0; index < kept.Count; index++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                YoloObbCandidate item = kept[index];
                OrientedQuadrilateral model = OrientedGeometry.CreateCenterSizeAngleCorners(item.CenterX, item.CenterY, item.Width, item.Height, item.Angle, schema, frame.ModelSize);
                var sourcePoints = new PointF[4];
                for (int point = 0; point < 4; point++) sourcePoints[point] = frame.Transform.ToSource(model.Vertices[point]);
                OrientedQuadrilateral source = OrientedQuadrilateral.Canonicalize(sourcePoints, OrientedVertexOrder.CounterClockwise, OrientedStartVertexRule.MinimumYThenX);
                results.Add(new OrientedDetection(item.SourceIndex, item.ClassIndex, context.Profile.GetLabel(item.ClassIndex), item.Score, source, -item.Angle, true));
            }
            return new OrientedDetectionResult(results, frame.SourceSize, context.Profile.ProfileId, context.Profile.ModelId);
        }

        private List<YoloObbCandidate> FastRotatedNms(List<YoloObbCandidate> ordered, System.Threading.CancellationToken cancellationToken)
        {
            var kept = new List<YoloObbCandidate>(Math.Min(ordered.Count, Options.MaximumDetections));
            // Ultralytics fast_nms compares every candidate with all higher-scored rows, including rows that are themselves suppressed. / Ultralytics fast_nms 将每个候选与所有更高分行比较，包括自身已被抑制的行。
            for (int candidateIndex = 0; candidateIndex < ordered.Count && kept.Count < Options.MaximumDetections; candidateIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                YoloObbCandidate candidate = ordered[candidateIndex];
                bool suppressed = false;
                for (int priorIndex = 0; priorIndex < candidateIndex; priorIndex++)
                {
                    if ((priorIndex & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    YoloObbCandidate prior = ordered[priorIndex];
                    if (Options.NmsMode == DetectionNmsMode.ClassAware && prior.ClassIndex != candidate.ClassIndex) continue;
                    if (ProbabilisticIou(prior, candidate) >= Options.IouThreshold) { suppressed = true; break; }
                }
                if (!suppressed) kept.Add(candidate);
            }
            return kept;
        }

        private static float ProbabilisticIou(YoloObbCandidate first, YoloObbCandidate second)
        {
            const double epsilon = 1e-7d;
            double a1; double b1; double c1; Covariance(first, out a1, out b1, out c1);
            double a2; double b2; double c2; Covariance(second, out a2, out b2, out c2);
            double dx = first.CenterX - second.CenterX;
            double dy = first.CenterY - second.CenterY;
            double denominator = ((a1 + a2) * (b1 + b2)) - ((c1 + c2) * (c1 + c2)) + epsilon;
            double t1 = (((a1 + a2) * dy * dy) + ((b1 + b2) * dx * dx)) / denominator * 0.25d;
            double t2 = ((c1 + c2) * (second.CenterX - first.CenterX) * (first.CenterY - second.CenterY)) / denominator * 0.5d;
            double determinant1 = Math.Max(0d, (a1 * b1) - (c1 * c1));
            double determinant2 = Math.Max(0d, (a2 * b2) - (c2 * c2));
            double numerator = ((a1 + a2) * (b1 + b2)) - ((c1 + c2) * (c1 + c2));
            double t3 = Math.Log((numerator / ((4d * Math.Sqrt(determinant1 * determinant2)) + epsilon)) + epsilon) * 0.5d;
            double distance = Math.Max(epsilon, Math.Min(100d, t1 + t2 + t3));
            double hellinger = Math.Sqrt(Math.Max(0d, 1d - Math.Exp(-distance) + epsilon));
            return (float)(1d - hellinger);
        }

        private static void Covariance(YoloObbCandidate box, out double a, out double b, out double c)
        {
            double widthVariance = box.Width * box.Width / 12d;
            double heightVariance = box.Height * box.Height / 12d;
            double cosine = Math.Cos(box.Angle);
            double sine = Math.Sin(box.Angle);
            a = (widthVariance * cosine * cosine) + (heightVariance * sine * sine);
            b = (widthVariance * sine * sine) + (heightVariance * cosine * cosine);
            c = (widthVariance - heightVariance) * cosine * sine;
        }

        private static void Regularize(ref float width, ref float height, ref float angle)
        {
            float pi = (float)Math.PI;
            angle %= pi;
            if (angle < 0) angle += pi;
            if (angle >= pi / 2f) { float swap = width; width = height; height = swap; }
            angle %= pi / 2f;
        }

        private static List<YoloObbCandidate> Take(List<YoloObbCandidate> source, int maximum)
        {
            int count = Math.Min(source.Count, maximum);
            var result = new List<YoloObbCandidate>(count);
            for (int index = 0; index < count; index++) result.Add(source[index]);
            return result;
        }
    }

    internal sealed class YoloSelectedCandidate
    {
        public YoloSelectedCandidate(int sourceIndex, int classIndex, float score, RectangleF modelBox) { SourceIndex = sourceIndex; ClassIndex = classIndex; Score = score; ModelBox = modelBox; }
        public int SourceIndex { get; }
        public int ClassIndex { get; }
        public float Score { get; }
        public RectangleF ModelBox { get; }
    }

    internal sealed class YoloObbCandidate
    {
        public YoloObbCandidate(int sourceIndex, int classIndex, float score, float centerX, float centerY, float width, float height, float angle) { SourceIndex = sourceIndex; ClassIndex = classIndex; Score = score; CenterX = centerX; CenterY = centerY; Width = width; Height = height; Angle = angle; }
        public int SourceIndex { get; }
        public int ClassIndex { get; }
        public float Score { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float Width { get; }
        public float Height { get; }
        public float Angle { get; }
        public static int Compare(YoloObbCandidate left, YoloObbCandidate right) { int score = right.Score.CompareTo(left.Score); return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex); }
    }

    internal static class YoloPackedTensor
    {
        public static int ResolveBatch(VisualDecodeContext context, ITensor tensor, YoloPackedTensorLayout layout, int candidates, int fields)
        {
            TensorShape shape = tensor.Shape;
            int expected = context.Input.BatchSize;
            bool valid = shape.Rank == 3 && shape[0] == expected
                && (layout == YoloPackedTensorLayout.AttributeMajor ? shape[1] == fields && shape[2] == candidates : shape[1] == candidates && shape[2] == fields)
                && tensor.Length == (long)expected * candidates * fields;
            if (!valid) throw Failure(context, "A YOLO packed tensor does not match the input batch and exact layout.", tensorName: null, technicalDetails: shape.ToString());
            return expected;
        }

        public static PreparedVisualInput CreateRowInput(VisualDecodeContext context, VisualInputFrame frame, int row)
        {
            TensorShape inputShape = context.Input.Tensor.Shape;
            if (inputShape.Rank < 1 || inputShape[0] != context.Input.BatchSize) throw Failure(context, "A batched YOLO input must expose its batch dimension.", context.Input.InputName, inputShape.ToString());
            long rowLengthLong = inputShape.GetElementCount() / inputShape[0];
            int rowLength = checked((int)rowLengthLong);
            float[] rowValues = new float[rowLength];
            int offset = checked(row * rowLength);
            if (context.Input.Tensor.ElementType == TensorElementType.Float32 && context.Input.Tensor.Buffer is float[] values)
            {
                if (offset < 0 || offset > values.Length - rowLength) throw Failure(context, "A YOLO input row exceeds its tensor.", context.Input.InputName, inputShape.ToString());
                Array.Copy(values, offset, rowValues, 0, rowLength);
            }
            else if (context.Input.Tensor.ElementType == TensorElementType.Float64 && context.Input.Tensor.Buffer is double[] doubles)
            {
                if (offset < 0 || offset > doubles.Length - rowLength) throw Failure(context, "A YOLO input row exceeds its tensor.", context.Input.InputName, inputShape.ToString());
                for (int index = 0; index < rowLength; index++) rowValues[index] = checked((float)doubles[offset + index]);
            }
            else throw Failure(context, "Batched YOLO inputs require Float32 or Float64 tensors.", context.Input.InputName, inputShape.ToString());
            long[] dimensions = inputShape.ToArray();
            dimensions[0] = 1;
            return new PreparedVisualInput(context.Input.InputName, new Tensor<float>(new TensorShape(dimensions), rowValues, TensorBufferOwnership.Transfer), frame.SourceSize, frame.ModelSize, 1, context.Input.Layout, frame.Transform, context.Input.Preprocessing, frame.InputId, PreparedInputOwnership.Borrowed);
        }

        public static ITensor Required(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, "A required YOLO output is missing.", name, null, exception); }
        }

        public static float[] Read(VisualDecodeContext context, ITensor tensor, string name, YoloPackedTensorLayout layout, int candidates, int fields)
        {
            ValidateShape(context, tensor, name, layout, candidates, fields);
            return VisualTensorReader.ReadFiniteScores(tensor, context.Profile.ProfileId, name);
        }

        public static void ValidateShape(VisualDecodeContext context, ITensor tensor, string name, YoloPackedTensorLayout layout, int candidates, int fields)
        {
            TensorShape shape = tensor.Shape;
            bool valid = layout == YoloPackedTensorLayout.AttributeMajor
                ? shape.Rank == 3 && shape[0] == 1 && shape[1] == fields && shape[2] == candidates
                : shape.Rank == 3 && shape[0] == 1 && shape[1] == candidates && shape[2] == fields;
            if (!valid || tensor.Length != (long)candidates * fields) throw Failure(context, "A packed YOLO tensor does not match its exact layout and shape.", name, shape.ToString());
        }

        public static float Value(float[] values, int candidates, int fields, int candidate, int field, YoloPackedTensorLayout layout)
            => layout == YoloPackedTensorLayout.AttributeMajor ? values[(field * candidates) + candidate] : values[(candidate * fields) + field];

        public static float Value(float[] values, int baseOffset, int candidates, int fields, int candidate, int field, YoloPackedTensorLayout layout)
            => layout == YoloPackedTensorLayout.AttributeMajor ? values[baseOffset + (field * candidates) + candidate] : values[baseOffset + (candidate * fields) + field];

        public static float Probability(float value, VisualDecodeContext context, string tensorName, int candidate, string field)
        {
            if (value < 0 || value > 1) throw Failure(context, "A YOLO probability is outside [0,1].", tensorName, "candidate=" + candidate + ";field=" + field);
            return value;
        }

        public static int ClassIndex(float value, int classCount, VisualDecodeContext context, string tensorName, int candidate)
        {
            if (value < 0 || value >= classCount || value != (float)Math.Floor(value)) throw Failure(context, "A YOLO class index is outside its exact label set.", tensorName, "candidate=" + candidate + ";class=" + value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return checked((int)value);
        }

        public static int CompareCandidates(VisualDetectionCandidate left, VisualDetectionCandidate right) { int score = right.Score.CompareTo(left.Score); return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex); }

        public static VisualException Failure(VisualDecodeContext context, string message, string? tensorName = null, string? technicalDetails = null, Exception? exception = null)
            => new VisualException(VisualErrorCodes.YoloContractInvalid, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);
    }
}
