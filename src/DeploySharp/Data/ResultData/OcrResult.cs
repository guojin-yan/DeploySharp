using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents OCR (Optical Character Recognition) results containing text detection, 
    /// orientation classification, and text recognition information
    /// 表示OCR（光学字符识别）结果，包含文本检测、方向分类和文本识别信息
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class encapsulates the complete OCR pipeline results including:
    /// - Text area detection with oriented bounding boxes
    /// - Text orientation classification (0°, 90°, 180°, 270°)
    /// - Text content recognition
    /// 
    /// 此类封装了完整的OCR管道结果，包括：
    /// - 带方向边界框的文本区域检测
    /// - 文本方向分类（0°、90°、180°、270°）
    /// - 文本内容识别
    /// </para>
    /// <para>
    /// Supports sorting operations to organize text in reading order (left-to-right, top-to-bottom).
    /// 支持排序操作以按阅读顺序组织文本（从左到右、从上到下）。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var ocrResult = new OcrResult
    /// {
    ///     TextAreas = new[] { new ObbResult { Bounds = rotatedRect, Confidence = 0.95f } },
    ///     TextOrientations = new[] { new Result { Id = 0, Category = "0" } },
    ///     TextContents = new[] { new TextRecResult { Text = "Hello", Confidence = 0.92f } }
    /// };
    /// 
    /// // Sort by reading order
    /// ocrResult.SortByYThenX();
    /// Console.WriteLine(ocrResult.ToString());
    /// </code>
    /// </example>
    /// </remarks>
    public class OcrResult
    {
        /// <summary>
        /// Detected text regions represented as oriented bounding boxes
        /// 检测到的文本区域，表示为方向边界框
        /// </summary>
        /// <value>
        /// Array of oriented bounding box detection results, each containing
        /// the spatial location and confidence of a detected text region.
        /// 
        /// 方向边界框检测结果数组，每个元素包含检测到的文本区域的空间位置和置信度。
        /// </value>
        public ObbResult[] TextAreas { get; set; }

        /// <summary>
        /// Text orientation classification results
        /// 文本方向分类结果
        /// </summary>
        /// <value>
        /// Array of classification results where each element represents
        /// the detected orientation angle category (typically "0", "90", "180", "270").
        /// 
        /// 分类结果数组，每个元素表示检测到的方向角度类别（通常为"0"、"90"、"180"、"270"）。
        /// </value>
        public Result[] TextOrientations { get; set; }

        /// <summary>
        /// Text recognition results containing the actual text content
        /// 包含实际文本内容的文本识别结果
        /// </summary>
        /// <value>
        /// Array of text recognition results, each containing the recognized
        /// text string and recognition confidence.
        /// 
        /// 文本识别结果数组，每个元素包含识别的文本字符串和识别置信度。
        /// </value>
        public TextRecResult[] TextContents { get; set; }

        /// <summary>
        /// Generates a formatted string of text recognition contents
        /// 生成文本识别内容的格式化字符串
        /// </summary>
        /// <returns>
        /// A formatted string displaying all recognized text contents with their indices
        /// and confidence scores.
        /// 
        /// 格式化字符串，显示所有识别的文本内容及其索引和置信度分数。
        /// </returns>
        /// <remarks>
        /// This method only displays the text content part of the OCR result.
        /// For complete OCR output including text areas and orientations, use <see cref="ToString()"/>.
        /// 
        /// 此方法仅显示OCR结果的文本内容部分。
        /// 如需包含文本区域和方向的完整OCR输出，请使用<see cref="ToString()"/>。
        /// </remarks>
        public string TextContentsToString()
        {
            // Use StringBuilder for efficient string concatenation
            StringBuilder sb = new StringBuilder();
            int countContents = TextContents?.Length ?? 0;

            // Add header information
            sb.AppendLine($"========== OCR Text Recognition Results (Total: {countContents}) ==========");
            
            // Iterate through all recognized texts
            for (int i = 0; i < countContents; i++)
            {
                sb.AppendLine($"[Index {i + 1}]");
           
                // Process recognition content
                if (i < countContents && TextContents[i] != null)
                {
                    var content = TextContents[i];
                    sb.Append($"  Recognition Confidence: {content.Confidence:F2}");
                    sb.AppendLine($"   |   Content: {content.Text}");
                }
                else
                {
                    sb.AppendLine("  Content: (No data)");
                }
                
                // Add separator for readability (except after last item)
                if (i < countContents - 1)
                {
                    sb.AppendLine("  ----------------------------------------");
                }
            }
            
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a formatted string of text area detection results
        /// 生成文本区域检测结果的格式化字符串
        /// </summary>
        /// <returns>
        /// A formatted string displaying all detected text areas with their 
        /// oriented bounding boxes and detection confidence scores.
        /// 
        /// 格式化字符串，显示所有检测到的文本区域及其方向边界框和检测置信度分数。
        /// </returns>
        public string TextAreasToString()
        {
            StringBuilder sb = new StringBuilder();
            int countAreas = TextAreas?.Length ?? 0;

            sb.AppendLine($"========== OCR Text Area Detection Results (Total: {countAreas}) ==========");
            
            for (int i = 0; i < countAreas; i++)
            {
                sb.AppendLine($"[Index {i + 1}]");
                
                if (i < countAreas && TextAreas[i] != null)
                {
                    var area = TextAreas[i];
                    sb.AppendLine($"  Region: {area.Bounds}");
                    sb.AppendLine($"  Detection Confidence: {area.Confidence:F2}");
                }
                else
                {
                    sb.AppendLine("  Region: (No data)");
                }
                
                if (i < countAreas - 1)
                {
                    sb.AppendLine("  ----------------------------------------");
                }
            }
            
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        /// <summary>
        /// Generates a formatted string of text orientation classification results
        /// 生成文本方向分类结果的格式化字符串
        /// </summary>
        /// <returns>
        /// A formatted string displaying all text orientation classifications
        /// with their angle categories and confidence scores.
        /// 
        /// 格式化字符串，显示所有文本方向分类及其角度类别和置信度分数。
        /// </returns>
        public string TextOrientationsToString()
        {
            StringBuilder sb = new StringBuilder();
            int countOrientations = TextOrientations?.Length ?? 0;

            sb.AppendLine($"========== OCR Text Orientation Results (Total: {countOrientations}) ==========");
            
            for (int i = 0; i < countOrientations; i++)
            {
                sb.AppendLine($"[Index {i + 1}]");
              
                if (i < countOrientations && TextOrientations[i] != null)
                {
                    var orient = TextOrientations[i];
                    sb.AppendLine($"  Orientation: {orient.Category} (ID:{orient.Id}, Confidence:{orient.Confidence:F2})");
                }
                else
                {
                    sb.AppendLine("  Orientation: (No data)");
                }
           
                if (i < countOrientations - 1)
                {
                    sb.AppendLine("  ----------------------------------------");
                }
            }
            
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        /// <summary>
        /// Returns a formatted string representation of the complete OCR results
        /// 返回完整OCR结果的格式化字符串表示
        /// </summary>
        /// <returns>
        /// A comprehensive formatted string containing text areas, orientations,
        /// and recognition contents for all detected text regions.
        /// 
        /// 包含所有检测到的文本区域的文本区域、方向和识别内容的综合格式化字符串。
        /// </returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            int countAreas = TextAreas?.Length ?? 0;
            int countOrientations = TextOrientations?.Length ?? 0;
            int countContents = TextContents?.Length ?? 0;
            
            // Determine maximum count for iteration
            int maxCount = Math.Max(Math.Max(countAreas, countOrientations), countContents);
            
            sb.AppendLine($"========== OCR Recognition Results (Total: {maxCount}) ==========");
            
            for (int i = 0; i < maxCount; i++)
            {
                sb.AppendLine($"[Index {i + 1}]");
                
                // Process text area
                if (i < countAreas && TextAreas[i] != null)
                {
                    var area = TextAreas[i];
                    sb.AppendLine($"  Region: {area.Bounds}");
                    sb.AppendLine($"  Detection Confidence: {area.Confidence:F2}");
                }
                else
                {
                    sb.AppendLine("  Region: (No data)");
                }
                
                // Process text orientation
                if (i < countOrientations && TextOrientations[i] != null)
                {
                    var orient = TextOrientations[i];
                    sb.AppendLine($"  Orientation: {orient.Category} (ID:{orient.Id}, Confidence:{orient.Confidence:F2})");
                }
                else
                {
                    sb.AppendLine("  Orientation: (No data)");
                }
                
                // Process recognition content
                if (i < countContents && TextContents[i] != null)
                {
                    var content = TextContents[i];
                    sb.AppendLine($"  Content: {content.Text}");
                    if (content.Confidence > 0)
                        sb.AppendLine($"  Recognition Confidence: {content.Confidence:F2}");
                }
                else
                {
                    sb.AppendLine("  Content: (No data)");
                }
                
                // Add separator
                if (i < maxCount - 1)
                {
                    sb.AppendLine("  ----------------------------------------");
                }
            }
            
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        #region Synchronous Sorting Methods

        /// <summary>
        /// Helper method: Reorders all three arrays (TextAreas, TextOrientations, TextContents) 
        /// according to the provided sorted indices to maintain correspondence.
        /// 辅助方法：根据提供的排序索引重新排列三个数组（TextAreas、TextOrientations、TextContents），
        /// 以确保它们保持一一对应关系。
        /// </summary>
        /// <param name="sortedIndices">
        /// Array of indices defining the new order.
        /// 定义新顺序的索引数组。
        /// </param>
        private void ReorderArrays(int[] sortedIndices)
        {
            if (sortedIndices == null || sortedIndices.Length == 0) return;
            
            var newTextAreas = new ObbResult[sortedIndices.Length];
            var newTextOrientations = new Result[sortedIndices.Length];
            var newTextContents = new TextRecResult[sortedIndices.Length];
            
            for (int i = 0; i < sortedIndices.Length; i++)
            {
                int oldIndex = sortedIndices[i];
                newTextAreas[i] = TextAreas[oldIndex];
                newTextOrientations[i] = TextOrientations[oldIndex];
                newTextContents[i] = TextContents[oldIndex];
            }
            
            // Update properties
            TextAreas = newTextAreas;
            TextOrientations = newTextOrientations;
            TextContents = newTextContents;
        }

        /// <summary>
        /// Sorts text regions by X coordinate (horizontal position).
        /// Sorts TextAreas and synchronously updates TextOrientations and TextContents.
        /// 按X坐标（水平位置）对文本区域进行排序。
        /// 对TextAreas进行排序，并同步更新TextOrientations和TextContents。
        /// </summary>
        /// <param name="ascending">
        /// If true, sorts in ascending order (left to right);
        /// if false, sorts in descending order (right to left).
        /// 如果为true，按升序排序（从左到右）；
        /// 如果为false，按降序排序（从右到左）。
        /// </param>
        public void SortByX(bool ascending = true)
        {
            if (TextAreas == null) return;
            
            int[] indices = ascending
                ? TextAreas.Select((t, i) => new { Index = i, Val = t.Bounds.Center.X }).OrderBy(x => x.Val).Select(x => x.Index).ToArray()
                : TextAreas.Select((t, i) => new { Index = i, Val = t.Bounds.Center.X }).OrderByDescending(x => x.Val).Select(x => x.Index).ToArray();
            
            ReorderArrays(indices);
        }

        /// <summary>
        /// Sorts text regions by Y coordinate (vertical position).
        /// Sorts TextAreas and synchronously updates TextOrientations and TextContents.
        /// 按Y坐标（垂直位置）对文本区域进行排序。
        /// 对TextAreas进行排序，并同步更新TextOrientations和TextContents。
        /// </summary>
        /// <param name="ascending">
        /// If true, sorts in ascending order (top to bottom);
        /// if false, sorts in descending order (bottom to top).
        /// 如果为true，按升序排序（从上到下）；
        /// 如果为false，按降序排序（从下到上）。
        /// </param>
        public void SortByY(bool ascending = true)
        {
            if (TextAreas == null) return;
            
            int[] indices = ascending
                ? TextAreas.Select((t, i) => new { Index = i, Val = t.Bounds.Center.Y }).OrderBy(x => x.Val).Select(x => x.Index).ToArray()
                : TextAreas.Select((t, i) => new { Index = i, Val = t.Bounds.Center.Y }).OrderByDescending(x => x.Val).Select(x => x.Index).ToArray();
            
            ReorderArrays(indices);
        }

        /// <summary>
        /// Sorts text regions by Y coordinate first, then by X coordinate.
        /// This creates a reading order suitable for left-to-right languages.
        /// Sorts TextAreas and synchronously updates TextOrientations and TextContents.
        /// 先按Y坐标排序，然后按X坐标排序。
        /// 这会创建适合从左到右语言的阅读顺序。
        /// 对TextAreas进行排序，并同步更新TextOrientations和TextContents。
        /// </summary>
        /// <param name="yAscending">
        /// If true, sorts Y in ascending order (top to bottom);
        /// if false, sorts Y in descending order (bottom to top).
        /// 如果为true，Y按升序排序（从上到下）；
        /// 如果为false，Y按降序排序（从下到上）。
        /// </param>
        /// <param name="xAscending">
        /// If true, sorts X in ascending order (left to right);
        /// if false, sorts X in descending order (right to left).
        /// 如果为true，X按升序排序（从左到右）；
        /// 如果为false，X按降序排序（从右到左）。
        /// </param>
        public void SortByYThenX(bool yAscending = true, bool xAscending = true)
        {
            if (TextAreas == null) return;
            
            int[] indices;
            if (yAscending)
            {
                if (xAscending)
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderBy(o => o.Y).ThenBy(o => o.X).Select(o => o.Index).ToArray();
                else
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderBy(o => o.Y).ThenByDescending(o => o.X).Select(o => o.Index).ToArray();
            }
            else
            {
                if (xAscending)
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderByDescending(o => o.Y).ThenBy(o => o.X).Select(o => o.Index).ToArray();
                else
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderByDescending(o => o.Y).ThenByDescending(o => o.X).Select(o => o.Index).ToArray();
            }
            
            ReorderArrays(indices);
        }

        /// <summary>
        /// Sorts text regions by X coordinate first, then by Y coordinate.
        /// This creates a column-major reading order.
        /// Sorts TextAreas and synchronously updates TextOrientations and TextContents.
        /// 先按X坐标排序，然后按Y坐标排序。
        /// 这会创建按列优先的阅读顺序。
        /// 对TextAreas进行排序，并同步更新TextOrientations和TextContents。
        /// </summary>
        /// <param name="xAscending">
        /// If true, sorts X in ascending order (left to right);
        /// if false, sorts X in descending order (right to left).
        /// 如果为true，X按升序排序（从左到右）；
        /// 如果为false，X按降序排序（从右到左）。
        /// </param>
        /// <param name="yAscending">
        /// If true, sorts Y in ascending order (top to bottom);
        /// if false, sorts Y in descending order (bottom to top).
        /// 如果为true，Y按升序排序（从上到下）；
        /// 如果为false，Y按降序排序（从下到上）。
        /// </param>
        public void SortByXThenY(bool xAscending = true, bool yAscending = true)
        {
            if (TextAreas == null) return;
            
            int[] indices;
            if (xAscending)
            {
                if (yAscending)
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderBy(o => o.X).ThenBy(o => o.Y).Select(o => o.Index).ToArray();
                else
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderBy(o => o.X).ThenByDescending(o => o.Y).Select(o => o.Index).ToArray();
            }
            else
            {
                if (yAscending)
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderByDescending(o => o.X).ThenBy(o => o.Y).Select(o => o.Index).ToArray();
                else
                    indices = TextAreas.Select((t, i) => new { Index = i, X = t.Bounds.Center.X, Y = t.Bounds.Center.Y })
                                       .OrderByDescending(o => o.X).ThenByDescending(o => o.Y).Select(o => o.Index).ToArray();
            }
            
            ReorderArrays(indices);
        }

        #endregion
    }
}
