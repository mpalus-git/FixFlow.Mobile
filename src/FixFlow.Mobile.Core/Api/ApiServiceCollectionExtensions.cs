using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Refit;

namespace FixFlow.Mobile.Core.Api;

public static class ApiServiceCollectionExtensions
{
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(30);

    public static IHttpClientBuilder AddFixFlowApi(this IServiceCollection services, Uri baseAddress)
    {
        var builder = services
            .AddRefitGeneratedClient<IFixFlowApi>()
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress);

        builder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = AttemptTimeout;
            options.TotalRequestTimeout.Timeout = TotalRequestTimeout;
            options.Retry.DisableForUnsafeHttpMethods();
        });

        return builder;
    }
}
