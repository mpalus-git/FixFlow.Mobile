using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class TechnicianDevice : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly string _dataDirectory;

    private TechnicianDevice(ServiceProvider services, string dataDirectory)
    {
        _services = services;
        _dataDirectory = dataDirectory;
    }

    public static async Task<TechnicianDevice> LogInAsync(Uri baseAddress, string email, string password)
    {
        var dataDirectory = Directory.CreateTempSubdirectory("fixflow-it-").FullName;
        var services = new ServiceCollection()
            .AddSingleton<ISecureStore, MemorySecureStore>()
            .AddSingleton<IConnectivityService, OnlineConnectivity>()
            .AddSingleton(new LocalDatabaseOptions(dataDirectory))
            .AddCore();
        services.AddFixFlowApi(baseAddress);
        var device = new TechnicianDevice(services.BuildServiceProvider(), dataDirectory);
        var result = await device.Get<AuthService>().LoginAsync(email, password, CancellationToken.None);
        result.Outcome.ShouldBe(LoginOutcome.Succeeded);
        return device;
    }

    public T Get<T>()
        where T : notnull => _services.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        Directory.Delete(_dataDirectory, recursive: true);
    }

    private sealed class MemorySecureStore : ISecureStore
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public void Remove(string key) => _values.Remove(key);
    }

    private sealed class OnlineConnectivity : IConnectivityService
    {
        public bool IsOnline => true;
    }
}
