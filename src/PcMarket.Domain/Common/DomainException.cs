namespace PcMarket.Domain.Common;

/// <summary>Thrown when an operation would violate a domain invariant (e.g. an illegal order transition).</summary>
public sealed class DomainException(string message) : Exception(message);
