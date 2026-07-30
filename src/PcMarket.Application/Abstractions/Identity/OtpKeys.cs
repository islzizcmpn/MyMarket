namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Cache-key convention for phone OTP codes. Shared so tests can read the pending code.</summary>
public static class OtpKeys
{
    public static string For(string phone) => $"otp:{phone}";

    /// <summary>Wrong-guess counter for <see cref="For"/>. Kept in a separate entry so the code itself stays a
    /// plain string — several call sites read it directly.</summary>
    public static string AttemptsFor(string phone) => $"otp:{phone}:attempts";
}
