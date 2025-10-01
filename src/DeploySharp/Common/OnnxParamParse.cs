using DeploySharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DeploySharp.Common
{
    /// <summary>
    /// Provides utilities for parsing ONNX model parameter strings.
    /// 提供解析ONNX模型参数字串的工具方法
    /// </summary>
    /// <remarks>
    /// This class specializes in parsing label mapping strings commonly found in ONNX model exports.
    /// 此类专门解析ONNX模型导出中常见的标签映射字符串。
    /// </remarks>
    public static class OnnxParamParse
    {
        /// <summary>
        /// Regular expression for matching key-value pairs formatted as `2: 'storage tank'`
        /// 用于匹配类似`2: 'storage tank'`键值对的正则表达式
        /// </summary>
        /// <remarks>
        /// Pattern explanation:
        ///   (\d+)    - Captures one or more digits (the key)
        ///   \s*:\s*  - Colon with optional whitespace
        ///   '([^']*) - Captures text inside single quotes (the value)
        /// 
        /// 正则模式说明：
        ///   (\d+)    - 匹配数字部分(键)
        ///   \s*:\s*  - 冒号及周围可能有空格
        ///   '([^']*) - 匹配单引号内的文本(值)
        /// </remarks>
        private static readonly Regex PairRegex = new Regex(
            @"(\d+)\s*:\s*'([^']*)'",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Parses a label mapping string into dictionary of index-label pairs.
        /// 将标签映射字符串解析为索引-标签的字典
        /// </summary>
        /// <param name="input">
        /// The input string containing label mappings (e.g., "1: 'person', 2: 'car'").
        /// 包含标签映射的输入字符串(例如："1: 'person', 2: 'car'")
        /// </param>
        /// <returns>
        /// Dictionary where keys are label indices and values are label names.
        /// 返回键为标签索引、值为标签名称的字典
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when input string is null.
        /// 当输入字符串为null时抛出
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when invalid key-value format is encountered.
        /// 当遇到无效的键值格式时抛出
        /// </exception>
        /// <example>
        /// <code>
        /// string labels = "1: 'person', 2: 'car', 3: 'dog'";
        /// var labelMap = OnnxParamParse.ParseLabelString(labels);
        /// // Returns dictionary:
        /// // {
        /// //     {1, "person"},
        /// //     {2, "car"},
        /// //     {3, "dog"}
        /// // }
        /// </code>
        /// </example>
        public static Dictionary<int, string> ParseLabelString(string input)
        {
            // Validate input
            // 参数验证
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input),
                    "Input string cannot be null. 输入字符串不能为null");
            }

            var pairs = new Dictionary<int, string>();

            // Process each matched pair
            // 处理每个匹配到的键值对
            foreach (Match match in PairRegex.Matches(input))
            {
                // Skip malformed matches
                // 跳过格式不正确的匹配项
                if (match.Groups.Count != 3)
                {
                    continue;
                }

                try
                {
                    // Parse key as integer
                    // 将键解析为整数
                    int key = int.Parse(match.Groups[1].Value);
                    string value = match.Groups[2].Value;

                    // Add to dictionary (will throw on duplicate keys)
                    // 添加到字典(遇到重复键会抛出异常)
                    pairs.Add(key, value);
                }
                catch (FormatException ex)
                {
                    throw new FormatException(
                        $"Invalid key format in label string: '{match.Groups[1].Value}'. " +
                        $"标签字符串中的键格式无效: '{match.Groups[1].Value}'", ex);
                }
                catch (OverflowException ex)
                {
                    throw new FormatException(
                        $"Key value too large in label string: '{match.Groups[1].Value}'. " +
                        $"标签字符串中的键值过大: '{match.Groups[1].Value}'", ex);
                }
                catch (ArgumentException ex)
                {
                    throw new FormatException(
                        $"Duplicate key found in label string: '{match.Groups[1].Value}'. " +
                        $"标签字符串中发现重复键: '{match.Groups[1].Value}'", ex);
                }
            }

            return pairs;
        }
    }

}
