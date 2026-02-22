using System;
using System.Collections.Generic;
using System.Text;

namespace DeploySharp.Data
{
    /// <summary>
    /// Represents text recognition results containing recognized text content
    /// 表示包含识别文本内容的文本识别结果
    /// </summary>
    /// <remarks>
    /// <para>
    /// Extends <see cref="Result"/> with text-specific properties for OCR (Optical Character Recognition) tasks.
    /// Used for recognizing and extracting text from images.
    /// </para>
    /// <para>
    /// 继承自<see cref="Result"/>并增加了文本专用属性，用于OCR（光学字符识别）任务。
    /// 用于从图像中识别和提取文本。
    /// </para>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var textResult = new TextRecResult 
    /// {
    ///     Text = "Hello World",
    ///     Confidence = 0.95f,
    ///     Category = "english_text"
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="Result"/>
    /// <seealso cref="ResultType.TextRecResult"/>
    public class TextRecResult : Result
    {
        /// <summary>
        /// The recognized text content
        /// 识别的文本内容
        /// </summary>
        /// <value>
        /// <para>
        /// Contains the actual text string extracted from the image.
        /// May be empty string if no text was recognized.
        /// </para>
        /// <para>
        /// 包含从图像中提取的实际文本字符串。
        /// 如果没有识别到文本，可能为空字符串。
        /// </para>
        /// </value>
        /// <remarks>
        /// The encoding depends on the OCR model capabilities (UTF-8 for multi-language support).
        /// 编码取决于OCR模型的能力（多语言支持使用UTF-8）。
        /// </remarks>
        public string Text = "";

        /// <summary>
        /// Initializes a new text recognition result with proper type configuration
        /// 初始化一个新的文本识别结果，自动配置正确的结果类型
        /// </summary>
        /// <remarks>
        /// Automatically sets <see cref="Result.Type"/> to <see cref="ResultType.TextRecResult"/>
        /// 自动将<see cref="Result.Type"/>设置为<see cref="ResultType.TextRecResult"/>
        /// </remarks>
        public TextRecResult()
        {
            Type = ResultType.TextRecResult;
        }

        /// <summary>
        /// Creates a deep copy of this text recognition result
        /// 创建此文本识别结果的深拷贝
        /// </summary>
        /// <returns>
        /// A new <see cref="TextRecResult"/> with copied properties
        /// 包含复制属性的新<see cref="TextRecResult"/>对象
        /// </returns>
        public new TextRecResult Clone()
        {
            return new TextRecResult
            {
                Type = Type,
                ImageSize = ImageSize,
                Id = Id,
                Confidence = Confidence,
                Category = Category,
                Text = Text
            };
        }

        /// <summary>
        /// Returns formatted string representation including the recognized text
        /// 返回包含识别文本的格式化字符串表示
        /// </summary>
        /// <returns>
        /// Combined string with base result info and recognized text
        /// 包含基础结果信息和识别文本的组合字符串
        /// </returns>
        public override string ToString()
        {
            return $"{base.ToString()}, Text: \"{Text}\"";
        }
    }
}
