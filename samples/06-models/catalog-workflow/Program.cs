using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;

internal static class Program
{
    private static int Main(string[] args)
    {
        ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
        string? requestedModelId = ReadOption(args, "--model-id");
        IEnumerable<ModelCatalogEntry> entries = catalog.Document.Entries;
        if (!string.IsNullOrWhiteSpace(requestedModelId))
        {
            entries = entries.Where(entry => string.Equals(entry.ModelId, requestedModelId, StringComparison.OrdinalIgnoreCase));
            if (!entries.Any()) throw new ArgumentException("The requested model ID is not present in the official catalog: " + requestedModelId);
        }

        int entryCount = 0;
        int artifactCount = 0;
        var taskCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ModelCatalogEntry entry in entries.OrderBy(value => value.ModelId, StringComparer.Ordinal))
        {
            entryCount++;
            taskCounts[entry.Task ?? "unknown"] = taskCounts.TryGetValue(entry.Task ?? "unknown", out int count) ? count + 1 : 1;
            foreach (ModelCatalogArtifact artifact in entry.Artifacts.OrderBy(value => value.ArtifactId, StringComparer.Ordinal))
            {
                artifactCount++;
                string backend = artifact.CompatibleBackends.Contains("onnxruntime", StringComparer.OrdinalIgnoreCase)
                    ? "onnxruntime"
                    : artifact.CompatibleBackends.First();
                ModelSelection selection = ModelCatalogQuery.Select(catalog, new ModelQuery(
                    modelId: entry.ModelId,
                    backend: backend,
                    format: artifact.Format,
                    precision: artifact.Precision,
                    quantization: artifact.Quantization,
                    includePreview: true))
                    .Single(value => string.Equals(value.Artifact.ArtifactId, artifact.ArtifactId, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine(string.Join("\t", new[]
                {
                    selection.Entry.ModelId ?? "",
                    selection.Entry.Task ?? "",
                    selection.Artifact.ArtifactId ?? "",
                    backend,
                    selection.Artifact.Format ?? "",
                    selection.Artifact.Assets.Count.ToString()
                }));
            }
        }

        Console.WriteLine($"DEPLOYSHARP_MODELFACTORY_SAMPLE_OK entries={entryCount} artifacts={artifactCount} preview={catalog.Document.Entries.Count(entry => entry.Status == ModelCatalogStatus.Preview)} revision={catalog.Document.CatalogRevision}");
        Console.WriteLine("tasks=" + string.Join(",", taskCounts.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => value.Key + "/" + value.Value)));
        return 0;
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (int index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count) throw new ArgumentException("Missing value for " + name);
                return args[index + 1];
            }
            string prefix = name + "=";
            if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return args[index].Substring(prefix.Length);
        }

        return null;
    }
}
