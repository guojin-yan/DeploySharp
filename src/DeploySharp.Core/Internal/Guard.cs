using System;

namespace JYPPX.DeploySharp.Internal
{
    internal static class Guard
    {
        public static T NotNull<T>(T? value, string parameterName)
            where T : class
        {
            return value ?? throw new ArgumentNullException(parameterName);
        }

        public static string NotNullOrWhiteSpace(string? value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value cannot be null, empty, or whitespace.", parameterName);
            }

            return value!;
        }

        public static string Identifier(string? value, string parameterName)
        {
            string identifier = NotNullOrWhiteSpace(value, parameterName);
            for (int index = 0; index < identifier.Length; index++)
            {
                char character = identifier[index];
                bool valid = (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '.'
                    || character == '-'
                    || character == '_'
                    || character == '/';

                if (!valid)
                {
                    throw new ArgumentException(
                        "Identifiers must contain only lowercase ASCII letters, numbers, '.', '-', '_', or '/'.",
                        parameterName);
                }
            }

            return identifier;
        }
    }
}
