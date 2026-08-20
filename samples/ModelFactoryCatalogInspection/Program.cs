using System;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;

internal static class Program
{
    private static int Main()
    {
        ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
        int preview = catalog.Document.Entries.Count(entry => entry.Status == ModelCatalogStatus.Preview);
        Console.WriteLine($"DEPLOYSHARP_MODELFACTORY_SAMPLE_OK entries={catalog.Document.Entries.Count} preview={preview} revision={catalog.Document.CatalogRevision}");
        return 0;
    }
}
