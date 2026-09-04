using System;

namespace JYPPX.DeploySharp.Extensibility
{
    internal static class ExtGuard
    {
        public static string NotNullOrWhiteSpace(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
            return value!;
        }

        public static string Identifier(string? value, string parameterName)
        {
            string identifier = NotNullOrWhiteSpace(value, parameterName);
            for (int index = 0; index < identifier.Length; index++)
            {
                char c = identifier[index];
                bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_' || c == '/';
                if (!valid) throw new ArgumentException("Identifiers contain only letters, numbers, '.', '-', '_', or '/'.", parameterName);
            }
            return identifier;
        }
    }
}
