using Client.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Tests;

/// <summary>Import shows an inward arrow (into the tray); Export an outward one.</summary>
public class IconDirectionTests : TestContext
{
    private const string Inward = "M7 10l5 5 5-5";   // the "download" glyph: arrow pointing down into the tray
    private const string Outward = "M17 8l-5-5-5 5"; // the "upload" glyph: arrow pointing up out of the tray

    [Fact]
    public void The_import_all_button_uses_the_inward_arrow()
    {
        Services.AddSingleton(new ApiClient(new HttpClient { BaseAddress = new Uri("http://localhost/") }));
        Services.AddSingleton<ModalService>();
        Services.AddSingleton<AppState>();
        Services.AddSingleton<ToastService>();
        var cut = RenderComponent<ImportModal>();
        cut.InvokeAsync(() => Services.GetRequiredService<ModalService>().ShowImport());

        var button = cut.FindAll("button").Single(b => b.TextContent.Contains("Import all"));
        button.InnerHtml.Should().Contain(Inward).And.NotContain(Outward);
    }

    [Theory]
    [InlineData("download", Inward)]
    [InlineData("upload", Outward)]
    public void Glyphs_point_the_documented_way(string name, string path)
    {
        RenderComponent<Icon>(p => p.Add(x => x.Name, name)).Markup.Should().Contain(path);
    }
}
