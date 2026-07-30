using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PcMarket.ApiClient;

/// <summary>Shared request/response plumbing. Each request is built explicitly so the current auth and
/// guest-cart tokens (read per-call from <see cref="IApiTokenProvider"/>) attach correctly under Blazor
/// Server's circuit scope — pooled <c>HttpClientFactory</c> handlers cannot consume scoped state safely.</summary>
public abstract class ApiClientBase(HttpClient http, IApiTokenProvider tokens)
{
    protected async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    /// <summary>GET that returns <c>default</c> on 404 rather than throwing (for by-slug/by-id lookups).</summary>
    protected async Task<T?> GetOrDefaultAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, content: null, cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? default : await ReadAsync<T>(response, cancellationToken);
    }

    protected async Task<TResult> PostAsync<TBody, TResult>(string url, TBody body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, JsonContent.Create(body, options: ApiJson.Options), cancellationToken);
        return await ReadAsync<TResult>(response, cancellationToken);
    }

    protected async Task PostAsync<TBody>(string url, TBody body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, JsonContent.Create(body, options: ApiJson.Options), cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    protected async Task<TResult> PostAsync<TResult>(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, content: null, cancellationToken);
        return await ReadAsync<TResult>(response, cancellationToken);
    }

    protected async Task<TResult> PutAsync<TBody, TResult>(string url, TBody body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, url, JsonContent.Create(body, options: ApiJson.Options), cancellationToken);
        return await ReadAsync<TResult>(response, cancellationToken);
    }

    /// <summary>PUT that returns <c>default</c> on 404 (for updates to a possibly-missing resource).</summary>
    protected async Task<TResult?> PutOrDefaultAsync<TBody, TResult>(string url, TBody body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, url, JsonContent.Create(body, options: ApiJson.Options), cancellationToken);
        return response.StatusCode == HttpStatusCode.NotFound ? default : await ReadAsync<TResult>(response, cancellationToken);
    }

    protected async Task<TResult> PostContentAsync<TResult>(string url, HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, content, cancellationToken);
        return await ReadAsync<TResult>(response, cancellationToken);
    }

    protected async Task PostContentAsync(string url, HttpContent content, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, url, content, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    protected async Task<TResult> DeleteWithResultAsync<TResult>(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, url, content: null, cancellationToken);
        return await ReadAsync<TResult>(response, cancellationToken);
    }

    protected async Task<bool> DeleteAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, url, content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureAsync(response, cancellationToken);
        return true;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        var accessToken = await tokens.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var cartToken = await tokens.GetCartTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(cartToken))
        {
            request.Headers.Add("X-Cart-Token", cartToken);
        }

        // Tells the API which language to resolve database-backed content into. Invariant culture — hosts
        // that never set one, such as the bot — has an empty name; sending nothing lets the API apply its
        // own default rather than asking for a language that does not exist.
        var culture = CultureInfo.CurrentUICulture;
        if (!string.IsNullOrEmpty(culture.Name))
        {
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture.Name));
        }

        return await http.SendAsync(request, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken))!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new ApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetail>(ApiJson.Options, cancellationToken);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem!.Detail!;
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem!.Title!;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException)
        {
            // Fall through to the generic message below.
        }

        return $"Request failed with status {(int)response.StatusCode}.";
    }

    private sealed record ProblemDetail(string? Title, string? Detail);
}
