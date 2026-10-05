using FixFlow.Mobile.Core.Features.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Refit;

namespace FixFlow.Mobile.Core.Api;

public static class ApiServiceCollectionExtensions
{
    public const string AnonymousClientKey = "anonymous";

    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(30);

    public static IHttpClientBuilder AddFixFlowApi(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<ServerReadinessProbe>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services
            .AddHttpClient(AnonymousClientKey, client => client.BaseAddress = baseAddress)
            .AddStandardResilienceHandler(ConfigureResilience);
        services.AddKeyedTransient(AnonymousClientKey, (provider, _) =>
            RestService.ForGenerated<IFixFlowApi>(provider.GetRequiredService<IHttpClientFactory>().CreateClient(AnonymousClientKey)));

        services.AddSingleton<TokenRefresher>();
        services.AddTransient<AuthenticationHandler>();

        var builder = services
            .AddRefitGeneratedClient<IFixFlowApi>()
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress);
        builder.AddStandardResilienceHandler(ConfigureResilience);
        builder.AddHttpMessageHandler<AuthenticationHandler>();

        return builder;
    }

    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = AttemptTimeout;
        options.TotalRequestTimeout.Timeout = TotalRequestTimeout;
        options.Retry.DisableForUnsafeHttpMethods();
    }
}
