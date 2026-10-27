using System;
using System.IO;

namespace DeploySharp.Visual.Tests;

internal static class RepositoryTestFiles
{
    private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

    public static string Root => RepositoryRoot.Value;

    public static string Find(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("The test asset path must be repository-relative.", nameof(relativePath));

        string root = Root;
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The test asset path escapes the repository root.");
        if (!File.Exists(path))
            throw new FileNotFoundException("The repository test asset is missing.", relativePath);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("DEPLOYSHARP_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string root = Path.GetFullPath(configured);
            if (!File.Exists(Path.Combine(root, "DeploySharp.sln")))
                throw new DirectoryNotFoundException($"DEPLOYSHARP_REPOSITORY_ROOT is not a DeploySharp checkout: {root}");
            return root;
        }

        foreach (string seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (DirectoryInfo? directory = new(seed); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "DeploySharp.sln")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Cannot locate the DeploySharp checkout. Set DEPLOYSHARP_REPOSITORY_ROOT when tests use an external output directory.");
    }
}
