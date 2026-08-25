# Visual workflow / Visual 工作流

VisualProfileInspection composes profile creation, exact named input/output contracts, task registration, decoder ownership, and registry freezing. It is a complete profile lifecycle case rather than a single constructor call.

```powershell
dotnet run --project samples/02-visual/VisualProfileInspection.csproj -c Release
```

The real image workflows are under samples/06-models/release-inference and the task-specific clean consumers under tests/clean-consumer.
