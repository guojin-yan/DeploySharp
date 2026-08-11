using System;
using JYPPX.DeploySharp.ModelFactory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.ModelFactory.Tests
{
    [TestClass]
    public sealed class Stage29LlmCatalogAdmissionTests
    {
        [TestMethod]
        public void ExternalGgufBlockerDoesNotPopulateOfficialCatalog()
        {
            ValidatedModelCatalog catalog = OfficialModelCatalog.Load();
            Assert.AreEqual(0, catalog.Document.Entries.Count);
            Assert.IsTrue(catalog.Document.SourceRepository != null);
            Assert.AreEqual("1.0", catalog.Document.SchemaVersion);
        }
    }
}
