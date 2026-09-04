using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeploySharpApp.Infrastructure;

namespace DeploySharpApp.Application.Tests
{
    [TestClass]
    public class BoundaryTests
    {
        [TestMethod]
        public void Net48HostDoesNotReferenceModernEngineOrTensorRt()
        {
            var path = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "DeploySharpApp.Desktop.Net48", "DeploySharpApp.Desktop.Net48.csproj"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("DeploySharpApp.Engine"));
            Assert.IsFalse(text.Contains("Backend.TensorRT"));
            Assert.IsFalse(text.Contains("Visual.TensorRT"));

            var assetsPath = Path.Combine(Path.GetDirectoryName(path)!, "obj", "project.assets.json");
            using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var libraries = assets.RootElement.GetProperty("libraries").EnumerateObject().Select(item => item.Name).ToArray();
            Assert.IsFalse(libraries.Any(item => item.Contains("DeploySharpApp.Engine", System.StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(libraries.Any(item => item.Contains("OnnxRuntime", System.StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(libraries.Any(item => item.Contains("TensorRT", System.StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(libraries.Any(item => item.Contains("CUDA", System.StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(libraries.Any(item => item.Contains("cuDNN", System.StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void WebHostDoesNotDirectlyReferenceNativeSdkProjectsOrPackages()
        {
            var path = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "DeploySharpApp.Web", "DeploySharpApp.Web.csproj"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("Microsoft.ML.OnnxRuntime"));
            Assert.IsFalse(text.Contains("Backend.OnnxRuntime"));
            Assert.IsFalse(text.Contains("Backend.TensorRT"));
            Assert.IsFalse(text.Contains("Visual.TensorRT"));
        }

        [TestMethod]
        public void Net48FormKeepsVisualStudioDesignerStructure()
        {
            var projectPath = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "DeploySharpApp.Desktop.Net48", "DeploySharpApp.Desktop.Net48.csproj"));
            var directory = Path.GetDirectoryName(projectPath)!;
            var project = File.ReadAllText(projectPath);
            var code = File.ReadAllText(Path.Combine(directory, "MainForm.cs"));
            var designer = File.ReadAllText(Path.Combine(directory, "MainForm.Designer.cs"));

            Assert.IsTrue(File.Exists(Path.Combine(directory, "MainForm.resx")));
            Assert.IsTrue(project.Contains("<SubType>Form</SubType>"));
            Assert.IsTrue(project.Contains("<DependentUpon>MainForm.cs</DependentUpon>"));
            Assert.IsTrue(code.Contains("partial class MainForm"));
            Assert.IsTrue(code.Contains("InitializeComponent();"));
            Assert.IsTrue(designer.Contains("private void InitializeComponent()"));
        }

        [TestMethod]
        public void EngineReferencesOnlyTheRequiredDeploySharpRuntimeProjects()
        {
            var path = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "DeploySharpApp.Engine", "DeploySharpApp.Engine.csproj"));
            var text = File.ReadAllText(path);
            Assert.IsTrue(text.Contains("DeploySharp.Core"));
            Assert.IsTrue(text.Contains("DeploySharp.Extensibility"));
            Assert.IsTrue(text.Contains("DeploySharp.Backend.OnnxRuntime"));
            Assert.IsFalse(text.Contains("TensorRT"));
            Assert.IsFalse(text.Contains("DeploySharp.Visual"));
        }

        public TestContext? TestContext { get; set; }
    }
}
