using System;
using System.Collections.Generic;

namespace JYPPX.DeploySharp.Extensibility
{
    internal static class ContractValidation
    {
        public static IReadOnlyList<string> Identifiers(IEnumerable<string>? values, string parameterName, bool lowerCase = true)
        {
            var result = new List<string>();
            if (values != null)
            {
                foreach (string value in values)
                {
                    if (value == null) throw new ArgumentException("Collections cannot contain null entries.", parameterName);
                    string normalized = lowerCase ? value!.Trim().ToLowerInvariant() : value!.Trim();
                    string identifier = ExtGuard.Identifier(normalized, parameterName);
                    if (result.Contains(identifier)) throw new ArgumentException("Values must be unique.", parameterName);
                    result.Add(identifier);
                }
            }
            return result.AsReadOnly();
        }

        public static IReadOnlyList<T> Items<T>(IEnumerable<T>? values, string parameterName)
            where T : class
        {
            var result = new List<T>();
            if (values != null)
            {
                foreach (T value in values)
                {
                    if (value == null) throw new ArgumentException("Collections cannot contain null entries.", parameterName);
                    result.Add(value);
                }
            }
            return result.AsReadOnly();
        }

        public static string? Path(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string path = value!.Trim();
            if (path.IndexOf('\0') >= 0) throw new ArgumentException("Paths cannot contain a null character.", parameterName);
            return path;
        }

        public static string Sha256(string value, string parameterName)
        {
            string sha = ExtGuard.NotNullOrWhiteSpace(value, parameterName).Trim();
            if (sha.Length != 64) throw new ArgumentException("SHA-256 values must contain exactly 64 hexadecimal characters.", parameterName);
            for (int index = 0; index < sha.Length; index++)
            {
                char c = sha[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) throw new ArgumentException("SHA-256 values must be hexadecimal.", parameterName);
            }
            return sha.ToLowerInvariant();
        }
    }
}
