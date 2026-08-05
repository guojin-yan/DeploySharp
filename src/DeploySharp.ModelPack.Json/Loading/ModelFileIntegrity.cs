using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Computes streaming SHA256 values for local model files. / 为本地模型文件流式计算 SHA256。</summary>
    public static class ModelFileIntegrity
    {
        /// <summary>Validates and normalizes a SHA256 value to lowercase hexadecimal text. / 验证 SHA256 值并规范化为小写十六进制文本。</summary>
        /// <exception cref="ArgumentException">The value is not exactly 64 hexadecimal characters. / 值不是恰好 64 个十六进制字符。</exception>
        public static string NormalizeSha256(string sha256)
        {
            if (string.IsNullOrWhiteSpace(sha256) || sha256.Length != 64)
            {
                throw new ArgumentException("SHA256 must contain exactly 64 hexadecimal characters.", nameof(sha256));
            }

            for (int index = 0; index < sha256.Length; index++)
            {
                if (!Uri.IsHexDigit(sha256[index])) throw new ArgumentException("SHA256 contains a non-hexadecimal character.", nameof(sha256));
            }

            return sha256.ToLowerInvariant();
        }

        /// <summary>Computes lowercase SHA256 for a file. / 计算文件的小写 SHA256。</summary>
        public static string ComputeSha256(string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var algorithm = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0) break;
                    algorithm.TransformBlock(buffer, 0, read, buffer, 0);
                }

                algorithm.TransformFinalBlock(new byte[0], 0, 0);
                return ToHex(algorithm.Hash!);
            }
        }

        /// <summary>Asynchronously computes lowercase SHA256 for a file. / 异步计算文件的小写 SHA256。</summary>
        public static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
            using (var algorithm = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    algorithm.TransformBlock(buffer, 0, read, buffer, 0);
                }

                algorithm.TransformFinalBlock(new byte[0], 0, 0);
                return ToHex(algorithm.Hash!);
            }
        }

        private static string ToHex(byte[] hash)
        {
            var characters = new char[hash.Length * 2];
            for (int index = 0; index < hash.Length; index++)
            {
                string pair = hash[index].ToString("x2", CultureInfo.InvariantCulture);
                characters[index * 2] = pair[0];
                characters[(index * 2) + 1] = pair[1];
            }

            return new string(characters);
        }
    }
}
