using System;
using System.Collections.Generic;
using System.IO;

namespace JYPPX.DeploySharp.ModelPack.Json
{
    /// <summary>Provides platform-neutral normalization for untrusted package-relative paths. / 为不可信包内相对路径提供平台无关的规范化。</summary>
    public static class ModelPackagePath
    {
        private static readonly HashSet<string> WindowsDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "con", "prn", "aux", "nul",
            "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
            "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
        };

        /// <summary>Normalizes a safe relative path to forward-slash form. / 将安全相对路径规范化为正斜杠形式。</summary>
        /// <exception cref="ArgumentException">The path is empty, rooted, traversing, ambiguous, or not portable. / 路径为空、带根、发生穿越、存在歧义或不可移植。</exception>
        public static string NormalizeRelativePath(string path)
        {
            if (!TryNormalizeRelativePath(path, out string? normalized, out string? error))
            {
                throw new ArgumentException(error, nameof(path));
            }

            return normalized!;
        }

        internal static bool TryNormalizeRelativePath(string? path, out string? normalized, out string? error)
        {
            normalized = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "A package-relative path is required.";
                return false;
            }

            if (path!.IndexOf('\0') >= 0 || path.Length > 32768)
            {
                error = "The path contains a NUL character or exceeds the portable length limit.";
                return false;
            }

            if (path[0] == '/' || path[0] == '\\' || path.IndexOf(':') >= 0)
            {
                error = "Rooted, UNC, and drive-qualified paths are not allowed.";
                return false;
            }

            string canonical = path.Replace('\\', '/');
            string[] segments = canonical.Split(new[] { '/' }, StringSplitOptions.None);
            var output = new List<string>(segments.Length);
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    error = "Empty, current-directory, and parent-directory path segments are not allowed.";
                    return false;
                }

                if (segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                {
                    error = "Path segments ending in a dot or space are not portable.";
                    return false;
                }

                for (int characterIndex = 0; characterIndex < segment.Length; characterIndex++)
                {
                    char character = segment[characterIndex];
                    if (character < 32 || character == '<' || character == '>' || character == '"' || character == '|' || character == '?' || character == '*')
                    {
                        error = "The path contains a control character or a platform-reserved character.";
                        return false;
                    }
                }

                string deviceCandidate = segment;
                int dotIndex = segment.IndexOf('.');
                if (dotIndex >= 0) deviceCandidate = segment.Substring(0, dotIndex);
                if (WindowsDeviceNames.Contains(deviceCandidate))
                {
                    error = "The path contains a Windows reserved device name.";
                    return false;
                }

                output.Add(segment);
            }

            normalized = string.Join("/", output);
            return true;
        }

        internal static string ToPlatformPath(string normalizedRelativePath)
        {
            return normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        internal static bool IsWithinRoot(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return normalizedCandidate.StartsWith(normalizedRoot, comparison);
        }
    }
}
