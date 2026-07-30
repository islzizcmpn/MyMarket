using PcMarket.Application.Abstractions.Storage;
using PcMarket.Contracts.Admin;

namespace PcMarket.Api.Endpoints;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        // Admin upload → MinIO; returns an absolute URL served by the GET below.
        app.MapPost("/api/v1/admin/media", async (
                IFormFile file,
                IMediaStorage storage,
                HttpRequest request,
                CancellationToken ct) =>
            {
                var extension = Path.GetExtension(file.FileName);
                var key = $"products/{Guid.CreateVersion7():N}{extension}";

                await using var stream = file.OpenReadStream();
                await storage.UploadAsync(key, stream, file.ContentType, ct);

                var url = $"{request.Scheme}://{request.Host}/api/v1/media/{key}";
                return Results.Ok(new MediaUploadResponse(url));
            })
            .RequireAuthorization(AdminEndpoints.Policy)
            .DisableAntiforgery()
            .WithTags("Admin");

        // Public read-through from MinIO so stored image URLs stay stable.
        app.MapGet("/api/v1/media/{**key}", async (string key, IMediaStorage storage, CancellationToken ct) =>
        {
            try
            {
                var stream = await storage.GetAsync(key, ct);
                return Results.File(stream, ContentType(key));
            }
            catch
            {
                return Results.NotFound();
            }
        }).WithTags("Media");
    }

    private static string ContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "image/jpeg"
    };
}
