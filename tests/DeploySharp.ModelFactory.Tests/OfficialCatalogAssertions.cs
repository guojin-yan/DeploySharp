using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    internal static class OfficialCatalogAssertions
    {
        public static void Excludes(ValidatedModelCatalog externalCatalog)
        {
            var officialIds = new HashSet<string>(
                OfficialModelCatalog.Load().Document.Entries
                    .Select(entry => entry.ModelId)
                    .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
                    .Select(modelId => modelId!),
                StringComparer.OrdinalIgnoreCase);

            foreach (ModelCatalogEntry entry in externalCatalog.Document.Entries)
            {
                Assert.IsFalse(!string.IsNullOrWhiteSpace(entry.ModelId) && officialIds.Contains(entry.ModelId), "External catalog entry was admitted to the official catalog: " + entry.ModelId);
            }
        }
    }
}
