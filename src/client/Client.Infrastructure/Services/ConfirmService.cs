namespace Client.Infrastructure.Services;

/// <summary>
/// Drives the application's confirmation dialog. Awaitable: callers do
/// <c>if (await Confirm.AskAsync(...)) { ... }</c> instead of the browser's
/// blocking <c>window.confirm</c>, so confirmations get the app's own UI/UX.
/// A single dialog is shown at a time; a new request supersedes any pending one.
/// </summary>
public sealed class ConfirmService
{
    private TaskCompletionSource<bool>? _pending;

    /// <summary>The request currently being shown, or null when the dialog is closed.</summary>
    public ConfirmRequest? Current { get; private set; }

    /// <summary>Raised whenever the dialog opens or closes so the host can re-render.</summary>
    public event Action? OnChange;

    /// <summary>Shows the dialog and completes when the user confirms (true) or cancels (false).</summary>
    public Task<bool> AskAsync(ConfirmRequest request)
    {
        // Resolve any previously-pending request as cancelled before replacing it.
        _pending?.TrySetResult(false);

        Current = request;
        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OnChange?.Invoke();
        return _pending.Task;
    }

    /// <summary>Convenience overload for the common "danger delete" case.</summary>
    public Task<bool> AskAsync(string title, string message, string confirmText = "Confirm", bool danger = false, string? icon = null) =>
        AskAsync(new ConfirmRequest { Title = title, Message = message, ConfirmText = confirmText, Danger = danger, Icon = icon });

    /// <summary>Closes the dialog and resolves the awaiting caller with the user's choice.</summary>
    public void Resolve(bool result)
    {
        if (_pending is null) return;
        Current = null;
        OnChange?.Invoke();
        var pending = _pending;
        _pending = null;
        pending.TrySetResult(result);
    }
}
