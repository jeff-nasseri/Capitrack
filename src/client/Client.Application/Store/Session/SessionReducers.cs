using Fluxor;

namespace Client.Application.Store.Session;

/// <summary>Pure reducers for the session feature.</summary>
public static class SessionReducers
{
    [ReducerMethod(typeof(InitializeSessionAction))]
    public static SessionState OnInitialize(SessionState state) => state;

    [ReducerMethod]
    public static SessionState OnSetUser(SessionState state, SetUserAction action)
        => state with { User = action.User, Initialized = true };

    [ReducerMethod(typeof(LogoutAction))]
    public static SessionState OnLogout(SessionState state) => state;
}
