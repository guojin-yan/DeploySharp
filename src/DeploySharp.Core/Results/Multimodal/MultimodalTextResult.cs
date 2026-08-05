using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Results.Language;

namespace JYPPX.DeploySharp.Results.Multimodal
{
    /// <summary>
    /// Associates a generated text result with media consumed by the model. / 将生成文本结果与模型使用的媒体关联。
    /// </summary>
    public sealed class MultimodalTextResult
    {
        private readonly IReadOnlyList<MediaReference> _media;

        /// <summary>Initializes a multimodal text result. / 初始化多模态文本结果。</summary>
        public MultimodalTextResult(GenerationResult generation, IEnumerable<MediaReference> media)
        {
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            if (media == null) throw new ArgumentNullException(nameof(media));
            var values = new List<MediaReference>();
            foreach (MediaReference item in media)
            {
                if (item == null) throw new ArgumentException("Media references cannot contain null values.", nameof(media));
                values.Add(item);
            }

            _media = values.AsReadOnly();
        }

        /// <summary>Gets the generated text result. / 获取生成文本结果。</summary>
        public GenerationResult Generation { get; }

        /// <summary>Gets media references consumed by the model. / 获取模型使用的媒体引用。</summary>
        public IReadOnlyList<MediaReference> Media => _media;
    }
}
