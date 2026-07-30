using PcMarket.ApiClient;
using PcMarket.Admin.Components;
using PcMarket.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AdminSession>();
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped<IApiTokenProvider, AdminApiTokenProvider>();

var apiRoot = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5055";
builder.Services.AddPcMarketApiClient(apiRoot);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
