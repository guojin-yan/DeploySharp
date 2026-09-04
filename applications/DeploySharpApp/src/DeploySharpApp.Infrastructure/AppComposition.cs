using System;
using System.IO;
using System.Linq;
using DeploySharpApp.Application;
using DeploySharpApp.Contracts;
using DeploySharpApp.Plugin.Abstractions;
#if NET10_0_OR_GREATER
using DeploySharpApp.Engine;
#endif

namespace DeploySharpApp.Infrastructure
{
    public static class AppComposition
    {
        public static DeploySharpAppService CreateService(string? manifestDirectory = null)
        {
            var manifests = new PluginCatalog();
            var directory = manifestDirectory;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Any()) manifests.AddDirectory(directory!);
            else foreach (var manifest in DefaultManifests.Create()) manifests.Add(manifest);
            IModelRunner runner;
#if NET10_0_OR_GREATER
            runner = new EngineModelRunner(new DeploySharpEngine(), new FakeModelRunner(), new BackendHostWorkerClient());
#else
            runner = new LegacyHostModelRunner(new FakeModelRunner());
#endif
            return new DeploySharpAppService(new InMemoryAppCatalog(manifests.Manifests, new LocalRuntimeProbe()), runner);
        }

        public static ExperienceViewModel CreateViewModel(string? manifestDirectory = null) => new ExperienceViewModel(CreateService(manifestDirectory));
    }
}
