using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JYPPX.DeploySharp.ModelFactory;

namespace DeploySharp.ModelFactory.Cli
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
                {
                    PrintUsage();
                    return args.Length == 0 ? 1 : 0;
                }

                string command = args[0].Trim().ToLowerInvariant();
                string[] commandArgs = args.Skip(1).ToArray();
                switch (command)
                {
                    case "list":
                        return List(commandArgs);
                    case "install":
                        return await InstallAsync(commandArgs).ConfigureAwait(false);
                    default:
                        Console.Error.WriteLine("Unknown command: " + args[0]);
                        PrintUsage();
                        return 1;
                }
            }
            catch (ModelFactoryException exception)
            {
                Console.Error.WriteLine("ModelFactory failed: " + exception.Message);
                foreach (ModelFactoryDiagnostic diagnostic in exception.Diagnostics)
                {
                    Console.Error.WriteLine("  " + diagnostic.Code + ": " + diagnostic.Message);
                }

                return 2;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Model installation was cancelled.");
                return 3;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
        }

        private static int List(string[] args)
        {
            EnsureKnownOptions(args, "--preview");
            bool includePreview = HasFlag(args, "--preview");
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            Console.WriteLine("catalog-revision=" + catalog.Document.CatalogRevision);
            Console.WriteLine("model-id\tstatus\ttask\tartifact\tformat\tbackends");
            foreach (ModelCatalogEntry entry in catalog.Document.Entries.OrderBy(value => value.ModelId, StringComparer.Ordinal))
            {
                if (!includePreview && entry.Status != ModelCatalogStatus.Supported) continue;
                foreach (ModelCatalogArtifact artifact in entry.Artifacts.OrderBy(value => value.ArtifactId, StringComparer.Ordinal))
                {
                    Console.WriteLine(string.Join("\t", new[]
                    {
                        entry.ModelId ?? "",
                        entry.Status.ToString(),
                        entry.Task ?? "",
                        artifact.ArtifactId ?? "",
                        artifact.Format ?? "",
                        string.Join(",", artifact.CompatibleBackends)
                    }));
                }
            }

            return 0;
        }

        private static async Task<int> InstallAsync(string[] args)
        {
            EnsureKnownOptions(args, "--model-id", "--backend", "--format", "--precision", "--quantization", "--cache", "--preview", "--offline", "--timeout-minutes", "--max-retries");
            string modelId = RequireOption(args, "--model-id");
            string? backend = ReadOption(args, "--backend");
            string? format = ReadOption(args, "--format");
            string? precision = ReadOption(args, "--precision");
            string? quantization = ReadOption(args, "--quantization");
            string cacheRoot = ReadOption(args, "--cache") ?? GetDefaultCacheRoot();
            bool includePreview = HasFlag(args, "--preview");
            bool offline = HasFlag(args, "--offline");
            double timeoutMinutes = ReadPositiveDouble(args, "--timeout-minutes", 10);
            int maximumRetries = ReadNonNegativeInt(args, "--max-retries", 3);

            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            var options = new ModelFactoryOptions(
                cacheRoot,
                offline: offline,
                requestTimeout: TimeSpan.FromMinutes(timeoutMinutes),
                maximumRetries: maximumRetries);
            using var factory = new ModelFactoryClient(catalog, options);
            ModelSelection selection = factory.Select(new ModelQuery(
                modelId: modelId,
                backend: backend,
                format: format,
                precision: precision,
                quantization: quantization,
                includePreview: includePreview));

            Console.WriteLine("selected=" + selection.Entry.ModelId + ";artifact=" + selection.Artifact.ArtifactId + ";format=" + selection.Artifact.Format + ";backend=" + string.Join(",", selection.Artifact.CompatibleBackends));
            Console.WriteLine("cache=" + options.CacheRoot + ";offline=" + options.Offline);
            var lastProgress = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            var progress = new Progress<ModelDownloadProgress>(value => ReportProgress(value, lastProgress));
            MaterializedModel model = await factory.GetModelAsync(selection, progress).ConfigureAwait(false);
            if (!await factory.VerifyModelCacheAsync(selection).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The materialized cache did not pass the final verification.");
            }

            Console.WriteLine("installed=" + model.Selection.Entry.ModelId);
            Console.WriteLine("package-root=" + model.PackageRoot);
            Console.WriteLine("cache-key=" + model.CacheKey);
            return 0;
        }

        private static void ReportProgress(ModelDownloadProgress value, IDictionary<string, DateTime> lastProgress)
        {
            DateTime now = DateTime.UtcNow;
            if (value.Stage == ModelDownloadStage.Downloading
                && lastProgress.TryGetValue(value.AssetId, out DateTime previous)
                && now - previous < TimeSpan.FromMilliseconds(500)
                && value.ReceivedBytes < value.TotalBytes)
            {
                return;
            }

            lastProgress[value.AssetId] = now;
            string total = value.TotalBytes > 0 ? value.TotalBytes.ToString(CultureInfo.InvariantCulture) : "?";
            Console.WriteLine("progress=" + value.AssetId + ";stage=" + value.Stage + ";bytes=" + value.ReceivedBytes.ToString(CultureInfo.InvariantCulture) + "/" + total + ";attempt=" + value.Attempt);
        }

        private static string GetDefaultCacheRoot()
        {
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData)) localApplicationData = AppContext.BaseDirectory;
            return Path.Combine(localApplicationData, "DeploySharp", "ModelFactory");
        }

        private static string RequireOption(IReadOnlyList<string> args, string name)
        {
            string? value = ReadOption(args, name);
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing required option " + name + ".");
            return value;
        }

        private static string? ReadOption(IReadOnlyList<string> args, string name)
        {
            for (int index = 0; index < args.Count; index++)
            {
                string current = args[index];
                if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Option " + name + " requires a value.");
                    return args[index + 1];
                }

                string prefix = name + "=";
                if (current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return current.Substring(prefix.Length);
            }

            return null;
        }

        private static bool HasFlag(IReadOnlyList<string> args, string name)
        {
            return args.Any(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        }

        private static double ReadPositiveDouble(IReadOnlyList<string> args, string name, double fallback)
        {
            string? value = ReadOption(args, name);
            if (value == null) return fallback;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) || result <= 0) throw new ArgumentException("Option " + name + " must be a positive number.");
            return result;
        }

        private static int ReadNonNegativeInt(IReadOnlyList<string> args, string name, int fallback)
        {
            string? value = ReadOption(args, name);
            if (value == null) return fallback;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) || result < 0) throw new ArgumentException("Option " + name + " must be a non-negative integer.");
            return result;
        }

        private static void EnsureKnownOptions(IReadOnlyList<string> args, params string[] valueOptions)
        {
            var known = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase)
            {
                "--help",
                "-h"
            };
            for (int index = 0; index < args.Count; index++)
            {
                string value = args[index];
                if (!value.StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Unexpected argument: " + value);
                string option = value;
                int equals = option.IndexOf('=');
                if (equals >= 0) option = option.Substring(0, equals);
                if (!known.Contains(option)) throw new ArgumentException("Unknown option: " + value);
                if (RequiresValue(option) && equals < 0)
                {
                    if (index + 1 >= args.Count || args[index + 1].StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Option " + option + " requires a value.");
                    index++;
                }
            }
        }

        private static bool RequiresValue(string option)
        {
            return string.Equals(option, "--model-id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--backend", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--format", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--precision", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--quantization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--cache", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--timeout-minutes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(option, "--max-retries", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("DeploySharp ModelFactory CLI");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list [--preview]");
            Console.WriteLine("  install --model-id <id> [--backend <id>] [--format <format>] [--precision <id>] [--quantization <id>]");
            Console.WriteLine("          [--cache <path>] [--preview] [--offline]");
            Console.WriteLine("          [--timeout-minutes <number>] [--max-retries <number>]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run --project tools/DeploySharp.ModelFactory.Cli -- list --preview");
            Console.WriteLine("  dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --backend onnxruntime --format onnx --preview");
            Console.WriteLine("  dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install --model-id bria/rmbg-2.0 --backend onnxruntime --format onnx --precision int8 --quantization dynamic --preview");
            Console.WriteLine("  dotnet run --project tools/DeploySharp.ModelFactory.Cli -- install --model-id yolo/v8/detect/n --offline --cache D:\\DeploySharpCache");
        }
    }
}
