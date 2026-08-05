using System;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Controls local package integrity and boundary verification. / 控制本地模型包完整性与边界验证。</summary>
    public sealed class ModelPackageLoadOptions
    {
        /// <summary>Initializes local loading options. / 初始化本地加载选项。</summary>
        public ModelPackageLoadOptions(
            ModelPackageValidationOptions? validationOptions = null,
            bool verifySha256 = true,
            bool verifyFileSize = true,
            bool rejectUnsafeLinks = true,
            long maximumTotalFileBytes = 128L * 1024L * 1024L * 1024L)
        {
            if (maximumTotalFileBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTotalFileBytes));
            ValidationOptions = validationOptions ?? ModelPackageValidationOptions.Default;
            VerifySha256 = verifySha256;
            VerifyFileSize = verifyFileSize;
            RejectUnsafeLinks = rejectUnsafeLinks;
            MaximumTotalFileBytes = maximumTotalFileBytes;
        }

        /// <summary>Gets JSON and manifest validation limits. / 获取 JSON 与清单验证限制。</summary>
        public ModelPackageValidationOptions ValidationOptions { get; }
        /// <summary>Gets whether every file SHA256 is verified. / 获取是否验证每个文件的 SHA256。</summary>
        public bool VerifySha256 { get; }
        /// <summary>Gets whether every file size is verified. / 获取是否验证每个文件大小。</summary>
        public bool VerifyFileSize { get; }
        /// <summary>Gets whether unsafe or unresolved links are rejected. / 获取是否拒绝不安全或无法解析的链接。</summary>
        public bool RejectUnsafeLinks { get; }
        /// <summary>Gets maximum total declared package-file bytes. / 获取声明的包文件总字节上限。</summary>
        public long MaximumTotalFileBytes { get; }

        /// <summary>Gets default strict loading options. / 获取默认严格加载选项。</summary>
        public static ModelPackageLoadOptions Default { get; } = new ModelPackageLoadOptions();
    }
}
