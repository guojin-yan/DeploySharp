using DeploySharpApp.Application;
using DeploySharpApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEPLOYSHARPAPP_URL") ?? "http://127.0.0.1:5180");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddScoped<DeploySharpAppService>(_ => AppComposition.CreateService());
var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<DeploySharpApp.Web.Components.App>().AddInteractiveServerRenderMode();
app.Run();
