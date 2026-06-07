using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Client.Infrastructure.Services;

/// <summary>Modal + import-modal state — port of modules/modal.js.</summary>
public class ModalService
{
    public string? Title { get; private set; }
    public RenderFragment? Body { get; private set; }
    public bool IsOpen => Body != null;

    public bool ImportVisible { get; private set; }

    /// <summary>The files chosen for a "Check &amp; Import" preview, or null when the check modal is closed.</summary>
    public IReadOnlyList<IBrowserFile>? CheckImportFiles { get; private set; }
    public bool CheckImportVisible => CheckImportFiles != null;

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

    /// <summary>Opens the "Check &amp; Import" modal to preview the given file(s) before importing.</summary>
    public void ShowCheckImport(IReadOnlyList<IBrowserFile> files)
    {
        CheckImportFiles = files;
        OnChange?.Invoke();
    }

    public void CloseCheckImport()
    {
        CheckImportFiles = null;
        OnChange?.Invoke();
    }
}
