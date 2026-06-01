namespace Client.Infrastructure.Services;

/// <summary>Toast notifications — port of utils.js toast(). Raises <see cref="OnToast"/> for the host component to render.</summary>
public class ToastService
{
    public event Action<ToastMessage>? OnToast;
    public void Show(string message, string type = "info") => OnToast?.Invoke(new ToastMessage(message, type));
}
