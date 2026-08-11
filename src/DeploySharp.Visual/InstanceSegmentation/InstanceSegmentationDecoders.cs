using System;
using System.Collections.Generic;
using System.Threading;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Decodes strict named per-candidate masks after score filtering and deterministic box NMS. / 在分数筛选和确定性边界框 NMS 后解码严格命名的逐候选掩码。</summary>
    public sealed class DirectInstanceSegmentationDecoder : IVisualDecoder
    {
        /// <summary>Initializes a direct instance-mask decoder. / 初始化直接实例掩码解码器。</summary>
        public DirectInstanceSegmentationDecoder(DirectInstanceSegmentationOutputSchema schema, InstanceSegmentationDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new InstanceSegmentationDecoderOptions();
            InstanceSegmentationDecoding.ValidateThresholds(Schema.Candidates, Schema.ValueKind, Schema.Activation, Options);
        }

        /// <inheritdoc />
        /// <remarks>The task identifier is immutable and backend-neutral. / 任务标识符不可变且与后端无关。</remarks>
        public VisualTaskId Task => VisualTaskId.InstanceSegmentation;
        /// <summary>Gets exact direct-mask output semantics. / 获取精确的直接掩码输出语义。</summary>
        public DirectInstanceSegmentationOutputSchema Schema { get; }
        /// <summary>Gets bounded decoding and overlap options. / 获取有界解码及重叠选项。</summary>
        public InstanceSegmentationDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>NMS precedes mask materialization; returned masks own source-space bytes and never borrow backend tensors. / NMS 先于掩码实体化；返回掩码拥有源图空间字节且绝不借用后端张量。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.DecodeFailed, "Instance segmentation currently requires batch size one.", Schema.MasksOutputName);
            if (context.Outputs.Count != 4) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Direct instance segmentation requires exactly four declared outputs.", Schema.MasksOutputName);

            InstanceCandidateBatch batch = InstanceSegmentationDecoding.ReadCandidates(context, Schema.Candidates, Options);
            ITensor masksTensor = InstanceSegmentationDecoding.Required(context, Schema.MasksOutputName);
            DirectMaskDimensions dimensions = ResolveDimensions(masksTensor, batch.CandidateCount, context);
            InstanceSegmentationDecoding.EnsureTensorMaskBound(context, checked((long)batch.CandidateCount * dimensions.Height * dimensions.Width), Options, Schema.MasksOutputName);
            InstanceSegmentationDecoding.EnsureConversionWorkspace(context, masksTensor, Options, Schema.MasksOutputName);
            float[] maskValues = VisualTensorReader.ReadFiniteScores(masksTensor, context.Profile.ProfileId, Schema.MasksOutputName);
            InstanceSegmentationDecoding.ValidateDeclaredValues(maskValues, Schema.ValueKind, context, Schema.MasksOutputName, context.CancellationToken);

            List<VisualDetectionCandidate> kept = InstanceSegmentationDecoding.ApplyNms(batch.Candidates, Options, context.CancellationToken);
            InstanceSegmentationDecoding.EnsureResultBound(context, kept.Count, Options);
            int plane = checked(dimensions.Width * dimensions.Height);
            var instances = new List<InstanceSegmentationInstance>(kept.Count);
            for (int instanceIndex = 0; instanceIndex < kept.Count; instanceIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                VisualDetectionCandidate candidate = kept[instanceIndex];
                int offset = checked(candidate.SourceIndex * plane);
                InstanceBinaryMask mask = InstanceMaskRestorer.Restore(
                    maskValues, offset, dimensions.Width, dimensions.Height, context.Input, candidate.ModelBox,
                    Schema.ValueKind, Schema.Activation, Schema.Interpolation, Schema.ThresholdOrder, Schema.CropSpace, Schema.CropOrder,
                    Options.MaskThreshold, context.CancellationToken);
                InstanceMaskRle? rle = InstanceSegmentationDecoding.EncodeRle(mask, Options, context, Schema.MasksOutputName);
                instances.Add(new InstanceSegmentationInstance(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, candidate.SourceBox, mask, rle));
            }

            return InstanceSegmentationDecoding.CreateResult(instances, context, Options);
        }

        private DirectMaskDimensions ResolveDimensions(ITensor tensor, int candidates, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            long height;
            long width;
            bool valid;
            if (Schema.Layout == InstanceMaskTensorLayout.Nchw)
            {
                valid = shape.Rank == 4 && shape[0] == 1 && shape[1] == candidates;
                height = valid ? shape[2] : 0;
                width = valid ? shape[3] : 0;
            }
            else
            {
                valid = shape.Rank == 5 && shape[0] == 1 && shape[1] == candidates && shape[4] == 1;
                height = valid ? shape[2] : 0;
                width = valid ? shape[3] : 0;
            }

            if (!valid || height <= 0 || width <= 0 || tensor.Length != (long)candidates * height * width) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Direct masks must match [1,N,H,W] or [1,N,H,W,1] exactly.", Schema.MasksOutputName, shape.ToString());
            try { return new DirectMaskDimensions(checked((int)width), checked((int)height)); }
            catch (OverflowException exception) { throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Direct mask dimensions exceed Int32 bounds.", Schema.MasksOutputName, shape.ToString(), exception); }
        }
    }

    /// <summary>Decodes strict prototype/coefficient masks after bounded candidate filtering and deterministic box NMS. / 在有界候选筛选和确定性边界框 NMS 后解码严格的原型/系数掩码。</summary>
    public sealed class PrototypeInstanceSegmentationDecoder : IVisualDecoder
    {
        /// <summary>Initializes a prototype/coefficient instance-mask decoder. / 初始化原型/系数实例掩码解码器。</summary>
        public PrototypeInstanceSegmentationDecoder(PrototypeInstanceSegmentationOutputSchema schema, InstanceSegmentationDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? new InstanceSegmentationDecoderOptions();
            InstanceSegmentationDecoding.ValidateThresholds(Schema.Candidates, Schema.CombinationValueKind, Schema.Activation, Options);
        }

        /// <inheritdoc />
        /// <remarks>The task identifier is immutable and backend-neutral. / 任务标识符不可变且与后端无关。</remarks>
        public VisualTaskId Task => VisualTaskId.InstanceSegmentation;
        /// <summary>Gets exact prototype and coefficient semantics. / 获取精确的原型和系数语义。</summary>
        public PrototypeInstanceSegmentationOutputSchema Schema { get; }
        /// <summary>Gets bounded decoding and overlap options. / 获取有界解码及重叠选项。</summary>
        public InstanceSegmentationDecoderOptions Options { get; }

        /// <inheritdoc />
        /// <remarks>Only candidates retained by NMS receive a bounded linear-combination workspace. / 仅为 NMS 保留的候选分配有界线性组合工作区。</remarks>
        public object Decode(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.DecodeFailed, "Instance segmentation currently requires batch size one.", Schema.PrototypesOutputName);
            if (context.Outputs.Count != 5) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Prototype instance segmentation requires exactly five declared outputs.", Schema.PrototypesOutputName);

            InstanceCandidateBatch batch = InstanceSegmentationDecoding.ReadCandidates(context, Schema.Candidates, Options);
            ITensor prototypesTensor = InstanceSegmentationDecoding.Required(context, Schema.PrototypesOutputName);
            PrototypeDimensions dimensions = ResolvePrototypeDimensions(prototypesTensor, context);
            if (dimensions.Channels > Options.MaximumPrototypeChannels) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.DecodeFailed, "Prototype channel count exceeds the configured bound.", Schema.PrototypesOutputName, "channels=" + dimensions.Channels);
            InstanceSegmentationDecoding.EnsureTensorMaskBound(context, checked((long)dimensions.Channels * dimensions.Height * dimensions.Width), Options, Schema.PrototypesOutputName);
            ITensor coefficientsTensor = InstanceSegmentationDecoding.Required(context, Schema.CoefficientsOutputName);
            ValidateCoefficients(coefficientsTensor, batch.CandidateCount, dimensions.Channels, context);
            int plane = checked(dimensions.Width * dimensions.Height);
            long combinationBytes = checked((long)plane * sizeof(float));
            InstanceSegmentationDecoding.EnsureCombinedWorkspace(context, Options, combinationBytes, prototypesTensor, coefficientsTensor);
            float[] prototypes = VisualTensorReader.ReadFiniteScores(prototypesTensor, context.Profile.ProfileId, Schema.PrototypesOutputName);
            float[] coefficients = VisualTensorReader.ReadFiniteScores(coefficientsTensor, context.Profile.ProfileId, Schema.CoefficientsOutputName);

            List<VisualDetectionCandidate> kept = InstanceSegmentationDecoding.ApplyNms(batch.Candidates, Options, context.CancellationToken);
            InstanceSegmentationDecoding.EnsureResultBound(context, kept.Count, Options);
            var instances = new List<InstanceSegmentationInstance>(kept.Count);
            for (int instanceIndex = 0; instanceIndex < kept.Count; instanceIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                VisualDetectionCandidate candidate = kept[instanceIndex];
                float[] combined = Combine(prototypes, coefficients, candidate.SourceIndex, dimensions, context);
                InstanceSegmentationDecoding.ValidateDeclaredValues(combined, Schema.CombinationValueKind, context, Schema.PrototypesOutputName, context.CancellationToken);
                InstanceBinaryMask mask = InstanceMaskRestorer.Restore(
                    combined, 0, dimensions.Width, dimensions.Height, context.Input, candidate.ModelBox,
                    Schema.CombinationValueKind, Schema.Activation, Schema.Interpolation, Schema.ThresholdOrder, Schema.CropSpace, Schema.CropOrder,
                    Options.MaskThreshold, context.CancellationToken);
                InstanceMaskRle? rle = InstanceSegmentationDecoding.EncodeRle(mask, Options, context, Schema.PrototypesOutputName);
                instances.Add(new InstanceSegmentationInstance(candidate.SourceIndex, candidate.ClassIndex, context.Profile.GetLabel(candidate.ClassIndex), candidate.Score, candidate.SourceBox, mask, rle));
            }

            return InstanceSegmentationDecoding.CreateResult(instances, context, Options);
        }

        private PrototypeDimensions ResolvePrototypeDimensions(ITensor tensor, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 4 || shape[0] != 1) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Prototype masks must have rank four and batch one.", Schema.PrototypesOutputName, shape.ToString());
            long channels = Schema.PrototypeLayout == InstanceMaskTensorLayout.Nchw ? shape[1] : shape[3];
            long height = Schema.PrototypeLayout == InstanceMaskTensorLayout.Nchw ? shape[2] : shape[1];
            long width = Schema.PrototypeLayout == InstanceMaskTensorLayout.Nchw ? shape[3] : shape[2];
            long expectedElements;
            try { expectedElements = checked(checked(channels * height) * width); }
            catch (OverflowException exception) { throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Prototype element count exceeds Int64 bounds.", Schema.PrototypesOutputName, shape.ToString(), exception); }
            if (channels <= 0 || height <= 0 || width <= 0 || tensor.Length != expectedElements) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Prototype dimensions are empty or inconsistent.", Schema.PrototypesOutputName, shape.ToString());
            try { return new PrototypeDimensions(checked((int)channels), checked((int)width), checked((int)height)); }
            catch (OverflowException exception) { throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Prototype dimensions exceed Int32 bounds.", Schema.PrototypesOutputName, shape.ToString(), exception); }
        }

        private void ValidateCoefficients(ITensor tensor, int candidates, int channels, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 3 || shape[0] != 1 || shape[1] != candidates || shape[2] != channels || tensor.Length != (long)candidates * channels) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.TensorInvalid, "Coefficients must match [1,N,C] and the prototype channel count exactly.", Schema.CoefficientsOutputName, shape.ToString());
        }

        private float[] Combine(float[] prototypes, float[] coefficients, int candidateIndex, PrototypeDimensions dimensions, VisualDecodeContext context)
        {
            int plane = checked(dimensions.Width * dimensions.Height);
            var combined = new float[plane];
            int coefficientOffset = checked(candidateIndex * dimensions.Channels);
            // NCHW favors channel-major accumulation; NHWC favors position-major dot products. / NCHW 采用通道优先累加，NHWC 采用位置优先点积。
            if (Schema.PrototypeLayout == InstanceMaskTensorLayout.Nchw)
            {
                for (int channel = 0; channel < dimensions.Channels; channel++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    float coefficient = coefficients[coefficientOffset + channel];
                    int prototypeOffset = checked(channel * plane);
                    for (int position = 0; position < plane; position++) combined[position] += coefficient * prototypes[prototypeOffset + position];
                }
            }
            else
            {
                for (int position = 0; position < plane; position++)
                {
                    if ((position & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    int prototypeOffset = checked(position * dimensions.Channels);
                    double sum = 0;
                    for (int channel = 0; channel < dimensions.Channels; channel++) sum += coefficients[coefficientOffset + channel] * prototypes[prototypeOffset + channel];
                    combined[position] = checked((float)sum);
                }
            }

            for (int position = 0; position < combined.Length; position++) if (float.IsNaN(combined[position]) || float.IsInfinity(combined[position])) throw InstanceSegmentationDecoding.Failure(context, VisualErrorCodes.DecodeFailed, "Prototype linear combination produced a non-finite mask value.", Schema.PrototypesOutputName, "position=" + position);
            return combined;
        }
    }

    internal sealed class InstanceCandidateBatch
    {
        public InstanceCandidateBatch(int candidateCount, List<VisualDetectionCandidate> candidates) { CandidateCount = candidateCount; Candidates = candidates; }
        public int CandidateCount { get; }
        public List<VisualDetectionCandidate> Candidates { get; }
    }

    internal readonly struct DirectMaskDimensions
    {
        public DirectMaskDimensions(int width, int height) { Width = width; Height = height; }
        public int Width { get; }
        public int Height { get; }
    }

    internal readonly struct PrototypeDimensions
    {
        public PrototypeDimensions(int channels, int width, int height) { Channels = channels; Width = width; Height = height; }
        public int Channels { get; }
        public int Width { get; }
        public int Height { get; }
    }

    internal static class InstanceSegmentationDecoding
    {
        public static void ValidateThresholds(InstanceSegmentationCandidateSchema candidates, InstanceMaskValueKind valueKind, InstanceMaskActivation activation, InstanceSegmentationDecoderOptions options)
        {
            if (candidates.ScoreKind == InstanceScoreKind.Probability && options.ScoreThreshold > 1) throw new ArgumentOutOfRangeException(nameof(options), "Probability score thresholds must be in [0,1].");
            if ((valueKind != InstanceMaskValueKind.Logits || activation == InstanceMaskActivation.Sigmoid) && (options.MaskThreshold < 0 || options.MaskThreshold > 1)) throw new ArgumentOutOfRangeException(nameof(options), "Probability or binary mask thresholds must be in [0,1].");
        }

        public static InstanceCandidateBatch ReadCandidates(VisualDecodeContext context, InstanceSegmentationCandidateSchema schema, InstanceSegmentationDecoderOptions options)
        {
            ITensor boxesTensor = Required(context, schema.BoxesOutputName);
            TensorShape boxesShape = boxesTensor.Shape;
            if (boxesShape.Rank != 3 || boxesShape[0] != 1 || boxesShape[2] != 4) throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance boxes must have shape [1,N,4].", schema.BoxesOutputName, boxesShape.ToString());
            int candidates;
            try { candidates = checked((int)boxesShape[1]); }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance candidate count exceeds Int32 bounds.", schema.BoxesOutputName, boxesShape.ToString(), exception); }
            if (candidates < 0 || candidates > options.MaximumCandidates || boxesTensor.Length != (long)candidates * 4) throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance candidate count exceeds its bound or box element count is inconsistent.", schema.BoxesOutputName, "candidates=" + candidates);
            ITensor scoresTensor = Required(context, schema.ScoresOutputName);
            ITensor classesTensor = Required(context, schema.ClassesOutputName);
            ValidateVector(scoresTensor, candidates, context, schema.ScoresOutputName);
            ValidateVector(classesTensor, candidates, context, schema.ClassesOutputName);
            EnsureCombinedWorkspace(context, options, 0, boxesTensor, scoresTensor, classesTensor);
            float[] boxes = VisualTensorReader.ReadFiniteScores(boxesTensor, context.Profile.ProfileId, schema.BoxesOutputName);
            float[] scores = VisualTensorReader.ReadFiniteScores(scoresTensor, context.Profile.ProfileId, schema.ScoresOutputName);
            float[] classes = VisualTensorReader.ReadFiniteScores(classesTensor, context.Profile.ProfileId, schema.ClassesOutputName);
            var decoded = new List<VisualDetectionCandidate>(Math.Min(candidates, options.MaximumInstances));
            for (int candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                float score = scores[candidateIndex];
                if (score < 0 || (schema.ScoreKind == InstanceScoreKind.Probability && score > 1)) throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance score violates its declared numeric semantics.", schema.ScoresOutputName, "candidate=" + candidateIndex);
                float classValue = classes[candidateIndex];
                if (classValue < 0 || classValue > int.MaxValue || classValue != (float)Math.Floor(classValue)) throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance class values must be non-negative integers.", schema.ClassesOutputName, "candidate=" + candidateIndex);
                int classIndex = checked((int)classValue);
                int offset = checked(candidateIndex * 4);
                RectangleF modelBox;
                try { modelBox = DetectionPostprocessing.DecodeModelBox(schema.BoxFormat, schema.NormalizedBoxes, context.Input.ModelSize, boxes[offset], boxes[offset + 1], boxes[offset + 2], boxes[offset + 3]); }
                catch (ArgumentOutOfRangeException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance box has negative width or height.", schema.BoxesOutputName, "candidate=" + candidateIndex, exception); }
                if (score < options.ScoreThreshold || modelBox.Width == 0 || modelBox.Height == 0) continue;
                RectangleF sourceBox = context.Input.Transform.ClipToSource(context.Input.Transform.ToSource(modelBox));
                if (sourceBox.Width <= 0 || sourceBox.Height <= 0) continue;
                decoded.Add(new VisualDetectionCandidate(candidateIndex, classIndex, score, modelBox, sourceBox));
            }

            decoded.Sort(CompareCandidates);
            return new InstanceCandidateBatch(candidates, decoded);
        }

        public static List<VisualDetectionCandidate> ApplyNms(List<VisualDetectionCandidate> ordered, InstanceSegmentationDecoderOptions options, CancellationToken cancellationToken)
            => DetectionPostprocessing.Suppress(ordered, options.IouThreshold, options.NmsMode, options.MaximumInstances, cancellationToken);

        public static void ValidateDeclaredValues(float[] values, InstanceMaskValueKind kind, VisualDecodeContext context, string tensorName, CancellationToken cancellationToken)
        {
            if (kind == InstanceMaskValueKind.Logits) return;
            for (int index = 0; index < values.Length; index++)
            {
                if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                float value = values[index];
                bool valid = kind == InstanceMaskValueKind.Probabilities ? value >= 0 && value <= 1 : value == 0 || value == 1;
                if (!valid) throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance mask value violates its declared numeric semantics.", tensorName, "index=" + index);
            }
        }

        public static void EnsureTensorMaskBound(VisualDecodeContext context, long pixels, InstanceSegmentationDecoderOptions options, string tensorName)
        {
            if (pixels < 0 || pixels > options.MaximumMaskPixels) throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance mask tensor positions exceed the configured bound.", tensorName, "maskPixels=" + pixels);
        }

        public static void EnsureConversionWorkspace(VisualDecodeContext context, ITensor tensor, InstanceSegmentationDecoderOptions options, string tensorName)
        {
            if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance segmentation outputs require Float32 or Float64 tensors.", tensorName);
            if (tensor.ElementType == TensorElementType.Float64)
            {
                long bytes;
                try { bytes = checked(tensor.Length * sizeof(float)); }
                catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Tensor conversion workspace size overflowed.", tensorName, null, exception); }
                if (bytes > options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "Tensor conversion workspace exceeds the configured bound.", tensorName, "workspaceBytes=" + bytes);
            }
        }

        public static void EnsureCombinedWorkspace(VisualDecodeContext context, InstanceSegmentationDecoderOptions options, long additionalBytes, params ITensor[] tensors)
        {
            try
            {
                long bytes = additionalBytes;
                for (int index = 0; index < tensors.Length; index++)
                {
                    ITensor tensor = tensors[index];
                    if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance segmentation outputs require Float32 or Float64 tensors.");
                    if (tensor.ElementType == TensorElementType.Float64) bytes = checked(bytes + checked(tensor.Length * sizeof(float)));
                }
                if (bytes > options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "Combined instance decoder workspace exceeds the configured bound.", technicalDetails: "workspaceBytes=" + bytes);
            }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Combined instance decoder workspace size overflowed.", technicalDetails: exception.ToString(), exception: exception); }
        }

        public static void EnsureResultBound(VisualDecodeContext context, int instances, InstanceSegmentationDecoderOptions options)
        {
            try
            {
                long sourcePixels = checked((long)context.Input.SourceSize.Width * context.Input.SourceSize.Height);
                long densePixels = checked(sourcePixels * instances);
                if (densePixels > options.MaximumMaskPixels) throw Failure(context, VisualErrorCodes.DecodeFailed, "Retained source-space mask pixels exceed the configured bound.", technicalDetails: "maskPixels=" + densePixels);
                long bytes = densePixels;
                if (options.GenerateRle)
                {
                    long maximumRunsPerMask = Math.Min(options.MaximumRleRuns, (sourcePixels + 1) / 2);
                    bytes = checked(bytes + checked(maximumRunsPerMask * instances * 8L));
                }
                if (options.OverlapMode == InstanceMaskOverlapMode.ScorePriorityOwnership) bytes = checked(bytes + checked(sourcePixels * sizeof(int)));
                bytes = checked(bytes + (instances * 256L));
                if (bytes > options.MaximumResultBytes) throw Failure(context, VisualErrorCodes.DecodeFailed, "Estimated instance segmentation result exceeds the configured byte bound.", technicalDetails: "estimatedBytes=" + bytes);
            }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance segmentation result size estimation overflowed.", technicalDetails: exception.ToString(), exception: exception); }
        }

        public static InstanceMaskRle? EncodeRle(InstanceBinaryMask mask, InstanceSegmentationDecoderOptions options, VisualDecodeContext context, string tensorName)
        {
            if (!options.GenerateRle) return null;
            try { return InstanceMaskRle.Encode(mask, options.MaximumRleRuns, context.CancellationToken); }
            catch (InvalidOperationException exception) { throw Failure(context, VisualErrorCodes.DecodeFailed, "Instance mask exceeds the configured RLE run bound.", tensorName, null, exception); }
        }

        public static InstanceSegmentationResult CreateResult(List<InstanceSegmentationInstance> instances, VisualDecodeContext context, InstanceSegmentationDecoderOptions options)
        {
            InstanceMaskOwnershipMap? ownership = null;
            if (options.OverlapMode == InstanceMaskOverlapMode.ScorePriorityOwnership)
            {
                int pixels = checked(context.Input.SourceSize.Width * context.Input.SourceSize.Height);
                var owners = new int[pixels];
                for (int index = 0; index < owners.Length; index++) owners[index] = -1;
                // Instances are already score-descending with source-index tie breaking; first foreground wins. / 实例已按分数降序并以源索引打破同分，首个前景实例获得所有权。
                for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    InstanceBinaryMask mask = instances[instanceIndex].Mask;
                    for (int pixel = 0; pixel < pixels; pixel++)
                    {
                        if ((pixel & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                        if (owners[pixel] < 0 && mask.GetPixelUnchecked(pixel) != 0) owners[pixel] = instanceIndex;
                    }
                }
                ownership = new InstanceMaskOwnershipMap(context.Input.SourceSize.Width, context.Input.SourceSize.Height, instances.Count, owners, true);
            }

            return new InstanceSegmentationResult(instances, context.Input.SourceSize, context.Profile.ProfileId, context.Profile.ModelId, options.OverlapMode, ownership);
        }

        public static ITensor Required(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.TensorInvalid, "A required instance segmentation output is missing.", name, null, exception); }
        }

        public static VisualException Failure(VisualDecodeContext context, string code, string message, string? tensorName = null, string? technicalDetails = null, Exception? exception = null)
            => new VisualException(code, message, exception, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);

        private static void ValidateVector(ITensor tensor, int candidates, VisualDecodeContext context, string name)
        {
            TensorShape shape = tensor.Shape;
            if (shape.Rank != 2 || shape[0] != 1 || shape[1] != candidates || tensor.Length != candidates) throw Failure(context, VisualErrorCodes.TensorInvalid, "Instance score and class outputs must match [1,N] exactly.", name, shape.ToString());
        }

        private static int CompareCandidates(VisualDetectionCandidate left, VisualDetectionCandidate right)
        {
            int score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.SourceIndex.CompareTo(right.SourceIndex);
        }
    }

    internal static class InstanceMaskRestorer
    {
        public static InstanceBinaryMask Restore(
            float[] grid,
            int gridOffset,
            int gridWidth,
            int gridHeight,
            PreparedVisualInput input,
            RectangleF modelBox,
            InstanceMaskValueKind valueKind,
            InstanceMaskActivation activation,
            InstanceMaskInterpolationMode interpolation,
            InstanceMaskThresholdOrder thresholdOrder,
            InstanceMaskCropSpace cropSpace,
            InstanceMaskCropOrder cropOrder,
            float threshold,
            CancellationToken cancellationToken,
            bool thresholdIsStrict = false)
        {
            int sourceWidth = input.SourceSize.Width;
            int sourceHeight = input.SourceSize.Height;
            var result = new byte[checked(sourceWidth * sourceHeight)];
            int foreground = 0;
            for (int sourceY = 0; sourceY < sourceHeight; sourceY++)
            {
                if ((sourceY & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                float modelY = ((sourceY + 0.5f) * input.Transform.ScaleY) + input.Transform.OffsetY;
                for (int sourceX = 0; sourceX < sourceWidth; sourceX++)
                {
                    float modelX = ((sourceX + 0.5f) * input.Transform.ScaleX) + input.Transform.OffsetX;
                    int destination = (sourceY * sourceWidth) + sourceX;
                    if (modelX < 0 || modelX >= input.ModelSize.Width || modelY < 0 || modelY >= input.ModelSize.Height) continue;
                    if (cropSpace == InstanceMaskCropSpace.ModelInput && cropOrder == InstanceMaskCropOrder.AfterResize && !Contains(modelBox, modelX, modelY)) continue;
                    float sampled = Sample(grid, gridOffset, gridWidth, gridHeight, input.ModelSize, modelBox, modelX, modelY, valueKind, activation, interpolation, thresholdOrder, cropSpace, cropOrder, threshold);
                    float comparisonThreshold = thresholdOrder == InstanceMaskThresholdOrder.BeforeResize ? 0.5f : threshold;
                    if (thresholdIsStrict ? sampled > comparisonThreshold : sampled >= comparisonThreshold)
                    {
                        result[destination] = 1;
                        foreground++;
                    }
                }
            }

            return new InstanceBinaryMask(sourceWidth, sourceHeight, result, InstanceMaskCoordinateSpace.SourceImage, 0, 0, foreground);
        }

        private static float Sample(float[] grid, int offset, int width, int height, VisualSize modelSize, RectangleF modelBox, float modelX, float modelY, InstanceMaskValueKind kind, InstanceMaskActivation activation, InstanceMaskInterpolationMode interpolation, InstanceMaskThresholdOrder thresholdOrder, InstanceMaskCropSpace cropSpace, InstanceMaskCropOrder cropOrder, float threshold)
        {
            if (interpolation == InstanceMaskInterpolationMode.NearestNeighbor)
            {
                int x = Math.Min(width - 1, Math.Max(0, (int)Math.Floor(modelX * width / modelSize.Width)));
                int y = Math.Min(height - 1, Math.Max(0, (int)Math.Floor(modelY * height / modelSize.Height)));
                float value = Read(grid, offset, width, height, x, y, modelSize, modelBox, kind, activation, cropSpace, cropOrder);
                return thresholdOrder == InstanceMaskThresholdOrder.BeforeResize ? (value >= threshold ? 1f : 0f) : value;
            }

            float gridX;
            float gridY;
            if (interpolation == InstanceMaskInterpolationMode.BilinearAlignCorners)
            {
                gridX = width == 1 || modelSize.Width == 1 ? 0 : (modelX - 0.5f) * (width - 1f) / (modelSize.Width - 1f);
                gridY = height == 1 || modelSize.Height == 1 ? 0 : (modelY - 0.5f) * (height - 1f) / (modelSize.Height - 1f);
            }
            else
            {
                gridX = (modelX * width / modelSize.Width) - 0.5f;
                gridY = (modelY * height / modelSize.Height) - 0.5f;
            }

            int x0 = (int)Math.Floor(gridX);
            int y0 = (int)Math.Floor(gridY);
            float xWeight = gridX - x0;
            float yWeight = gridY - y0;
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            x0 = Math.Min(width - 1, Math.Max(0, x0));
            x1 = Math.Min(width - 1, Math.Max(0, x1));
            y0 = Math.Min(height - 1, Math.Max(0, y0));
            y1 = Math.Min(height - 1, Math.Max(0, y1));
            float top = Read(grid, offset, width, height, x0, y0, modelSize, modelBox, kind, activation, cropSpace, cropOrder) * (1 - xWeight)
                + Read(grid, offset, width, height, x1, y0, modelSize, modelBox, kind, activation, cropSpace, cropOrder) * xWeight;
            float bottom = Read(grid, offset, width, height, x0, y1, modelSize, modelBox, kind, activation, cropSpace, cropOrder) * (1 - xWeight)
                + Read(grid, offset, width, height, x1, y1, modelSize, modelBox, kind, activation, cropSpace, cropOrder) * xWeight;
            return top * (1 - yWeight) + bottom * yWeight;
        }

        private static float Read(float[] grid, int offset, int width, int height, int x, int y, VisualSize modelSize, RectangleF modelBox, InstanceMaskValueKind kind, InstanceMaskActivation activation, InstanceMaskCropSpace cropSpace, InstanceMaskCropOrder cropOrder)
        {
            if (cropSpace == InstanceMaskCropSpace.ModelInput && cropOrder == InstanceMaskCropOrder.BeforeResize)
            {
                float modelX = ((x + 0.5f) * modelSize.Width) / width;
                float modelY = ((y + 0.5f) * modelSize.Height) / height;
                if (!Contains(modelBox, modelX, modelY)) return 0;
            }

            float value = grid[offset + (y * width) + x];
            if (activation == InstanceMaskActivation.Sigmoid)
            {
                if (value >= 0) return 1f / (1f + (float)Math.Exp(-value));
                float exponential = (float)Math.Exp(value);
                return exponential / (1f + exponential);
            }

            return value;
        }

        private static bool Contains(RectangleF box, float x, float y) => x >= box.X && x < box.Right && y >= box.Y && y < box.Bottom;
    }
}
