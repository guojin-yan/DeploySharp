using DeploySharpApp.Application;
using DeploySharpApp.Infrastructure;
using DeploySharpApp.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEPLOYSHARPAPP_URL") ?? "http://127.0.0.1:5180");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// Release assets include multi-gigabyte model bundles. Each operation supplies its own cancellation token.
builder.Services.AddHttpClient<VisualReleaseCatalogService>(client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddHttpClient<VisualTestImageCatalogService>(client => client.Timeout = TimeSpan.FromMinutes(10));
builder.Services.AddSingleton<ModelFactoryCatalogService>();
builder.Services.AddSingleton<ModelFactoryRuntimeService>();
builder.Services.AddSingleton<ModelPackRuntimeService>();
builder.Services.AddScoped<DeploySharpAppService>(_ => AppComposition.CreateService());
builder.Services.AddHttpClient<BackendLifecycleService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DeploySharpApp/2.0");
});
builder.Services.AddScoped<IBackendHostWorkerClient>(_ => new BackendHostWorkerClient());
builder.Services.AddScoped<RuntimeProbeService>();
builder.Services.AddSingleton<WebActivityStore>();
builder.Services.AddSingleton<BenchmarkHistoryStore>();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<DeploySharpApp.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
