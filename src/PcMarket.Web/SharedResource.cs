namespace PcMarket.Web;

/// <summary>Marker type that names the storefront's shared string table. <c>IStringLocalizer&lt;SharedResource&gt;</c>
/// resolves it against <c>Resources/SharedResource.{culture}.resx</c> (the neutral file holds the English text, so
/// <c>en</c> needs no file of its own). It lives in the project's root namespace on purpose: nesting it deeper would
/// make the localizer look under <c>Resources/&lt;namespace&gt;/</c> instead.</summary>
public sealed class SharedResource;
