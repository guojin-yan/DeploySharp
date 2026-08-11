using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JYPPX.DeploySharp.Visual
{
    /// <summary>Parses bounded Donut tag output without repairing malformed syntax. / 在不修复畸形语法的前提下 Parse 受限 Donut Tag 输出。</summary>
    public static class DocumentStructuredOutputParser
    {
        /// <summary>Parses exact balanced tags, retains raw tokens/text/status/schema/provenance, and emits deterministic JSON only on success. / Parse 精确平衡 Tag，保留原始 Token/Text/Status/Schema/Provenance，仅在成功时输出确定性 JSON。</summary>
        public static DocumentStructuredOutput Parse(IEnumerable<int> tokenIds, string rawText, DocumentSchemaContract schema, string requestedSchemaId, string pageIdentity, string promptSha256, int pageIndex = 0)
        {
            if (tokenIds == null || rawText == null || schema == null) throw new ArgumentNullException(tokenIds == null ? nameof(tokenIds) : rawText == null ? nameof(rawText) : nameof(schema));
            int[] tokens = tokenIds.ToArray();
            if (!string.Equals(requestedSchemaId, schema.SchemaId, StringComparison.Ordinal)) return Failure(tokens, rawText, schema, DocumentParseStatus.SchemaMismatch, "DS-DOCUMENT-SCHEMA-MISMATCH");
            if (rawText.Length > schema.MaximumTextCharacters) return Failure(tokens, rawText, schema, DocumentParseStatus.LimitExceeded, "DS-DOCUMENT-TEXT-LIMIT");
            if (!string.Equals(schema.Grammar, "donut-tags-v1", StringComparison.Ordinal)) return Failure(tokens, rawText, schema, DocumentParseStatus.InvalidSyntax, "DS-DOCUMENT-GRAMMAR-UNSUPPORTED");
            if (!DocumentUnderstandingHash.IsSha256(pageIdentity) || !DocumentUnderstandingHash.IsSha256(promptSha256) || pageIndex < 0) throw new VisualException(VisualErrorCodes.DocumentUnderstandingContractInvalid, "Structured parse provenance is invalid.");

            var roots = new List<DocumentStructuredNode>();
            var stack = new Stack<Frame>();
            int fields = 0;
            int index = 0;
            try
            {
                while (index < rawText.Length)
                {
                    int marker = rawText.IndexOf('<', index);
                    if (marker < 0)
                    {
                        AppendText(stack, rawText, index, rawText.Length);
                        index = rawText.Length;
                        break;
                    }
                    AppendText(stack, rawText, index, marker);
                    int end = rawText.IndexOf('>', marker + 1);
                    if (end < 0) throw new ParseFailure("DS-DOCUMENT-TAG-UNTERMINATED");
                    string tag = rawText.Substring(marker + 1, end - marker - 1);
                    if (string.Equals(tag, "sep/", StringComparison.Ordinal))
                    {
                        if (stack.Count == 0 || stack.Peek().Children.Count != 0) throw new ParseFailure("DS-DOCUMENT-SEP-CONTEXT");
                        stack.Peek().Text.Append('\u001f');
                    }
                    else if (tag.StartsWith("s_", StringComparison.Ordinal))
                    {
                        string name = tag.Substring(2);
                        ValidateName(name);
                        if (stack.Count >= schema.MaximumDepth) throw new LimitFailure("DS-DOCUMENT-DEPTH-LIMIT");
                        stack.Push(new Frame(name, marker, end + 1));
                    }
                    else if (tag.StartsWith("/s_", StringComparison.Ordinal))
                    {
                        string name = tag.Substring(3);
                        ValidateName(name);
                        if (stack.Count == 0 || !string.Equals(stack.Peek().Name, name, StringComparison.Ordinal)) throw new ParseFailure("DS-DOCUMENT-TAG-MISMATCH");
                        Frame frame = stack.Pop();
                        List<DocumentStructuredNode> completed = Complete(frame, end + 1, schema, pageIdentity, promptSha256, pageIndex);
                        fields = checked(fields + completed.Sum(CountNodes));
                        if (fields > schema.MaximumFields) throw new LimitFailure("DS-DOCUMENT-FIELD-LIMIT");
                        if (stack.Count == 0) roots.AddRange(completed); else
                        {
                            if (HasNonWhitespace(stack.Peek().Text)) throw new ParseFailure("DS-DOCUMENT-MIXED-CONTENT");
                            stack.Peek().Children.AddRange(completed);
                        }
                    }
                    else throw new ParseFailure("DS-DOCUMENT-TAG-UNKNOWN");
                    index = end + 1;
                }
                if (stack.Count != 0) throw new ParseFailure("DS-DOCUMENT-TAG-UNCLOSED");
                if (roots.Count == 0) throw new ParseFailure("DS-DOCUMENT-EMPTY");
                string json = EmitObject(roots);
                return new DocumentStructuredOutput(tokens, rawText, DocumentParseStatus.Success, schema.SchemaId, schema.Identity, roots, json, null);
            }
            catch (LimitFailure failure) { return Failure(tokens, rawText, schema, DocumentParseStatus.LimitExceeded, failure.Message); }
            catch (OverflowException) { return Failure(tokens, rawText, schema, DocumentParseStatus.LimitExceeded, "DS-DOCUMENT-COUNT-OVERFLOW"); }
            catch (ParseFailure failure) { return Failure(tokens, rawText, schema, DocumentParseStatus.InvalidSyntax, failure.Message); }
        }

        private static void AppendText(Stack<Frame> stack, string source, int start, int end)
        {
            if (end <= start) return;
            string value = source.Substring(start, end - start);
            if (stack.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(value)) throw new ParseFailure("DS-DOCUMENT-TEXT-OUTSIDE-ROOT");
                return;
            }
            Frame frame = stack.Peek();
            if (frame.Children.Count != 0 && !string.IsNullOrWhiteSpace(value)) throw new ParseFailure("DS-DOCUMENT-MIXED-CONTENT");
            if (frame.Text.Length == 0) frame.ValueStart = start;
            frame.Text.Append(value);
        }

        private static List<DocumentStructuredNode> Complete(Frame frame, int end, DocumentSchemaContract schema, string pageIdentity, string promptSha, int pageIndex)
        {
            var provenance = new DocumentFieldProvenance(pageIndex, pageIdentity, schema.Identity, promptSha, frame.Start, end);
            if (frame.Children.Count != 0)
            {
                if (HasNonWhitespace(frame.Text)) throw new ParseFailure("DS-DOCUMENT-MIXED-CONTENT");
                return new List<DocumentStructuredNode> { new DocumentStructuredNode(frame.Name, null, frame.Children, provenance) };
            }
            string[] parts = frame.Text.ToString().Split(new[] { '\u001f' }, StringSplitOptions.None);
            var result = new List<DocumentStructuredNode>();
            foreach (string part in parts)
            {
                string value = part.Trim();
                if (value.Length == 0) throw new ParseFailure("DS-DOCUMENT-SCALAR-EMPTY");
                result.Add(new DocumentStructuredNode(frame.Name, value, new DocumentStructuredNode[0], provenance));
            }
            return result;
        }

        private static int CountNodes(DocumentStructuredNode node) => checked(1 + node.Children.Sum(CountNodes));
        private static bool HasNonWhitespace(StringBuilder value)
        {
            for (int index = 0; index < value.Length; index++) if (!char.IsWhiteSpace(value[index])) return true;
            return false;
        }
        private static void ValidateName(string name)
        {
            if (name.Length == 0 || name.Length > 128) throw new ParseFailure("DS-DOCUMENT-FIELD-NAME");
            foreach (char value in name) if (!char.IsLetterOrDigit(value) && value != '_' && value != '-') throw new ParseFailure("DS-DOCUMENT-FIELD-NAME");
        }

        private static string EmitObject(IReadOnlyList<DocumentStructuredNode> nodes)
        {
            var output = new StringBuilder("{");
            bool first = true;
            foreach (IGrouping<string, DocumentStructuredNode> group in nodes.GroupBy(value => value.Name, StringComparer.Ordinal))
            {
                if (!first) output.Append(','); first = false; AppendString(output, group.Key); output.Append(':');
                DocumentStructuredNode[] occurrences = group.ToArray();
                if (occurrences.Length == 1) EmitValue(output, occurrences[0]);
                else { output.Append('['); for (int index = 0; index < occurrences.Length; index++) { if (index != 0) output.Append(','); EmitValue(output, occurrences[index]); } output.Append(']'); }
            }
            return output.Append('}').ToString();
        }
        private static void EmitValue(StringBuilder output, DocumentStructuredNode node)
        {
            if (node.Value != null) { AppendString(output, node.Value); return; }
            output.Append(EmitObject(node.Children));
        }
        private static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            foreach (char current in value)
            {
                switch (current)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default: if (current < 0x20) output.Append("\\u" + ((int)current).ToString("x4")); else output.Append(current); break;
                }
            }
            output.Append('"');
        }
        private static DocumentStructuredOutput Failure(IEnumerable<int> tokens, string rawText, DocumentSchemaContract schema, DocumentParseStatus status, string diagnostic) => new DocumentStructuredOutput(tokens, rawText, status, schema.SchemaId, schema.Identity, new DocumentStructuredNode[0], null, diagnostic);

        private sealed class Frame
        {
            internal Frame(string name, int start, int contentStart) { Name = name; Start = start; ValueStart = contentStart; }
            internal string Name { get; }
            internal int Start { get; }
            internal int ValueStart { get; set; }
            internal StringBuilder Text { get; } = new StringBuilder();
            internal List<DocumentStructuredNode> Children { get; } = new List<DocumentStructuredNode>();
        }
        private sealed class ParseFailure : Exception { internal ParseFailure(string message) : base(message) { } }
        private sealed class LimitFailure : Exception { internal LimitFailure(string message) : base(message) { } }
    }
}
