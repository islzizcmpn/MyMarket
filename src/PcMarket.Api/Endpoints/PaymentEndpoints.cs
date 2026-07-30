using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Payments;
using PcMarket.Contracts.Payments;
using PcMarket.Payments.Click;
using PcMarket.Payments.Payme;

namespace PcMarket.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments").WithTags("Payments");

        group.MapPost("/initiate", async (
                PaymentInitiateRequest request,
                PaymentService payments,
                ICurrentUser currentUser,
                CancellationToken ct) =>
                Results.Ok(await payments.InitiateAsync(currentUser.UserId!.Value, request.OrderId, ct)))
            .RequireAuthorization()
            .WithValidation<PaymentInitiateRequest>();

        // Gateway webhooks authenticate via each provider's own scheme (signature / Basic header), not JWT.
        group.MapPost("/click/callback", async (
            HttpRequest httpRequest,
            ClickCallbackService click,
            CancellationToken ct) =>
        {
            var form = await httpRequest.ReadFormAsync(ct);
            var request = new ClickCallbackRequest(
                Field(form, "click_trans_id"),
                Field(form, "service_id"),
                Field(form, "click_paydoc_id"),
                Field(form, "merchant_trans_id"),
                form.TryGetValue("merchant_prepare_id", out var prepareId) ? prepareId.ToString() : null,
                Field(form, "amount"),
                ParseInt(form, "action"),
                ParseInt(form, "error"),
                form.TryGetValue("error_note", out var errorNote) ? errorNote.ToString() : null,
                Field(form, "sign_time"),
                Field(form, "sign_string"));

            return Results.Json(await click.HandleAsync(request, ct));
        }).AllowAnonymous();

        group.MapPost("/payme/callback", async (
            [FromBody] JsonElement rpc,
            HttpRequest httpRequest,
            PaymeRpcService payme,
            CancellationToken ct) =>
        {
            var authorization = httpRequest.Headers.Authorization.ToString();
            return Results.Json(await payme.HandleAsync(rpc, authorization, ct));
        }).AllowAnonymous();
    }

    private static string Field(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;

    private static int ParseInt(IFormCollection form, string key) =>
        int.TryParse(Field(form, key), out var value) ? value : 0;
}
