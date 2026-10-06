using FixFlow.Mobile.Core;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Clipboard;
using FixFlow.Mobile.Core.Connectivity;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Dispatching;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.Core.Location;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Notifications;
using FixFlow.Mobile.Core.Storage;
using FixFlow.Mobile.Core.Features.Auth;
using FixFlow.Mobile.Core.Features.Settings;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests;

public sealed class CoreServiceCollectionExtensionsTests
{
    [Fact]
    public async Task Should_Resolve_All_ViewModels_When_Core_Services_Are_Registered()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ISettingsStore>())
            .AddSingleton(Substitute.For<ISecureStore>())
            .AddSingleton(Substitute.For<IConnectivityService>())
            .AddSingleton(Substitute.For<INavigationService>())
            .AddSingleton(Substitute.For<IDialogService>())
            .AddSingleton(Substitute.For<IUriLauncher>())
            .AddSingleton(Substitute.For<IMapLauncher>())
            .AddSingleton(Substitute.For<IDocumentLauncher>())
            .AddSingleton(Substitute.For<ILocationService>())
            .AddSingleton(Substitute.For<IPhotoService>())
            .AddSingleton(Substitute.For<IUiDispatcher>())
            .AddSingleton(Substitute.For<IClipboardService>())
            .AddSingleton(Substitute.For<INotificationScheduler>())
            .AddSingleton(new DemoAccount(string.Empty, string.Empty))
            .AddSingleton(new LocalDatabaseOptions(Path.GetTempPath()))
            .AddCore();
        services.AddFixFlowApi(new Uri("https://api.test"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var viewModelTypes = typeof(CoreServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
            .ToList();

        viewModelTypes.ShouldNotBeEmpty();
        viewModelTypes.ShouldAllBe(type => provider.GetService(type) != null);
    }
}
