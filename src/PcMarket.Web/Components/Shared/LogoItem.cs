namespace PcMarket.Web.Components.Shared;

/// <summary>One tile in a <c>LogoStrip</c>. <paramref name="LogoUrl"/> is optional — tiles without an
/// image fall back to a monogram, so the strip still reads correctly before artwork exists.</summary>
public sealed record LogoItem(string Label, string? LogoUrl = null, string? Href = null);
