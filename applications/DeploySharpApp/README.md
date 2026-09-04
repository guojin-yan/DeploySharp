# DeploySharpApp

DeploySharpApp is a host-neutral local experience application for DeploySharp. It contains a shared application layer and three hosts: .NET Framework 4.8 WinForms, .NET 10 WPF, and ASP.NET Core/Blazor Web.

The information architecture and visual direction follow `plan/DeploySharpAppPlan/网页原型`. Production hosts reuse that workbench/status/result structure while replacing prototype data and install animations with application services and structured runtime evidence.

The application currently uses controlled `ProjectReference` entries to the DeploySharp source tree. `DeploySharpApp.Engine` is the only application adapter that directly references Core, Extensibility, and the ONNX Runtime backend. The .NET 10 composition root can execute real ONNX Runtime CPU inference from explicit local model and named-tensor inputs; built-in `demo/*` entries remain clearly marked Fake operations.

The .NET Framework WinForms host keeps Visual Studio designer-compatible `MainForm.cs`, `MainForm.Designer.cs`, and `MainForm.resx` files. Event and application logic remain in `MainForm.cs`; generated-style control declarations and layout remain in the Designer partial class.

## Run

```powershell
dotnet build DeploySharpApp.sln
dotnet run --project src/DeploySharpApp.Web
```

The Web host listens on `http://127.0.0.1:5180` by default. Native runtimes are never loaded by the browser; they remain server/worker responsibilities. The first release exposes structured unavailable diagnostics when a runtime is not installed.

## Scope

The current slice provides a manifest-driven catalog, runtime diagnostics, model selection, real ONNX Runtime CPU inference, an explicit demo fallback, the JSON Lines worker protocol, and shared state consumed by all hosts. CUDA, LLamaSharp, TensorRT, OpenCV, and OpenVINO execution are not wired into the Engine yet. The net48 host never references the Engine or TensorRT and requires the future net10 BackendHost Worker for real ONNX inference.
