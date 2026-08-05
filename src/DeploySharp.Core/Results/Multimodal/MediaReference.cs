using JYPPX.DeploySharp.Internal;

namespace JYPPX.DeploySharp.Results.Multimodal
{
    /// <summary>
    /// Defines media kinds that may participate in a multimodal request or result. / 定义可参与多模态请求或结果的媒体类型。
    /// </summary>
    public enum MediaKind
    {
        /// <summary>An image input. / 图像输入。</summary>
        Image = 0,
        /// <summary>An audio input. / 音频输入。</summary>
        Audio = 1,
        /// <summary>A video input. / 视频输入。</summary>
        Video = 2
    }

    /// <summary>
    /// Identifies media without embedding backend or imaging types in a result DTO. / 标识媒体，同时不在结果 DTO 中嵌入后端或图像类型。
    /// </summary>
    public sealed class MediaReference
    {
        /// <summary>Initializes a media reference. / 初始化媒体引用。</summary>
        public MediaReference(string id, MediaKind kind)
        {
            Id = Guard.Identifier(id, nameof(id));
            Kind = kind;
        }

        /// <summary>Gets the request-local media identifier. / 获取请求范围内的媒体标识符。</summary>
        public string Id { get; }

        /// <summary>Gets the media kind. / 获取媒体类型。</summary>
        public MediaKind Kind { get; }
    }
}
