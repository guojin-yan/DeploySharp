#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Geometry;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Optionally processes uncovered image regions while bounding per-page VL work. / 可选处理未被其它任务覆盖的图片区域，并限制每页 VL 工作量。</summary>
    public sealed class PaddleOcrVlImageFallbackOptions
    {
        /// <summary>Creates an opt-in fallback for image regions skipped by the category map. / 为类别映射跳过的图片区域创建显式回退。</summary>
        public PaddleOcrVlImageFallbackOptions(PaddleOcrVlTask task = PaddleOcrVlTask.TextRecognition,
            double minimumUncoveredAreaRatio = .25, int maximumRegionsPerPage = 1)
        {
            if (!Enum.IsDefined(typeof(PaddleOcrVlTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            if (double.IsNaN(minimumUncoveredAreaRatio) || minimumUncoveredAreaRatio <= 0 || minimumUncoveredAreaRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumUncoveredAreaRatio));
            if (maximumRegionsPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(maximumRegionsPerPage));
            Task = task;
            MinimumUncoveredAreaRatio = minimumUncoveredAreaRatio;
            MaximumRegionsPerPage = maximumRegionsPerPage;
        }

        /// <summary>Gets the VL task used for a selected image region. / 获取选中图片区域的 VL 任务。</summary>
        public PaddleOcrVlTask Task { get; }
        /// <summary>Gets the minimum image area fraction not covered by normally routed regions. / 获取未被常规任务覆盖的最小图片面积比例。</summary>
        public double MinimumUncoveredAreaRatio { get; }
        /// <summary>Gets the maximum number of fallback regions on one page. / 获取单页回退区域数量上限。</summary>
        public int MaximumRegionsPerPage { get; }
    }

    /// <summary>Selects VL tasks from layout categories using an immutable, case-insensitive mapping. / 使用不可变、不区分大小写的版面类别映射选择 VL 任务。</summary>
    /// <remarks>A null mapping explicitly skips that category, including when an unmatched-category task is configured. This router does not detect layout or infer reading order. / 空映射明确跳过该类别，即使配置了未匹配类别任务也如此。本类不执行版面检测或推断阅读顺序。</remarks>
    public sealed class PaddleOcrVlTaskRouter
    {
        private readonly IReadOnlyDictionary<string, PaddleOcrVlTask?> _taskByCategory;

        /// <summary>Copies category mappings and an optional task for unmatched categories. / 复制类别映射及可选的未匹配类别任务。</summary>
        public PaddleOcrVlTaskRouter(IEnumerable<KeyValuePair<string, PaddleOcrVlTask?>> taskByCategory,
            PaddleOcrVlTask? unmatchedCategoryTask = null, PaddleOcrVlImageFallbackOptions? imageFallback = null)
        {
            if (taskByCategory == null) throw new ArgumentNullException(nameof(taskByCategory));
            ValidateTask(unmatchedCategoryTask);
            var copied = new Dictionary<string, PaddleOcrVlTask?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, PaddleOcrVlTask?> mapping in taskByCategory)
            {
                if (string.IsNullOrWhiteSpace(mapping.Key)) throw new ArgumentException("A layout category cannot be empty.", nameof(taskByCategory));
                ValidateTask(mapping.Value);
                if (!copied.TryAdd(mapping.Key.Trim(), mapping.Value))
                    throw new ArgumentException("Duplicate layout category: " + mapping.Key, nameof(taskByCategory));
            }
            _taskByCategory = new ReadOnlyDictionary<string, PaddleOcrVlTask?>(copied);
            UnmatchedCategoryTask = unmatchedCategoryTask;
            ImageFallback = imageFallback;
        }

        /// <summary>Gets the copied category mappings; null values explicitly skip a category. / 获取复制的类别映射；空值明确跳过该类别。</summary>
        public IReadOnlyDictionary<string, PaddleOcrVlTask?> TaskByCategory => _taskByCategory;
        /// <summary>Gets the task for unmatched categories, or null to skip them. / 获取未匹配类别的任务；空值表示跳过。</summary>
        public PaddleOcrVlTask? UnmatchedCategoryTask { get; }
        /// <summary>Gets the optional bounded image fallback. / 获取可选且有界的图片区域回退设置。</summary>
        public PaddleOcrVlImageFallbackOptions? ImageFallback { get; }

        /// <summary>Resolves a trimmed category; returns false when generation should be skipped. / 解析去除两端空白的类别；应跳过生成时返回 false。</summary>
        public bool TryGetTask(string category, out PaddleOcrVlTask task)
        {
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("A layout category is required.", nameof(category));
            PaddleOcrVlTask? selected = _taskByCategory.TryGetValue(category.Trim(), out PaddleOcrVlTask? mapped) ? mapped : UnmatchedCategoryTask;
            task = selected.GetValueOrDefault();
            return selected.HasValue;
        }

        /// <summary>Selects fallback regions once per page; explicit task mappings retain priority. / 每页选择一次回退区域；显式任务映射仍优先。</summary>
        internal HashSet<int> GetImageFallbackRegionIndices(IReadOnlyList<PaddleDocumentRegion> regions)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            var selected = new HashSet<int>();
            PaddleOcrVlImageFallbackOptions? options = ImageFallback;
            if (options == null) return selected;

            var candidates = new List<(int Index, double UncoveredArea)>();
            for (int index = 0; index < regions.Count; index++)
            {
                PaddleDocumentRegion region = regions[index];
                if (!string.Equals(region.Category.Trim(), "image", StringComparison.OrdinalIgnoreCase)
                    || TryGetTask(region.Category, out _) || !Valid(region.Bounds)) continue;
                double area = (double)region.Bounds.Width * region.Bounds.Height;
                double uncovered = Math.Max(0, area - CoveredArea(region.Bounds, regions));
                if (uncovered / area >= options.MinimumUncoveredAreaRatio) candidates.Add((index, uncovered));
            }
            candidates.Sort((left, right) =>
            {
                int areaOrder = right.UncoveredArea.CompareTo(left.UncoveredArea);
                return areaOrder != 0 ? areaOrder : left.Index.CompareTo(right.Index);
            });
            for (int rank = 0; rank < Math.Min(candidates.Count, options.MaximumRegionsPerPage); rank++)
                selected.Add(candidates[rank].Index);
            return selected;
        }

        /// <summary>Maps existing PP-Structure layout labels to OCR/table/formula/chart/seal tasks, with optional overrides. Figure/image regions are skipped by default. / 将现有 PP-Structure 版面标签映射到文字/表格/公式/图表/印章任务，可覆盖映射；默认跳过图片区域。</summary>
        public static PaddleOcrVlTaskRouter CreatePpStructure(IEnumerable<KeyValuePair<string, PaddleOcrVlTask?>>? overrides = null,
            PaddleOcrVlTask? unmatchedCategoryTask = null, PaddleOcrVlImageFallbackOptions? imageFallback = null)
        {
            var mappings = new Dictionary<string, PaddleOcrVlTask?>(StringComparer.OrdinalIgnoreCase);
            foreach (string category in new[] { "text", "title", "list", "paragraph_title", "number", "abstract", "content", "figure_title",
                "table_title", "reference", "doc_title", "footnote", "header", "algorithm", "footer", "chart_title", "formula_number", "aside_text", "reference_content" })
                mappings.Add(category, PaddleOcrVlTask.TextRecognition);
            mappings.Add("table", PaddleOcrVlTask.TableRecognition);
            mappings.Add("formula", PaddleOcrVlTask.FormulaRecognition);
            mappings.Add("chart", PaddleOcrVlTask.ChartRecognition);
            mappings.Add("seal", PaddleOcrVlTask.SealRecognition);
            foreach (string category in new[] { "image", "figure", "header_image", "footer_image" }) mappings.Add(category, null);
            if (overrides != null)
                foreach (KeyValuePair<string, PaddleOcrVlTask?> mapping in new PaddleOcrVlTaskRouter(overrides).TaskByCategory)
                    mappings[mapping.Key] = mapping.Value;
            return new PaddleOcrVlTaskRouter(mappings, unmatchedCategoryTask, imageFallback);
        }

        private static bool Valid(RectangleF bounds)
            => bounds.Width > 0 && bounds.Height > 0 && !float.IsNaN(bounds.X) && !float.IsNaN(bounds.Y)
                && !float.IsNaN(bounds.Width) && !float.IsNaN(bounds.Height)
                && !float.IsInfinity(bounds.X) && !float.IsInfinity(bounds.Y)
                && !float.IsInfinity(bounds.Right) && !float.IsInfinity(bounds.Bottom);

        private double CoveredArea(RectangleF target, IReadOnlyList<PaddleDocumentRegion> regions)
        {
            var clipped = new List<(double Left, double Top, double Right, double Bottom)>();
            var edges = new SortedSet<double>();
            foreach (PaddleDocumentRegion region in regions)
            {
                if (!TryGetTask(region.Category, out _) || !Valid(region.Bounds)) continue;
                double left = Math.Max(target.X, region.Bounds.X);
                double top = Math.Max(target.Y, region.Bounds.Y);
                double right = Math.Min(target.Right, region.Bounds.Right);
                double bottom = Math.Min(target.Bottom, region.Bounds.Bottom);
                if (left >= right || top >= bottom) continue;
                clipped.Add((left, top, right, bottom));
                edges.Add(left);
                edges.Add(right);
            }
            double[] x = new double[edges.Count];
            edges.CopyTo(x);
            double area = 0;
            for (int slab = 0; slab + 1 < x.Length; slab++)
            {
                var intervals = new List<(double Top, double Bottom)>();
                foreach (var rectangle in clipped)
                    if (rectangle.Left < x[slab + 1] && rectangle.Right > x[slab])
                        intervals.Add((rectangle.Top, rectangle.Bottom));
                intervals.Sort((left, right) => left.Top.CompareTo(right.Top));
                if (intervals.Count == 0) continue;
                double top = intervals[0].Top, bottom = intervals[0].Bottom, height = 0;
                for (int index = 1; index < intervals.Count; index++)
                {
                    if (intervals[index].Top <= bottom) bottom = Math.Max(bottom, intervals[index].Bottom);
                    else { height += bottom - top; top = intervals[index].Top; bottom = intervals[index].Bottom; }
                }
                area += (height + bottom - top) * (x[slab + 1] - x[slab]);
            }
            return area;
        }

        private static void ValidateTask(PaddleOcrVlTask? task)
        {
            if (task.HasValue && !Enum.IsDefined(typeof(PaddleOcrVlTask), task.Value)) throw new ArgumentOutOfRangeException(nameof(task));
        }
    }
}
#endif
