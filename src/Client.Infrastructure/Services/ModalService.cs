using Microsoft.AspNetCore.Components;

namespace Client.Infrastructure.Services;

/// <summary>Modal + import-modal state — port of modules/modal.js.</summary>
public class ModalService
{
    public string? Title { get; private set; }
    public RenderFragment? Body { get; private set; }
    public bool IsOpen => Body != null;

    public bool ImportVisible { get; private set; }

    public event Action? OnChange;
    public event Action? OnImported;
    public void RaiseImported() => OnImported?.Invoke();

    public void Show(string title, RenderFragment body)
    {
        Title = title;
        Body = body;
        OnChange?.Invoke();
    }

    public void Close()
    {
        Body = null;
        Title = null;
        OnChange?.Invoke();
    }

    public void ShowImport()
    {
        ImportVisible = true;
        OnChange?.Invoke();
    }

    public void CloseImport()
    {
        ImportVisible = false;
        OnChange?.Invoke();
    }
}
