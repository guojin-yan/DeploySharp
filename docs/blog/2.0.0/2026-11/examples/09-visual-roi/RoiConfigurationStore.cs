using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.Configuration.Json;

// Application-level file reload. The library owns JSON validation and atomic snapshot replacement.
internal sealed class RoiConfigurationStore(bool allowRebindNormalized)
{
    private readonly Dictionary<string, (VisualRoiManager Manager, string Json)> _entries = new();

    public VisualRoiSnapshot Capture(string path, VisualSize sourceSize)
    {
        try
        {
            string json = File.ReadAllText(path);
            if (!_entries.TryGetValue(path, out var current))
            {
                VisualRoiSnapshot first = ForSource(VisualRoiJsonSerializer.Deserialize(json), sourceSize);
                _entries.Add(path, (new VisualRoiManager(first), json));
                return first;
            }
            if (current.Json != json || current.Manager.Snapshot.SourceSize != sourceSize)
            {
                // Validate the entire candidate, including source size, before changing the manager.
                VisualRoiSnapshot candidate = ForSource(VisualRoiJsonSerializer.Deserialize(json), sourceSize);
                current.Manager.Replace(candidate.SourceSize, candidate.Rois);
                _entries[path] = (current.Manager, json);
            }
            return current.Manager.Snapshot;
        }
        catch (Exception e) when (e is VisualRoiJsonException or InvalidDataException or IOException)
        {
            if (!_entries.TryGetValue(path, out var previous) || previous.Manager.Snapshot.SourceSize != sourceSize) throw;
            Console.WriteLine("Config rejected; keeping snapshot " + previous.Manager.Snapshot.Version + ": " + e.Message);
            return previous.Manager.Snapshot;
        }
    }

    private VisualRoiSnapshot ForSource(VisualRoiSnapshot snapshot, VisualSize sourceSize)
    {
        if (snapshot.SourceSize == sourceSize) return snapshot;
        if (!allowRebindNormalized || snapshot.Rois.Any(r => r.CoordinateSpace != RoiCoordinateSpace.Normalized))
            throw new InvalidDataException("Config source size differs from image; edit it or use --rebind-normalized for all-normalized ROIs.");
        return new VisualRoiSnapshot(sourceSize, snapshot.Rois, snapshot.Version);
    }
}
