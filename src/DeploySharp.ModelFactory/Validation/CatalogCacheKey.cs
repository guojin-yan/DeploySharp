using System;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.ModelPack.Json;

namespace JYPPX.DeploySharp.ModelFactory
{
    internal static class CatalogCacheKey
    {
        public static string Compute(string catalogRevision, string releaseTag, string sha256, string relativePath)
        {
            string normalized = catalogRevision + "\n" + releaseTag + "\n" + ModelFileIntegrity.NormalizeSha256(sha256) + "\n" + ModelPackagePath.NormalizeRelativePath(relativePath);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}
