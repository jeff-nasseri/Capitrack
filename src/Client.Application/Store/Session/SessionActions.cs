using Client.Application.Models;

namespace Client.Application.Store.Session;

/// <summary>Kicks off the initial session probe (GET /api/auth/session).</summary>
public sealed record InitializeSessionAction;

/// <summary>Sets (or clears) the current user and marks the session initialized.</summary>
public sealed record SetUserAction(SessionDto? User);

/// <summary>Logs the user out (POST /api/auth/logout, then clears the user).</summary>
public sealed record LogoutAction;
