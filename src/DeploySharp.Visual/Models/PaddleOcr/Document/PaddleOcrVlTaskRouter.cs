#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Selects VL tasks from layout categories using an immutable, case-insensitive mapping. / 使用不可变、不区分大小写的版面类别映射选择 VL 任务。</summary>
    /// <remarks>A null mapping explicitly skips that category, including when an unmatched-category task is configured. This router does not detect layout or infer reading order. / 空映射明确跳过该类别，即使配置了未匹配类别任务也如此。本类不执行版面检测或推断阅读顺序。</remarks>
    public sealed class PaddleOcrVlTaskRouter
    {
        private readonly IReadOnlyDictionary<string, PaddleOcrVlTask?> _taskByCategory;

        /// <summary>Copies category mappings and an optional task for unmatched categories. / 复制类别映射及可选的未匹配类别任务。</summary>
        public PaddleOcrVlTaskRouter(IEnumerable<KeyValuePair<string, PaddleOcrVlTask?>> taskByCategory, PaddleOcrVlTask? unmatchedCategoryTask = null)
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
        }

        /// <summary>Gets the copied category mappings; null values explicitly skip a category. / 获取复制的类别映射；空值明确跳过该类别。</summary>
        public IReadOnlyDictionary<string, PaddleOcrVlTask?> TaskByCategory => _taskByCategory;
        /// <summary>Gets the task for unmatched categories, or null to skip them. / 获取未匹配类别的任务；空值表示跳过。</summary>
        public PaddleOcrVlTask? UnmatchedCategoryTask { get; }

        /// <summary>Resolves a trimmed category; returns false when generation should be skipped. / 解析去除两端空白的类别；应跳过生成时返回 false。</summary>
        public bool TryGetTask(string category, out PaddleOcrVlTask task)
        {
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("A layout category is required.", nameof(category));
            PaddleOcrVlTask? selected = _taskByCategory.TryGetValue(category.Trim(), out PaddleOcrVlTask? mapped) ? mapped : UnmatchedCategoryTask;
            task = selected.GetValueOrDefault();
            return selected.HasValue;
        }

        /// <summary>Maps existing PP-Structure layout labels to OCR/table/formula/chart/seal tasks, with optional overrides. Figure/image regions are skipped by default. / 将现有 PP-Structure 版面标签映射到文字/表格/公式/图表/印章任务，可覆盖映射；默认跳过图片区域。</summary>
        public static PaddleOcrVlTaskRouter CreatePpStructure(IEnumerable<KeyValuePair<string, PaddleOcrVlTask?>>? overrides = null, PaddleOcrVlTask? unmatchedCategoryTask = null)
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
            return new PaddleOcrVlTaskRouter(mappings, unmatchedCategoryTask);
        }

        private static void ValidateTask(PaddleOcrVlTask? task)
        {
            if (task.HasValue && !Enum.IsDefined(typeof(PaddleOcrVlTask), task.Value)) throw new ArgumentOutOfRangeException(nameof(task));
        }
    }
}
#endif
