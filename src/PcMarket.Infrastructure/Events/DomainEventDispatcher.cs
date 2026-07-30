using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Events;
using PcMarket.Domain.Common;

namespace PcMarket.Infrastructure.Events;

/// <summary>Resolves and invokes the registered <see cref="IDomainEventHandler{TEvent}"/>s for each event at
/// runtime. Handlers are isolated: a throwing handler is logged and does not abort the others or the caller.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

            IEnumerable<object?> handlers;
            try
            {
                handlers = services.GetServices(handlerType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not resolve handlers for domain event {Event}.", domainEvent.GetType().Name);
                continue;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Handler {Handler} failed for domain event {Event}.",
                        handler?.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
