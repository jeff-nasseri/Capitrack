using System.Net;
using System.Text;
using Client.Domain;
using Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Client.Tests;

public class CheckImportModalTests : TestContext
{
    private sealed class FakeFile(string name, string content) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => Encoding.UTF8.GetByteCount(content);
        public string ContentType => "text/csv";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private sealed class FakeApi : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.AbsolutePath, body));
            var json = request.RequestUri.AbsolutePath.EndsWith("/import/selected") ? ResultJson : PreviewJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private const string PreviewJson = """
    {"files":[{"file_name":"wallet.csv","format":"trezor","rows":[
      {"row":1,"status":"new","reason":null,"date":"2026-04-26","occurred_at":"2026-04-26T16:22:41Z","can_stake":false,
       "legs":[{"symbol":"SOL-USD","type":"transfer_in","quantity":0.000000001,"price":0,"fee":0,"currency":"USD","status":"new"}]},
      {"row":2,"status":"duplicate","reason":"Already imported","date":"2026-04-28","occurred_at":null,"can_stake":false,
       "legs":[{"symbol":"SOL-USD","type":"transfer_in","quantity":1.5,"price":80,"fee":0,"currency":"USD","status":"duplicate"}]},
      {"row":3,"status":"rejected","reason":"Amount \"ID 0\" is not a number — an NFT/token id","date":null,"occurred_at":null,"can_stake":false,"legs":[]},
      {"row":4,"status":"new","reason":null,"date":"2026-04-29","occurred_at":"2026-04-29T09:00:00Z","can_stake":true,
       "legs":[{"symbol":"BTC-USD","type":"transfer_out","quantity":0.0001,"price":91250,"fee":0,"currency":"USD","status":"new"},
               {"symbol":"BTC-USD","type":"fee","quantity":0.00000517,"price":91250,"fee":0,"currency":"USD","status":"new"}]}],
     "summary":{"rows_read":4,"new":2,"duplicates":1,"updates":0,"rejected":1,"unselected":0,
       "counts_by_type":{"fee":1,"transfer_in":2,"transfer_out":1},
       "assets":[{"symbol":"BTC-USD","received":0,"sent":0.0001,"fees":0.00000517,"current_balance":0.5,"resulting_balance":0.49989483}]}}]}
    """;

    private const string ResultJson = """{"imported":3,"skipped":0,"total":4,"errors":[],"format":"trezor","rejected":1,"rejections":["Row 3: NFT"],"updated":0}""";

    private (IRenderedComponent<CheckImportModal> Cut, FakeApi Api, ModalService Modal) Open()
    {
        var api = new FakeApi();
        Services.AddSingleton(new ApiClient(new HttpClient(api) { BaseAddress = new Uri("http://localhost/") }));
        Services.AddSingleton<ModalService>();
        Services.AddSingleton(new AppState { CurrentAccountId = 7 });
        Services.AddSingleton<ToastService>();
        var cut = RenderComponent<CheckImportModal>();
        var modal = Services.GetRequiredService<ModalService>();
        cut.InvokeAsync(() => modal.ShowCheckImport([new FakeFile("wallet.csv", "Timestamp,Type\n1,RECV\n")]));
        cut.WaitForState(() => cut.Markup.Contains("rows read"), TimeSpan.FromSeconds(5));
        return (cut, api, modal);
    }

    [Fact]
    public void Preview_shows_the_reconciliation_markers_reasons_and_exact_amounts()
    {
        var (cut, _, _) = Open();

        cut.Find(".ci-recon").TextContent.Should().Contain("4").And.Contain("2 new").And.Contain("1 duplicate").And.Contain("1 rejected");
        cut.FindAll("tr.ci-row").Select(r => r.ClassList.Last()).Should().Equal("new", "duplicate", "rejected", "new");
        cut.Find("tr.ci-row.rejected .ci-reason").TextContent.Should().Contain("Row 3").And.Contain("NFT");

        cut.Markup.Should().Contain("0.000000001");            // dust is shown exactly, never as 0.0000
        cut.Markup.Should().Contain("0.00000517 BTC");          // the network fee has its own column, in its unit
        cut.Find("tr.ci-row.duplicate").QuerySelector("input[type=checkbox]").Should().BeNull(); // already imported: not selectable
        cut.FindAll("label.ci-check span").Select(s => s.TextContent).Should().Contain("stake");
        cut.Markup.Should().Contain("0.49989483");               // resulting balance from the server's plan
        cut.Find(".ci-footer .btn-pri").TextContent.Should().Contain("Import 2 rows");
    }

    [Fact]
    public void Import_resends_the_files_with_exactly_the_previewed_selection()
    {
        var (cut, api, _) = Open();

        cut.Find(".ci-footer .btn-pri").Click();
        cut.WaitForState(() => cut.Markup.Contains("Import complete"), TimeSpan.FromSeconds(5));

        var import = api.Requests.Single(r => r.Path.EndsWith("/import/selected"));
        import.Body.Should().Contain("filename=wallet.csv").And.Contain("Timestamp,Type");
        import.Body.Should().Contain("\"rows\":[1,4]").And.Contain("\"staked\":[]");
        cut.Find(".ci-done").TextContent.Should().Contain("3 transactions added").And.Contain("1 row not importable");
    }

    [Theory]
    [InlineData("0.000000001", "0.000000001")]
    [InlineData("1.027316849205573418", "1.027316849205573418")]
    [InlineData("1234.5", "1,234.5")]
    [InlineData("12", "12")]
    public void Quantities_are_formatted_exactly(string value, string expected)
    {
        Format.Shares(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);
    }
}
