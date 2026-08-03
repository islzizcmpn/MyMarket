using Microsoft.AspNetCore.Localization;
using PcMarket.ApiClient;
using PcMarket.Admin;
using PcMarket.Admin.Components;
using PcMarket.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

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

// The account is the source of truth for a manager's language (AspNetUsers.Language, shared with the bot and
// read at sign-in); this cookie is only how that choice reaches the *next request*, which is what a Blazor
// circuit takes its culture from. The cookie provider is deliberately the only one registered: with the
// default set, an English-configured browser would be served English via Accept-Language, but the panel
// opens in Russian until someone chooses otherwise.
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

// A Blazor Server circuit takes its culture from the HTTP request that opens it, so the switcher navigates
// here for real instead of flipping state inside the circuit. Persisting to the account happens in the
// circuit, which is where the manager's access token lives.
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
