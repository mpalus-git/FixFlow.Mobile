namespace FixFlow.Mobile.Core.Features.Auth;

public interface ISecureStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}
