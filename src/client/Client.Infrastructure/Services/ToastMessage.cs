namespace Client.Infrastructure.Services;

/// <summary>A single toast notification: the message text and its type ("info"/"success"/"error").</summary>
public record ToastMessage(string Message, string Type);
