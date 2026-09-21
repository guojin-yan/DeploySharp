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
            Assert.IsFalse(text.Contains("Backend.OpenCV"));
            Assert.IsFalse(text.Contains("Backend.OpenVINO"));
            Assert.IsFalse(text.Contains("Backend.LlamaSharp"));
            Assert.IsFalse(text.Contains("Backend.TensorRT"));
            Assert.IsFalse(text.Contains("Visual.TensorRT"));
            Assert.IsFalse(text.Contains("CUDA", System.StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(text.Contains("cuDNN", System.StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TensorRtBridgeIsScopedToWinX64Worker()
        {
            string appRoot = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
            string workerProject = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.BackendHost", "DeploySharpApp.BackendHost.csproj"));
            string webProject = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.Web", "DeploySharpApp.Web.csproj"));
            string net48Project = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.Desktop.Net48", "DeploySharpApp.Desktop.Net48.csproj"));

            StringAssert.Contains(workerProject, "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");
            StringAssert.Contains(workerProject, "JYPPX.TensorRT.CSharp.API.Runtime.win-x64.trt10.11.cuda12.9.cudnn9.22.Bridge");
            Assert.IsTrue(File.Exists(Path.Combine(appRoot, "src", "DeploySharpApp.BackendHost", "TensorRtOnnxEngineAdapter.cs")));
            Assert.IsFalse(webProject.Contains("TensorRT", System.StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(net48Project.Contains("TensorRT", System.StringComparison.OrdinalIgnoreCase));
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

        [TestMethod]
        public void ReleaseVisualAdapterKeepsAuditedModelContracts()
        {
            string appRoot = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
            string adapter = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.BackendHost", "VisualReleaseInferenceAdapter.cs"));

            StringAssert.Contains(adapter, "registry.UseOnnxRuntime()");
            StringAssert.Contains(adapter, "registry.UseOpenVino()");
            StringAssert.Contains(adapter, "CreatePaddleOcrOfficialInferenceDetectionOptions");
            StringAssert.Contains(adapter, "CreatePaddleOcrOfficialInferenceRecognitionOptions");
            StringAssert.Contains(adapter, "CreatePaddleOcrOfficialInferenceTextLineOrientationOptions");
            StringAssert.Contains(adapter, "int candidates = id.Contains(\"v26\") ? 300");
            StringAssert.Contains(adapter, "PaddleOcrProfiles.LoadCharacterSet");
            StringAssert.Contains(adapter, "value is OcrOrientationResult orientation");
            StringAssert.Contains(adapter, "prediction.Segmentation");
            StringAssert.Contains(adapter, "paddleProbabilityThreshold");
            StringAssert.Contains(adapter, "paddleMaximumRegions");
            StringAssert.Contains(adapter, "DSAPP-PADDLEOCR-PACKAGE-UPGRADE-REQUIRED");
            StringAssert.Contains(adapter, "RunPaddleOcrWorkflowAsync");
            StringAssert.Contains(adapter, "new OcrPipeline(");
            StringAssert.Contains(adapter, "PP-OCRv5 DET + CLS + REC");
            StringAssert.Contains(adapter, "kind = \"ocr\"");
            StringAssert.Contains(adapter, "paddleDictionarySha256");
        }

        [TestMethod]
        public void WebExposesDedicatedRoiAndDocumentOcrWorkspace()
        {
            string appRoot = Path.GetFullPath(Path.Combine(TestContext?.TestRunDirectory ?? Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
            string page = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.Web", "Components", "Pages", "RoiOcr.razor"));
            string navigation = File.ReadAllText(Path.Combine(appRoot, "src", "DeploySharpApp.Web", "Components", "Layout", "MainLayout.razor"));

            StringAssert.Contains(page, "@page \"/roi-ocr\"");
            StringAssert.Contains(page, "schemaVersion 1.0");
            StringAssert.Contains(page, "PADDLE DOCUMENT PIPELINE");
            StringAssert.Contains(page, "等待新版 NuGet");
            StringAssert.Contains(page, "DET + CLS + REC");
            StringAssert.Contains(page, "paddleWorkflowVariant");
            StringAssert.Contains(page, "recognizedLines");
            StringAssert.Contains(navigation, "href=\"/roi-ocr\"");
        }

        public TestContext? TestContext { get; set; }
    }
}
