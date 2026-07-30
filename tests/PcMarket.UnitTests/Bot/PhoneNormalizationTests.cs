using PcMarket.Bot.Handlers;

namespace PcMarket.UnitTests.Bot;

/// <summary>Telegram hands over a shared contact as bare digits while people type all sorts of spacing and
/// punctuation; both must land on the same phone the account was registered with.</summary>
public class PhoneNormalizationTests
{
    [Theory]
    [InlineData("+998901234567")]
    [InlineData("998901234567")]
    [InlineData("+998 90 123 45 67")]
    [InlineData("(998) 90-123-45-67")]
    public void NormalizePhone_ProducesTheCanonicalForm(string input)
    {
        Assert.Equal("+998901234567", AccountFlow.NormalizePhone(input));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("12345")]
    [InlineData("9989012345671234567")]
    public void NormalizePhone_RejectsWhatIsNotAPhoneNumber(string input)
    {
        Assert.Null(AccountFlow.NormalizePhone(input));
    }
}
