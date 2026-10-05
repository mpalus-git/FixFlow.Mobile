using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Navigation;
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
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ISettingsStore>())
            .AddSingleton(Substitute.For<ISecureStore>())
            .AddSingleton(Substitute.For<IConnectivityService>())
            .AddSingleton(Substitute.For<INavigationService>())
            .AddSingleton(Substitute.For<IDialogService>())
            .AddSingleton(Substitute.For<IUriLauncher>())
            .AddCore();
        services.AddFixFlowApi(new Uri("https://api.test"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var viewModelTypes = typeof(CoreServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
            .ToList();

        viewModelTypes.ShouldNotBeEmpty();
        viewModelTypes.ShouldAllBe(type => provider.GetService(type) != null);
    }
}
