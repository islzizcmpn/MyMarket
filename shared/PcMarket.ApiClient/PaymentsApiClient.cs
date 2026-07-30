using PcMarket.Contracts.Payments;

namespace PcMarket.ApiClient;

/// <summary>Typed access to payment initiation. Gateway callbacks are server-to-server and not exposed here.</summary>
public sealed class PaymentsApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<PaymentInitiationResponse> InitiateAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        PostAsync<PaymentInitiateRequest, PaymentInitiationResponse>(
            "payments/initiate", new PaymentInitiateRequest(orderId), cancellationToken);
}
