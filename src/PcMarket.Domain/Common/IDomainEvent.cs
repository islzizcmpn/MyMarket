namespace PcMarket.Domain.Common;

/// <summary>Marker for an in-process domain event raised by an aggregate.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOn => DateTimeOffset.UtcNow;
}
