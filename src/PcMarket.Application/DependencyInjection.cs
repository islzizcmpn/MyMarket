using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Application.Admin;
using PcMarket.Application.Carts;
using PcMarket.Application.Catalog;
using PcMarket.Application.Localization;
using PcMarket.Application.Notifications;
using PcMarket.Application.Orders;
using PcMarket.Application.Payments;
using PcMarket.Application.Users;
using PcMarket.Domain.Ordering.Events;

namespace PcMarket.Application;

/// <summary>Registers Application-layer use-case services and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();
        services.AddScoped<CartService>();
        services.AddScoped<AddressService>();
        services.AddScoped<DeviceTokenService>();
        services.AddScoped<OrderService>();
        services.AddScoped<OrderMaintenanceService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<NotificationDeliveryService>();
        services.AddScoped<AdminCatalogService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<AdminCustomerService>();
        services.AddScoped<AdminDashboardService>();
        services.AddScoped<AdminAuditService>();
        services.AddScoped<AdminContentService>();
        services.AddScoped<Content.ContentService>();
        services.AddScoped<TranslationReader>();
        services.AddScoped<TranslationWriter>();

        services.AddScoped<IDomainEventHandler<OrderPlacedEvent>, OrderNotificationHandler>();
        services.AddScoped<IDomainEventHandler<OrderPaidEvent>, OrderNotificationHandler>();
        services.AddScoped<IDomainEventHandler<OrderStatusChangedEvent>, OrderNotificationHandler>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }
}
