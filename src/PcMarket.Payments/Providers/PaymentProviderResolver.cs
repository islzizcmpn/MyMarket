using PcMarket.Application.Abstractions.Payments;
using PcMarket.Domain.Common;
using PcMarket.Domain.Enums;

namespace PcMarket.Payments.Providers;

/// <summary>Selects the enabled <see cref="IPaymentProvider"/> for a chosen method from the set registered
/// in DI, rejecting methods that are unknown or switched off.</summary>
public sealed class PaymentProviderResolver(IEnumerable<IPaymentProvider> providers) : IPaymentProviderResolver
{
    public IPaymentProvider Resolve(PaymentMethod method)
    {
        var provider = providers.FirstOrDefault(p => p.Method == method)
                       ?? throw new DomainException($"Payment method {method} is not supported.");

        if (!provider.IsEnabled)
        {
            throw new DomainException($"Payment method {method} is currently unavailable.");
        }

        return provider;
    }
}
