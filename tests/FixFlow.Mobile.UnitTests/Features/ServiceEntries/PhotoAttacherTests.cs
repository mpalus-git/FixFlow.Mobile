using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.UnitTests.Storage;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.ServiceEntries;

public sealed class PhotoAttacherTests : IAsyncLifetime
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 7, 7];

    private readonly TestUserDatabase _userDatabase = new();
    private readonly IPhotoService _photoService = Substitute.For<IPhotoService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly PhotoStore _store;
    private readonly PhotoAttacher _attacher;

    public PhotoAttacherTests()
    {
        _store = new PhotoStore(_userDatabase.Options, _userDatabase.Database);
        _attacher = new PhotoAttacher(_photoService, _store, new ApiAddress(new Uri("https://api.test")), _dialogs);
    }

    public async ValueTask InitializeAsync() => await _userDatabase.Database.OpenAsync(Guid.NewGuid());

    [Fact]
    public async Task Should_Store_Captured_Photo_Under_Its_Future_Address_When_Camera_Is_Allowed()
    {
        _photoService.HasCameraPermissionAsync().Returns(true);
        _photoService.CaptureAsync(Arg.Any<CancellationToken>()).Returns(Jpeg);

        var photo = (await _attacher.CaptureAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        photo.Url.ShouldBe($"https://api.test/api/v1/photos/{photo.Id}");
        (await _store.ReadAsync(photo.Id)).ShouldBe(Jpeg);
        await _dialogs.DidNotReceiveWithAnyArgs().ShowAlertAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Should_Explain_And_Offer_Settings_When_Camera_Permission_Is_Denied()
    {
        _dialogs.ConfirmAsync(AppResources.CameraExplanationTitle, AppResources.CameraDeniedMessage, AppResources.OpenSettingsButton, AppResources.CancelButton)
            .Returns(true);

        var photo = await _attacher.CaptureAsync(TestContext.Current.CancellationToken);

        photo.ShouldBeNull();
        await _dialogs.Received(1).ShowAlertAsync(AppResources.CameraExplanationTitle, AppResources.CameraExplanationMessage, AppResources.ContinueButton);
        await _photoService.Received(1).OpenSettingsAsync();
        await _photoService.DidNotReceiveWithAnyArgs().CaptureAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_Return_Nothing_When_Technician_Cancels_Picker()
    {
        _photoService.PickAsync(Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        (await _attacher.PickAsync(TestContext.Current.CancellationToken)).ShouldBeNull();

        await _dialogs.DidNotReceiveWithAnyArgs().ShowAlertAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Should_Reject_Picked_File_When_It_Is_Not_Jpeg()
    {
        _photoService.PickAsync(Arg.Any<CancellationToken>()).Returns([0x89, 0x50, 0x4E, 0x47]);

        (await _attacher.PickAsync(TestContext.Current.CancellationToken)).ShouldBeNull();

        await _dialogs.Received(1).ShowAlertAsync(AppResources.ErrorTitle, AppResources.PhotoUnsupportedFormat, AppResources.CloseButton);
    }

    [Fact]
    public async Task Should_Reject_Photo_When_It_Exceeds_Upload_Limit()
    {
        var tooLarge = new byte[PhotoCompression.MaxBytes + 1];
        Jpeg.CopyTo(tooLarge, 0);
        _photoService.PickAsync(Arg.Any<CancellationToken>()).Returns(tooLarge);

        (await _attacher.PickAsync(TestContext.Current.CancellationToken)).ShouldBeNull();

        await _dialogs.Received(1).ShowAlertAsync(AppResources.ErrorTitle, AppResources.PhotoTooLarge, AppResources.CloseButton);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();
}
