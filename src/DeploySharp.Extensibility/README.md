# JYPPX.DeploySharp.Extensibility

Framework-neutral contracts for DeploySharp hosts that discover backend plugins, describe package/native dependencies, expose parameter schemas, and probe runtime health. The package does not download packages, load assemblies, start workers, or bundle vendor native runtimes.

Install the package together with `JYPPX.DeploySharp.Core`. A host can adapt an `IBackendPluginFactory` to the existing explicit `BackendRegistry` through `BackendPluginRegistryAdapter`, while native probes return structured `BackendRuntimeStatus` values suitable for an application-owned worker.
