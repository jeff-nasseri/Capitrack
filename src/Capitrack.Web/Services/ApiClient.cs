using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Capitrack.Web.Services;

/// <summary>
/// Typed HTTP wrapper around the Capitrack API. Mirrors modules/api.js:
/// snake_case JSON, and a 401 (outside /auth) raises Unauthorized.
/// </summary>
public class ApiClient(HttpClient http)
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
}
