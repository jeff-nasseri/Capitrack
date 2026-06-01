using System.Net.Http;
using System.Text.Json;

namespace Client.Application;

/// <summary>
/// Typed HTTP surface over the Capitrack API (implemented by Client.Infrastructure's
/// ApiClient). Mirrors modules/api.js: snake_case JSON, and a 401 outside /auth raises
/// <see cref="Unauthorized"/>.
/// </summary>
public interface IApiClient
{
    /// <summary>Raised when the API returns 401 for a non-/auth/ request.</summary>
    event Action? Unauthorized;

    Task<T?> GetAsync<T>(string url);

    Task<T?> PostAsync<T>(string url, object body, JsonSerializerOptions? opts = null);

    Task<(bool ok, T? value)> PostWithStatusAsync<T>(string url, object body, JsonSerializerOptions? opts = null);

    Task<T?> PutAsync<T>(string url, object body, JsonSerializerOptions? opts = null);

    Task<(bool ok, T? value)> PutWithStatusAsync<T>(string url, object body, JsonSerializerOptions? opts = null);

    Task<T?> DeleteAsync<T>(string url);

    Task<T?> PostFormAsync<T>(string url, MultipartFormDataContent content);
}
