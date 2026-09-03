using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Identifies the coordinate space occupied by an owned instance mask. / 标识自有实例掩码所在的坐标空间。</summary>
    public enum InstanceMaskCoordinateSpace
    {
        /// <summary>Original source-image pixels. / 原始源图像素。</summary>
        SourceImage = 0,
        /// <summary>Model-input pixels. / 模型输入像素。</summary>
        ModelInput = 1,
        /// <summary>Output tensor-grid positions. / 输出张量网格位置。</summary>
        TensorGrid = 2
    }

    /// <summary>Identifies how overlapping independent masks are represented. / 标识如何表示相互重叠的独立掩码。</summary>
    public enum InstanceMaskOverlapMode
    {
        /// <summary>Retain every independent mask and do not create an ownership map. / 保留每个独立掩码且不创建所有权图。</summary>
        Independent = 0,
        /// <summary>Also create a single-owner map, resolving overlaps by descending score then source index. / 另建单一所有者图，并按分数降序及源索引解决重叠。</summary>
        ScorePriorityOwnership = 1
    }

    /// <summary>Represents one foreground run in DeploySharp row-major binary RLE. / 表示 DeploySharp 行优先二值 RLE 中的一段前景游程。</summary>
    public readonly struct InstanceMaskRun : IEquatable<InstanceMaskRun>
    {
        /// <summary>Initializes a foreground run. / 初始化前景游程。</summary>
        public InstanceMaskRun(int start, int length)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            Start = start;
            Length = length;
        }

        /// <summary>Gets the zero-based row-major start index. / 获取从零开始的行优先起始索引。</summary>
        public int Start { get; }
        /// <summary>Gets the positive run length. / 获取正数游程长度。</summary>
        public int Length { get; }
        /// <inheritdoc />
        /// <remarks>Compares start and length exactly. / 精确比较起始索引和长度。</remarks>
        public bool Equals(InstanceMaskRun other) => Start == other.Start && Length == other.Length;
        /// <inheritdoc />
        /// <remarks>Compares an object with this run. / 将对象与此游程比较。</remarks>
        public override bool Equals(object? obj) => obj is InstanceMaskRun other && Equals(other);
        /// <inheritdoc />
        /// <remarks>Computes a component-based hash code. / 根据各分量计算哈希码。</remarks>
        public override int GetHashCode() => unchecked((Start * 397) ^ Length);
        /// <summary>Compares two runs for equality. / 比较两个游程是否相等。</summary>
        public static bool operator ==(InstanceMaskRun left, InstanceMaskRun right) => left.Equals(right);
        /// <summary>Compares two runs for inequality. / 比较两个游程是否不相等。</summary>
        public static bool operator !=(InstanceMaskRun left, InstanceMaskRun right) => !left.Equals(right);
    }

    /// <summary>Stores DeploySharp row-major foreground runs; this is not COCO compressed RLE. / 存储 DeploySharp 行优先前景游程；此格式不是 COCO 压缩 RLE。</summary>
    public sealed class InstanceMaskRle
    {
        private readonly IReadOnlyList<InstanceMaskRun> _runs;

        /// <summary>Initializes and validates row-major foreground runs. / 初始化并验证行优先前景游程。</summary>
        public InstanceMaskRle(int width, int height, IEnumerable<InstanceMaskRun> runs)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (runs == null) throw new ArgumentNullException(nameof(runs));
            int pixels = checked(width * height);
            var copy = new List<InstanceMaskRun>();
            int previousEnd = 0;
            foreach (InstanceMaskRun run in runs)
            {
                int end = checked(run.Start + run.Length);
                if (run.Start < previousEnd || end > pixels) throw new ArgumentException("RLE runs must be ordered, non-overlapping, and inside the mask.", nameof(runs));
                copy.Add(run);
                previousEnd = end;
            }

            Width = width;
            Height = height;
            _runs = new ReadOnlyCollection<InstanceMaskRun>(copy);
        }

        /// <summary>Gets the stable format identifier. / 获取稳定格式标识符。</summary>
        public string Format => "deploysharp-row-major-foreground-runs-v1";
        /// <summary>Gets the mask width. / 获取掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the mask height. / 获取掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets ordered foreground runs. / 获取有序前景游程。</summary>
        public IReadOnlyList<InstanceMaskRun> Runs => _runs;

        /// <summary>Encodes a binary mask with a bounded number of row-major foreground runs. / 使用有界数量的行优先前景游程编码二值掩码。</summary>
        public static InstanceMaskRle Encode(InstanceBinaryMask mask, int maximumRuns = int.MaxValue)
            => Encode(mask, maximumRuns, System.Threading.CancellationToken.None);

        internal static InstanceMaskRle Encode(InstanceBinaryMask mask, int maximumRuns, System.Threading.CancellationToken cancellationToken)
        {
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (maximumRuns <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRuns));
            var runs = new List<InstanceMaskRun>();
            byte[] pixels = mask.GetPixelsUnsafe();
            int pixelOffset = mask.PixelOffset;
            int pixelCount = mask.PixelCount;
            int index = 0;
            while (index < pixelCount)
            {
                if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                while (index < pixelCount && pixels[pixelOffset + index] == 0) index++;
                if (index == pixelCount) break;
                int start = index;
                while (index < pixelCount && pixels[pixelOffset + index] != 0)
                {
                    index++;
                    if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                }
                if (runs.Count == maximumRuns) throw new InvalidOperationException("The binary mask exceeds the configured RLE run bound.");
                runs.Add(new InstanceMaskRun(start, index - start));
            }

            return new InstanceMaskRle(mask.Width, mask.Height, runs);
        }

        /// <summary>Decodes this RLE into an owned source-independent binary mask. / 将此 RLE 解码为自有且与源缓冲区独立的二值掩码。</summary>
        public InstanceBinaryMask Decode(InstanceMaskCoordinateSpace coordinateSpace = InstanceMaskCoordinateSpace.SourceImage, int originX = 0, int originY = 0)
        {
            var pixels = new byte[checked(Width * Height)];
            for (int runIndex = 0; runIndex < _runs.Count; runIndex++)
            {
                InstanceMaskRun run = _runs[runIndex];
                for (int index = run.Start; index < run.Start + run.Length; index++) pixels[index] = 1;
            }

            int foreground = 0;
            for (int index = 0; index < _runs.Count; index++) foreground = checked(foreground + _runs[index].Length);
            return new InstanceBinaryMask(Width, Height, pixels, coordinateSpace, originX, originY, foreground);
        }
    }

    /// <summary>Stores an owned dense row-major binary instance mask. / 存储自有的稠密行优先二值实例掩码。</summary>
    public sealed class InstanceBinaryMask
    {
        private readonly byte[] _pixels;
        private readonly int _pixelOffset;
        private readonly int _foregroundPixelCount;

        /// <summary>Initializes a mask by defensively copying bytes whose values must be zero or one. / 通过防御性复制值必须为零或一的字节来初始化掩码。</summary>
        public InstanceBinaryMask(int width, int height, byte[] pixels, InstanceMaskCoordinateSpace coordinateSpace = InstanceMaskCoordinateSpace.SourceImage, int originX = 0, int originY = 0)
            : this(width, height, pixels, coordinateSpace, originX, originY, false)
        {
        }

        internal InstanceBinaryMask(int width, int height, byte[] pixels, InstanceMaskCoordinateSpace coordinateSpace, int originX, int originY, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (!Enum.IsDefined(typeof(InstanceMaskCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if ((long)width * height != pixels.LongLength) throw new ArgumentException("Mask dimensions do not match the pixel count.", nameof(pixels));
            int foreground = 0;
            for (int index = 0; index < pixels.Length; index++)
            {
                if (pixels[index] > 1) throw new ArgumentException("A binary mask may contain only zero and one.", nameof(pixels));
                foreground += pixels[index];
            }

            Width = width;
            Height = height;
            CoordinateSpace = coordinateSpace;
            OriginX = originX;
            OriginY = originY;
            _foregroundPixelCount = foreground;
            _pixels = takeOwnership ? pixels : (byte[])pixels.Clone();
            _pixelOffset = 0;
        }

        internal InstanceBinaryMask(int width, int height, byte[] pixels, InstanceMaskCoordinateSpace coordinateSpace, int originX, int originY, int foregroundPixelCount)
        {
            if (width <= 0 || height <= 0 || pixels == null || (long)width * height != pixels.LongLength) throw new ArgumentException("Owned mask dimensions are invalid.", nameof(pixels));
            if (!Enum.IsDefined(typeof(InstanceMaskCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (foregroundPixelCount < 0 || foregroundPixelCount > pixels.Length) throw new ArgumentOutOfRangeException(nameof(foregroundPixelCount));
            Width = width;
            Height = height;
            CoordinateSpace = coordinateSpace;
            OriginX = originX;
            OriginY = originY;
            _foregroundPixelCount = foregroundPixelCount;
            _pixels = pixels;
            _pixelOffset = 0;
        }

        internal InstanceBinaryMask(int width, int height, byte[] pixels, int pixelOffset, InstanceMaskCoordinateSpace coordinateSpace, int originX, int originY, int foregroundPixelCount)
        {
            int pixelCount = checked(width * height);
            if (width <= 0 || height <= 0 || pixels == null || pixelOffset < 0 || pixelOffset > pixels.Length - pixelCount) throw new ArgumentException("Owned mask slice dimensions are invalid.", nameof(pixels));
            if (!Enum.IsDefined(typeof(InstanceMaskCoordinateSpace), coordinateSpace)) throw new ArgumentOutOfRangeException(nameof(coordinateSpace));
            if (foregroundPixelCount < 0 || foregroundPixelCount > pixelCount) throw new ArgumentOutOfRangeException(nameof(foregroundPixelCount));
            Width = width;
            Height = height;
            CoordinateSpace = coordinateSpace;
            OriginX = originX;
            OriginY = originY;
            _foregroundPixelCount = foregroundPixelCount;
            _pixels = pixels;
            _pixelOffset = pixelOffset;
        }

        /// <summary>Gets the mask width. / 获取掩码宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the mask height. / 获取掩码高度。</summary>
        public int Height { get; }
        /// <summary>Gets the mask coordinate space. / 获取掩码坐标空间。</summary>
        public InstanceMaskCoordinateSpace CoordinateSpace { get; }
        /// <summary>Gets the horizontal origin in the declared coordinate space. / 获取所声明坐标空间中的水平原点。</summary>
        public int OriginX { get; }
        /// <summary>Gets the vertical origin in the declared coordinate space. / 获取所声明坐标空间中的垂直原点。</summary>
        public int OriginY { get; }
        /// <summary>Gets the total pixel count. / 获取总像素数。</summary>
        public int PixelCount => checked(Width * Height);
        /// <summary>Gets the foreground pixel count. / 获取前景像素数。</summary>
        public int ForegroundPixelCount => _foregroundPixelCount;

        /// <summary>Gets whether a zero-based mask coordinate is foreground. / 获取从零开始的掩码坐标是否为前景。</summary>
        public bool IsForeground(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _pixels[_pixelOffset + (y * Width) + x] != 0;
        }

        /// <summary>Copies all row-major pixels to a caller-provided buffer. / 将所有行优先像素复制到调用方提供的缓冲区。</summary>
        public void CopyTo(byte[] destination, int destinationIndex = 0)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int pixelCount = PixelCount;
            if (destinationIndex < 0 || destinationIndex > destination.Length - pixelCount) throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            Array.Copy(_pixels, _pixelOffset, destination, destinationIndex, pixelCount);
        }

        /// <summary>Copies all row-major pixels as Boolean foreground values. / 将所有行优先像素作为布尔前景值复制到调用方缓冲区。</summary>
        public void CopyTo(bool[] destination, int destinationIndex = 0)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int pixelCount = PixelCount;
            if (destinationIndex < 0 || destinationIndex > destination.Length - pixelCount) throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            for (int index = 0; index < pixelCount; index++) destination[destinationIndex + index] = _pixels[_pixelOffset + index] != 0;
        }

        /// <summary>Gets the half-open foreground bounds in the declared coordinate space, or null for an empty mask. / 获取所声明坐标空间中的半开前景边界；空掩码返回 null。</summary>
        public RectangleF? GetForegroundBounds()
        {
            if (_foregroundPixelCount == 0) return null;
            int minimumX = Width;
            int minimumY = Height;
            int maximumX = -1;
            int maximumY = -1;
            for (int y = 0; y < Height; y++)
            {
                int rowOffset = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (_pixels[_pixelOffset + rowOffset + x] == 0) continue;
                    if (x < minimumX) minimumX = x;
                    if (x > maximumX) maximumX = x;
                    if (y < minimumY) minimumY = y;
                    if (y > maximumY) maximumY = y;
                }
            }

            return new RectangleF(OriginX + minimumX, OriginY + minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
        }

        /// <summary>Returns a defensive row-major pixel copy. / 返回行优先像素的防御性副本。</summary>
        public byte[] ToArray()
        {
            var result = new byte[PixelCount];
            Array.Copy(_pixels, _pixelOffset, result, 0, result.Length);
            return result;
        }

        /// <summary>Computes SHA-256 over coordinate metadata and row-major pixels. / 对坐标元数据和行优先像素计算 SHA-256。</summary>
        public string ComputeSha256()
        {
            using var stream = new MemoryStream(PixelCount + 32);
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write((int)CoordinateSpace);
                writer.Write(OriginX);
                writer.Write(OriginY);
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(_pixels, _pixelOffset, PixelCount);
            }

            using SHA256 sha = SHA256.Create();
            return Hex(sha.ComputeHash(stream.ToArray()));
        }

        internal byte GetPixelUnchecked(int index) => _pixels[_pixelOffset + index];

        // Internal decoder fast path. The array remains owned by this mask and is never exposed
        // through the public API; callers use it only while the mask is immutable.
        internal byte[] GetPixelsUnsafe() => _pixels;

        internal int PixelOffset => _pixelOffset;

        internal static string Hex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = alphabet[bytes[index] >> 4];
                characters[(index * 2) + 1] = alphabet[bytes[index] & 15];
            }

            return new string(characters);
        }
    }

    /// <summary>Represents one scored, classified instance with an owned source-space mask. / 表示一个带分数、类别和自有源图空间掩码的实例。</summary>
    public sealed class InstanceSegmentationInstance
    {
        private readonly IReadOnlyDictionary<string, string> _metadata;

        /// <summary>Initializes a canonical instance segmentation item. / 初始化规范实例分割项。</summary>
        public InstanceSegmentationInstance(int sourceIndex, int classIndex, string label, float score, RectangleF boundingBox, InstanceBinaryMask mask, InstanceMaskRle? rle = null, string? externalId = null, IEnumerable<KeyValuePair<string, string>>? metadata = null)
        {
            if (sourceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            if (classIndex < 0) throw new ArgumentOutOfRangeException(nameof(classIndex));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A class label is required.", nameof(label));
            if (float.IsNaN(score) || float.IsInfinity(score) || score < 0) throw new ArgumentOutOfRangeException(nameof(score));
            if (float.IsNaN(boundingBox.X) || float.IsInfinity(boundingBox.X) || float.IsNaN(boundingBox.Y) || float.IsInfinity(boundingBox.Y) || float.IsNaN(boundingBox.Width) || float.IsInfinity(boundingBox.Width) || float.IsNaN(boundingBox.Height) || float.IsInfinity(boundingBox.Height) || boundingBox.Width <= 0 || boundingBox.Height <= 0) throw new ArgumentOutOfRangeException(nameof(boundingBox));
            Mask = mask ?? throw new ArgumentNullException(nameof(mask));
            if (rle != null && (rle.Width != mask.Width || rle.Height != mask.Height)) throw new ArgumentException("RLE dimensions must match the dense mask.", nameof(rle));
            var metadataCopy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (metadata != null)
            {
                foreach (KeyValuePair<string, string> pair in metadata)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) throw new ArgumentException("Metadata keys and values must be non-empty.", nameof(metadata));
                    if (metadataCopy.ContainsKey(pair.Key)) throw new ArgumentException("Metadata keys must be unique.", nameof(metadata));
                    metadataCopy.Add(pair.Key, pair.Value);
                }
            }

            SourceIndex = sourceIndex;
            ClassIndex = classIndex;
            Label = label;
            Score = score;
            BoundingBox = boundingBox;
            Rle = rle;
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId;
            _metadata = new ReadOnlyDictionary<string, string>(metadataCopy);
        }

        /// <summary>Gets the original candidate index before filtering and NMS. / 获取筛选和 NMS 前的原始候选索引。</summary>
        public int SourceIndex { get; }
        /// <summary>Gets the zero-based class index. / 获取从零开始的类别索引。</summary>
        public int ClassIndex { get; }
        /// <summary>Gets the stable display label. / 获取稳定显示标签。</summary>
        public string Label { get; }
        /// <summary>Gets the instance confidence score. / 获取实例置信分数。</summary>
        public float Score { get; }
        /// <summary>Gets the clipped half-open source-space bounding box. / 获取裁剪后的半开区间源图空间边界框。</summary>
        public RectangleF BoundingBox { get; }
        /// <summary>Gets the owned dense binary mask. / 获取自有的稠密二值掩码。</summary>
        public InstanceBinaryMask Mask { get; }
        /// <summary>Gets optional DeploySharp row-major foreground-run RLE. / 获取可选的 DeploySharp 行优先前景游程 RLE。</summary>
        public InstanceMaskRle? Rle { get; }
        /// <summary>Gets an optional external instance identifier. / 获取可选的外部实例标识符。</summary>
        public string? ExternalId { get; }
        /// <summary>Gets immutable application metadata. / 获取不可变应用元数据。</summary>
        public IReadOnlyDictionary<string, string> Metadata => _metadata;
    }

    /// <summary>Stores a source-space row-major map from pixels to result instance indices. / 存储从源图空间像素到结果实例索引的行优先映射。</summary>
    public sealed class InstanceMaskOwnershipMap
    {
        private readonly int[] _owners;

        /// <summary>Initializes an ownership map by defensively copying indices; minus one denotes background. / 通过防御性复制索引初始化所有权图；负一表示背景。</summary>
        public InstanceMaskOwnershipMap(int width, int height, int instanceCount, int[] owners)
            : this(width, height, instanceCount, owners, false)
        {
        }

        internal InstanceMaskOwnershipMap(int width, int height, int instanceCount, int[] owners, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (instanceCount < 0) throw new ArgumentOutOfRangeException(nameof(instanceCount));
            if (owners == null) throw new ArgumentNullException(nameof(owners));
            if ((long)width * height != owners.LongLength) throw new ArgumentException("Ownership dimensions do not match the element count.", nameof(owners));
            if (!takeOwnership) for (int index = 0; index < owners.Length; index++) if (owners[index] < -1 || owners[index] >= instanceCount) throw new ArgumentException("An owner index is outside the result instance range.", nameof(owners));
            Width = width;
            Height = height;
            InstanceCount = instanceCount;
            _owners = takeOwnership ? owners : (int[])owners.Clone();
        }

        /// <summary>Gets the ownership-map width. / 获取所有权图宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the ownership-map height. / 获取所有权图高度。</summary>
        public int Height { get; }
        /// <summary>Gets the number of addressable result instances. / 获取可寻址的结果实例数。</summary>
        public int InstanceCount { get; }
        /// <summary>Gets the result instance index at a source pixel, or minus one for background. / 获取源图像素处的结果实例索引，背景为负一。</summary>
        public int GetOwnerIndex(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y));
            return _owners[(y * Width) + x];
        }

        /// <summary>Returns a defensive row-major owner-index copy. / 返回行优先所有者索引的防御性副本。</summary>
        public int[] ToArray() => (int[])_owners.Clone();
    }

    /// <summary>Contains deterministic, owned instance segmentation results. / 包含确定性且自有的实例分割结果。</summary>
    public sealed class InstanceSegmentationResult
    {
        private readonly IReadOnlyList<InstanceSegmentationInstance> _instances;

        /// <summary>Initializes a result whose instances must be ordered by descending score then source index. / 初始化实例必须按分数降序再按源索引排序的结果。</summary>
        public InstanceSegmentationResult(IEnumerable<InstanceSegmentationInstance> instances, VisualSize sourceSize, string profileId, ModelId modelId, InstanceMaskOverlapMode overlapMode = InstanceMaskOverlapMode.Independent, InstanceMaskOwnershipMap? ownershipMap = null)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile identifier is required.", nameof(profileId));
            if (modelId.IsEmpty) throw new ArgumentException("A model identifier is required.", nameof(modelId));
            if (!Enum.IsDefined(typeof(InstanceMaskOverlapMode), overlapMode)) throw new ArgumentOutOfRangeException(nameof(overlapMode));
            var copy = new List<InstanceSegmentationInstance>();
            var sourceIndices = new HashSet<int>();
            InstanceSegmentationInstance? previous = null;
            foreach (InstanceSegmentationInstance instance in instances)
            {
                if (instance == null) throw new ArgumentException("Instances cannot contain null.", nameof(instances));
                if (!sourceIndices.Add(instance.SourceIndex)) throw new ArgumentException("Instance source indices must be unique.", nameof(instances));
                if (instance.Mask.CoordinateSpace != InstanceMaskCoordinateSpace.SourceImage || instance.Mask.OriginX != 0 || instance.Mask.OriginY != 0 || instance.Mask.Width != sourceSize.Width || instance.Mask.Height != sourceSize.Height) throw new ArgumentException("Result masks must occupy the full source-image space.", nameof(instances));
                if (previous != null && (instance.Score > previous.Score || (instance.Score == previous.Score && instance.SourceIndex < previous.SourceIndex))) throw new ArgumentException("Instances are not in deterministic score/source-index order.", nameof(instances));
                copy.Add(instance);
                previous = instance;
            }

            if (overlapMode == InstanceMaskOverlapMode.Independent && ownershipMap != null) throw new ArgumentException("Independent overlap mode cannot include an ownership map.", nameof(ownershipMap));
            if (overlapMode == InstanceMaskOverlapMode.ScorePriorityOwnership && ownershipMap == null) throw new ArgumentNullException(nameof(ownershipMap));
            if (ownershipMap != null && (ownershipMap.Width != sourceSize.Width || ownershipMap.Height != sourceSize.Height || ownershipMap.InstanceCount != copy.Count)) throw new ArgumentException("Ownership map dimensions or instance count do not match the result.", nameof(ownershipMap));
            _instances = new ReadOnlyCollection<InstanceSegmentationInstance>(copy);
            SourceSize = sourceSize;
            ProfileId = profileId;
            ModelId = modelId;
            OverlapMode = overlapMode;
            OwnershipMap = ownershipMap;
        }

        /// <summary>Gets instances in deterministic score/source-index order. / 获取按确定性分数及源索引顺序排列的实例。</summary>
        public IReadOnlyList<InstanceSegmentationInstance> Instances => _instances;
        /// <summary>Gets the original source-image size. / 获取原始源图尺寸。</summary>
        public VisualSize SourceSize { get; }
        /// <summary>Gets the profile identifier. / 获取 Profile 标识符。</summary>
        public string ProfileId { get; }
        /// <summary>Gets the logical model identifier. / 获取逻辑模型标识符。</summary>
        public ModelId ModelId { get; }
        /// <summary>Gets the overlap representation mode. / 获取重叠表示模式。</summary>
        public InstanceMaskOverlapMode OverlapMode { get; }
        /// <summary>Gets the optional score-priority ownership map. / 获取可选的分数优先所有权图。</summary>
        public InstanceMaskOwnershipMap? OwnershipMap { get; }

        /// <summary>Computes SHA-256 over canonical result metadata and independent masks. / 对规范结果元数据和独立掩码计算 SHA-256。</summary>
        public string ComputeSha256()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(ProfileId);
                writer.Write(ModelId.Value);
                writer.Write(SourceSize.Width);
                writer.Write(SourceSize.Height);
                writer.Write((int)OverlapMode);
                writer.Write(_instances.Count);
                for (int instanceIndex = 0; instanceIndex < _instances.Count; instanceIndex++)
                {
                    InstanceSegmentationInstance instance = _instances[instanceIndex];
                    writer.Write(instance.SourceIndex);
                    writer.Write(instance.ClassIndex);
                    writer.Write(instance.Label);
                    writer.Write(instance.Score);
                    writer.Write(instance.BoundingBox.X);
                    writer.Write(instance.BoundingBox.Y);
                    writer.Write(instance.BoundingBox.Width);
                    writer.Write(instance.BoundingBox.Height);
                    writer.Write(instance.Mask.ComputeSha256());
                    writer.Write(instance.ExternalId ?? string.Empty);
                    KeyValuePair<string, string>[] metadata = instance.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                    writer.Write(metadata.Length);
                    for (int metadataIndex = 0; metadataIndex < metadata.Length; metadataIndex++) { writer.Write(metadata[metadataIndex].Key); writer.Write(metadata[metadataIndex].Value); }
                }

                if (OwnershipMap == null) writer.Write(false);
                else
                {
                    writer.Write(true);
                    int[] owners = OwnershipMap.ToArray();
                    for (int index = 0; index < owners.Length; index++) writer.Write(owners[index]);
                }
            }

            using SHA256 sha = SHA256.Create();
            return InstanceBinaryMask.Hex(sha.ComputeHash(stream.ToArray()));
        }
    }

    /// <summary>Contains one instance-segmentation result for every row of a true model batch. / 包含真正模型 Batch 中每一行的实例分割结果。</summary>
    public sealed class InstanceSegmentationBatchResult
    {
        private readonly IReadOnlyList<InstanceSegmentationResult> _items;

        /// <summary>Initializes an ordered instance-segmentation batch result. / 初始化有序实例分割 Batch 结果。</summary>
        public InstanceSegmentationBatchResult(IEnumerable<InstanceSegmentationResult> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var copied = new List<InstanceSegmentationResult>();
            foreach (InstanceSegmentationResult item in items) copied.Add(item ?? throw new ArgumentException("Instance-segmentation batch items cannot contain null values.", nameof(items)));
            if (copied.Count <= 1) throw new ArgumentException("A batch result requires at least two items; batch one uses InstanceSegmentationResult.", nameof(items));
            _items = new ReadOnlyCollection<InstanceSegmentationResult>(copied);
        }

        /// <summary>Gets the number of decoded rows. / 获取已解码行数。</summary>
        public int Count => _items.Count;
        /// <summary>Gets a result by input-row index. / 按输入行索引获取结果。</summary>
        public InstanceSegmentationResult this[int index] => _items[index];
        /// <summary>Gets ordered instance-segmentation results. / 获取有序实例分割结果。</summary>
        public IReadOnlyList<InstanceSegmentationResult> Items => _items;
    }
}
