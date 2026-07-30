using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PcMarket.Bot.Configuration;
using PcMarket.Bot.Handlers;
using PcMarket.Bot.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PcMarket.Api.Endpoints;

/// <summary>Telegram webhook plus the two operational endpoints used to point Telegram at it. The webhook
/// is anonymous by necessity — Telegram cannot present a JWT — and is instead authenticated by the secret
/// token Telegram echoes in <c>X-Telegram-Bot-Api-Secret-Token</c>, which only ever leaves this host when
/// the webhook is registered.</summary>
public static class BotEndpoints
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static void MapBotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/bot/telegram").WithTags("Bot");

        group.MapPost("/webhook", async (
            HttpRequest request,
            TelegramClientAccessor accessor,
            TelegramUpdateHandler handler,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var settings = accessor.Settings;
            if (!settings.Enabled)
            {
                return Results.NotFound();
            }

            if (!IsAuthentic(request, settings))
            {
                return Results.Unauthorized();
            }

            Update? update;
            try
            {
                update = await JsonSerializer.DeserializeAsync<Update>(request.Body, JsonBotAPI.Options, ct);
            }
            catch (JsonException ex)
            {
                loggerFactory.CreateLogger(typeof(BotEndpoints)).LogWarning(ex, "Malformed Telegram update payload.");
                return Results.BadRequest();
            }

            if (update is null)
            {
                return Results.BadRequest();
            }

            // Handling never throws (the handler reports failures into the chat), so Telegram always gets a
            // 200 and does not retry the same update.
            await handler.HandleAsync(update, ct);
            return Results.Ok();
        }).AllowAnonymous();

        group.MapPost("/set-webhook", async (TelegramClientAccessor accessor, CancellationToken ct) =>
        {
            var settings = accessor.Settings;
            if (!accessor.IsConfigured)
            {
                return Results.Problem("Telegram bot is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (string.IsNullOrWhiteSpace(settings.PublicApiUrl) || string.IsNullOrWhiteSpace(settings.WebhookSecretToken))
            {
                return Results.Problem(
                    "Telegram:PublicApiUrl and Telegram:WebhookSecretToken must both be set.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var url = $"{settings.PublicApiUrl.TrimEnd('/')}{TelegramSettings.WebhookPath}";
            await accessor.Client.SetWebhook(url, secretToken: settings.WebhookSecretToken, cancellationToken: ct);
            return Results.Ok(new { webhookUrl = url });
        }).RequireAuthorization(AdminEndpoints.Policy);

        group.MapGet("/webhook-info", async (TelegramClientAccessor accessor, CancellationToken ct) =>
        {
            if (!accessor.IsConfigured)
            {
                return Results.Problem("Telegram bot is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var info = await accessor.Client.GetWebhookInfo(ct);
            var me = await accessor.Client.GetMe(ct);
            return Results.Ok(new
            {
                bot = me.Username,
                info.Url,
                info.PendingUpdateCount,
                info.LastErrorMessage
            });
        }).RequireAuthorization(AdminEndpoints.Policy);
    }

    private static bool IsAuthentic(HttpRequest request, TelegramSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.WebhookSecretToken))
        {
            return false;
        }

        var presented = request.Headers[SecretHeader].ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(settings.WebhookSecretToken));
    }
}
