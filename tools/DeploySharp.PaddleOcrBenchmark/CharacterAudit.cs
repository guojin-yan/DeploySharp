using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Models.PaddleOcr;

internal static partial class Program
{
    // Audit only dictionaries/requirements. No backend, native runtime, image or model is loaded.
    // 仅审计字典和需求，不加载后端、native 运行时、图片或模型。
    private static int RunCharacterAuditCommand(string[] args)
    {
        if ((args.Length != 4 && args.Length != 5) || (args.Length == 5 && args[4] != "--no-space"))
        {
            Console.Error.WriteLine("Usage: --audit-characters <dictionary.txt> <requirements.json> <report.json> [--no-space]");
            return 2;
        }
        try
        {
            EnsureAuditOutputIsDistinct(args[3], args[1], args[2]);
            CharacterRequirements requirements = LoadCharacterRequirements(args[2]);
            DictionaryAudit audit = AuditDictionary(args[1], "paddleocr.audit", "file", requirements, args.Length != 5);
            WriteCharacterAudit(args[3], requirements, new[] { audit });
            return audit.IsCovered ? 0 : 4;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("PADDLEOCR_CHARACTER_AUDIT_ERROR " + exception.Message);
            return 2;
        }
    }

    private static int RunStartupCharacterAudit(IReadOnlyList<ModelCase> models, string output)
    {
        string mode = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT")?.Trim().ToLowerInvariant() ?? "disabled";
        if (mode == "disabled") return 0;
        try
        {
            if (mode != "report" && mode != "require") throw new ArgumentException("DEPLOYSHARP_PADDLEOCR_CHARACTER_AUDIT accepts Disabled, Report or Require.");
            string path = Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS")
                ?? throw new ArgumentException("Set DEPLOYSHARP_PADDLEOCR_CHARACTER_REQUIREMENTS to your requirements JSON.");
            string report = output + ".characters.json";
            EnsureAuditOutputIsDistinct(report, path);
            CharacterRequirements requirements = LoadCharacterRequirements(path);
            var selected = new HashSet<string>((Environment.GetEnvironmentVariable("DEPLOYSHARP_PADDLEOCR_VERSIONS") ?? "v4,v5,v6").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
            var audits = new List<DictionaryAudit>();
            foreach (ModelCase model in models.Where(m => m.Role == "rec" && (selected.Contains(m.Version) || selected.Contains(m.Version + "-" + m.Variant))))
            {
                string dictionary = RecognitionDictionaryPath(model);
                EnsureAuditOutputIsDistinct(report, dictionary);
                audits.Add(AuditDictionary(dictionary, "external." + model.Version + ".dict", model.Version, requirements, true));
            }
            if (audits.Count == 0) throw new ArgumentException("No selected recognition dictionaries were found.");
            WriteCharacterAudit(report, requirements, audits);
            if (mode == "require" && audits.Any(audit => !audit.IsCovered))
            {
                Console.Error.WriteLine("PADDLEOCR_CHARACTER_AUDIT_REJECTED " + VisualErrorCodes.OcrCharacterCoverageMissing + "; inference not started; report=" + Path.GetFullPath(report));
                return 4;
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("PADDLEOCR_CHARACTER_AUDIT_ERROR " + exception.Message);
            return 2;
        }
    }

    private static CharacterRequirements LoadCharacterRequirements(string path)
    {
        using (var stream = File.OpenRead(path))
            if (stream.Length > 1024 * 1024) throw new ArgumentException("Requirements JSON exceeds 1 MiB.");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > 1024 * 1024) throw new ArgumentException("Requirements JSON exceeds 1 MiB.");
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        string json = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Requirements must be a JSON object mapping group names to required characters.");
        var groups = new Dictionary<string, string>(StringComparer.Ordinal);
        int total = 0;
        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            if (groups.Count >= 64 || string.IsNullOrWhiteSpace(property.Name) || property.Name.Length > 128 || property.Value.ValueKind != JsonValueKind.String)
                throw new ArgumentException("Use at most 64 named groups with nonempty string requirements; group names accept at most 128 UTF-16 units.");
            string text = property.Value.GetString()!;
            if (text.Length == 0 || (total += text.Length) > 65536) throw new ArgumentException("Requirements must be nonempty and contain at most 65536 total UTF-16 units.");
            // JsonDocument.GetString may repair invalid escaped surrogates. Require a lossless
            // roundtrip for escaped surrogate code units by validating them before decoding.
            ValidateJsonSurrogates(property.Value.GetRawText());
            if (!groups.TryAdd(property.Name, text)) throw new ArgumentException("Duplicate requirement group: " + property.Name);
        }
        if (groups.Count == 0) throw new ArgumentException("At least one requirement group is required.");
        return new CharacterRequirements(Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), groups);
    }

    private static void ValidateJsonSurrogates(string raw)
    {
        for (int index = 1; index < raw.Length - 1; index++)
        {
            if (raw[index] != '\\') continue;
            if (raw[++index] != 'u') continue;
            int code = int.Parse(raw.Substring(index + 1, 4), System.Globalization.NumberStyles.HexNumber, Invariant);
            index += 4;
            if (code >= 0xDC00 && code <= 0xDFFF) throw new ArgumentException("Requirements contain an unpaired escaped surrogate.");
            if (code < 0xD800 || code > 0xDBFF) continue;
            if (index + 6 >= raw.Length || raw[index + 1] != '\\' || raw[index + 2] != 'u') throw new ArgumentException("Requirements contain an unpaired escaped surrogate.");
            int low = int.Parse(raw.Substring(index + 3, 4), System.Globalization.NumberStyles.HexNumber, Invariant);
            if (low < 0xDC00 || low > 0xDFFF) throw new ArgumentException("Requirements contain an unpaired escaped surrogate.");
            index += 6;
        }
    }

    private static DictionaryAudit AuditDictionary(string path, string id, string version, CharacterRequirements requirements, bool appendSpace)
    {
        using (var sizeProbe = File.OpenRead(path))
            if (sizeProbe.Length > 16 * 1024 * 1024) throw new ArgumentException("Dictionary audit accepts files up to 16 MiB.");
        string sha;
        using (var stream = File.OpenRead(path)) sha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        OcrCharacterSet chars = PaddleOcrProfiles.LoadCharacterSet(path, id, version, appendSpace, sha);
        var auditor = new OcrCharacterSetAuditor(chars);
        var groups = requirements.Groups.Select(pair => new CharacterGroupAudit(pair.Key, auditor.Audit(pair.Value))).ToArray();
        var result = new DictionaryAudit(Path.GetFullPath(path), sha, appendSpace, auditor.TokenCount + 1, auditor, groups);
        Console.WriteLine("PADDLEOCR_CHARACTER_AUDIT dictionary=" + path + ";covered=" + result.IsCovered + ";duplicateEntries=" + auditor.DuplicateTokens.Count);
        foreach (CharacterGroupAudit group in groups)
            Console.WriteLine("PADDLEOCR_CHARACTER_GROUP name=" + group.Name + ";required=" + group.Coverage.DistinctRequiredScalarCount
                + ";missing=" + group.Coverage.MissingCharacters.Count + ";firstMissing=" + string.Join(",", group.Coverage.MissingCharacters.Take(16).Select(item => item.CodePoint)));
        return result;
    }

    private static void WriteCharacterAudit(string path, CharacterRequirements requirements, IReadOnlyList<DictionaryAudit> dictionaries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var report = new
        {
            SchemaVersion = 1, GeneratedAtUtc = DateTimeOffset.UtcNow,
            SourceRevision = Environment.GetEnvironmentVariable("DEPLOYSHARP_BENCHMARK_SOURCE_REVISION"),
            Semantics = "Independent Unicode scalar coverage; no normalization; not OCR accuracy or compound-token text segmentation.",
            Requirements = requirements, Dictionaries = dictionaries,
            IsCovered = dictionaries.Count > 0 && dictionaries.All(item => item.IsCovered)
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine("PADDLEOCR_CHARACTER_REPORT=" + Path.GetFullPath(path));
    }

    private static void EnsureAuditOutputIsDistinct(string output, params string[] inputs)
    {
        foreach (string input in inputs)
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(input), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The audit report must not overwrite its dictionary or requirements.");
    }

    private sealed record CharacterRequirements(string Path, string FileSha256, IReadOnlyDictionary<string, string> Groups);
    private sealed record CharacterGroupAudit(string Name, OcrCharacterCoverageReport Coverage);
    private sealed record DictionaryAudit(string Path, string FileSha256, bool SpaceAppended, int ExpectedCtcClassCount, OcrCharacterSetAuditor Dictionary, IReadOnlyList<CharacterGroupAudit> Groups)
    {
        public int BlankClassIndex => 0;
        public bool IsCovered => Groups.Count > 0 && Groups.All(group => group.Coverage.IsCovered);
    }
}
