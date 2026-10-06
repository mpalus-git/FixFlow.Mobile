using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class ApiEnvironment : IAsyncLifetime
{
    public const string ApiImage = "ghcr.io/mpalus-git/fixflow.api:sha-388294ddf5111adfaff6f5bb0a9217c9f3aa9e69";
    public const string AdminEmail = "admin@fixflow.local";

    private const string DatabaseAlias = "postgres";
    private const int ApiPort = 8080;

    private readonly string _databasePassword = GeneratePassword();
    private readonly string _signingKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    private INetwork? _network;
    private PostgreSqlContainer? _database;
    private IContainer? _api;

    public string AdminPassword { get; } = GeneratePassword();

    public Uri BaseAddress { get; private set; } = new("http://localhost");

    public static string GeneratePassword() => $"Fx-{Guid.NewGuid():N}-a1";

    public async ValueTask InitializeAsync()
    {
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync();
        _database = new PostgreSqlBuilder("postgres:18")
            .WithNetwork(_network)
            .WithNetworkAliases(DatabaseAlias)
            .WithDatabase("fixflow")
            .WithUsername("fixflow")
            .WithPassword(_databasePassword)
            .Build();
        await _database.StartAsync();
        _api = CreateApiContainer();
        await _api.StartAsync();
        BaseAddress = AddressOf(_api);
    }

    public IContainer CreateApiContainer() => new ContainerBuilder(ApiImage)
        .WithNetwork(_network)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
        .WithEnvironment("ConnectionStrings__Database", $"Host={DatabaseAlias};Port=5432;Database=fixflow;Username=fixflow;Password={_databasePassword}")
        .WithEnvironment("Jwt__SigningKey", _signingKey)
        .WithEnvironment("Seed__DemoUsers__Enabled", "true")
        .WithEnvironment("Seed__DemoUsers__AdminPassword", AdminPassword)
        .WithEnvironment("Seed__DemoUsers__DispatcherPassword", GeneratePassword())
        .WithEnvironment("Seed__DemoUsers__TechnicianPassword", GeneratePassword())
        .WithEnvironment("Seed__DemoData__Enabled", "false")
        .WithEnvironment("Email__Enabled", "false")
        .WithEnvironment("RateLimiting__Auth__PermitLimit", "1000")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(ApiPort).ForPath("/health/ready")))
        .Build();

    public static Uri AddressOf(IContainer api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return new Uri($"http://{api.Hostname}:{api.GetMappedPublicPort(ApiPort)}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DisposeAsync();
        }
    }
}
