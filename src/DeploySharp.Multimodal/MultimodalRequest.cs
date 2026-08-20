using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.LLM;
using JYPPX.DeploySharp.Results.Multimodal;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Describes one ordered, immutable multimodal generation request. / 描述一个有序且不可变的多模态生成请求。</summary>
    public sealed class MultimodalRequest
    {
        private readonly IReadOnlyList<MultimodalMediaInput> _media;

        /// <summary>Initializes a prompt and preserves the exact media order. / 初始化提示词并保留精确媒体顺序。</summary>
        public MultimodalRequest(
            string prompt,
            IEnumerable<MultimodalMediaInput> media,
            MultimodalTask task = MultimodalTask.QuestionAnswering,
            GenerationOptions? options = null)
        {
            if (!Enum.IsDefined(typeof(MultimodalTask), task)) throw new ArgumentOutOfRangeException(nameof(task));
            string text = prompt ?? throw new ArgumentNullException(nameof(prompt));
            if (task != MultimodalTask.Captioning && string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Question and instruction requests require a prompt.", nameof(prompt));
            if (media == null) throw new ArgumentNullException(nameof(media));
            var values = new List<MultimodalMediaInput>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MultimodalMediaInput input in media)
            {
                if (input == null) throw new ArgumentException("Media cannot contain null values.", nameof(media));
                if (!ids.Add(input.Id)) throw new MultimodalException(MultimodalErrorCodes.RequestInvalid, "Media identifiers must be unique within a request.", technicalDetails: "mediaId=" + input.Id);
                values.Add(input);
            }
            if (values.Count == 0) throw new ArgumentException("A multimodal request requires at least one media item.", nameof(media));
            Prompt = text;
            Task = task;
            Options = options ?? GenerationOptions.Default;
            _media = values.AsReadOnly();
        }

        /// <summary>Gets the exact caller prompt. / 获取调用方的精确提示词。</summary>
        public string Prompt { get; }
        /// <summary>Gets the request task. / 获取请求任务。</summary>
        public MultimodalTask Task { get; }
        /// <summary>Gets generation and timeout options. / 获取生成与超时选项。</summary>
        public GenerationOptions Options { get; }
        /// <summary>Gets media in stable prompt-mapping order. / 按稳定的提示词映射顺序获取媒体。</summary>
        public IReadOnlyList<MultimodalMediaInput> Media => _media;

        internal IReadOnlyList<MediaReference> CreateReferences() => _media.Select(value => value.ToReference()).ToList().AsReadOnly();
    }
}
