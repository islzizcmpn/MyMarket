using PcMarket.Domain.Common;

namespace PcMarket.UnitTests.Domain;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("ASUS VivoBook 15", "asus-vivobook-15")]
    [InlineData("  Kingston  FURY   16GB ", "kingston-fury-16gb")]
    [InlineData("Logitech M330 (Silent!)", "logitech-m330-silent")]
    public void Generate_ProducesUrlSafeSlug(string input, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(input));
    }

    [Fact]
    public void Generate_BlankInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => SlugGenerator.Generate("   "));
    }
}
