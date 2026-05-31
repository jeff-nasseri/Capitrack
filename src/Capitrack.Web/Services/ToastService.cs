namespace Capitrack.Web.Services;

public record ToastMessage(string Message, string Type);

/// <summary>Toast notifications — port of utils.js toast().</summary>
public class ToastService
{
    public event Action<ToastMessage>? OnToast;
    public void Show(string message, string type = "info") => OnToast?.Invoke(new ToastMessage(message, type));
}
