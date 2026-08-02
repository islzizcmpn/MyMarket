using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Messaging;
using PcMarket.Application.Carts;
using PcMarket.Bot.Configuration;
using PcMarket.Bot.Conversations;
using PcMarket.Bot.Localization;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Auth;
using Telegram.Bot.Types.ReplyMarkups;

namespace PcMarket.Bot.Handlers;

/// <summary>Account linking and the main menu.
///
/// There are two ways in, and they carry very different weight. A <b>shared contact card belonging to the
/// sender</b> is proof enough on its own — Telegram verified that number at signup and delivers it from its own
/// servers — so it links immediately, with no code and no SMS cost. A <b>typed</b> number is unverified input,
/// so it goes through an OTP: a code this bot issues for an existing account, or the one
/// <see cref="IAuthService.RegisterAsync"/> sends for an unknown phone.
///
/// Either way the Telegram id is bound to exactly one account and the guest cart is merged in.</summary>
public sealed class AccountFlow(
    BotSession session,
    ITelegramLinkStore links,
    IUserDirectory users,
    IAuthService auth,
    ISmsSender sms,
    CartService carts,
    IConversationStore conversations,
    BotLanguageService languages,
    BotResponder responder,
    IOptions<TelegramSettings> settings,
    ILogger<AccountFlow> logger)
{
    private readonly TelegramSettings _settings = settings.Value;

    public async Task ShowMenuAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        await responder.ReplyAsync(
            context,
            BotText.MainMenu(context.Culture, link?.Phone),
            BotKeyboards.MainMenu(context.Culture, link is not null),
            cancellationToken);
    }

    public async Task StartAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        await conversations.ClearAsync(context.TelegramUserId, cancellationToken);
        var link = await session.GetLinkAsync(context, cancellationToken);
        await responder.ReplyAsync(
            context,
            BotText.Welcome(context.Culture, context.FirstName),
            BotKeyboards.MainMenu(context.Culture, link is not null),
            cancellationToken);
    }

    public Task ShowHelpAsync(BotContext context, CancellationToken cancellationToken = default) =>
        responder.ReplyAsync(context, BotText.Help(context.Culture), keyboard: null, cancellationToken);

    public async Task BeginLinkAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var link = await session.GetLinkAsync(context, cancellationToken);
        if (link is not null)
        {
            await responder.ReplyAsync(
                context,
                BotPhrases.Format(context.Culture, Phrase.AlreadyLinked, BotText.Escape(link.Phone)),
                BotKeyboards.MainMenu(context.Culture, true),
                cancellationToken);
            return;
        }

        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        await conversations.SetAsync(context.TelegramUserId, state with { Stage = BotStage.AwaitingPhone }, cancellationToken);

        var prompt = BotPhrases.Get(
            context.Culture,
            _settings.AllowPhoneEntry ? Phrase.LinkPromptWithTyping : Phrase.LinkPromptShareOnly);

        await responder.SendAsync(context.ChatId, prompt, BotKeyboards.RequestContact(context.Culture), cancellationToken);
    }

    /// <param name="phoneVerified">True only when Telegram itself vouched for the number — a shared contact card
    /// belonging to the sender. Such a number is linked immediately: an OTP would ask the customer to prove
    /// something Telegram has already proven, at the cost of an SMS. Everything else takes the OTP path.</param>
    public async Task HandlePhoneAsync(
        BotContext context,
        string rawPhone,
        bool phoneVerified = false,
        CancellationToken cancellationToken = default)
    {
        var phone = NormalizePhone(rawPhone);
        if (phone is null)
        {
            await responder.SendAsync(
                context.ChatId,
                BotPhrases.Get(context.Culture, Phrase.InvalidPhone),
                new ReplyKeyboardRemove(),
                cancellationToken);
            return;
        }

        if (phoneVerified)
        {
            var verifiedUserId = await auth.RegisterVerifiedAsync(
                new RegisterRequest(phone, GeneratePassword(), context.FirstName),
                cancellationToken);

            await CompleteLinkAsync(context, verifiedUserId, phone, cancellationToken);
            return;
        }

        // Confirming a typed number costs an SMS, so it is opt-in. Note this check sits *after* the verified
        // branch: the contact-share route is unaffected and stays free.
        if (!_settings.AllowPhoneEntry)
        {
            await responder.SendAsync(
                context.ChatId,
                BotPhrases.Get(context.Culture, Phrase.UseShareButton),
                BotKeyboards.RequestContact(context.Culture),
                cancellationToken);
            return;
        }

        var existing = await FindUserAsync(phone, cancellationToken);
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);

        if (existing is not null)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await conversations.SetOtpAsync(context.TelegramUserId, code, cancellationToken);
            await sms.SendAsync(phone, BotPhrases.Format(context.Culture, Phrase.OtpSms, code), cancellationToken);
            await conversations.SetAsync(
                context.TelegramUserId,
                state with { Stage = BotStage.AwaitingOtp, Phone = phone, PendingRegistration = false },
                cancellationToken);
        }
        else
        {
            // No account yet: register one (which sends its own OTP) and verify through the auth service.
            await auth.RegisterAsync(new RegisterRequest(phone, GeneratePassword(), context.FirstName), cancellationToken);
            await conversations.SetAsync(
                context.TelegramUserId,
                state with { Stage = BotStage.AwaitingOtp, Phone = phone, PendingRegistration = true },
                cancellationToken);
        }

        await responder.SendAsync(
            context.ChatId,
            BotPhrases.Format(context.Culture, Phrase.OtpSent, BotText.Escape(phone)),
            new ReplyKeyboardRemove(),
            cancellationToken);
    }

    public async Task HandleOtpAsync(BotContext context, string rawCode, CancellationToken cancellationToken = default)
    {
        var state = await conversations.GetAsync(context.TelegramUserId, cancellationToken);
        if (state.Phone is null)
        {
            await BeginLinkAsync(context, cancellationToken);
            return;
        }

        var code = rawCode.Trim();
        Guid userId;

        if (state.PendingRegistration)
        {
            var outcome = await auth.VerifyOtpAsync(new VerifyOtpRequest(state.Phone, code), cancellationToken);
            if (!outcome.Succeeded || outcome.Response is null)
            {
                await responder.SendAsync(
                    context.ChatId,
                    BotPhrases.Format(context.Culture, Phrase.OtpRejected, BotText.Escape(outcome.Error)),
                    keyboard: null,
                    cancellationToken: cancellationToken);
                return;
            }

            userId = outcome.Response.UserId;
        }
        else
        {
            var expected = await conversations.GetOtpAsync(context.TelegramUserId, cancellationToken);
            if (expected is null || !CodesMatch(expected, code))
            {
                if (expected is not null)
                {
                    await conversations.CountFailedOtpAttemptAsync(context.TelegramUserId, cancellationToken);
                }

                await responder.SendAsync(context.ChatId, BotPhrases.Get(context.Culture, Phrase.OtpInvalid), keyboard: null, cancellationToken: cancellationToken);
                return;
            }

            var user = await FindUserAsync(state.Phone, cancellationToken);
            if (user is null)
            {
                await responder.SendAsync(context.ChatId, BotPhrases.Get(context.Culture, Phrase.AccountGone), keyboard: null, cancellationToken: cancellationToken);
                return;
            }

            userId = user.Id;
        }

        await CompleteLinkAsync(context, userId, state.Phone, cancellationToken);
    }

    /// <summary>Binds the account, clears the conversation, and carries the guest cart and language over. Shared
    /// by both routes in, so a contact share and a verified OTP land in exactly the same state.</summary>
    private async Task CompleteLinkAsync(BotContext context, Guid userId, string phone, CancellationToken cancellationToken)
    {
        await conversations.ClearOtpAsync(context.TelegramUserId, cancellationToken);
        await links.LinkAsync(userId, context.TelegramUserId, cancellationToken);
        await conversations.ClearAsync(context.TelegramUserId, cancellationToken);
        await languages.AdoptAsync(userId, context.Culture, cancellationToken);

        try
        {
            await carts.MergeAsync(userId, context.GuestCartToken, cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed merge must not block the link itself — the account cart is still usable.
            logger.LogWarning(ex, "Merging the Telegram guest cart into user {UserId} failed.", userId);
        }

        await responder.SendAsync(
            context.ChatId,
            BotPhrases.Format(context.Culture, Phrase.LinkedSuccess, BotText.Escape(phone)),
            BotKeyboards.MainMenu(context.Culture, true),
            cancellationToken);
    }

    public async Task UnlinkAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var removed = await links.UnlinkAsync(context.TelegramUserId, cancellationToken);
        await conversations.ClearAsync(context.TelegramUserId, cancellationToken);
        await responder.ReplyAsync(
            context,
            BotPhrases.Get(context.Culture, removed ? Phrase.Unlinked : Phrase.NotLinked),
            BotKeyboards.MainMenu(context.Culture, false),
            cancellationToken);
    }

    /// <summary>Normalizes a typed or shared phone to <c>+digits</c>; returns null when it is clearly not one.</summary>
    public static string? NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length is >= 9 and <= 15 ? "+" + digits : null;
    }

    private async Task<UserSummary?> FindUserAsync(string phone, CancellationToken cancellationToken) =>
        await users.FindByPhoneAsync(phone, cancellationToken)
        ?? await users.FindByPhoneAsync(phone.TrimStart('+'), cancellationToken);

    private static bool CodesMatch(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    /// <summary>Password for an account the bot creates during linking. Identity requires one, but the
    /// customer never learns it: their credential here is the phone + OTP. Signing the same account in on
    /// the web therefore needs a password-reset flow, which does not exist yet.</summary>
    private static string GeneratePassword() => $"Tg{RandomNumberGenerator.GetHexString(16)}!a1";
}
