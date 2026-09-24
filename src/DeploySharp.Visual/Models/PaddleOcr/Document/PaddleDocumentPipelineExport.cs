using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Exports a completed PP-Structure page pipeline without serializing caller-owned source objects. / 导出已完成的 PP-Structure 页面流水线结果且不序列化调用方源对象。</summary>
    public static class PaddleDocumentPipelineExport
    {
        /// <summary>Serializes page provenance, stage timings, module metadata, regions and known task payloads to JSON. / 将页来源、阶段耗时、模块元数据、区域和已知任务载荷序列化为 JSON。</summary>
        public static string ToJson(PaddleDocumentPipelineResult result, bool indented = true)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var document = new Dictionary<string, object?>
            {
                ["page"] = new Dictionary<string, object?>
                {
                    ["pageIndex"] = result.Page.PageIndex,
                    ["sourceSize"] = new { width = result.Page.SourceSize.Width, height = result.Page.SourceSize.Height }
                },
                ["elapsedMs"] = result.Elapsed.TotalMilliseconds,
                ["timings"] = result.Timings.Select(value => new { module = value.Module.ToString(), elapsedMs = value.Elapsed.TotalMilliseconds }).ToArray(),
                ["results"] = result.Results.Select(ToResultObject).ToArray()
            };
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = indented });
        }

        /// <summary>Creates a compact human-readable page summary with stage timings and task payloads. / 创建包含阶段耗时和任务载荷的紧凑可读页面摘要。</summary>
        public static string ToMarkdown(PaddleDocumentPipelineResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var builder = new StringBuilder();
            builder.Append("# PP-Structure page ").Append(result.Page.PageIndex).AppendLine();
            builder.Append("- Source size: ").Append(result.Page.SourceSize.Width).Append('x').Append(result.Page.SourceSize.Height).AppendLine();
            builder.Append("- Total time: ").Append(result.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" ms");
            builder.AppendLine();
            builder.AppendLine("## Stages");
            builder.AppendLine();
            builder.AppendLine("| Module | Time (ms) |");
            builder.AppendLine("| --- | ---: |");
            foreach (PaddleDocumentPipelineStageTiming timing in result.Timings)
                builder.Append("| ").Append(timing.Module).Append(" | ").Append(timing.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" |");
            builder.AppendLine();
            builder.AppendLine("## Results");
            builder.AppendLine();
            foreach (PaddleDocumentModuleResult item in result.Results)
            {
                builder.Append("### ").Append(item.Module).AppendLine();
                builder.Append("- Model: `").Append(item.Metadata.Model.ModelId).AppendLine("`");
                builder.Append("- Backend: `").Append(item.Metadata.Backend).AppendLine("`");
                builder.Append("- Input SHA-256: `").Append(item.Metadata.InputSha256).AppendLine("`");
                builder.Append("- Regions: ").Append(item.Regions.Count).AppendLine();
                if (item.Warnings.Count > 0) builder.Append("- Warnings: ").AppendLine(string.Join("; ", item.Warnings));
                AppendPayloadMarkdown(builder, item);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static object ToResultObject(PaddleDocumentModuleResult result)
        {
            var value = new Dictionary<string, object?>
            {
                ["module"] = result.Module.ToString(),
                ["metadata"] = new
                {
                    modelId = result.Metadata.Model.ModelId,
                    officialName = result.Metadata.Model.OfficialName,
                    backend = result.Metadata.Backend,
                    elapsedMs = result.Metadata.Elapsed.TotalMilliseconds,
                    inputSha256 = result.Metadata.InputSha256,
                    pageIndex = result.Metadata.PageIndex
                },
                ["regions"] = result.Regions.Select(ToRegionObject).ToArray(),
                ["warnings"] = result.Warnings.ToArray(),
                ["payload"] = ToPayloadObject(result)
            };
            return value;
        }

        private static object ToRegionObject(PaddleDocumentRegion region)
            => new { category = region.Category, score = region.Score, bounds = new { x = region.Bounds.X, y = region.Bounds.Y, width = region.Bounds.Width, height = region.Bounds.Height }, metadata = region.Metadata };

        private static object? ToPayloadObject(PaddleDocumentModuleResult result)
        {
            if (result is PaddleDocumentOrientationResult orientation)
                return new { label = orientation.Label, rotationDegrees = orientation.RotationDegrees };
            if (result is PaddleDocumentUnwarpingResult unwarping)
                return new { width = unwarping.Width, height = unwarping.Height, channels = unwarping.Channels, transform = unwarping.Transform, pixelCount = unwarping.Pixels.Count };
            if (result is PaddleDocumentTableResult table)
                return new { markup = table.Markup, tableType = table.TableType, averageScore = table.AverageScore, tokens = table.Tokens.Select(token => new { token.Index, token.Value, token.Score }).ToArray() };
            if (result is PaddleDocumentFormulaResult formula)
                return new { latex = formula.Latex, tokenIds = formula.TokenIds.ToArray() };
            if (result is PaddleDocumentChartResult chart)
                return new { structuredData = chart.StructuredData, tokenIds = chart.TokenIds.ToArray() };
            if (result is PaddleDocumentSealResult seal)
                return new { maskWidth = seal.MaskWidth, maskHeight = seal.MaskHeight };
            return null;
        }

        private static void AppendPayloadMarkdown(StringBuilder builder, PaddleDocumentModuleResult result)
        {
            if (result is PaddleDocumentOrientationResult orientation)
                builder.Append("- Orientation: ").Append(orientation.Label).Append(" (").Append(orientation.RotationDegrees).AppendLine("°)");
            else if (result is PaddleDocumentUnwarpingResult unwarping)
                builder.Append("- Unwarped image: ").Append(unwarping.Width).Append('x').Append(unwarping.Height).Append(" / ").Append(unwarping.Channels).AppendLine(" channels");
            else if (result is PaddleDocumentTableResult table)
                builder.Append("- Table: ").Append(table.TableType).Append("; average score ").Append(table.AverageScore.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            else if (result is PaddleDocumentFormulaResult formula)
                builder.Append("- Formula: `").Append(formula.Latex.Replace("`", "\\`")).AppendLine("`");
            else if (result is PaddleDocumentChartResult chart)
                builder.Append("- Chart data: ").AppendLine(chart.StructuredData);
            else if (result is PaddleDocumentSealResult seal)
                builder.Append("- Seal mask: ").Append(seal.MaskWidth).Append('x').AppendLine(seal.MaskHeight.ToString());
        }
    }
}
