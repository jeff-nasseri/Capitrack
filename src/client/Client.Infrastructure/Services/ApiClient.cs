using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Client.Application;

namespace Client.Infrastructure.Services;

/// <summary>
/// Typed HTTP wrapper around the Capitrack API. Mirrors modules/api.js:
/// snake_case JSON, and a 401 (outside /auth) raises Unauthorized.
/// </summary>
public class ApiClient(HttpClient http) : IApiClient
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    // Default options (no policy) — for the one camelCase endpoint (password change).
    public static readonly JsonSerializerOptions RawJson = new();

    public event Action? Unauthorized;

    private bool Check(HttpResponseMessage r, string url)
    {
        if (r.StatusCode == HttpStatusCode.Unauthorized && !url.Contains("/auth/"))
        {
            Unauthorized?.Invoke();
            return false;
        }
        return r.IsSuccessStatusCode;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        var r = await http.GetAsync(url);
        if (!Check(r, url)) return default;
        return await r.Content.ReadFromJsonAsync<T>(Json);
    }

    /// <summary>
    /// GETs a payload together with the total row count advertised by the
    /// <c>X-Total-Count</c> response header (used for server-side pagination).
    /// Returns a zero count when the header is absent.
    /// </summary>
    public async Task<(T? value, int total)> GetWithCountAsync<T>(string url)
    {
        var r = await http.GetAsync(url);
        if (!Check(r, url)) return (default, 0);
        var value = await r.Content.ReadFromJsonAsync<T>(Json);

        var total = 0;
        if ((r.Headers.TryGetValues("X-Total-Count", out var values) ||
             r.Content.Headers.TryGetValues("X-Total-Count", out values)) &&
            int.TryParse(values.FirstOrDefault(), out var parsed))
            total = parsed;

        return (value, total);
    }

    public async Task<T?> PostAsync<T>(string url, object body, JsonSerializerOptions? opts = null)
    {
        var r = await http.PostAsJsonAsync(url, body, opts ?? Json);
        if (!Check(r, url)) return default;
        return await r.Content.ReadFromJsonAsync<T>(Json);
    }

    public async Task<(bool ok, T? value)> PostWithStatusAsync<T>(string url, object body, JsonSerializerOptions? opts = null)
    {
        var r = await http.PostAsJsonAsync(url, body, opts ?? Json);
        if (r.StatusCode == HttpStatusCode.Unauthorized && !url.Contains("/auth/")) Unauthorized?.Invoke();
        var value = await r.Content.ReadFromJsonAsync<T>(Json);
        return (r.IsSuccessStatusCode, value);
    }

    /// <summary>
    /// Like <see cref="PostWithStatusAsync{T}"/> but also surfaces the numeric HTTP
    /// status code, so callers can distinguish e.g. 401 (bad credentials) from 429
    /// (rate-limited / locked out).
    /// </summary>
    public async Task<(bool ok, int status, T? value)> PostWithFullStatusAsync<T>(string url, object body, JsonSerializerOptions? opts = null)
    {
        var r = await http.PostAsJsonAsync(url, body, opts ?? Json);
        if (r.StatusCode == HttpStatusCode.Unauthorized && !url.Contains("/auth/")) Unauthorized?.Invoke();
        var value = await r.Content.ReadFromJsonAsync<T>(Json);
        return (r.IsSuccessStatusCode, (int)r.StatusCode, value);
    }

    public async Task<T?> PutAsync<T>(string url, object body, JsonSerializerOptions? opts = null)
    {
        var r = await http.PutAsJsonAsync(url, body, opts ?? Json);
        if (!Check(r, url)) return default;
        return await r.Content.ReadFromJsonAsync<T>(Json);
    }

    public async Task<(bool ok, T? value)> PutWithStatusAsync<T>(string url, object body, JsonSerializerOptions? opts = null)
    {
        var r = await http.PutAsJsonAsync(url, body, opts ?? Json);
        if (r.StatusCode == HttpStatusCode.Unauthorized && !url.Contains("/auth/")) Unauthorized?.Invoke();
        var value = await r.Content.ReadFromJsonAsync<T>(Json);
        return (r.IsSuccessStatusCode, value);
    }

    public async Task<T?> DeleteAsync<T>(string url)
    {
        var r = await http.DeleteAsync(url);
        if (!Check(r, url)) return default;
        return await r.Content.ReadFromJsonAsync<T>(Json);
    }

    public async Task<T?> PostFormAsync<T>(string url, MultipartFormDataContent content)
    {
        var r = await http.PostAsync(url, content);
        if (r.StatusCode == HttpStatusCode.Unauthorized) { Unauthorized?.Invoke(); return default; }
        return await r.Content.ReadFromJsonAsync<T>(Json);
    }

    /// <summary>Multipart POST that also reports whether the request succeeded (for upload flows that surface errors).</summary>
    public async Task<(bool ok, T? value)> PostFormWithStatusAsync<T>(string url, MultipartFormDataContent content)
    {
        var r = await http.PostAsync(url, content);
        if (r.StatusCode == HttpStatusCode.Unauthorized && !url.Contains("/auth/")) Unauthorized?.Invoke();
        var value = await r.Content.ReadFromJsonAsync<T>(Json);
        return (r.IsSuccessStatusCode, value);
    }
}
