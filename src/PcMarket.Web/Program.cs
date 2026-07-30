using Microsoft.AspNetCore.Localization;
using PcMarket.ApiClient;
using PcMarket.Web;
using PcMarket.Web.Components;
using PcMarket.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Per-circuit session + typed API clients.
builder.Services.AddScoped<WebSession>();
builder.Services.AddScoped<SessionStore>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<StoreCart>();
builder.Services.AddScoped<IApiTokenProvider, WebApiTokenProvider>();

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

// The cookie provider is deliberately the only one registered: with the default set, an
// English-configured browser would be served English via Accept-Language, but Russian is the
// storefront default until the shopper picks otherwise.
var localization = new RequestLocalizationOptions()
    .SetDefaultCulture(SupportedCultures.Default)
    .AddSupportedCultures(SupportedCultures.Codes)
    .AddSupportedUICultures(SupportedCultures.Codes);
localization.RequestCultureProviders = [new CookieRequestCultureProvider()];
app.UseRequestLocalization(localization);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// A Blazor Server circuit takes its culture from the HTTP request that opens it, so the header's
// language switcher navigates here for real instead of flipping state inside the circuit.
app.MapGet("/set-language", (HttpContext http, string? culture, string? redirect) =>
{
    var selected = SupportedCultures.IsSupported(culture) ? culture! : SupportedCultures.Default;

    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selected)),
        new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
        });

    return Results.LocalRedirect(IsLocalPath(redirect) ? redirect! : "/");
});

app.Run();

// Only same-site paths may be redirected to. Rejects "//host" and "/\host", which browsers would
// otherwise follow off-site.
static bool IsLocalPath(string? value) =>
    value is { Length: > 0 }
    && value[0] == '/'
    && (value.Length == 1 || (value[1] != '/' && value[1] != '\\'));
