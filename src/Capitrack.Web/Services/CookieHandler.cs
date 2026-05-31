using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Capitrack.Web.Services;

/// <summary>Sends the auth cookie with every request (same-origin via nginx).</summary>
public class CookieHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Headers.Remove("X-Requested-With");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return base.SendAsync(request, cancellationToken);
    }
}
