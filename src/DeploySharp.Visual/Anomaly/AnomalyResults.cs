using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Results;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Owns one finite row-major anomaly score map and its source-image metadata. / 拥有一个有限行优先异常分数图及其源图元数据。</summary>
    public sealed class AnomalyScoreMap
    {
        private readonly float[] _values;

        /// <summary>Initializes a score map by defensively copying values. / 通过防御性复制值初始化分数图。</summary>
        public AnomalyScoreMap(VisualSize sourceSize, int width, int height, float[] values, AnomalyMapValueMode valueMode, AnomalyNormalizationMode normalization)
            : this(sourceSize, width, height, values, valueMode, normalization, false)
        {
        }

        internal AnomalyScoreMap(VisualSize sourceSize, int width, int height, float[] values, AnomalyMapValueMode valueMode, AnomalyNormalizationMode normalization, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if ((long)width * height != values.LongLength) throw new ArgumentException("Map dimensions do not match the value count.", nameof(values));
            if (!Enum.IsDefined(typeof(AnomalyMapValueMode), valueMode)) throw new ArgumentOutOfRangeException(nameof(valueMode));
            if (!Enum.IsDefined(typeof(AnomalyNormalizationMode), normalization)) throw new ArgumentOutOfRangeException(nameof(normalization));
            if (!takeOwnership) for (int index = 0; index < values.Length; index++) ValidateValue(values[index], valueMode, normalization, nameof(values));
            SourceSize = sourceSize;
            Width = width;
            Height = height;
            ValueMode = valueMode;
            Normalization = normalization;
            _values = takeOwnership ? values : (float[])values.Clone();
        }

        /// <summary>Gets the source-image size associated with this map. / 获取与此异常图关联的源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets map width. / 获取异常图宽度。</summary>
        public int Width { get; }
        /// <summary>Gets map height. / 获取异常图高度。</summary>
        public int Height { get; }
        /// <summary>Gets map pixel count. / 获取异常图像素数。</summary>
        public int PixelCount => _values.Length;
        /// <summary>Gets original value semantics. / 获取原始值语义。</summary>
        public AnomalyMapValueMode ValueMode { get; }
        /// <summary>Gets applied normalization. / 获取已应用的归一化。</summary>
        public AnomalyNormalizationMode Normalization { get; }

        /// <summary>Gets one value using zero-based image coordinates. / 使用从零开始的图像坐标获取一个值。</summary>
        public float GetValue(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _values[(y * Width) + x];
        }

        /// <summary>Returns a defensive row-major copy. / 返回行优先防御性副本。</summary>
        public float[] ToArray() => (float[])_values.Clone();

        internal float[] DangerousGetReadOnlyBuffer() => _values;

        private static void ValidateValue(float value, AnomalyMapValueMode valueMode, AnomalyNormalizationMode normalization, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Anomaly-map values must be finite.", parameterName);
            if (normalization != AnomalyNormalizationMode.None && (value < 0f || value > 1f)) throw new ArgumentException("Normalized anomaly-map values must be in [0,1].", parameterName);
            if (normalization == AnomalyNormalizationMode.None && valueMode == AnomalyMapValueMode.Probabilities && (value < 0f || value > 1f)) throw new ArgumentException("Probability values must be in [0,1].", parameterName);
            if (normalization == AnomalyNormalizationMode.None && valueMode == AnomalyMapValueMode.Distances && value < 0f) throw new ArgumentException("Distance values must be non-negative.", parameterName);
            if (normalization == AnomalyNormalizationMode.None && valueMode == AnomalyMapValueMode.Binary && value != 0f && value != 1f) throw new ArgumentException("Binary values must be zero or one.", parameterName);
        }
    }

    /// <summary>Owns a row-major binary anomaly mask with values zero or one. / 拥有值为零或一的行优先二值异常掩码。</summary>
    public sealed class AnomalyBinaryMask
    {
        private readonly byte[] _values;

        /// <summary>Initializes a binary mask by defensively copying values. / 通过防御性复制值初始化二值掩码。</summary>
        public AnomalyBinaryMask(int width, int height, byte[] values) : this(width, height, values, false) { }

        internal AnomalyBinaryMask(int width, int height, byte[] values, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if ((long)width * height != values.LongLength) throw new ArgumentException("Mask dimensions do not match the value count.", nameof(values));
            if (!takeOwnership) for (int index = 0; index < values.Length; index++) if (values[index] > 1) throw new ArgumentException("Binary mask values must be zero or one.", nameof(values));
            Width = width;
            Height = height;
            _values = takeOwnership ? values : (byte[])values.Clone();
        }

        /// <summary>Gets mask width. / 获取掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets mask height. / 获取掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets mask pixel count. / 获取掩码像素数。</summary>
        public int PixelCount => _values.Length;
        /// <summary>Gets whether one zero-based pixel is anomalous. / 获取一个从零开始的像素是否异常。</summary>
        public bool IsAnomalous(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _values[(y * Width) + x] != 0;
        }
        /// <summary>Returns a defensive row-major copy. / 返回行优先防御性副本。</summary>
        public byte[] ToArray() => (byte[])_values.Clone();
        internal byte[] DangerousGetReadOnlyBuffer() => _values;
    }

    /// <summary>Contains owned anomaly score, map, mask, transform, timing, and warning state. / 包含自有异常分数、异常图、掩码、变换、时长与警告状态。</summary>
    public sealed class AnomalyDetectionResult
    {
        private readonly IReadOnlyList<PredictionWarning> _warnings;
        private readonly int _anomalousPixelCount;

        /// <summary>Initializes a complete anomaly result. / 初始化完整异常结果。</summary>
        public AnomalyDetectionResult(float imageScore, AnomalyScoreMap? rawMap, AnomalyScoreMap normalizedMap, AnomalyBinaryMask mask, float threshold, ImageTransform transform, InferenceTiming? timing = null, IEnumerable<PredictionWarning>? warnings = null)
            : this(imageScore, rawMap, normalizedMap, mask, threshold, transform, null, timing, warnings)
        {
        }

        internal AnomalyDetectionResult(float imageScore, AnomalyScoreMap? rawMap, AnomalyScoreMap normalizedMap, AnomalyBinaryMask mask, float threshold, ImageTransform transform, int anomalousPixelCount, InferenceTiming? timing = null, IEnumerable<PredictionWarning>? warnings = null)
            : this(imageScore, rawMap, normalizedMap, mask, threshold, transform, (int?)anomalousPixelCount, timing, warnings)
        {
        }

        private AnomalyDetectionResult(float imageScore, AnomalyScoreMap? rawMap, AnomalyScoreMap normalizedMap, AnomalyBinaryMask mask, float threshold, ImageTransform transform, int? trustedAnomalousPixelCount, InferenceTiming? timing, IEnumerable<PredictionWarning>? warnings)
        {
            if (float.IsNaN(imageScore) || float.IsInfinity(imageScore)) throw new ArgumentOutOfRangeException(nameof(imageScore));
            if (normalizedMap == null) throw new ArgumentNullException(nameof(normalizedMap));
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (float.IsNaN(threshold) || float.IsInfinity(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (normalizedMap.Width != mask.Width || normalizedMap.Height != mask.Height) throw new ArgumentException("Normalized map and mask dimensions must match.", nameof(mask));
            if (!normalizedMap.SourceSize.Equals(transform.SourceSize)) throw new ArgumentException("Result source size must match the image transform.", nameof(normalizedMap));
            ImageScore = imageScore;
            RawMap = rawMap;
            NormalizedMap = normalizedMap;
            Mask = mask;
            Threshold = threshold;
            Transform = transform;
            Timing = timing ?? InferenceTiming.Zero;
            byte[] maskValues = mask.DangerousGetReadOnlyBuffer();
            int anomalous = trustedAnomalousPixelCount ?? CountAnomalous(maskValues);
            if (anomalous < 0 || anomalous > maskValues.Length) throw new ArgumentOutOfRangeException(nameof(trustedAnomalousPixelCount));
            _anomalousPixelCount = anomalous;
            AnomalousPixelRatio = (double)anomalous / maskValues.Length;
            var copiedWarnings = new List<PredictionWarning>();
            if (warnings != null) foreach (PredictionWarning warning in warnings) copiedWarnings.Add(warning ?? throw new ArgumentException("Warnings cannot contain null.", nameof(warnings)));
            _warnings = copiedWarnings.AsReadOnly();
        }

        /// <summary>Gets the backend-provided image-level anomaly score. / 获取后端提供的图像级异常分数。</summary>
        public float ImageScore { get; }
        /// <summary>Gets the optional aggregated tensor-resolution raw map. / 获取可选的聚合后张量分辨率原始图。</summary>
        public AnomalyScoreMap? RawMap { get; }
        /// <summary>Gets the normalized or explicitly unnormalized restored score map. / 获取已归一化或显式未归一化的恢复后分数图。</summary>
        public AnomalyScoreMap NormalizedMap { get; }
        /// <summary>Gets the owned thresholded binary mask. / 获取自有阈值化二值掩码。</summary>
        public AnomalyBinaryMask Mask { get; }
        /// <summary>Gets the applied fixed threshold. / 获取已应用的固定阈值。</summary>
        public float Threshold { get; }
        /// <summary>Gets the ratio of anomalous pixels in [0,1]. / 获取 [0,1] 内的异常像素比例。</summary>
        public double AnomalousPixelRatio { get; }
        /// <summary>Gets the authoritative input transform. / 获取权威输入变换。</summary>
        public ImageTransform Transform { get; }
        /// <summary>Gets measured inference and postprocessing durations. / 获取测得的推理与后处理时长。</summary>
        public InferenceTiming Timing { get; }
        /// <summary>Gets non-fatal deterministic warnings. / 获取非致命确定性警告。</summary>
        public IReadOnlyList<PredictionWarning> Warnings => _warnings;

        /// <summary>Computes a canonical SHA256 excluding machine-dependent timing. / 计算不含机器相关时长的规范 SHA256。</summary>
        public string ComputeSha256()
        {
            using (SHA256 hash = SHA256.Create())
            {
                var writer = new HashWriter(hash);
                writer.WriteFloat(ImageScore);
                writer.WriteFloat(Threshold);
                writer.WriteInt32(NormalizedMap.SourceSize.Width);
                writer.WriteInt32(NormalizedMap.SourceSize.Height);
                WriteMap(writer, RawMap);
                WriteMap(writer, NormalizedMap);
                writer.WriteBytes(Mask.DangerousGetReadOnlyBuffer());
                writer.Complete();
                return ToHex(hash.Hash!);
            }
        }

        internal AnomalyDetectionResult WithTiming(InferenceTiming timing) => new AnomalyDetectionResult(ImageScore, RawMap, NormalizedMap, Mask, Threshold, Transform, _anomalousPixelCount, timing, _warnings);

        private static int CountAnomalous(byte[] values)
        {
            int result = 0;
            for (int index = 0; index < values.Length; index++) result += values[index];
            return result;
        }

        private static void WriteMap(HashWriter writer, AnomalyScoreMap? map)
        {
            writer.WriteInt32(map == null ? 0 : 1);
            if (map == null) return;
            writer.WriteInt32(map.Width);
            writer.WriteInt32(map.Height);
            writer.WriteInt32((int)map.ValueMode);
            writer.WriteInt32((int)map.Normalization);
            writer.WriteFloats(map.DangerousGetReadOnlyBuffer());
        }

        private static string ToHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++) { characters[index * 2] = hex[bytes[index] >> 4]; characters[(index * 2) + 1] = hex[bytes[index] & 15]; }
            return new string(characters);
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float Float;
            [FieldOffset(0)] public int Int32;
        }

        private sealed class HashWriter
        {
            private readonly HashAlgorithm _hash;
            private readonly byte[] _buffer = new byte[8192];
            public HashWriter(HashAlgorithm hash) { _hash = hash; }
            public void WriteInt32(int value)
            {
                _buffer[0] = (byte)value; _buffer[1] = (byte)(value >> 8); _buffer[2] = (byte)(value >> 16); _buffer[3] = (byte)(value >> 24);
                _hash.TransformBlock(_buffer, 0, 4, _buffer, 0);
            }
            public void WriteFloat(float value) { var bits = new FloatBits { Float = value }; WriteInt32(bits.Int32); }
            public void WriteFloats(float[] values)
            {
                int offset = 0;
                while (offset < values.Length)
                {
                    int count = Math.Min(_buffer.Length / 4, values.Length - offset);
                    for (int index = 0; index < count; index++)
                    {
                        var bits = new FloatBits { Float = values[offset + index] };
                        int destination = index * 4;
                        _buffer[destination] = (byte)bits.Int32; _buffer[destination + 1] = (byte)(bits.Int32 >> 8); _buffer[destination + 2] = (byte)(bits.Int32 >> 16); _buffer[destination + 3] = (byte)(bits.Int32 >> 24);
                    }
                    _hash.TransformBlock(_buffer, 0, count * 4, _buffer, 0);
                    offset += count;
                }
            }
            public void WriteBytes(byte[] values)
            {
                int offset = 0;
                while (offset < values.Length) { int count = Math.Min(_buffer.Length, values.Length - offset); Buffer.BlockCopy(values, offset, _buffer, 0, count); _hash.TransformBlock(_buffer, 0, count, _buffer, 0); offset += count; }
            }
            public void Complete() => _hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        }
    }

    /// <summary>Contains one anomaly result for every row of a true model batch. / 包含真正模型 Batch 中每一行的异常结果。</summary>
    public sealed class AnomalyDetectionBatchResult
    {
        private readonly IReadOnlyList<AnomalyDetectionResult> _items;

        /// <summary>Initializes an ordered anomaly batch result. / 初始化有序异常 Batch 结果。</summary>
        public AnomalyDetectionBatchResult(IEnumerable<AnomalyDetectionResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<AnomalyDetectionResult>();
            foreach (AnomalyDetectionResult item in items) copied.Add(item ?? throw new ArgumentException("Anomaly batch items cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A batch result requires at least two items; batch one uses AnomalyDetectionResult.", nameof(items));
            _items = copied.AsReadOnly();
        }

        /// <summary>Gets the number of decoded rows. / 获取已解码行数。</summary>
        public int Count => _items.Count;
        /// <summary>Gets an anomaly result by input-row index. / 按输入行索引获取异常结果。</summary>
        public AnomalyDetectionResult this[int index] => _items[index];
        /// <summary>Gets ordered anomaly results. / 获取有序异常结果。</summary>
        public IReadOnlyList<AnomalyDetectionResult> Items => _items;
    }
}
