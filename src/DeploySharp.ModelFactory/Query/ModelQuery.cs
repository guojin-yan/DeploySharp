using System;
using System.Collections.Generic;
using System.Linq;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Defines deterministic catalog selection filters. / 定义确定性目录选择筛选条件。</summary>
    public sealed class ModelQuery
    {
        /// <summary>Initializes a query. / 初始化查询。</summary>
        public ModelQuery(string? modelId = null, string? task = null, string? family = null, string? format = null, string? backend = null, string? precision = null, string? quantization = null, bool? portable = null, bool includePreview = false)
        {
            ModelId = Normalize(modelId);
            Task = Normalize(task);
            Family = Normalize(family);
            Format = Normalize(format);
            Backend = Normalize(backend);
            Precision = Normalize(precision);
            Quantization = Normalize(quantization);
            Portable = portable;
            IncludePreview = includePreview;
        }

        /// <summary>Gets the optional model identifier filter. / 获取可选模型标识筛选。</summary>
        public string? ModelId { get; }
        /// <summary>Gets the optional task filter. / 获取可选任务筛选。</summary>
        public string? Task { get; }
        /// <summary>Gets the optional family filter. / 获取可选模型族筛选。</summary>
        public string? Family { get; }
        /// <summary>Gets the optional format filter. / 获取可选格式筛选。</summary>
        public string? Format { get; }
        /// <summary>Gets the optional backend filter. / 获取可选后端筛选。</summary>
        public string? Backend { get; }
        /// <summary>Gets the optional precision filter. / 获取可选精度筛选。</summary>
        public string? Precision { get; }
        /// <summary>Gets the optional quantization filter. / 获取可选量化筛选。</summary>
        public string? Quantization { get; }
        /// <summary>Gets the optional portability filter. / 获取可选可移植性筛选。</summary>
        public bool? Portable { get; }
        /// <summary>Gets whether Preview entries are eligible. / 获取是否允许 Preview 条目。</summary>
        public bool IncludePreview { get; }

        internal bool Matches(ModelCatalogEntry entry, ModelCatalogArtifact artifact)
        {
            if (!IncludePreview && entry.Status != ModelCatalogStatus.Supported) return false;
            if (ModelId != null && !string.Equals(ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase)) return false;
            if (Task != null && !string.Equals(Task, entry.Task, StringComparison.OrdinalIgnoreCase)) return false;
            if (Family != null && !string.Equals(Family, entry.Family, StringComparison.OrdinalIgnoreCase)) return false;
            if (Format != null && !string.Equals(Format, artifact.Format, StringComparison.OrdinalIgnoreCase)) return false;
            if (Backend != null && !artifact.CompatibleBackends.Any(value => string.Equals(value, Backend, StringComparison.OrdinalIgnoreCase))) return false;
            if (Precision != null && !string.Equals(Precision, artifact.Precision, StringComparison.OrdinalIgnoreCase)) return false;
            if (Quantization != null && !string.Equals(Quantization, artifact.Quantization, StringComparison.OrdinalIgnoreCase)) return false;
            if (Portable.HasValue && artifact.Portable != Portable.Value) return false;
            return true;
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value!.Trim().ToLowerInvariant();
        }
    }

    /// <summary>Represents one deterministic model/artifact selection. / 表示一个确定性的模型/工件选择结果。</summary>
    public sealed class ModelSelection
    {
        internal ModelSelection(ModelCatalogEntry entry, ModelCatalogArtifact artifact, int score)
        {
            Entry = entry;
            Artifact = artifact;
            Score = score;
        }

        /// <summary>Gets the selected entry. / 获取选中的目录条目。</summary>
        public ModelCatalogEntry Entry { get; }
        /// <summary>Gets the selected artifact. / 获取选中的工件。</summary>
        public ModelCatalogArtifact Artifact { get; }
        /// <summary>Gets the deterministic match score. / 获取确定性匹配分数。</summary>
        public int Score { get; }
    }

    /// <summary>Provides deterministic selection over a validated catalog. / 提供已验证目录上的确定性选择。</summary>
    public static class ModelCatalogQuery
    {
        /// <summary>Returns matching artifacts sorted by score, model id, and artifact id. / 返回按分数、模型标识和工件标识排序的匹配工件。</summary>
        public static IReadOnlyList<ModelSelection> Select(ValidatedModelCatalog catalog, ModelQuery query)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (query == null) throw new ArgumentNullException(nameof(query));
            var selections = new List<ModelSelection>();
            foreach (ModelCatalogEntry entry in catalog.Document.Entries)
            {
                foreach (ModelCatalogArtifact artifact in entry.Artifacts)
                {
                    if (!query.Matches(entry, artifact)) continue;
                    int score = 0;
                    if (query.ModelId != null) score += 100;
                    if (query.Format != null) score += 20;
                    if (query.Backend != null) score += 20;
                    if (query.Precision != null) score += 10;
                    if (query.Quantization != null) score += 10;
                    selections.Add(new ModelSelection(entry, artifact, score));
                }
            }

            return selections
                .OrderByDescending(value => value.Score)
                .ThenBy(value => value.Entry.ModelId, StringComparer.Ordinal)
                .ThenBy(value => value.Artifact.ArtifactId, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();
        }
    }
}
