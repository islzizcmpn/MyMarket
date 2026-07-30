using PcMarket.Application.Content;

namespace PcMarket.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/content").WithTags("Content");

        group.MapGet("/banners", (ContentService content, CancellationToken ct) => content.GetActiveBannersAsync(ct));

        group.MapGet("/blocks/{key}", async (string key, ContentService content, CancellationToken ct) =>
        {
            var block = await content.GetBlockAsync(key, ct);
            return block is null ? Results.NotFound() : Results.Ok(block);
        });
    }
}
