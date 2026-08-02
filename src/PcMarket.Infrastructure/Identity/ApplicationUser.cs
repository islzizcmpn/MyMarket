using Microsoft.AspNetCore.Identity;

namespace PcMarket.Infrastructure.Identity;

/// <summary>The application's user, backed by ASP.NET Core Identity. Phone-first: <see cref="IdentityUser{TKey}.PhoneNumber"/>
/// is the primary credential, with email optional. Roles are managed through Identity role tables.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }

    /// <summary>Telegram user id once the account is linked to the bot; null otherwise.</summary>
    public long? TelegramUserId { get; set; }

    /// <summary>Two-letter language the customer chose to be addressed in, or null while they have not chosen
    /// one — in which case each client falls back to its own default.</summary>
    public string? Language { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
