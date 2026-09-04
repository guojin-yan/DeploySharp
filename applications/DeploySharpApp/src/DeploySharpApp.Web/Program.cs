using DeploySharpApp.Application;
using DeploySharpApp.Infrastructure;
using DeploySharpApp.Web;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEPLOYSHARPAPP_URL") ?? "http://127.0.0.1:5180");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient<VisualReleaseCatalogService>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<VisualTestImageCatalogService>(client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<DeploySharpAppService>(_ => AppComposition.CreateService());
builder.Services.AddSingleton<WebActivityStore>();
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<DeploySharpApp.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
