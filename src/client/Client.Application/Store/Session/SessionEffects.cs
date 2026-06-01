using Client.Application.Models;
using Fluxor;

namespace Client.Application.Store.Session;

/// <summary>
/// Side effects for the session feature: talks to the API and dispatches the
/// resulting <see cref="SetUserAction"/>. Mirrors the original App.razor / Rail
/// logic exactly (same endpoints).
/// </summary>
public sealed class SessionEffects(IApiClient api)
{
    [EffectMethod(typeof(InitializeSessionAction))]
    public async Task OnInitialize(IDispatcher dispatcher)
    {
        var user = await api.GetAsync<SessionDto>("/api/auth/session");
        dispatcher.Dispatch(new SetUserAction(user));
    }

    [EffectMethod(typeof(LogoutAction))]
    public async Task OnLogout(IDispatcher dispatcher)
    {
        await api.PostAsync<object>("/api/auth/logout", new { });
        dispatcher.Dispatch(new SetUserAction(null));
    }
}
