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

        /// <summary>Serializes multiple pages while preserving input order. / 按输入顺序序列化多页结果。</summary>
        public static string ToJson(IEnumerable<PaddleDocumentPipelineResult> results, bool indented = true)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var values = results.ToArray();
            if (values.Any(value => value == null)) throw new ArgumentException("Pipeline results cannot contain null values.", nameof(results));
            return JsonSerializer.Serialize(values.Select(value => JsonSerializer.Deserialize<object>(ToJson(value, false))).ToArray(), new JsonSerializerOptions { WriteIndented = indented });
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

        /// <summary>Concatenates page reports with explicit page separators. / 使用明确页分隔符拼接多页报告。</summary>
        public static string ToMarkdown(IEnumerable<PaddleDocumentPipelineResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            var values = results.ToArray();
            if (values.Any(value => value == null)) throw new ArgumentException("Pipeline results cannot contain null values.", nameof(results));
            return string.Join(Environment.NewLine + "---" + Environment.NewLine, values.Select(ToMarkdown));
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
            if (result is PaddleDocumentClassificationResult classification)
                return new { label = classification.Label, index = classification.Index, score = classification.Score };
            if (result is PaddleDocumentUnwarpingResult unwarping)
                return new { width = unwarping.Width, height = unwarping.Height, channels = unwarping.Channels, transform = unwarping.Transform, pixelCount = unwarping.Pixels.Count };
            if (result is PaddleDocumentTableResult table)
                return new { markup = table.Markup, tableType = table.TableType, averageScore = table.AverageScore, tokens = table.Tokens.Select(token => new { token.Index, token.Value, token.Score }).ToArray() };
            if (result is PaddleDocumentFormulaResult formula)
                return new { latex = formula.Latex, tokenIds = formula.TokenIds.ToArray() };
            if (result is PaddleDocumentChartResult chart)
                return new { structuredData = chart.StructuredData, tokenIds = chart.TokenIds.ToArray(), finishReason = chart.FinishReason };
            if (result is PaddleDocumentSealResult seal)
                return new { maskWidth = seal.MaskWidth, maskHeight = seal.MaskHeight };
#if NET8_0 || NET9_0 || NET10_0
            if (result is PaddleDocumentVisionLanguageRoutedPageResult routedPage)
                return new
                {
                    routing = "layout-category",
                    succeededCount = routedPage.SucceededCount,
                    failedCount = routedPage.FailedCount,
                    skippedCount = routedPage.SkippedCount,
                    coordinatesAvailable = routedPage.CoordinatesAvailable,
                    spatialOutputStatus = routedPage.SpatialOutputStatus,
                    regions = routedPage.Executions.Select(ToVisionLanguageExecutionObject).ToArray()
                };
            if (result is PaddleDocumentVisionLanguagePageResult visionLanguagePage)
                return new
                {
                    task = visionLanguagePage.Task.ToString(),
                    succeededCount = visionLanguagePage.SucceededCount,
                    failedCount = visionLanguagePage.FailedCount,
                    coordinatesAvailable = visionLanguagePage.CoordinatesAvailable,
                    spatialOutputStatus = visionLanguagePage.SpatialOutputStatus,
                    regions = visionLanguagePage.Executions.Select(ToVisionLanguageExecutionObject).ToArray()
                };
            if (result is PaddleDocumentVisionLanguageResult visionLanguage)
                return new
                {
                    task = visionLanguage.Task.ToString(),
                    text = visionLanguage.Text,
                    tokenIds = visionLanguage.TokenIds.ToArray(),
                    finishReason = visionLanguage.FinishReason.ToString(),
                    coordinatesAvailable = visionLanguage.CoordinatesAvailable,
                    spatialOutputStatus = visionLanguage.SpatialOutputStatus
                };
#endif
            if (result is PaddleDocumentTextResult text)
                return new { items = text.Items.Select(item => new { item.RegionIndex, item.Text, item.Confidence, bounds = new { x = item.Bounds.X, y = item.Bounds.Y, width = item.Bounds.Width, height = item.Bounds.Height }, metadata = item.Metadata }).ToArray() };
            return null;
        }

        private static void AppendPayloadMarkdown(StringBuilder builder, PaddleDocumentModuleResult result)
        {
            if (result is PaddleDocumentOrientationResult orientation)
                builder.Append("- Orientation: ").Append(orientation.Label).Append(" (").Append(orientation.RotationDegrees).AppendLine("°)");
            else if (result is PaddleDocumentClassificationResult classification)
                builder.Append("- Classification: ").Append(classification.Label).Append(" (").Append(classification.Score.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(")");
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
#if NET8_0 || NET9_0 || NET10_0
            else if (result is PaddleDocumentVisionLanguageRoutedPageResult routedPage)
            {
                builder.AppendLine("- VL tasks: selected by layout category (original layout order)");
                builder.Append("- Regional success/failure/skipped: ").Append(routedPage.SucceededCount).Append('/').Append(routedPage.FailedCount).Append('/').Append(routedPage.SkippedCount).AppendLine();
                builder.Append("- Coordinates: ").AppendLine(routedPage.SpatialOutputStatus);
                foreach (PaddleDocumentVisionLanguageRegionExecution execution in routedPage.Executions)
                {
                    builder.Append("- Region ").Append(execution.RegionIndex).Append(" / ").Append(execution.Task?.ToString() ?? "no-task").Append(" / ").Append(execution.Status).Append(": ");
                    if (execution.Result != null)
                    {
                        builder.Append(execution.Result.FinishReason).AppendLine().AppendLine();
                        AppendRawText(builder, execution.Result.Text);
                    }
                    else builder.AppendLine(execution.Skipped ? execution.SkipReason : execution.FailureMessage);
                }
            }
            else if (result is PaddleDocumentVisionLanguagePageResult visionLanguagePage)
            {
                builder.Append("- VL task: `").Append(visionLanguagePage.Task).AppendLine("` (regional routing)");
                builder.Append("- Regional success/failure: ").Append(visionLanguagePage.SucceededCount).Append('/').Append(visionLanguagePage.FailedCount).AppendLine();
                builder.Append("- Coordinates: ").AppendLine(visionLanguagePage.SpatialOutputStatus);
                foreach (PaddleDocumentVisionLanguageRegionExecution execution in visionLanguagePage.Executions)
                {
                    builder.Append("- Region ").Append(execution.RegionIndex).Append(execution.Succeeded ? ":" : " failed: ");
                    if (execution.Succeeded)
                    {
                        builder.AppendLine().AppendLine();
                        AppendRawText(builder, execution.Result!.Text);
                    }
                    else builder.AppendLine(execution.FailureMessage);
                }
            }
            else if (result is PaddleDocumentVisionLanguageResult visionLanguage)
            {
                builder.Append("- VL task: `").Append(visionLanguage.Task).AppendLine("`");
                builder.AppendLine("- Raw text:").AppendLine();
                AppendRawText(builder, visionLanguage.Text);
                builder.Append("- Finish reason: `").Append(visionLanguage.FinishReason).AppendLine("`");
                builder.Append("- Coordinates: ").AppendLine(visionLanguage.SpatialOutputStatus);
            }
#endif
            else if (result is PaddleDocumentTextResult text)
                foreach (PaddleDocumentTextItem item in text.Items) builder.Append("- [").Append(item.RegionIndex).Append("] ").Append(item.Text).Append(" (score ").Append(item.Confidence.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(")");
        }

#if NET8_0 || NET9_0 || NET10_0
        private static object ToVisionLanguageExecutionObject(PaddleDocumentVisionLanguageRegionExecution execution)
            => new
            {
                regionIndex = execution.RegionIndex,
                source = ToRegionObject(execution.SourceRegion),
                task = execution.Task?.ToString(),
                status = execution.Status.ToString(),
                succeeded = execution.Succeeded,
                skipped = execution.Skipped,
                skipReason = execution.SkipReason,
                failureType = execution.FailureType,
                failureMessage = execution.FailureMessage,
                result = execution.Result == null ? null : new
                {
                    task = execution.Result.Task.ToString(),
                    module = execution.Result.Module.ToString(),
                    modelId = execution.Result.Metadata.Model.ModelId,
                    backend = execution.Result.Metadata.Backend,
                    pageIndex = execution.Result.Metadata.PageIndex,
                    text = execution.Result.Text,
                    tokenIds = execution.Result.TokenIds.ToArray(),
                    finishReason = execution.Result.FinishReason.ToString(),
                    inputSha256 = execution.Result.Metadata.InputSha256,
                    elapsedMs = execution.Result.Metadata.Elapsed.TotalMilliseconds,
                    warnings = execution.Result.Warnings.ToArray()
                }
            };

        private static void AppendRawText(StringBuilder builder, string text)
        {
            // Keep HTML-looking model tokens and Markdown literal. A model-emitted
            // fence must not terminate the surrounding raw-output block.
            int longest = 0, current = 0;
            foreach (char character in text)
            {
                current = character == '~' ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }
            string fence = new string('~', Math.Max(3, longest + 1));
            builder.Append(fence).AppendLine("text").Append(text);
            if (!text.EndsWith("\n", StringComparison.Ordinal)) builder.AppendLine();
            builder.AppendLine(fence).AppendLine();
        }
#endif
    }
}
