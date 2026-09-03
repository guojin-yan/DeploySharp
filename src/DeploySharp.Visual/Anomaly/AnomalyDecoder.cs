using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Results;
using JYPPX.DeploySharp.Tensors;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Decodes strict image-score and pixel-map outputs into an owned anomaly result. / 将严格的图像分数与像素图输出解码为自有异常结果。</summary>
    public sealed class AnomalyDecoder : IAnomalyPostprocessor
    {
        /// <summary>Initializes an anomaly decoder. / 初始化异常解码器。</summary>
        public AnomalyDecoder(AnomalyMapSchema schema, AnomalyDecoderOptions? options = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Options = options ?? AnomalyDecoderOptions.Default;
            if (Options.ChannelAggregation == AnomalyChannelAggregation.SingleChannel && Options.ChannelIndex >= Schema.ChannelCount) throw new ArgumentException("The selected anomaly channel exceeds the schema channel count.", nameof(options));
            if (Options.Normalization == AnomalyNormalizationMode.None && (Schema.ValueMode == AnomalyMapValueMode.Probabilities || Schema.ValueMode == AnomalyMapValueMode.Binary) && (Options.Threshold < 0f || Options.Threshold > 1f)) throw new ArgumentException("A probability or binary threshold must be in [0,1].", nameof(options));
        }

        /// <summary>Gets the immutable output schema. / 获取不可变输出 Schema。</summary>
        public AnomalyMapSchema Schema { get; }
        /// <summary>Gets immutable bounded decoder options. / 获取不可变有界解码选项。</summary>
        public AnomalyDecoderOptions Options { get; }
        /// <inheritdoc />
        /// <remarks>Produces anomaly-detection and anomaly-segmentation results. / 生成异常检测与异常分割结果。</remarks>
        public VisualTaskId Task => VisualTaskId.AnomalyDetection;

        /// <inheritdoc />
        /// <remarks>Returns an owned anomaly result, or an ordered batch result when the profile accepts a dynamic batch. / 返回自有异常结果；当 Profile 接受动态 Batch 时返回有序 Batch 结果。</remarks>
        public object Decode(VisualDecodeContext context) => DecodeAny(context);

        /// <summary>Decodes a validated visual response into an owned batch-one anomaly result. / 将已验证的视觉响应解码为自有 batch-one 异常检测结果。</summary>
        public AnomalyDetectionResult DecodeAnomaly(VisualDecodeContext context)
        {
            object decoded = DecodeAny(context);
            if (decoded is AnomalyDetectionResult result) return result;
            throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "A batch anomaly response requires Decode or the batch result type.");
        }

        internal object DecodeAny(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize > 1) return DecodeAnomalyBatch(context);
            return DecodeAnomalySingle(context);
        }

        private AnomalyDetectionResult DecodeAnomalySingle(VisualDecodeContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (context.Input.BatchSize != 1) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "Anomaly row decoding requires input batch size one.");
            if (Options.ThresholdPolicy != AnomalyThresholdPolicy.Fixed) throw Failure(context, VisualErrorCodes.AnomalyCapabilityUnavailable, "Only a fixed anomaly threshold is currently supported.", technicalDetails: "thresholdPolicy=" + Options.ThresholdPolicy);
            if (context.Outputs.Count != 2) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly response must contain exactly the configured score and map outputs.", technicalDetails: "outputCount=" + context.Outputs.Count);
            for (int index = 0; index < context.Outputs.Count; index++)
            {
                string name = context.Outputs[index].Name;
                if (!string.Equals(name, Schema.ScoreOutputName, StringComparison.Ordinal) && !string.Equals(name, Schema.MapOutputName, StringComparison.Ordinal)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly response contains an unexpected output.", tensorName: name);
            }

            ITensor scoreTensor = RequireOutput(context, Schema.ScoreOutputName);
            ITensor mapTensor = RequireOutput(context, Schema.MapOutputName);
            float imageScore = ReadImageScore(scoreTensor, context);
            MapDimensions dimensions = ResolveDimensions(mapTensor, context);
            VisualSize targetSize = ResolveTargetSize(context, dimensions);
            EnsureBounded(context, mapTensor, dimensions, new[] { context.Input.BatchFrames[0] });

            float[] sourceValues = ReadMapValues(mapTensor, context);
            float[] aggregated = Aggregate(sourceValues, dimensions, context);
            var warnings = new List<PredictionWarning>();
            // When no normalized/raw dual view is requested, the freshly allocated
            // aggregate already has the required ownership and can be reused directly.
            // This removes a full-map clone from the common distance-map path.
            float[] normalized = Options.Normalization == AnomalyNormalizationMode.None && !Options.PreserveRawMap
                ? aggregated
                : Normalize(aggregated, warnings, context.CancellationToken);
            AnomalyScoreMap? rawMap = Options.PreserveRawMap
                ? new AnomalyScoreMap(context.Input.SourceSize, dimensions.Width, dimensions.Height, aggregated, Schema.ValueMode, AnomalyNormalizationMode.None, true)
                : null;
            float[] restored = Restore(normalized, dimensions, context);
            if (ReferenceEquals(restored, sourceValues)) restored = (float[])restored.Clone();
            var normalizedMap = new AnomalyScoreMap(context.Input.SourceSize, targetSize.Width, targetSize.Height, restored, Schema.ValueMode, Options.Normalization, true);
            byte[] maskValues = Threshold(restored, context);
            var mask = new AnomalyBinaryMask(targetSize.Width, targetSize.Height, maskValues, true);
            return new AnomalyDetectionResult(imageScore, rawMap, normalizedMap, mask, Options.Threshold, context.Input.Transform, InferenceTiming.Zero, warnings);
        }

        private AnomalyDetectionBatchResult DecodeAnomalyBatch(VisualDecodeContext context)
        {
            if (Options.ThresholdPolicy != AnomalyThresholdPolicy.Fixed) throw Failure(context, VisualErrorCodes.AnomalyCapabilityUnavailable, "Only a fixed anomaly threshold is currently supported.", technicalDetails: "thresholdPolicy=" + Options.ThresholdPolicy);
            ValidateOutputNames(context);
            ITensor scoreTensor = RequireOutput(context, Schema.ScoreOutputName);
            ITensor mapTensor = RequireOutput(context, Schema.MapOutputName);
            MapDimensions dimensions = ResolveDimensions(mapTensor, context);
            if (dimensions.Batch != context.Input.BatchSize) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map batch dimension must match the prepared input batch.", tensorName: Schema.MapOutputName, technicalDetails: mapTensor.Shape.ToString());
            if (Schema.CoordinateSpace == AnomalyMapCoordinateSpace.SourceImage)
            {
                for (int index = 0; index < context.Input.BatchFrames.Count; index++)
                {
                    VisualSize source = context.Input.BatchFrames[index].SourceSize;
                    if (source.Width != dimensions.Width || source.Height != dimensions.Height) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "A source-space anomaly map must match every batch row source size.", tensorName: Schema.MapOutputName, technicalDetails: mapTensor.Shape.ToString());
                }
            }
            int scoreStride = ResolveScoreStride(scoreTensor, dimensions.Batch, context);
            EnsureBounded(context, mapTensor, dimensions, context.Input.BatchFrames);
            float[] sourceValues = ReadMapValues(mapTensor, context);
            var results = new List<AnomalyDetectionResult>(dimensions.Batch);
            int rowElements = checked(dimensions.Channels * dimensions.Width * dimensions.Height);
            for (int batchIndex = 0; batchIndex < dimensions.Batch; batchIndex++)
            {
                if ((batchIndex & 7) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                VisualInputFrame frame = context.Input.BatchFrames[batchIndex];
                float[] rowMap = new float[rowElements];
                Array.Copy(sourceValues, checked(batchIndex * rowElements), rowMap, 0, rowElements);
                ITensor rowScore = SliceScore(scoreTensor, scoreStride, batchIndex, context);
                ITensor rowMapTensor = new Tensor<float>(MapShape(dimensions, batchDimension: 1), rowMap, TensorBufferOwnership.Transfer);
                var rowInput = new PreparedVisualInput(context.Input.InputName, context.Input.Tensor, frame.SourceSize, frame.ModelSize, 1, context.Input.Layout, frame.Transform, context.Input.Preprocessing, frame.InputId);
                var rowOutputs = new InferenceOutputs(new[] { new NamedTensor(Schema.ScoreOutputName, rowScore), new NamedTensor(Schema.MapOutputName, rowMapTensor) });
                results.Add(DecodeAnomalySingle(new VisualDecodeContext(rowInput, context.Profile, rowOutputs, context.CancellationToken)));
            }

            return new AnomalyDetectionBatchResult(results);
        }

        private void ValidateOutputNames(VisualDecodeContext context)
        {
            if (context.Outputs.Count != 2) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly response must contain exactly the configured score and map outputs.", technicalDetails: "outputCount=" + context.Outputs.Count);
            for (int index = 0; index < context.Outputs.Count; index++)
            {
                string name = context.Outputs[index].Name;
                if (!string.Equals(name, Schema.ScoreOutputName, StringComparison.Ordinal) && !string.Equals(name, Schema.MapOutputName, StringComparison.Ordinal)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly response contains an unexpected output.", tensorName: name);
            }
        }

        private int ResolveScoreStride(ITensor tensor, int batch, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            bool valid = (shape.Rank == 1 && shape[0] == batch) || (shape.Rank == 2 && shape[0] == batch && shape[1] == 1);
            if (!valid || tensor.Length != batch) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score must contain one value per batch row.", tensorName: Schema.ScoreOutputName, technicalDetails: shape.ToString());
            if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score requires Float32 or Float64.", tensorName: Schema.ScoreOutputName);
            return 1;
        }

        private ITensor SliceScore(ITensor tensor, int stride, int row, VisualDecodeContext context)
        {
            float value;
            int offset = checked(row * stride);
            if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] floats) value = floats[offset];
            else if (tensor.ElementType == TensorElementType.Float64 && tensor.Buffer is double[] doubles) value = checked((float)doubles[offset]);
            else throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score requires Float32 or Float64.", tensorName: Schema.ScoreOutputName);
            return new Tensor<float>(new TensorShape(1), new[] { value }, TensorBufferOwnership.Transfer);
        }

        private TensorShape MapShape(MapDimensions dimensions, int batchDimension)
        {
            if (Schema.Layout == AnomalyTensorLayout.Nchw) return new TensorShape(batchDimension, dimensions.Channels, dimensions.Height, dimensions.Width);
            if (Schema.Layout == AnomalyTensorLayout.Nhwc) return new TensorShape(batchDimension, dimensions.Height, dimensions.Width, dimensions.Channels);
            if (Schema.Layout == AnomalyTensorLayout.Chw) return new TensorShape(dimensions.Channels, dimensions.Height, dimensions.Width);
            return new TensorShape(dimensions.Height, dimensions.Width, dimensions.Channels);
        }

        internal AnomalyDetectionResult CreateCudaDecodedResult(
            VisualDecodeContext context,
            float imageScore,
            int rawWidth,
            int rawHeight,
            float[] rawValues,
            float[] restoredValues,
            byte[] maskValues,
            int anomalousPixelCount)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rawValues == null) throw new ArgumentNullException(nameof(rawValues));
            if (restoredValues == null) throw new ArgumentNullException(nameof(restoredValues));
            if (maskValues == null) throw new ArgumentNullException(nameof(maskValues));
            context.CancellationToken.ThrowIfCancellationRequested();
            if (float.IsNaN(imageScore) || float.IsInfinity(imageScore)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score must be finite.", tensorName: Schema.ScoreOutputName);
            if (rawWidth <= 0 || rawHeight <= 0) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "CUDA-restored anomaly raw-map dimensions are invalid.", tensorName: Schema.MapOutputName);
            if (Options.PreserveRawMap && rawValues.LongLength != checked((long)rawWidth * rawHeight)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "CUDA-restored anomaly raw-map dimensions are invalid.", tensorName: Schema.MapOutputName);
            if (!Options.PreserveRawMap && rawValues.Length != 0) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "CUDA-restored anomaly output unexpectedly retained a raw map.", tensorName: Schema.MapOutputName);
            int targetPixels = checked(context.Input.SourceSize.Width * context.Input.SourceSize.Height);
            if (restoredValues.Length != targetPixels || maskValues.Length != targetPixels) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "CUDA-restored anomaly dimensions do not match the source image.", tensorName: Schema.MapOutputName);
            AnomalyScoreMap? rawMap = Options.PreserveRawMap
                ? new AnomalyScoreMap(context.Input.SourceSize, rawWidth, rawHeight, rawValues, Schema.ValueMode, AnomalyNormalizationMode.None, true)
                : null;
            var normalizedMap = new AnomalyScoreMap(context.Input.SourceSize, context.Input.SourceSize.Width, context.Input.SourceSize.Height, restoredValues, Schema.ValueMode, Options.Normalization, true);
            var mask = new AnomalyBinaryMask(context.Input.SourceSize.Width, context.Input.SourceSize.Height, maskValues, true);
            return new AnomalyDetectionResult(imageScore, rawMap, normalizedMap, mask, Options.Threshold, context.Input.Transform, anomalousPixelCount, InferenceTiming.Zero, Array.Empty<PredictionWarning>());
        }

        private ITensor RequireOutput(VisualDecodeContext context, string name)
        {
            try { return context.Outputs.GetRequired(name); }
            catch (KeyNotFoundException exception) { throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "A required anomaly output is missing.", exception, tensorName: name); }
        }

        private float ReadImageScore(ITensor tensor, VisualDecodeContext context)
        {
            if (tensor.Length != 1) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score must contain exactly one value.", tensorName: Schema.ScoreOutputName, technicalDetails: tensor.Shape.ToString());
            float value;
            if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] floats) value = floats[0];
            else if (tensor.ElementType == TensorElementType.Float64 && tensor.Buffer is double[] doubles) value = checked((float)doubles[0]);
            else throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The image-level anomaly score requires Float32 or Float64.", tensorName: Schema.ScoreOutputName);
            ValidateSemanticValue(value, context, Schema.ScoreOutputName, 0);
            return value;
        }

        private float[] ReadMapValues(ITensor tensor, VisualDecodeContext context)
        {
            if (tensor.ElementType == TensorElementType.Float32 && tensor.Buffer is float[] floats) return floats;
            if (tensor.ElementType == TensorElementType.Float64 && tensor.Buffer is double[] doubles)
            {
                var values = new float[doubles.Length];
                for (int index = 0; index < doubles.Length; index++)
                {
                    double value = doubles[index];
                    if (double.IsNaN(value) || double.IsInfinity(value) || value > float.MaxValue || value < -float.MaxValue) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly value cannot be represented as a finite Float32 value.", tensorName: Schema.MapOutputName, technicalDetails: "index=" + index);
                    values[index] = (float)value;
                }
                return values;
            }
            throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly map requires Float32 or Float64.", tensorName: Schema.MapOutputName);
        }

        private MapDimensions ResolveDimensions(ITensor tensor, VisualDecodeContext context)
        {
            TensorShape shape = tensor.Shape;
            long batch = 1;
            long channels;
            long height;
            long width;
            switch (Schema.Layout)
            {
                case AnomalyTensorLayout.Nchw:
                    RequireRank(shape, 4, context); batch = shape[0]; channels = shape[1]; height = shape[2]; width = shape[3]; break;
                case AnomalyTensorLayout.Nhwc:
                    RequireRank(shape, 4, context); batch = shape[0]; height = shape[1]; width = shape[2]; channels = shape[3]; break;
                case AnomalyTensorLayout.Chw:
                    RequireRank(shape, 3, context); channels = shape[0]; height = shape[1]; width = shape[2]; break;
                case AnomalyTensorLayout.Hwc:
                    RequireRank(shape, 3, context); height = shape[0]; width = shape[1]; channels = shape[2]; break;
                default:
                    throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map tensor layout is invalid.");
            }

            if (batch <= 0 || batch > int.MaxValue) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map batch dimension is invalid.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
            if (channels != Schema.ChannelCount) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map channel count does not match its schema.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
            if (width <= 0 || height <= 0 || width > int.MaxValue || height > int.MaxValue) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map spatial dimensions are invalid.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
            long expected;
            try { expected = checked(batch * channels * width * height); }
            catch (OverflowException exception) { throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "The anomaly-map dimensions overflow the supported element count.", exception, tensorName: Schema.MapOutputName); }
            if (tensor.Length != expected) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map element count is inconsistent.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
            if (tensor.ElementType != TensorElementType.Float32 && tensor.ElementType != TensorElementType.Float64) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly map requires Float32 or Float64.", tensorName: Schema.MapOutputName);
            var result = new MapDimensions(checked((int)batch), checked((int)width), checked((int)height), checked((int)channels));
            if (Schema.CoordinateSpace == AnomalyMapCoordinateSpace.SourceImage && (result.Width != context.Input.SourceSize.Width || result.Height != context.Input.SourceSize.Height)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "A source-space anomaly map must exactly match source-image dimensions.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
            return result;
        }

        private VisualSize ResolveTargetSize(VisualDecodeContext context, MapDimensions dimensions)
        {
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Source) return context.Input.SourceSize;
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Model) return context.Input.ModelSize;
            return new VisualSize(dimensions.Width, dimensions.Height);
        }

        private void EnsureBounded(VisualDecodeContext context, ITensor tensor, MapDimensions dimensions, IReadOnlyList<VisualInputFrame> frames)
        {
            try
            {
                long tensorPixels = checked((long)dimensions.Width * dimensions.Height);
                if (tensorPixels > Options.MaximumMapPixels) throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "An anomaly map exceeds the configured pixel limit.", tensorName: Schema.MapOutputName, technicalDetails: "maximumMapPixels=" + Options.MaximumMapPixels);
                if (tensorPixels > int.MaxValue || tensor.Length > int.MaxValue) throw new OverflowException();
                long workspace = 0L;
                long output = 0L;
                for (int index = 0; index < frames.Count; index++)
                {
                    VisualInputFrame frame = frames[index];
                    long modelPixels = checked((long)frame.ModelSize.Width * frame.ModelSize.Height);
                    long sourcePixels = checked((long)frame.SourceSize.Width * frame.SourceSize.Height);
                    VisualSize target = ResolveTargetSize(frame, dimensions);
                    long targetPixels = checked((long)target.Width * target.Height);
                    if (modelPixels > Options.MaximumMapPixels || sourcePixels > Options.MaximumMapPixels || targetPixels > Options.MaximumMapPixels) throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "An anomaly map exceeds the configured pixel limit.", tensorName: Schema.MapOutputName, technicalDetails: "maximumMapPixels=" + Options.MaximumMapPixels);
                    if (modelPixels > int.MaxValue || sourcePixels > int.MaxValue || targetPixels > int.MaxValue) throw new OverflowException();
                    workspace = checked(workspace + (tensorPixels * 8L) + (modelPixels * 4L) + (targetPixels * 4L));
                    output = checked(output + (Options.PreserveRawMap ? tensorPixels * 4L : 0L) + (targetPixels * 5L));
                }
                if (workspace > Options.MaximumWorkspaceBytes) throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "Estimated anomaly workspace exceeds its configured limit.", tensorName: Schema.MapOutputName, technicalDetails: "estimatedBytes=" + workspace + "; maximumBytes=" + Options.MaximumWorkspaceBytes);
                if (output > Options.MaximumOutputBytes) throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "Estimated anomaly result exceeds its configured limit.", tensorName: Schema.MapOutputName, technicalDetails: "estimatedBytes=" + output + "; maximumBytes=" + Options.MaximumOutputBytes);
            }
            catch (OverflowException exception)
            {
                throw Failure(context, VisualErrorCodes.AnomalyLimitExceeded, "Anomaly dimensions exceed supported managed-array limits.", exception, tensorName: Schema.MapOutputName);
            }
        }

        private float[] Aggregate(float[] values, MapDimensions dimensions, VisualDecodeContext context)
        {
            int pixels = checked(dimensions.Width * dimensions.Height);
            if (dimensions.Channels == 1 && !Options.PreserveRawMap)
            {
                for (int pixel = 0; pixel < pixels; pixel++)
                {
                    if ((pixel & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    ValidateSemanticValue(values[pixel], context, Schema.MapOutputName, pixel);
                }
                return values;
            }
            var result = new float[pixels];
            bool channelMajor = Schema.Layout == AnomalyTensorLayout.Nchw || Schema.Layout == AnomalyTensorLayout.Chw;
            int channels = dimensions.Channels;
            for (int pixel = 0; pixel < pixels; pixel++)
            {
                if ((pixel & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                float aggregate;
                if (Options.ChannelAggregation == AnomalyChannelAggregation.SingleChannel)
                {
                    int index = channelMajor ? (Options.ChannelIndex * pixels) + pixel : (pixel * channels) + Options.ChannelIndex;
                    aggregate = values[index];
                    ValidateSemanticValue(aggregate, context, Schema.MapOutputName, index);
                }
                else if (Options.ChannelAggregation == AnomalyChannelAggregation.Maximum)
                {
                    aggregate = float.NegativeInfinity;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int index = channelMajor ? (channel * pixels) + pixel : (pixel * channels) + channel;
                        float value = values[index];
                        ValidateSemanticValue(value, context, Schema.MapOutputName, index);
                        if (value > aggregate) aggregate = value;
                    }
                }
                else
                {
                    double sum = 0d;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int index = channelMajor ? (channel * pixels) + pixel : (pixel * channels) + channel;
                        float value = values[index];
                        ValidateSemanticValue(value, context, Schema.MapOutputName, index);
                        sum += value;
                    }
                    aggregate = (float)(sum / channels);
                }
                result[pixel] = aggregate;
            }
            return result;
        }

        private float ReadMapValue(float[] values, int pixel, int channel, MapDimensions dimensions, VisualDecodeContext context)
        {
            int y = pixel / dimensions.Width;
            int x = pixel - (y * dimensions.Width);
            int index = Schema.Layout == AnomalyTensorLayout.Nchw || Schema.Layout == AnomalyTensorLayout.Chw
                ? ((channel * dimensions.Height) + y) * dimensions.Width + x
                : ((y * dimensions.Width) + x) * dimensions.Channels + channel;
            float value = values[index];
            ValidateSemanticValue(value, context, Schema.MapOutputName, index);
            return value;
        }

        private void ValidateSemanticValue(float value, VisualDecodeContext context, string tensorName, int index)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly value must be finite.", tensorName: tensorName, technicalDetails: "index=" + index);
            if (Schema.ValueMode == AnomalyMapValueMode.Probabilities && (value < 0f || value > 1f)) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly probability must be in [0,1].", tensorName: tensorName, technicalDetails: "index=" + index + "; value=" + value);
            if (Schema.ValueMode == AnomalyMapValueMode.Distances && value < 0f) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "An anomaly distance must be non-negative.", tensorName: tensorName, technicalDetails: "index=" + index + "; value=" + value);
            if (Schema.ValueMode == AnomalyMapValueMode.Binary && value != 0f && value != 1f) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "A binary anomaly value must be zero or one.", tensorName: tensorName, technicalDetails: "index=" + index + "; value=" + value);
        }

        private float[] Normalize(float[] source, List<PredictionWarning> warnings, System.Threading.CancellationToken cancellationToken)
        {
            var result = new float[source.Length];
            if (Options.Normalization == AnomalyNormalizationMode.None) { Buffer.BlockCopy(source, 0, result, 0, source.Length * sizeof(float)); return result; }
            float minimum = Options.FixedRangeMinimum;
            float maximum = Options.FixedRangeMaximum;
            if (Options.Normalization == AnomalyNormalizationMode.MinMax)
            {
                minimum = float.PositiveInfinity;
                maximum = float.NegativeInfinity;
                for (int index = 0; index < source.Length; index++) { if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested(); minimum = Math.Min(minimum, source[index]); maximum = Math.Max(maximum, source[index]); }
                if (maximum == minimum)
                {
                    warnings.Add(new PredictionWarning("anomaly.constant-map", "Min-max normalization mapped a constant anomaly map to zero."));
                    return result;
                }
            }
            float inverseRange = 1f / (maximum - minimum);
            for (int index = 0; index < source.Length; index++)
            {
                if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                float value = (source[index] - minimum) * inverseRange;
                result[index] = value < 0f ? 0f : value > 1f ? 1f : value;
            }
            return result;
        }

        private float[] Restore(float[] tensorMap, MapDimensions dimensions, VisualDecodeContext context)
        {
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Tensor) return tensorMap;
            if (Schema.CoordinateSpace == AnomalyMapCoordinateSpace.SourceImage)
            {
                if (Options.OutputSizeMode == AnomalyOutputSizeMode.Source) return tensorMap;
                return Resize(tensorMap, dimensions.Width, dimensions.Height, context.Input.ModelSize.Width, context.Input.ModelSize.Height, Options.Interpolation, context.CancellationToken);
            }

            float[] modelMap = dimensions.Width == context.Input.ModelSize.Width && dimensions.Height == context.Input.ModelSize.Height
                ? tensorMap
                : Resize(tensorMap, dimensions.Width, dimensions.Height, context.Input.ModelSize.Width, context.Input.ModelSize.Height, Options.Interpolation, context.CancellationToken);
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Model) return modelMap;
            return RestoreSource(modelMap, context);
        }

        private float[] RestoreSource(float[] modelMap, VisualDecodeContext context)
        {
            int modelWidth = context.Input.ModelSize.Width;
            int modelHeight = context.Input.ModelSize.Height;
            int sourceWidth = context.Input.SourceSize.Width;
            int sourceHeight = context.Input.SourceSize.Height;
            return Options.Interpolation == AnomalyMapInterpolation.Nearest
                ? RestoreNearestSource(modelMap, modelWidth, modelHeight, sourceWidth, sourceHeight, context)
                : RestoreBilinearSource(modelMap, modelWidth, modelHeight, sourceWidth, sourceHeight, context);
        }

        private static float[] RestoreNearestSource(float[] modelMap, int modelWidth, int modelHeight, int sourceWidth, int sourceHeight, VisualDecodeContext context)
        {
            var result = new float[checked(sourceWidth * sourceHeight)];
            for (int y = 0; y < sourceHeight; y++)
            {
                if ((y & 63) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                float modelCenterY = ((y + 0.5f) * context.Input.Transform.ScaleY) + context.Input.Transform.OffsetY;
                if (modelCenterY < 0f || modelCenterY >= modelHeight) continue;
                int modelY = Math.Min(modelHeight - 1, (int)Math.Floor(modelCenterY));
                int modelRow = modelY * modelWidth;
                int resultRow = y * sourceWidth;
                for (int x = 0; x < sourceWidth; x++)
                {
                    float modelCenterX = ((x + 0.5f) * context.Input.Transform.ScaleX) + context.Input.Transform.OffsetX;
                    if (modelCenterX >= 0f && modelCenterX < modelWidth)
                    {
                        int modelX = Math.Min(modelWidth - 1, (int)Math.Floor(modelCenterX));
                        result[resultRow + x] = modelMap[modelRow + modelX];
                    }
                }
            }
            return result;
        }

        private static float[] RestoreBilinearSource(float[] modelMap, int modelWidth, int modelHeight, int sourceWidth, int sourceHeight, VisualDecodeContext context)
        {
            // The transform is affine and separable. Precompute the source-to-model
            // samples once per axis so the hot pixel loop only performs four reads and
            // two lerps instead of repeating coordinate math and Floor for every pixel.
            AxisSample[] xSamples = VisualArrayPool<AxisSample>.Rent(sourceWidth);
            AxisSample[] ySamples = VisualArrayPool<AxisSample>.Rent(sourceHeight);
            var result = new float[checked(sourceWidth * sourceHeight)];
            try
            {
                FillAxisSamples(xSamples, sourceWidth, modelWidth, context.Input.Transform.ScaleX, context.Input.Transform.OffsetX);
                FillAxisSamples(ySamples, sourceHeight, modelHeight, context.Input.Transform.ScaleY, context.Input.Transform.OffsetY);
                for (int y = 0; y < sourceHeight; y++)
                {
                    if ((y & 63) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                    AxisSample ySample = ySamples[y];
                    if (!ySample.Valid) continue;
                    int row0 = ySample.Lower * modelWidth;
                    int row1 = ySample.Upper * modelWidth;
                    float inverseY = 1f - ySample.Weight;
                    int resultRow = y * sourceWidth;
                    for (int x = 0; x < sourceWidth; x++)
                    {
                        AxisSample xSample = xSamples[x];
                        if (!xSample.Valid) continue;
                        float top = modelMap[row0 + xSample.Lower] + ((modelMap[row0 + xSample.Upper] - modelMap[row0 + xSample.Lower]) * xSample.Weight);
                        float bottom = modelMap[row1 + xSample.Lower] + ((modelMap[row1 + xSample.Upper] - modelMap[row1 + xSample.Lower]) * xSample.Weight);
                        result[resultRow + x] = (top * inverseY) + (bottom * ySample.Weight);
                    }
                }
                return result;
            }
            finally
            {
                VisualArrayPool<AxisSample>.Return(ySamples);
                VisualArrayPool<AxisSample>.Return(xSamples);
            }
        }

        private static void FillAxisSamples(AxisSample[] samples, int sourceLength, int modelLength, float scale, float offset)
        {
            for (int index = 0; index < sourceLength; index++)
            {
                float modelCenter = ((index + 0.5f) * scale) + offset;
                if (modelCenter < 0f || modelCenter >= modelLength)
                {
                    samples[index] = default;
                    continue;
                }
                float coordinate = Math.Max(0f, Math.Min(modelLength - 1, modelCenter - 0.5f));
                int lower = (int)Math.Floor(coordinate);
                samples[index] = new AxisSample(lower, Math.Min(modelLength - 1, lower + 1), coordinate - lower);
            }
        }

        private readonly struct AxisSample
        {
            public AxisSample(int lower, int upper, float weight)
            {
                Lower = lower;
                Upper = upper;
                Weight = weight;
                Valid = true;
            }

            public bool Valid { get; }
            public int Lower { get; }
            public int Upper { get; }
            public float Weight { get; }
        }

        private static float[] Resize(float[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, AnomalyMapInterpolation interpolation, System.Threading.CancellationToken cancellationToken)
        {
            var result = new float[checked(targetWidth * targetHeight)];
            for (int y = 0; y < targetHeight; y++)
            {
                if ((y & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                float sourceY = ((y + 0.5f) * sourceHeight / targetHeight) - 0.5f;
                for (int x = 0; x < targetWidth; x++)
                {
                    float sourceX = ((x + 0.5f) * sourceWidth / targetWidth) - 0.5f;
                    if (interpolation == AnomalyMapInterpolation.Nearest)
                    {
                        int nearestX = Math.Min(sourceWidth - 1, Math.Max(0, (int)Math.Floor(sourceX + 0.5f)));
                        int nearestY = Math.Min(sourceHeight - 1, Math.Max(0, (int)Math.Floor(sourceY + 0.5f)));
                        result[(y * targetWidth) + x] = source[(nearestY * sourceWidth) + nearestX];
                    }
                    else result[(y * targetWidth) + x] = SampleBilinear(source, sourceWidth, sourceHeight, sourceX, sourceY);
                }
            }
            return result;
        }

        private static float SampleBilinear(float[] source, int width, int height, float x, float y)
        {
            x = Math.Max(0f, Math.Min(width - 1, x));
            y = Math.Max(0f, Math.Min(height - 1, y));
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(width - 1, x0 + 1);
            int y1 = Math.Min(height - 1, y0 + 1);
            float dx = x - x0;
            float dy = y - y0;
            float top = source[(y0 * width) + x0] + ((source[(y0 * width) + x1] - source[(y0 * width) + x0]) * dx);
            float bottom = source[(y1 * width) + x0] + ((source[(y1 * width) + x1] - source[(y1 * width) + x0]) * dx);
            return top + ((bottom - top) * dy);
        }

        private byte[] Threshold(float[] values, VisualDecodeContext context)
        {
            var result = new byte[values.Length];
            for (int index = 0; index < values.Length; index++) { if ((index & 4095) == 0) context.CancellationToken.ThrowIfCancellationRequested(); if (values[index] >= Options.Threshold) result[index] = 1; }
            return result;
        }

        private void RequireRank(TensorShape shape, int expected, VisualDecodeContext context)
        {
            if (shape.Rank != expected) throw Failure(context, VisualErrorCodes.AnomalyContractInvalid, "The anomaly-map rank does not match its layout.", tensorName: Schema.MapOutputName, technicalDetails: shape.ToString());
        }

        private VisualException Failure(VisualDecodeContext context, string code, string message, Exception? innerException = null, string? tensorName = null, string? technicalDetails = null)
        {
            return new VisualException(code, message, innerException, context.Profile.ProfileId, tensorName, modelId: context.Profile.ModelId, technicalDetails: technicalDetails);
        }

        private VisualSize ResolveTargetSize(VisualInputFrame frame, MapDimensions dimensions)
        {
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Source) return frame.SourceSize;
            if (Options.OutputSizeMode == AnomalyOutputSizeMode.Model) return frame.ModelSize;
            return new VisualSize(dimensions.Width, dimensions.Height);
        }

        private sealed class MapDimensions
        {
            public MapDimensions(int batch, int width, int height, int channels) { Batch = batch; Width = width; Height = height; Channels = channels; }
            public int Batch { get; }
            public int Width { get; }
            public int Height { get; }
            public int Channels { get; }
        }
    }
}
