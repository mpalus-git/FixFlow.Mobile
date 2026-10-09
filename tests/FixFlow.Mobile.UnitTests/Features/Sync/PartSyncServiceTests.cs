using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.Sync.Pull;
using FixFlow.Mobile.UnitTests.Api;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class PartSyncServiceTests : IAsyncLifetime
{
    private readonly TestUserDatabase _userDatabase = new();
    private readonly StubHttpMessageHandler _handler;
    private readonly PartRepository _parts;
    private readonly PartSyncService _service;
    private HttpStatusCode _status = HttpStatusCode.OK;
    private int _remoteCount = 3;

    public PartSyncServiceTests()
    {
        _handler = new StubHttpMessageHandler(Route);
        var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };
        _parts = new PartRepository(_userDatabase.Database);
        _service = new PartSyncService(
            RestService.ForGenerated<IFixFlowApi>(httpClient),
            _parts,
            new SyncStateRepository(_userDatabase.Database, new FakeTimeProvider()));
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Replace_Local_Catalog_With_All_Pages_When_Pull_Succeeds()
    {
        await _parts.ReplaceAllAsync([new Part(Guid.NewGuid(), "Stara część", "OLD-1", 1, 1m, null)]);
        _remoteCount = 130;

        var result = await _service.PullAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(PullResult.Succeeded);
        (await _parts.SearchAsync("czesc-", 500)).Count.ShouldBe(130);
        (await _parts.SearchAsync("Stara")).ShouldBeEmpty();
        _handler.Requests.Select(request => HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]).ShouldBe(["1", "2"]);
        (await _service.GetLastPulledAtAsync()).ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Keep_Local_Catalog_When_Pull_Fails()
    {
        await _parts.ReplaceAllAsync([new Part(Guid.NewGuid(), "Filtr", "FP-100", 4, 12.34m, null)]);
        _status = HttpStatusCode.ServiceUnavailable;

        var result = await _service.PullAsync(TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(PullOutcome.Failed);
        (await _parts.SearchAsync("filtr")).ShouldHaveSingleItem().StockQuantity.ShouldBe(4);
        (await _service.GetLastPulledAtAsync()).ShouldBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        _handler.Dispose();
        await _userDatabase.DisposeAsync();
    }

    private HttpResponseMessage Route(HttpRequestMessage request)
    {
        if (_status != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(_status);
        }

        var page = int.Parse(HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]!, CultureInfo.InvariantCulture);
        var items = Enumerable.Range((page - 1) * PartSyncService.PageSize, Math.Max(0, Math.Min(PartSyncService.PageSize, _remoteCount - ((page - 1) * PartSyncService.PageSize))))
            .Select(index => $$"""{"id":"{{Guid.NewGuid()}}","name":"Część {{index}}","catalogNumber":"CZESC-{{index}}","stockQuantity":5,"unitPrice":10.5,"createdAt":"2026-01-01T00:00:00Z","archivedAt":null}""");
        var json = $$"""{"items":[{{string.Join(",", items)}}],"page":{{page}},"pageSize":100,"totalCount":{{_remoteCount}}}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
