namespace PcMarket.Web.Services;

/// <summary>
/// One news / promotion entry behind the Phase 17 "Stock" section. "Stock" here is the reference
/// site's news-and-promotions feed, not inventory.
/// </summary>
/// <param name="Slug">URL segment for <c>/stock/{slug}</c>. Lowercase, hyphenated, stable — it is
/// the article's permalink, so changing one breaks any link already shared.</param>
/// <param name="Title">Headline, shown on the card, the detail page and in the breadcrumb.</param>
/// <param name="Excerpt">Card summary. Kept to roughly two lines so the cards stay the same height.</param>
/// <param name="Body">Full copy, one entry per paragraph.</param>
/// <param name="PublishedOn">Publication date, rendered against the active UI culture.</param>
/// <param name="ImageFile">File name in <c>wwwroot/images/home</c>, resolved through
/// <see cref="HomeImages"/> so a missing file degrades to the gradient fallback rather than a
/// broken image.</param>
public sealed record StockArticle(
    string Slug,
    string Title,
    string Excerpt,
    IReadOnlyList<string> Body,
    DateOnly PublishedOn,
    string ImageFile);
