namespace PcMarket.Application.Abstractions.Storage;

/// <summary>Object storage for media (product images, banners, invoices), backed by MinIO/S3.</summary>
public interface IMediaStorage
{
    /// <summary>Uploads an object and returns its storage key.</summary>
    Task<string> UploadAsync(
        string objectName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> GetAsync(string objectName, CancellationToken cancellationToken = default);

    /// <summary>Returns a time-limited pre-signed URL for direct client download.</summary>
    Task<string> GetPresignedUrlAsync(string objectName, TimeSpan expiry, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectName, CancellationToken cancellationToken = default);
}
