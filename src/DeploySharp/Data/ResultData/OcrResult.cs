using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Data
{
    public class OcrResult
    {
        public ObbResult[] TextAreas { get; set; }
        public Result[] TextOrientations { get; set; }
        public TextRecResult[] TextContents { get; set; }

        /// <summary>
        /// 重写 ToString 方法，用于生成结构化的 OCR 结果字符串
        /// </summary>
        public override string ToString()
        {
            // 使用 StringBuilder 提高字符串拼接效率
            StringBuilder sb = new StringBuilder();
            // 1. 处理数组可能为 null 的情况，初始化长度
            // 如果为 null，则长度视为 0，方便后续循环逻辑
            int countAreas = TextAreas?.Length ?? 0;
            int countOrientations = TextOrientations?.Length ?? 0;
            int countContents = TextContents?.Length ?? 0;
            // 2. 确定循环的最大长度
            // 因为需要一一对应，所以取三个数组中最长的那个长度。
            // 如果某个数组较短，在循环中会做非空检查。
            int maxCount = Math.Max(Math.Max(countAreas, countOrientations), countContents);
            // 3. 添加头部信息
            sb.AppendLine($"========== OCR 识别结果 (共 {maxCount} 处) ==========");
            // 4. 遍历输出
            for (int i = 0; i < maxCount; i++)
            {
                sb.AppendLine($"[序号 {i + 1}]");
                // --- A. 处理文本区域 ---
                if (i < countAreas && TextAreas[i] != null)
                {
                    var area = TextAreas[i];
                    // 假设 RotatedRect 包含中心点、大小、角度，这里转换可读字符串
                    sb.AppendLine($"  区域: {area.Bounds}");
                    sb.AppendLine($"  检测置信度: {area.Confidence:F2}"); // F2 保留两位小数
                }
                else
                {
                    sb.AppendLine("  区域: (无数据)");
                }
                // --- B. 处理文本方向 ---
                if (i < countOrientations && TextOrientations[i] != null)
                {
                    var orient = TextOrientations[i];
                    sb.AppendLine($"  方向: {orient.Category} (ID:{orient.Id}, 置信度:{orient.Confidence:F2})");
                }
                else
                {
                    sb.AppendLine("  方向: (无数据)");
                }
                // --- C. 处理识别内容 ---
                if (i < countContents && TextContents[i] != null)
                {
                    var content = TextContents[i];
                    // 注意：TextRecResult 继承自 Result，所以也可以访问 Confidence
                    sb.AppendLine($"  内容: {content.Text}");
                    if (content.Confidence > 0)
                        sb.AppendLine($"  识别置信度: {content.Confidence:F2}");
                }
                else
                {
                    sb.AppendLine("  内容: (无数据)");
                }
                // 添加分隔线，方便阅读（最后一项后不加）
                if (i < maxCount - 1)
                {
                    sb.AppendLine("  ----------------------------------------");
                }
            }
            // 5. 添加尾部
            sb.AppendLine("========================================");
            return sb.ToString();
        }
    }
}
