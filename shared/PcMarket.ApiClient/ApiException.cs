using System.Net;

namespace PcMarket.ApiClient;

/// <summary>Thrown when the API returns a non-success response. Carries the HTTP status and the server's
/// problem-detail message so callers (and UI) can surface a meaningful error.</summary>
public sealed class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
