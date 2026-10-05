using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public void Should_Resolve_All_ViewModels_When_Core_Services_Are_Registered()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(Substitute.For<ISettingsStore>())
            .AddSingleton(Substitute.For<ISecureStore>())
            .AddCore()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var viewModelTypes = typeof(CoreServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
            .ToList();

        viewModelTypes.ShouldNotBeEmpty();
        viewModelTypes.ShouldAllBe(type => provider.GetService(type) != null);
    }
}
