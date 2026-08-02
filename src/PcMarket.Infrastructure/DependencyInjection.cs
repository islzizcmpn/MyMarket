using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using PcMarket.Application.Abstractions.Audit;
using PcMarket.Application.Abstractions.Caching;
using PcMarket.Application.Abstractions.Catalog;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Messaging;
using PcMarket.Application.Abstractions.Notifications;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Abstractions.Storage;
using PcMarket.Infrastructure.Audit;
using PcMarket.Infrastructure.Caching;
using PcMarket.Infrastructure.Catalog;
using PcMarket.Infrastructure.Configuration;
using PcMarket.Infrastructure.Events;
using PcMarket.Infrastructure.Identity;
using PcMarket.Infrastructure.Messaging;
using PcMarket.Infrastructure.Notifications;
using PcMarket.Infrastructure.Persistence;
using PcMarket.Infrastructure.Storage;
using StackExchange.Redis;

namespace PcMarket.Infrastructure;

/// <summary>Registers the Infrastructure layer. Use the <see cref="IConfiguration"/> overload from the API
/// host (full wiring); the connection-string overload registers persistence + Identity only (used by tests).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PcMarketDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(PcMarketDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<PcMarketDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 8;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PcMarketDbContext>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");
        services.AddInfrastructure(connectionString);

        services.Configure<RedisSettings>(configuration.GetSection("Redis"));
        services.Configure<MinioSettings>(configuration.GetSection("Minio"));
        services.Configure<NotificationSettings>(configuration.GetSection("Notifications"));

        AddRedis(services, configuration);
        AddMinio(services, configuration);

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IMediaStorage, MinioMediaStorage>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddSingleton<ISmsSender, NoOpSmsSender>();
        // Replaced by the live implementation when PcMarket.Bot is registered after Infrastructure.
        services.AddSingleton<ITelegramMessenger, NoOpTelegramMessenger>();
        // Swap for a live FCM sender once a Firebase service account exists; callers are unaffected.
        services.AddSingleton<IPushSender, LoggingPushSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductSearchQuery, PgProductSearchQuery>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ITelegramLinkStore, TelegramLinkStore>();
        services.AddScoped<IUserLanguageStore, UserLanguageStore>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<INotificationChannel, TelegramNotificationChannel>();
        services.AddScoped<INotificationChannel, PushNotificationChannel>();
        services.AddScoped<INotificationChannel, SmsNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();

        return services;
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetSection("Redis").Get<RedisSettings>() ?? new RedisSettings();
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redis.Configuration));
    }

    private static void AddMinio(IServiceCollection services, IConfiguration configuration)
    {
        var minio = configuration.GetSection("Minio").Get<MinioSettings>() ?? new MinioSettings();
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(minio.Endpoint)
            .WithCredentials(minio.AccessKey, minio.SecretKey)
            .WithSSL(minio.UseSsl)
            .Build());
    }
}
