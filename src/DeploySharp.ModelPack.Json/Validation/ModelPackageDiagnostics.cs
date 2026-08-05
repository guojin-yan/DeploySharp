using System;
using System.Collections.Generic;
using JYPPX.DeploySharp.Errors;
using JYPPX.DeploySharp.Models;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Defines stable ModelPack diagnostic identifiers. / 定义稳定的 ModelPack 诊断标识。</summary>
    public static class ModelPackageDiagnosticCodes
    {
        /// <summary>A required value is absent. / 缺少必需值。</summary>
        public const string Required = "modelpack.required";
        /// <summary>A schema version is invalid or unsupported. / Schema 版本无效或不受支持。</summary>
        public const string InvalidVersion = "modelpack.version";
        /// <summary>An unknown JSON property was encountered. / 遇到未知 JSON 属性。</summary>
        public const string UnknownProperty = "modelpack.unknown-property";
        /// <summary>An identifier is invalid. / 标识符无效。</summary>
        public const string InvalidIdentifier = "modelpack.identifier";
        /// <summary>A configured resource limit was exceeded. / 超出配置的资源限制。</summary>
        public const string LimitExceeded = "modelpack.limit";
        /// <summary>A value or normalized path is duplicated. / 值或规范化路径重复。</summary>
        public const string Duplicate = "modelpack.duplicate";
        /// <summary>A relative path is unsafe or invalid. / 相对路径不安全或无效。</summary>
        public const string InvalidPath = "modelpack.path";
        /// <summary>A SHA256 value is malformed. / SHA256 值格式错误。</summary>
        public const string InvalidHash = "modelpack.hash";
        /// <summary>A value is outside its allowed domain. / 值超出允许范围。</summary>
        public const string InvalidValue = "modelpack.value";
        /// <summary>A file is missing from the package root. / 包根目录缺少文件。</summary>
        public const string FileNotFound = "modelpack.file-not-found";
        /// <summary>File size or SHA256 does not match the manifest. / 文件大小或 SHA256 与清单不匹配。</summary>
        public const string IntegrityMismatch = "modelpack.integrity";
        /// <summary>A symbolic link or reparse point violates the package boundary. / 符号链接或重解析点违反包边界。</summary>
        public const string LinkBoundary = "modelpack.link-boundary";
        /// <summary>JSON syntax or value conversion failed. / JSON 语法或值转换失败。</summary>
        public const string InvalidJson = "modelpack.json";
    }

    /// <summary>Represents one structured validation or loading diagnostic. / 表示一条结构化验证或加载诊断。</summary>
    public sealed class ModelPackageDiagnostic
    {
        /// <summary>Initializes a diagnostic. / 初始化诊断。</summary>
        public ModelPackageDiagnostic(string code, string message, string? jsonPath = null, string? artifactId = null, string? filePath = null)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("A diagnostic code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A diagnostic message is required.", nameof(message));
            Code = code;
            Message = message;
            JsonPath = jsonPath;
            ArtifactId = artifactId;
            FilePath = filePath;
        }

        /// <summary>Gets the stable diagnostic identifier. / 获取稳定诊断标识。</summary>
        public string Code { get; }
        /// <summary>Gets the user-readable message. / 获取用户可读消息。</summary>
        public string Message { get; }
        /// <summary>Gets the JSON path when known. / 获取已知的 JSON 路径。</summary>
        public string? JsonPath { get; }
        /// <summary>Gets the artifact identifier when known. / 获取已知的工件标识。</summary>
        public string? ArtifactId { get; }
        /// <summary>Gets the package-relative file path when known. / 获取已知的包内相对文件路径。</summary>
        public string? FilePath { get; }
    }

    /// <summary>Reports one or more ModelPack validation or loading failures. / 报告一个或多个 ModelPack 验证或加载失败。</summary>
    public sealed class ModelPackageValidationException : DeploySharpException
    {
        private readonly IReadOnlyList<ModelPackageDiagnostic> _diagnostics;

        /// <summary>Initializes a validation exception. / 初始化验证异常。</summary>
        public ModelPackageValidationException(
            string message,
            IEnumerable<ModelPackageDiagnostic> diagnostics,
            Exception? innerException = null,
            ModelId? modelId = null,
            string? technicalDetails = null)
            : base(DeploySharpErrorCodes.ModelArtifactInvalid, message, innerException, modelId: modelId, technicalDetails: technicalDetails)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var copied = new List<ModelPackageDiagnostic>();
            foreach (ModelPackageDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null) throw new ArgumentException("Diagnostics cannot contain null values.", nameof(diagnostics));
                copied.Add(diagnostic);
            }

            if (copied.Count == 0) throw new ArgumentException("At least one diagnostic is required.", nameof(diagnostics));
            _diagnostics = copied.AsReadOnly();
        }

        /// <summary>Gets all structured diagnostics. / 获取全部结构化诊断。</summary>
        public IReadOnlyList<ModelPackageDiagnostic> Diagnostics => _diagnostics;
    }
}
