using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;

namespace FixFlow.Mobile.UnitTests.Features.Sync;

public sealed class SyncStateRepositoryTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly TestUserDatabase _userDatabase = new();
    private readonly SyncStateRepository _repository;

    public SyncStateRepositoryTests()
    {
        _repository = new SyncStateRepository(_userDatabase.Database, _timeProvider);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Return_Null_When_State_Was_Never_Saved()
    {
        (await _repository.GetAsync("work-orders")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Overwrite_Value_And_Time_When_State_Is_Saved_Again()
    {
        await _repository.SetAsync("work-orders", "\"etag-1\"");
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        await _repository.SetAsync("work-orders", "\"etag-2\"");

        (await _repository.GetAsync("work-orders")).ShouldBe(new SyncState("work-orders", "\"etag-2\"", _timeProvider.GetUtcNow()));
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
