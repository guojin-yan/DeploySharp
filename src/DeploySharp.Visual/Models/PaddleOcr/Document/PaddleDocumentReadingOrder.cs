using System;
using System.Collections.Generic;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Selects a geometric page ordering strategy. / 选择页面几何排序策略。</summary>
    public enum PaddleDocumentReadingOrderMode
    {
        /// <summary>Keeps the source region order. / 保持源区域顺序。</summary>
        SourceOrder,
        /// <summary>Sorts by top, then leading edge, then source index. / 按顶部、阅读方向的起始边和源索引排序。</summary>
        TopToBottom,
        /// <summary>Recursively separates columns and horizontal sections at whitespace gaps. / 沿空白间隙递归分隔栏和水平分区。</summary>
        RecursiveXyCut
    }

    /// <summary>Immutable geometric reading-order settings; no pixels or model tensors are copied. / 不可变的几何阅读顺序配置，不复制像素或模型张量。</summary>
    /// <remarks>XY-cut is a layout heuristic for upright pages, not an official PaddleOCR ordering model. Overlapping regions without whitespace use top-to-bottom order. / XY-cut 是正向页面的版面启发式，不是官方 PaddleOCR 排序模型；没有空白分隔的重叠区域退回从上到下排序。</remarks>
    public sealed class PaddleDocumentReadingOrderOptions
    {
        /// <summary>Creates settings with page-relative whitespace thresholds and a bounded recursion depth. Right-to-left affects columns and horizontal ties, not top-to-bottom flow. / 创建使用整页比例空白阈值和有界递归深度的配置；从右到左影响栏顺序和水平并列区域，不反转上下顺序。</summary>
        public PaddleDocumentReadingOrderOptions(PaddleDocumentReadingOrderMode mode = PaddleDocumentReadingOrderMode.RecursiveXyCut,
            bool rightToLeft = false, double minimumColumnGapRatio = .02, double minimumRowGapRatio = .005, int maximumDepth = 32)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentReadingOrderMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            ValidateRatio(minimumColumnGapRatio, nameof(minimumColumnGapRatio));
            ValidateRatio(minimumRowGapRatio, nameof(minimumRowGapRatio));
            if (maximumDepth < 1 || maximumDepth > 64) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            Mode = mode; RightToLeft = rightToLeft;
            MinimumColumnGapRatio = minimumColumnGapRatio; MinimumRowGapRatio = minimumRowGapRatio; MaximumDepth = maximumDepth;
        }

        /// <summary>Gets the requested strategy. / 获取所选策略。</summary>
        public PaddleDocumentReadingOrderMode Mode { get; }
        /// <summary>Gets whether columns and horizontal ties are read from right to left. / 获取是否从右到左阅读栏和水平并列区域。</summary>
        public bool RightToLeft { get; }
        /// <summary>Gets the minimum column gap as a fraction of full page width. / 获取最小栏间隙占整页宽度的比例。</summary>
        public double MinimumColumnGapRatio { get; }
        /// <summary>Gets the minimum section gap as a fraction of full page height. / 获取最小分区间隙占整页高度的比例。</summary>
        public double MinimumRowGapRatio { get; }
        /// <summary>Gets the maximum split depth; remaining regions use top-to-bottom order. / 获取最大分割深度；剩余区域采用从上到下排序。</summary>
        public int MaximumDepth { get; }

        private static void ValidateRatio(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value >= 1) throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Computes a permutation of original page-region indices without changing source regions. / 计算原始页面区域索引的排列，不改变源区域。</summary>
    public static class PaddleDocumentReadingOrder
    {
        /// <summary>Returns an immutable index permutation. Null settings preserve source order. Geometric modes clip only their sorting geometry to the page and reject empty/nonfinite/nonintersecting regions. / 返回不可变索引排列；空配置保持源顺序；几何模式仅将排序几何裁至页面，拒绝空、非有限或不相交的区域。</summary>
        public static IReadOnlyList<int> GetIndices(IReadOnlyList<PaddleDocumentRegion> regions, VisualSize pageSize,
            PaddleDocumentReadingOrderOptions? options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (pageSize.Width <= 0 || pageSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            cancellationToken.ThrowIfCancellationRequested();
            if (options == null || options.Mode == PaddleDocumentReadingOrderMode.SourceOrder)
                return CopyIndices(null, regions.Count);

            var blocks = new List<Block>(regions.Count);
            for (int index = 0; index < regions.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PaddleDocumentRegion region = regions[index] ?? throw new ArgumentException("A reading-order region cannot be null.", nameof(regions));
                var box = region.Bounds;
                if (!Finite(box.X) || !Finite(box.Y) || !Finite(box.Width) || !Finite(box.Height) || box.Width <= 0 || box.Height <= 0)
                    throw new ArgumentException("Reading-order regions must be finite, positive rectangles. index=" + index, nameof(regions));
                double left = Math.Max(0, (double)box.X), top = Math.Max(0, (double)box.Y);
                double right = Math.Min(pageSize.Width, (double)box.X + box.Width), bottom = Math.Min(pageSize.Height, (double)box.Y + box.Height);
                if (right <= left || bottom <= top) throw new ArgumentException("A reading-order region does not intersect the page. index=" + index, nameof(regions));
                blocks.Add(new Block(index, left, top, right, bottom));
            }
            var ordered = new List<int>(blocks.Count);
            if (options.Mode == PaddleDocumentReadingOrderMode.TopToBottom)
                AppendTopToBottom(blocks, options.RightToLeft, ordered);
            else
                Split(blocks, options, pageSize.Width * options.MinimumColumnGapRatio,
                    pageSize.Height * options.MinimumRowGapRatio, 0, ordered, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return ordered.AsReadOnly();
        }

        // Shared by exported result contracts, including caller-supplied custom orders.
        internal static IReadOnlyList<int> CopyIndices(IReadOnlyList<int>? indices, int count)
        {
            if (indices != null && indices.Count != count) throw new ArgumentException("Reading order must include every original region exactly once.", nameof(indices));
            var copied = new int[count];
            var seen = indices == null ? null : new bool[count];
            for (int index = 0; index < count; index++)
            {
                int value = indices == null ? index : indices[index];
                if (value < 0 || value >= count || (seen != null && seen[value]))
                    throw new ArgumentException("Reading order must be a permutation of the original region indices.", nameof(indices));
                copied[index] = value;
                if (seen != null) seen[value] = true;
            }
            return Array.AsReadOnly(copied);
        }

        private static void Split(List<Block> blocks, PaddleDocumentReadingOrderOptions options, double columnGap, double rowGap,
            int depth, List<int> ordered, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (blocks.Count <= 1 || depth >= options.MaximumDepth)
            {
                AppendTopToBottom(blocks, options.RightToLeft, ordered);
                return;
            }
            // A clear vertical gutter takes precedence over line/paragraph spacing.
            // A spanning title/table prevents that cut, so first split into horizontal sections.
            bool vertical = FindCut(blocks, true, columnGap, out double cut);
            if (!vertical && !FindCut(blocks, false, rowGap, out cut))
            {
                AppendTopToBottom(blocks, options.RightToLeft, ordered);
                return;
            }
            var first = new List<Block>();
            var second = new List<Block>();
            foreach (Block block in blocks)
            {
                bool before = (vertical ? block.Right : block.Bottom) < cut;
                (before ? first : second).Add(block);
            }
            if (vertical && options.RightToLeft) { var temporary = first; first = second; second = temporary; }
            Split(first, options, columnGap, rowGap, depth + 1, ordered, token);
            Split(second, options, columnGap, rowGap, depth + 1, ordered, token);
        }

        private static bool FindCut(List<Block> blocks, bool vertical, double minimumGap, out double cut)
        {
            blocks.Sort((left, right) =>
            {
                int compared = (vertical ? left.Left : left.Top).CompareTo(vertical ? right.Left : right.Top);
                return compared != 0 ? compared : left.Index.CompareTo(right.Index);
            });
            double reach = vertical ? blocks[0].Right : blocks[0].Bottom;
            double largestGap = 0;
            cut = 0;
            for (int index = 1; index < blocks.Count; index++)
            {
                Block block = blocks[index];
                double start = vertical ? block.Left : block.Top;
                double gap = start - reach;
                if (gap > 0 && gap >= minimumGap && gap > largestGap)
                {
                    largestGap = gap;
                    cut = reach + gap / 2;
                }
                reach = Math.Max(reach, vertical ? block.Right : block.Bottom);
            }
            return largestGap > 0;
        }

        private static void AppendTopToBottom(List<Block> blocks, bool rightToLeft, List<int> ordered)
        {
            blocks.Sort((left, right) =>
            {
                int compared = left.Top.CompareTo(right.Top);
                if (compared != 0) return compared;
                compared = rightToLeft ? right.Right.CompareTo(left.Right) : left.Left.CompareTo(right.Left);
                return compared != 0 ? compared : left.Index.CompareTo(right.Index);
            });
            foreach (Block block in blocks) ordered.Add(block.Index);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct Block
        {
            internal Block(int index, double left, double top, double right, double bottom)
            { Index = index; Left = left; Top = top; Right = right; Bottom = bottom; }
            internal int Index { get; }
            internal double Left { get; }
            internal double Top { get; }
            internal double Right { get; }
            internal double Bottom { get; }
        }
    }
}
