using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Represents an RGB display color without introducing an image-library dependency. / 表示不引入图像库依赖的 RGB 显示颜色。</summary>
    public readonly struct SegmentationColor : IEquatable<SegmentationColor>
    {
        /// <summary>Initializes an RGB color. / 初始化 RGB 颜色。</summary>
        public SegmentationColor(byte red, byte green, byte blue) { Red = red; Green = green; Blue = blue; }
        /// <summary>Gets the red component. / 获取红色分量。</summary>
        public byte Red { get; }
        /// <summary>Gets the green component. / 获取绿色分量。</summary>
        public byte Green { get; }
        /// <summary>Gets the blue component. / 获取蓝色分量。</summary>
        public byte Blue { get; }
        /// <inheritdoc />
        /// <remarks>Compares all RGB components. / 比较所有 RGB 分量。</remarks>
        public bool Equals(SegmentationColor other) => Red == other.Red && Green == other.Green && Blue == other.Blue;
        /// <inheritdoc />
        /// <remarks>Compares an object with this RGB color. / 将对象与此 RGB 颜色比较。</remarks>
        public override bool Equals(object? obj) => obj is SegmentationColor other && Equals(other);
        /// <inheritdoc />
        /// <remarks>Computes a component-based hash code. / 根据颜色分量计算哈希码。</remarks>
        public override int GetHashCode() => (Red << 16) | (Green << 8) | Blue;
        /// <summary>Compares two RGB colors for equality. / 比较两个 RGB 颜色是否相等。</summary>
        public static bool operator ==(SegmentationColor left, SegmentationColor right) => left.Equals(right);
        /// <summary>Compares two RGB colors for inequality. / 比较两个 RGB 颜色是否不相等。</summary>
        public static bool operator !=(SegmentationColor left, SegmentationColor right) => !left.Equals(right);
    }

    /// <summary>Describes one semantic class and its deterministic display color. / 描述一个语义类别及其确定性显示颜色。</summary>
    public sealed class SemanticSegmentationClass
    {
        /// <summary>Initializes semantic class metadata. / 初始化语义类别元数据。</summary>
        public SemanticSegmentationClass(int index, string label, SegmentationColor color, bool isBackground = false, bool isIgnored = false)
        {
            if (index < 0 || index > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(index));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A class label is required.", nameof(label));
            Index = index;
            Label = label;
            Color = color;
            IsBackground = isBackground;
            IsIgnored = isIgnored;
        }

        /// <summary>Gets the zero-based class index. / 获取从零开始的类别索引。</summary>
        public int Index { get; }
        /// <summary>Gets the display label. / 获取显示标签。</summary>
        public string Label { get; }
        /// <summary>Gets the deterministic display color. / 获取确定性显示颜色。</summary>
        public SegmentationColor Color { get; }
        /// <summary>Gets whether this is the configured background class. / 获取此类别是否为配置的背景类别。</summary>
        public bool IsBackground { get; }
        /// <summary>Gets whether this is the configured ignored class. / 获取此类别是否为配置的忽略类别。</summary>
        public bool IsIgnored { get; }
    }

    /// <summary>Contains pixel statistics for one semantic class. / 包含一个语义类别的像素统计信息。</summary>
    public sealed class SegmentationClassStatistics
    {
        /// <summary>Initializes class statistics. / 初始化类别统计信息。</summary>
        public SegmentationClassStatistics(int classIndex, long pixelCount, double fraction)
        {
            if (classIndex < 0 || classIndex > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (pixelCount < 0) throw new ArgumentOutOfRangeException(nameof(pixelCount));
            if (double.IsNaN(fraction) || double.IsInfinity(fraction) || fraction < 0 || fraction > 1) throw new ArgumentOutOfRangeException(nameof(fraction));
            ClassIndex = classIndex;
            PixelCount = pixelCount;
            Fraction = fraction;
        }

        /// <summary>Gets the class index. / 获取类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets the number of pixels assigned to the class. / 获取分配给该类别的像素数。</summary>
        public long PixelCount { get; }
        /// <summary>Gets the fraction of all mask pixels assigned to the class. / 获取该类别像素占全部掩码像素的比例。</summary>
        public double Fraction { get; }
    }

    /// <summary>Stores a dense row-major semantic class-index mask owned by DeploySharp. / 存储由 DeploySharp 拥有的稠密行优先语义类别索引掩码。</summary>
    public sealed class SemanticSegmentationMask
    {
        private readonly ushort[] _classIndices;

        /// <summary>Initializes a mask by defensively copying row-major class indices. / 通过防御性复制行优先类别索引初始化掩码。</summary>
        public SemanticSegmentationMask(int width, int height, ushort[] classIndices)
            : this(width, height, classIndices, false)
        {
        }

        internal SemanticSegmentationMask(int width, int height, ushort[] classIndices, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (classIndices == null) throw new ArgumentNullException(nameof(classIndices));
            if ((long)width * height != classIndices.LongLength) throw new ArgumentException("Mask dimensions do not match the class-index count.", nameof(classIndices));
            Width = width;
            Height = height;
            _classIndices = takeOwnership ? classIndices : (ushort[])classIndices.Clone();
        }

        /// <summary>Gets the mask width. / 获取掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the mask height. / 获取掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets the number of mask pixels. / 获取掩码像素数。</summary>
        public int PixelCount => _classIndices.Length;

        /// <summary>Gets a class index using zero-based image coordinates. / 使用从零开始的图像坐标获取类别索引。</summary>
        public ushort GetClassIndex(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _classIndices[(y * Width) + x];
        }

        /// <summary>Returns a defensive row-major copy of all class indices. / 返回所有类别索引的行优先防御性副本。</summary>
        public ushort[] ToArray() => (ushort[])_classIndices.Clone();

        /// <summary>Creates a row-major binary mask for one class. / 为一个类别创建行优先二值掩码。</summary>
        public byte[] CreateBinaryMask(int classIndex)
        {
            if (classIndex < 0 || classIndex > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(classIndex));
            var result = new byte[_classIndices.Length];
            for (int index = 0; index < result.Length; index++) if (_classIndices[index] == classIndex) result[index] = 1;
            return result;
        }

        /// <summary>Computes SHA256 over width, height, and little-endian row-major class indices. / 对宽度、高度及小端行优先类别索引计算 SHA256。</summary>
        public string ComputeSha256()
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                var header = new byte[8];
                WriteInt32LittleEndian(header, 0, Width);
                WriteInt32LittleEndian(header, 4, Height);
                algorithm.TransformBlock(header, 0, header.Length, header, 0);
                var block = new byte[8192];
                int sourceIndex = 0;
                while (sourceIndex < _classIndices.Length)
                {
                    int valuesInBlock = Math.Min(block.Length / 2, _classIndices.Length - sourceIndex);
                    for (int index = 0; index < valuesInBlock; index++)
                    {
                        ushort value = _classIndices[sourceIndex + index];
                        block[index * 2] = (byte)(value & 0xff);
                        block[(index * 2) + 1] = (byte)(value >> 8);
                    }

                    int byteCount = valuesInBlock * 2;
                    algorithm.TransformBlock(block, 0, byteCount, block, 0);
                    sourceIndex += valuesInBlock;
                }

                algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(algorithm.Hash!);
            }
        }

        private static void WriteInt32LittleEndian(byte[] destination, int offset, int value)
        {
            destination[offset] = (byte)(value & 0xff);
            destination[offset + 1] = (byte)((value >> 8) & 0xff);
            destination[offset + 2] = (byte)((value >> 16) & 0xff);
            destination[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static string ToHex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = hex[bytes[index] >> 4];
                characters[(index * 2) + 1] = hex[bytes[index] & 15];
            }

            return new string(characters);
        }

        internal ushort[] DangerousGetReadOnlyBuffer() => _classIndices;
    }

    /// <summary>Represents one run in DeploySharp row-major semantic RLE. / 表示 DeploySharp 行优先语义 RLE 中的一个游程。</summary>
    public sealed class SegmentationRleRun
    {
        /// <summary>Initializes a non-empty row-major run. / 初始化非空行优先游程。</summary>
        public SegmentationRleRun(int start, int length, ushort classIndex)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            Start = start;
            Length = length;
            ClassIndex = classIndex;
        }

        /// <summary>Gets the zero-based row-major pixel offset. / 获取从零开始的行优先像素偏移。</summary>
        public int Start { get; }
        /// <summary>Gets the run length in pixels. / 获取以像素计量的游程长度。</summary>
        public int Length { get; }
        /// <summary>Gets the class index repeated by the run. / 获取游程重复的类别索引。</summary>
        public ushort ClassIndex { get; }
    }

    /// <summary>Stores DeploySharp row-major RLE; it is not COCO compressed RLE. / 存储 DeploySharp 行优先 RLE；它不是 COCO 压缩 RLE。</summary>
    public sealed class SegmentationRle
    {
        private readonly IReadOnlyList<SegmentationRleRun> _runs;

        /// <summary>Initializes and validates a complete row-major RLE. / 初始化并验证完整的行优先 RLE。</summary>
        public SegmentationRle(int width, int height, IEnumerable<SegmentationRleRun> runs)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            int expectedStart = 0;
            var copied = new List<SegmentationRleRun>();
            foreach (SegmentationRleRun run in runs)
            {
                if (run == null) throw new ArgumentException("RLE runs cannot contain null values.", nameof(runs));
                if (run.Start != expectedStart) throw new ArgumentException("RLE runs must be contiguous and ordered.", nameof(runs));
                expectedStart = checked(expectedStart + run.Length);
                copied.Add(run);
            }

            if (expectedStart != checked(width * height)) throw new ArgumentException("RLE runs must cover the complete mask.", nameof(runs));
            Width = width;
            Height = height;
            _runs = copied.AsReadOnly();
        }

        /// <summary>Gets the encoded mask width. / 获取编码掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the encoded mask height. / 获取编码掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets ordered contiguous row-major runs. / 获取有序连续的行优先游程。</summary>
        public IReadOnlyList<SegmentationRleRun> Runs => _runs;

        /// <summary>Encodes a semantic mask using DeploySharp row-major runs. / 使用 DeploySharp 行优先游程编码语义掩码。</summary>
        public static SegmentationRle Encode(SemanticSegmentationMask mask)
        {
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            // The buffer remains private to the immutable mask; RLE reads it without retaining or modifying it. / 缓冲区仍由不可变掩码私有；RLE 只读且不保留或修改它。
            ushort[] values = mask.DangerousGetReadOnlyBuffer();
            var runs = new List<SegmentationRleRun>();
            int start = 0;
            ushort current = values[0];
            for (int index = 1; index <= values.Length; index++)
            {
                if (index != values.Length && values[index] == current) continue;
                runs.Add(new SegmentationRleRun(start, index - start, current));
                if (index != values.Length) { start = index; current = values[index]; }
            }

            return new SegmentationRle(mask.Width, mask.Height, runs);
        }

        /// <summary>Decodes this RLE into an owned dense semantic mask. / 将此 RLE 解码为自有稠密语义掩码。</summary>
        public SemanticSegmentationMask Decode()
        {
            var values = new ushort[checked(Width * Height)];
            foreach (SegmentationRleRun run in _runs)
            {
                int end = checked(run.Start + run.Length);
                for (int index = run.Start; index < end; index++) values[index] = run.ClassIndex;
            }

            return new SemanticSegmentationMask(Width, Height, values);
        }
    }

    /// <summary>Stores an optional probability map in canonical row-major HWC order at tensor resolution. / 以规范行优先 HWC 顺序和张量分辨率存储可选概率图。</summary>
    public sealed class SegmentationProbabilityMap
    {
        private readonly float[] _values;

        /// <summary>Initializes a probability map by defensively copying HWC values. / 通过防御性复制 HWC 值初始化概率图。</summary>
        public SegmentationProbabilityMap(int width, int height, int classCount, float[] values)
            : this(width, height, classCount, values, false)
        {
        }

        internal SegmentationProbabilityMap(int width, int height, int classCount, float[] values, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (classCount <= 0 || classCount > ushort.MaxValue + 1) throw new ArgumentOutOfRangeException(nameof(classCount));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if ((long)width * height * classCount != values.LongLength) throw new ArgumentException("Probability-map dimensions do not match its values.", nameof(values));
            Width = width;
            Height = height;
            ClassCount = classCount;
            for (int index = 0; !takeOwnership && index < values.Length; index++)
            {
                float value = values[index];
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1) throw new ArgumentException("Probability-map values must be finite and in [0,1].", nameof(values));
            }
            _values = takeOwnership ? values : (float[])values.Clone();
        }

        /// <summary>Gets the tensor-resolution width. / 获取张量分辨率宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the tensor-resolution height. / 获取张量分辨率高度。</summary>
        public int Height { get; }
        /// <summary>Gets the channel or class count. / 获取通道或类别数。</summary>
        public int ClassCount { get; }
        /// <summary>Returns a defensive canonical HWC copy. / 返回规范 HWC 防御性副本。</summary>
        public float[] ToArray() => (float[])_values.Clone();
    }

    /// <summary>Describes the semantic polygon capability conclusion for a result. / 描述结果的语义多边形能力结论。</summary>
    public enum SegmentationPolygonStatus
    {
        /// <summary>Polygon extraction is unsupported because hole and component semantics are not guaranteed. / 不支持多边形提取，因为无法保证孔洞和连通域语义。</summary>
        Unsupported = 0
    }

    /// <summary>Contains an owned semantic mask, palette metadata, statistics, and optional bounded representations. / 包含自有语义掩码、调色板元数据、统计信息及可选有界表示。</summary>
    public sealed class SemanticSegmentationResult
    {
        private readonly IReadOnlyList<SemanticSegmentationClass> _classes;
        private readonly IReadOnlyList<SegmentationClassStatistics> _statistics;

        /// <summary>Initializes a complete semantic segmentation result. / 初始化完整语义分割结果。</summary>
        public SemanticSegmentationResult(SemanticSegmentationMask mask, IEnumerable<SemanticSegmentationClass> classes, IEnumerable<SegmentationClassStatistics> statistics, SegmentationRle? rle = null, SegmentationProbabilityMap? probabilityMap = null)
        {
            Mask = mask ?? throw new ArgumentNullException(nameof(mask));
            _classes = Copy(classes, nameof(classes));
            _statistics = Copy(statistics, nameof(statistics));
            Rle = rle;
            ProbabilityMap = probabilityMap;
        }

        /// <summary>Gets the owned dense class-index mask. / 获取自有稠密类别索引掩码。</summary>
        public SemanticSegmentationMask Mask { get; }
        /// <summary>Gets class, label, background, ignore, and palette metadata. / 获取类别、标签、背景、忽略及调色板元数据。</summary>
        public IReadOnlyList<SemanticSegmentationClass> Classes => _classes;
        /// <summary>Gets per-class pixel statistics. / 获取逐类别像素统计信息。</summary>
        public IReadOnlyList<SegmentationClassStatistics> Statistics => _statistics;
        /// <summary>Gets optional DeploySharp row-major RLE. / 获取可选的 DeploySharp 行优先 RLE。</summary>
        public SegmentationRle? Rle { get; }
        /// <summary>Gets an optional probability map retained at tensor resolution. / 获取在张量分辨率保留的可选概率图。</summary>
        public SegmentationProbabilityMap? ProbabilityMap { get; }
        /// <summary>Gets the honest polygon capability conclusion. / 获取明确的多边形能力结论。</summary>
        public SegmentationPolygonStatus PolygonStatus => SegmentationPolygonStatus.Unsupported;

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName) where T : class
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            var result = new List<T>();
            foreach (T value in values)
            {
                if (value == null) throw new ArgumentException("Result collections cannot contain null values.", parameterName);
                result.Add(value);
            }

            return result.AsReadOnly();
        }
    }

    /// <summary>Contains ordered semantic-segmentation results decoded from one true model batch. / 包含从一个真正模型 Batch 解码出的有序语义分割结果。</summary>
    public sealed class SemanticSegmentationBatchResult
    {
        private readonly IReadOnlyList<SemanticSegmentationResult> _items;

        /// <summary>Initializes an ordered non-empty semantic-segmentation batch result. / 初始化有序且非空的语义分割批结果。</summary>
        public SemanticSegmentationBatchResult(IEnumerable<SemanticSegmentationResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<SemanticSegmentationResult>();
            foreach (SemanticSegmentationResult item in items)
            {
                if (item == null) throw new ArgumentException("Batch results cannot contain null.", nameof(items));
                copied.Add(item);
            }

            if (copied.Count == 0) throw new ArgumentException("A batch result requires at least one item.", nameof(items));
            _items = copied.AsReadOnly();
        }

        /// <summary>Gets results in input batch-row order. / 按输入 Batch 行顺序获取结果。</summary>
        public IReadOnlyList<SemanticSegmentationResult> Items => _items;

        /// <summary>Gets the number of decoded batch rows. / 获取已解码的 Batch 行数。</summary>
        public int Count => _items.Count;

        /// <summary>Gets one result by its zero-based input batch-row index. / 按从零开始的输入 Batch 行索引获取一个结果。</summary>
        public SemanticSegmentationResult this[int index] => _items[index];
    }
}
