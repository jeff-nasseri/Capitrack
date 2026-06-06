namespace Client.Infrastructure.Services;

/// <summary>
/// Describes a single confirmation prompt rendered by the in-app
/// <c>ConfirmDialog</c>, replacing the browser's native <c>window.confirm</c>.
/// </summary>
public sealed class ConfirmRequest
{
    /// <summary>Short, bold dialog heading (e.g. "Delete account").</summary>
    public required string Title { get; init; }

    /// <summary>Explanatory body text. Newlines are honoured (rendered pre-line).</summary>
    public required string Message { get; init; }

    /// <summary>Label of the affirmative button.</summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>Label of the dismissive button.</summary>
    public string CancelText { get; init; } = "Cancel";

    /// <summary>When true the dialog uses the destructive (red) styling and icon.</summary>
    public bool Danger { get; init; }

    /// <summary>Optional icon name override; defaults to a danger/info icon based on <see cref="Danger"/>.</summary>
    public string? Icon { get; init; }
}
