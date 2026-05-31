using Microsoft.JSInterop;

namespace Capitrack.Web.Services;

/// <summary>User settings persisted in localStorage — port of state.js settings.</summary>
public class SettingsStore(IJSRuntime js)
{
    private const string Key = "wealth-show-transaction-dots";
    public bool ShowTransactionDots { get; private set; } = true;
    public event Action? OnChange;

    public async Task InitAsync()
    {
        var v = await js.InvokeAsync<string?>("capitrack.getLocal", Key);
        ShowTransactionDots = v == null || v == "true";
    }

    public async Task SetShowTransactionDots(bool value)
    {
        ShowTransactionDots = value;
        await js.InvokeVoidAsync("capitrack.setLocal", Key, value ? "true" : "false");
        OnChange?.Invoke();
    }
}
