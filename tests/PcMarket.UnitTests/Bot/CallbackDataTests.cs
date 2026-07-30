using PcMarket.Bot.Presentation;

namespace PcMarket.UnitTests.Bot;

public class CallbackDataTests
{
    [Fact]
    public void Of_ThenParse_RoundTripsCommandAndArguments()
    {
        var orderId = Guid.NewGuid();
        var data = CallbackData.Parse(CallbackData.Of(BotCommands.AdminAdvance, orderId, 4));

        Assert.Equal(BotCommands.AdminAdvance, data.Command);
        Assert.Equal(orderId, data.GuidArg(0));
        Assert.Equal(4, data.IntArg(1, -1));
    }

    [Fact]
    public void Of_RejectsPayloadsOverTelegramsLimit()
    {
        // Telegram silently rejects buttons whose callback_data exceeds 64 bytes; fail at build time instead.
        Assert.Throws<ArgumentException>(() => CallbackData.Of("x", new string('a', CallbackData.MaxLength)));
    }

    [Fact]
    public void Of_StaysWithinTheLimitForEveryIdCarryingCommand()
    {
        var id = Guid.NewGuid();

        Assert.True(CallbackData.Of(BotCommands.Category, id, 99).Length <= CallbackData.MaxLength);
        Assert.True(CallbackData.Of(BotCommands.Product, id).Length <= CallbackData.MaxLength);
        Assert.True(CallbackData.Of(BotCommands.AddToCart, id).Length <= CallbackData.MaxLength);
        Assert.True(CallbackData.Of(BotCommands.AdminAdvance, id, 7).Length <= CallbackData.MaxLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_TreatsMissingDataAsNoop(string? raw)
    {
        Assert.Equal(BotCommands.Noop, CallbackData.Parse(raw).Command);
    }

    [Fact]
    public void Parse_ReturnsNullForMalformedArguments()
    {
        var data = CallbackData.Parse("p:not-a-guid");

        Assert.Null(data.GuidArg(0));
        Assert.Null(data.Arg(1));
        Assert.Equal(-1, data.IntArg(1, -1));
    }
}
