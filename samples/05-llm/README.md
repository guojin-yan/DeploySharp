# LLM workflow / LLM 工作流

LlmPromptInspection builds a system/user conversation, formats the prompt through the LLM prompt contract, and verifies the assistant turn boundary. Add a real GGUF runtime only when a local model and native backend are explicitly available.

```powershell
dotnet run --project samples/05-llm/LlmPromptInspection.csproj -c Release
```
