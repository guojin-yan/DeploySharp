#if NET8_0 || NET9_0 || NET10_0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr
{
    /// <summary>Describes one verified file in a PaddleOCR-VL-1.6 Bundle. / 描述 PaddleOCR-VL-1.6 Bundle 中一个已验证文件。</summary>
    public sealed class PaddleOcrVlBundleArtifact
    {
        internal PaddleOcrVlBundleArtifact(string role, string fileName, string format, long size, string sha256)
        {
            Role = role;
            FileName = fileName;
            Format = format;
            Size = size;
            Sha256 = sha256;
        }

        /// <summary>Gets the stable Bundle role. / 获取稳定 Bundle 角色。</summary>
        public string Role { get; }
        /// <summary>Gets the simple file name relative to the Bundle root. / 获取相对 Bundle 根目录的简单文件名。</summary>
        public string FileName { get; }
        /// <summary>Gets the declared file format. / 获取声明的文件格式。</summary>
        public string Format { get; }
        /// <summary>Gets the expected byte size. / 获取预期字节数。</summary>
        public long Size { get; }
        /// <summary>Gets the expected lower-case SHA-256. / 获取预期小写 SHA-256。</summary>
        public string Sha256 { get; }
    }

    /// <summary>Loads and verifies a fixed PaddleOCR-VL-1.6 four- or five-graph Bundle manifest. / 加载并校验固定 PaddleOCR-VL-1.6 四图或五图 Bundle manifest。</summary>
    /// <remarks>The manifest is intentionally independent of the GitHub catalog. It can be shipped beside a local Bundle and verified before a backend Session is created. / Manifest 独立于 GitHub catalog，可随本地 Bundle 分发，并在创建后端 Session 前完成校验。</remarks>
    public sealed class PaddleOcrVlBundleManifest
    {
        /// <summary>Gets the supported local manifest schema. / 获取支持的本地 manifest schema。</summary>
        public const string OfficialSchemaVersion = "deploysharp-paddleocr-vl-bundle-v1";
        /// <summary>Gets the schema for a Bundle with a separate no-past prefill graph. / 获取含独立无 Past Prefill 图的 Bundle schema。</summary>
        public const string PrefillSchemaVersion = "deploysharp-paddleocr-vl-bundle-v2";
        /// <summary>Gets the supported fixed Bundle ID. / 获取支持的固定 Bundle ID。</summary>
        public const string OfficialBundleId = "paddleocr-vl/1.6";
        /// <summary>Gets the pinned official checkpoint revision. / 获取固定官方 checkpoint revision。</summary>
        public const string OfficialCheckpointRevision = "c5630abae1d940eafe0697512a0325494b02ab42";

        private static readonly string[] RequiredRoles =
        {
            "vision", "embedding", "decoder", "head",
            "tokenizer-model", "tokenizer-json", "tokenizer-config", "chat-template"
        };
        private static readonly string[] PrefillRoles =
        {
            "vision", "embedding", "decoder", "head", "prefill",
            "tokenizer-model", "tokenizer-json", "tokenizer-config", "chat-template"
        };

        private readonly IReadOnlyDictionary<string, PaddleOcrVlBundleArtifact> _artifacts;

        private PaddleOcrVlBundleManifest(string schemaVersion, string bundleId, string modelId,
            string checkpointRevision, IEnumerable<PaddleOcrVlBundleArtifact> artifacts)
        {
            SchemaVersion = schemaVersion;
            BundleId = bundleId;
            ModelId = modelId;
            CheckpointRevision = checkpointRevision;
            _artifacts = artifacts.ToDictionary(value => value.Role, StringComparer.Ordinal);
        }

        /// <summary>Gets the manifest schema version. / 获取 manifest schema 版本。</summary>
        public string SchemaVersion { get; }
        /// <summary>Gets the stable Bundle ID. / 获取稳定 Bundle ID。</summary>
        public string BundleId { get; }
        /// <summary>Gets the primary model ID. / 获取主模型 ID。</summary>
        public string ModelId { get; }
        /// <summary>Gets the pinned upstream checkpoint revision. / 获取固定上游 checkpoint revision。</summary>
        public string CheckpointRevision { get; }
        /// <summary>Gets whether this Bundle includes a separately verified prefill graph. / 获取此 Bundle 是否包含独立校验的 Prefill 图。</summary>
        public bool HasPrefillGraph => SchemaVersion == PrefillSchemaVersion;
        /// <summary>Gets all graph and tokenizer files in role order. / 获取按角色排列的全部图和 tokenizer 文件。</summary>
        public IReadOnlyList<PaddleOcrVlBundleArtifact> Artifacts => (HasPrefillGraph ? PrefillRoles : RequiredRoles).Select(GetRequiredArtifact).ToArray();

        /// <summary>Gets one required artifact descriptor. / 获取一个必需工件描述。</summary>
        public PaddleOcrVlBundleArtifact GetRequiredArtifact(string role)
        {
            if (string.IsNullOrWhiteSpace(role) || !_artifacts.TryGetValue(role, out PaddleOcrVlBundleArtifact? artifact))
                throw Invalid("The PaddleOCR-VL manifest does not declare required role: " + role);
            return artifact;
        }

        /// <summary>Loads the default manifest from a Bundle directory. / 从 Bundle 目录加载默认 manifest。</summary>
        public static PaddleOcrVlBundleManifest LoadFromDirectory(string bundleDirectory, string manifestFileName = "paddleocr-vl-bundle.json")
        {
            if (string.IsNullOrWhiteSpace(bundleDirectory)) throw new ArgumentException("A PaddleOCR-VL Bundle directory is required.", nameof(bundleDirectory));
            ValidateSimpleFileName(manifestFileName, nameof(manifestFileName));
            string root = Path.GetFullPath(bundleDirectory);
            string path = Path.Combine(root, manifestFileName);
            if (!File.Exists(path)) throw Invalid("The PaddleOCR-VL Bundle manifest is missing: " + path);
            try { return Parse(File.ReadAllText(path)); }
            catch (VisualException) { throw; }
            catch (Exception exception) { throw Invalid("The PaddleOCR-VL Bundle manifest could not be parsed.", exception); }
        }

        /// <summary>Parses and structurally validates a manifest JSON document. / 解析并结构校验 manifest JSON 文档。</summary>
        public static PaddleOcrVlBundleManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("A manifest JSON document is required.", nameof(json));
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    string schema = Required(root, "schemaVersion");
                    string bundle = Required(root, "bundleId");
                    string model = Required(root, "modelId");
                    string revision = Required(root, "checkpointRevision");
                    if (schema != OfficialSchemaVersion && schema != PrefillSchemaVersion) throw Invalid("Unsupported PaddleOCR-VL Bundle manifest schema: " + schema);
                    if (!string.Equals(bundle, OfficialBundleId, StringComparison.Ordinal)) throw Invalid("The PaddleOCR-VL Bundle ID is not the pinned 1.6 Bundle: " + bundle);
                    if (!string.Equals(model, OfficialBundleId, StringComparison.Ordinal)) throw Invalid("The PaddleOCR-VL model ID does not match the Bundle ID: " + model);
                    if (!string.Equals(revision, OfficialCheckpointRevision, StringComparison.Ordinal)) throw Invalid("The PaddleOCR-VL checkpoint revision is not the pinned 1.6 revision: " + revision);
                    if (!root.TryGetProperty("artifacts", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array) throw Invalid("The PaddleOCR-VL Bundle manifest must declare an artifacts array.");

                    var artifacts = new List<PaddleOcrVlBundleArtifact>();
                    var roles = new HashSet<string>(StringComparer.Ordinal);
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonElement row in rows.EnumerateArray())
                    {
                        string role = Required(row, "role");
                        string fileName = Required(row, "fileName");
                        string format = Required(row, "format").ToLowerInvariant();
                        long size = row.TryGetProperty("size", out JsonElement sizeValue) && sizeValue.TryGetInt64(out long parsedSize) ? parsedSize : -1;
                        string sha = Required(row, "sha256").ToLowerInvariant();
                        ValidateSimpleFileName(fileName, "fileName");
                        if (!roles.Add(role) || !names.Add(fileName)) throw Invalid("PaddleOCR-VL artifact roles and file names must be unique.");
                        if (size <= 0 || sha.Length != 64 || sha.Any(value => !Uri.IsHexDigit(value))) throw Invalid("A PaddleOCR-VL artifact must declare a positive size and valid SHA-256.");
                        if (format != "onnx" && format != "sentencepiece" && format != "json" && format != "jinja") throw Invalid("Unsupported PaddleOCR-VL artifact format: " + format);
                        artifacts.Add(new PaddleOcrVlBundleArtifact(role, fileName, format, size, sha));
                    }

                    string[] expectedRoles = schema == PrefillSchemaVersion ? PrefillRoles : RequiredRoles;
                    if (artifacts.Count != expectedRoles.Length || expectedRoles.Any(role => !roles.Contains(role)))
                        throw Invalid("The PaddleOCR-VL 1.6 manifest does not contain the graph and tokenizer roles required by its schema.");
                    CheckFormat(artifacts, "vision", "vision.onnx", "onnx");
                    CheckFormat(artifacts, "embedding", "embedding.onnx", "onnx");
                    CheckFormat(artifacts, "decoder", "decoder.onnx", "onnx");
                    CheckFormat(artifacts, "head", "head.onnx", "onnx");
                    if (schema == PrefillSchemaVersion) CheckFormat(artifacts, "prefill", "prefill.onnx", "onnx");
                    CheckFormat(artifacts, "tokenizer-model", "tokenizer.model", "sentencepiece");
                    CheckFormat(artifacts, "tokenizer-json", "tokenizer.json", "json");
                    CheckFormat(artifacts, "tokenizer-config", "tokenizer_config.json", "json");
                    CheckFormat(artifacts, "chat-template", "chat_template.jinja", "jinja");
                    return new PaddleOcrVlBundleManifest(schema, bundle, model, revision, artifacts);
                }
            }
            catch (VisualException) { throw; }
            catch (JsonException exception) { throw Invalid("The PaddleOCR-VL Bundle manifest is not valid JSON.", exception); }
            catch (Exception exception) when (!(exception is ArgumentException)) { throw Invalid("The PaddleOCR-VL Bundle manifest is invalid.", exception); }
        }

        /// <summary>Verifies every declared file and returns its absolute path by role. / 校验所有声明文件并按角色返回绝对路径。</summary>
        public IReadOnlyDictionary<string, string> VerifyFiles(string bundleDirectory, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(bundleDirectory)) throw new ArgumentException("A PaddleOCR-VL Bundle directory is required.", nameof(bundleDirectory));
            string root = Path.GetFullPath(bundleDirectory);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PaddleOcrVlBundleArtifact artifact in Artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = Path.GetFullPath(Path.Combine(root, artifact.FileName));
                if (!IsUnderRoot(root, path)) throw Invalid("A PaddleOCR-VL artifact escapes the Bundle directory: " + artifact.FileName);
                if (!File.Exists(path)) throw Invalid("A required PaddleOCR-VL Bundle file is missing: " + artifact.FileName);
                if (new FileInfo(path).Length != artifact.Size) throw Invalid("A PaddleOCR-VL Bundle file has an unexpected size: " + artifact.FileName);
                string actual = ComputeSha256(path, cancellationToken);
                if (!string.Equals(actual, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw Invalid("A PaddleOCR-VL Bundle file has an unexpected SHA-256: " + artifact.FileName + ";expected=" + artifact.Sha256 + ";actual=" + actual);
                result.Add(artifact.Role, path);
            }
            return result;
        }

        /// <summary>Gets one verified role path after checking the complete Bundle. / 完整校验 Bundle 后获取一个角色路径。</summary>
        public string GetVerifiedPath(string bundleDirectory, string role, CancellationToken cancellationToken = default(CancellationToken))
        {
            VerifyFiles(bundleDirectory, cancellationToken);
            PaddleOcrVlBundleArtifact artifact = GetRequiredArtifact(role);
            return Path.Combine(Path.GetFullPath(bundleDirectory), artifact.FileName);
        }

        private static void CheckFormat(IEnumerable<PaddleOcrVlBundleArtifact> artifacts, string role, string fileName, string format)
        {
            PaddleOcrVlBundleArtifact artifact = artifacts.Single(value => value.Role == role);
            if (!string.Equals(artifact.FileName, fileName, StringComparison.Ordinal) || !string.Equals(artifact.Format, format, StringComparison.Ordinal))
                throw Invalid("PaddleOCR-VL role " + role + " must use " + fileName + " with format " + format + ".");
        }

        private static string ComputeSha256(string path, CancellationToken cancellationToken)
        {
            using FileStream stream = File.OpenRead(path);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[128 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static bool IsUnderRoot(string root, string path)
        {
            string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string Required(JsonElement row, string name)
            => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
                ? value.GetString()! : throw Invalid("PaddleOCR-VL manifest is missing " + name + ".");

        private static void ValidateSimpleFileName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." || value.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0)
                throw Invalid("PaddleOCR-VL manifest file names must be simple file names: " + value);
        }

        private static VisualException Invalid(string message, Exception? inner = null)
            => new VisualException(VisualErrorCodes.GenerativeVisionLanguageIdentityMismatch, message, technicalDetails: inner?.GetType().Name);
    }
}
#endif
