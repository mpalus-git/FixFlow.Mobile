using FixFlow.Mobile.Core.Features.Auth;

namespace FixFlow.Mobile.UnitTests.Features.Auth;

internal sealed class InMemorySecureStore : ISecureStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => Values.Remove(key);
}
