using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Application.Abstractions.Messaging;
using PcMarket.Bot.Configuration;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Localization;
using PcMarket.Bot.Notifications;
using PcMarket.Bot.Runtime;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.Bot;

/// <summary>Registers the Telegram bot module. Call this <em>after</em> <c>AddInfrastructure</c>: the live
/// <see cref="ITelegramMessenger"/> registered here supersedes Infrastructure's no-op, which is what wires
/// the Telegram notification channel to the real bot.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBot(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelegramSettings>(configuration.GetSection("Telegram"));

        services.AddSingleton<TelegramClientAccessor>();
        services.AddSingleton<ITelegramMessenger, TelegramMessenger>();

        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<BotResponder>();
        services.AddScoped<BotSession>();
        services.AddScoped<AccountFlow>();
        services.AddScoped<CatalogFlow>();
        services.AddScoped<CartFlow>();
        services.AddScoped<OrderFlow>();
        services.AddScoped<AdminFlow>();
        services.AddScoped<LanguageFlow>();
        services.AddScoped<BotLanguageService>();
        services.AddScoped<TelegramUpdateHandler>();

        services.AddScoped<IDomainEventHandler<OrderPlacedEvent>, TelegramAdminAlertHandler>();

        return services;
    }
}
