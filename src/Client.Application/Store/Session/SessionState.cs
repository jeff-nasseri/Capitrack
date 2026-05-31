using Client.Application.Models;
using Fluxor;

namespace Client.Application.Store.Session;

/// <summary>
/// The authenticated session. <see cref="User"/> is null when logged out;
/// <see cref="Initialized"/> flips true once the initial GET /api/auth/session resolves.
/// </summary>
[FeatureState]
public record SessionState
{
    public SessionDto? User { get; init; }
    public bool Initialized { get; init; }

    // Required by Fluxor to materialize the initial feature state.
    private SessionState() { }

    public SessionState(SessionDto? user, bool initialized)
    {
        User = user;
        Initialized = initialized;
    }
}
