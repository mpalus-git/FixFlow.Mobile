using FixFlow.Mobile.Core.Features.Auth;

namespace FixFlow.Mobile.Features.Auth;

public sealed class SecureStorageStore(ISecureStorage secureStorage) : ISecureStore
{
    public Task<string?> GetAsync(string key) => secureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) => secureStorage.SetAsync(key, value);

    public void Remove(string key) => secureStorage.Remove(key);
}
