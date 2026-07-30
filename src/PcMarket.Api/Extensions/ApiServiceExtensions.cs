using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PcMarket.Api.Auth;
using PcMarket.Api.ErrorHandling;
using PcMarket.Api.Health;
using PcMarket.Api.Localization;
using PcMarket.Api.Notifications;
using PcMarket.Api.Realtime;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Notifications;

namespace PcMarket.Api.Extensions;

/// <summary>Registers all API-host services: auth, authorization, rate limiting, OpenAPI, health checks,
/// Hangfire, problem-details, validation, output caching, and CORS.</summary>
public static class ApiServiceExtensions
{
    public const string ClientsCorsPolicy = "clients";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Content is answered in the caller's language. Unlike the storefront — which defaults to Russian for
        // a first-time visitor — an unlabelled API request falls back to English, matching the language the
        // canonical database columns are written in.
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.SetDefaultCulture(LanguageCodes.Fallback)
                .AddSupportedCultures(LanguageCodes.Supported)
                .AddSupportedUICultures(LanguageCodes.Supported);
            options.ApplyCurrentCultureToResponseHeaders = true;
        });
        services.AddScoped<ILanguageContext, RequestLanguageContext>();

        // Serialize enums as their names for readable payloads and OpenAPI schemas.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // SignalR delivers the access token in the query string during the WebSocket handshake.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization(options =>
            options.AddPolicy(Endpoints.AdminEndpoints.Policy, policy =>
                policy.RequireRole(PcMarket.Domain.Common.Roles.Admin, PcMarket.Domain.Common.Roles.Manager)));
        services.AddSignalR();

        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        services.AddScoped<INotificationJobScheduler, HangfireNotificationJobScheduler>();

        services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;

            // Only Nginx can reach the API — everything else sits on the internal Docker network — so the proxy
            // is always a private address. Trusting X-Forwarded-For from anywhere else would let a caller name
            // its own IP and thereby pick its own rate-limit partition, which is worse than not reading the
            // header at all. The defaults only trust loopback, and Nginx is a different container.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
            options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
        });

        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        var globalPermitLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned per caller, NOT a single bucket: AddFixedWindowLimiter would give the whole site one
            // shared allowance, so ten sign-ins anywhere would lock everyone else out for the rest of the minute.
            // Throttling brute force must not become a denial of service against legitimate customers.
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = globalPermitLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi();
        services.AddOutputCache();

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<MinioHealthCheck>("minio");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage =>
                storage.UseNpgsqlConnection(configuration.GetConnectionString("Postgres"))));
        services.AddHangfireServer();

        services.AddCors(options => options.AddPolicy(ClientsCorsPolicy, policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        return services;
    }

    /// <summary>Rate-limit bucket for a caller. Reads the client address, which is only the real one because
    /// <c>UseForwardedHeaders</c> runs first — without it every request behind Nginx shares a single bucket.</summary>
    private static string ClientPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
