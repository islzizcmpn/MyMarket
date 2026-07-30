using PcMarket.Domain.Common;

namespace PcMarket.Application.Abstractions.Events;

/// <summary>Handles a single kind of domain event. Multiple handlers may exist per event type.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Dispatches buffered domain events to their registered handlers after the aggregate is saved.
/// Delivery is best-effort: a failing handler is logged and never rolls back the committed business data.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
