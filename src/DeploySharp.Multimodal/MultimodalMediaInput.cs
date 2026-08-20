using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Results.Multimodal;

namespace JYPPX.DeploySharp.Multimodal
{
    /// <summary>Identifies a rectangular region within one media item. / 标识一个媒体项中的矩形区域。</summary>
    public sealed class MultimodalRegion
    {
        /// <summary>Initializes a positive pixel-space region. / 初始化正面积的像素空间区域。</summary>
        public MultimodalRegion(string id, int x, int y, int width, int height)
        {
            Id = MultimodalValidation.Identifier(id, nameof(id));
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the request-local region identifier. / 获取请求范围内的区域标识符。</summary>
        public string Id { get; }
        /// <summary>Gets the left coordinate in source pixels. / 获取源像素中的左坐标。</summary>
        public int X { get; }
        /// <summary>Gets the top coordinate in source pixels. / 获取源像素中的上坐标。</summary>
        public int Y { get; }
        /// <summary>Gets the region width in source pixels. / 获取源像素中的区域宽度。</summary>
        public int Width { get; }
        /// <summary>Gets the region height in source pixels. / 获取源像素中的区域高度。</summary>
        public int Height { get; }
    }

    /// <summary>Owns immutable encoded media bytes and their request-local identity. / 持有不可变的编码媒体字节及其请求范围身份。</summary>
    public sealed class MultimodalMediaInput
    {
        private readonly byte[] _content;

        /// <summary>Initializes media and verifies an optional expected SHA-256. / 初始化媒体并校验可选的预期 SHA-256。</summary>
        public MultimodalMediaInput(
            string id,
            MediaKind kind,
            string contentType,
            byte[] content,
            string? expectedSha256 = null,
            MultimodalRegion? region = null)
        {
            Id = MultimodalValidation.Identifier(id, nameof(id));
            if (!Enum.IsDefined(typeof(MediaKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(contentType) || contentType.IndexOf('/') <= 0) throw new ArgumentException("A MIME content type is required.", nameof(contentType));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (content.Length == 0) throw new ArgumentException("Media content cannot be empty.", nameof(content));
            Kind = kind;
            ContentType = contentType.Trim().ToLowerInvariant();
            _content = (byte[])content.Clone();
            Sha256 = MultimodalValidation.Sha256(_content);
            if (expectedSha256 != null && !string.Equals(Sha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new MultimodalException(MultimodalErrorCodes.MediaIdentityInvalid, "The media SHA-256 does not match the expected identity.", technicalDetails: "mediaId=" + Id);
            }

            Region = region;
        }

        /// <summary>Gets the request-local media identifier. / 获取请求范围内的媒体标识符。</summary>
        public string Id { get; }
        /// <summary>Gets the media kind. / 获取媒体类型。</summary>
        public MediaKind Kind { get; }
        /// <summary>Gets the normalized MIME content type. / 获取规范化的 MIME 内容类型。</summary>
        public string ContentType { get; }
        /// <summary>Gets the lowercase content SHA-256. / 获取小写内容 SHA-256。</summary>
        public string Sha256 { get; }
        /// <summary>Gets an optional request-local region. / 获取可选的请求范围区域。</summary>
        public MultimodalRegion? Region { get; }
        /// <summary>Gets the encoded content length. / 获取编码内容长度。</summary>
        public int Length => _content.Length;

        /// <summary>Returns a defensive copy of encoded media bytes. / 返回编码媒体字节的防御性副本。</summary>
        public byte[] ToArray() => (byte[])_content.Clone();

        /// <summary>Creates the Core result reference for this input. / 为此输入创建 Core 结果引用。</summary>
        public MediaReference ToReference() => new MediaReference(Id, Kind);
    }

    internal static class MultimodalValidation
    {
        internal static string Identifier(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An identifier is required.", parameterName);
            string result = value.Trim();
            for (int index = 0; index < result.Length; index++)
            {
                char character = result[index];
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.' || character == '/'))
                {
                    throw new ArgumentException("Identifiers may contain only letters, digits, dash, underscore, dot, or slash.", parameterName);
                }
            }

            return result;
        }

        internal static string Sha256(byte[] content)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(content);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
