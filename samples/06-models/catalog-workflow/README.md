# Catalog workflow

The catalog workflow validates all current official entries and all variants without downloading large model files. For each artifact it selects a compatible backend, applies precision and quantization filters, verifies the selected artifact identity, and prints its asset count.

```powershell
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release
dotnet run --project samples/06-models/catalog-workflow/ModelFactoryCatalogInspection.csproj -c Release -- --model-id bria/rmbg-2.0
```
