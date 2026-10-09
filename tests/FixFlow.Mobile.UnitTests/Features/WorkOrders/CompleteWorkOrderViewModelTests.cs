using CommunityToolkit.Mvvm.Messaging;
using FixFlow.Mobile.Core.Accessibility;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Dialogs;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Settings;
using FixFlow.Mobile.Core.Features.Sync.Outbox;
using FixFlow.Mobile.Core.Features.WorkOrders.Details;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Formatting;
using FixFlow.Mobile.Core.Navigation;
using FixFlow.Mobile.Core.Photos;
using FixFlow.Mobile.Core.Resources;
using FixFlow.Mobile.Core.Screens;
using FixFlow.Mobile.UnitTests.Storage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class CompleteWorkOrderViewModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly TestUserDatabase _userDatabase = new();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly WorkOrder _workOrder = TestWorkOrders.Create("ZL-1", Now.AddHours(4));
    private readonly OutboxRepository _outbox;
    private readonly OutboxWriter _writer;
    private readonly PhotoStore _photos;
    private readonly CompleteWorkOrderViewModel _viewModel;

    public CompleteWorkOrderViewModelTests()
    {
        var database = _userDatabase.Database;
        var formatter = new DisplayFormatter(new LanguageService(Substitute.For<ISettingsStore>()), BusinessClock.CreateForWarsaw(_timeProvider));
        _outbox = new OutboxRepository(database);
        _writer = new OutboxWriter(_outbox, _timeProvider, new WeakReferenceMessenger());
        _photos = new PhotoStore(_userDatabase.Options, database);
        _viewModel = new CompleteWorkOrderViewModel(
            new ScreenState(formatter, Substitute.For<IScreenReaderAnnouncer>()),
            new WorkOrderRepository(database),
            new ServiceEntryRepository(database),
            _outbox,
            _writer,
            _photos,
            _navigation,
            _dialogs,
            Substitute.For<IScreenReaderAnnouncer>());
    }

    public async ValueTask InitializeAsync()
    {
        await _userDatabase.Database.OpenAsync(Guid.NewGuid());
        await new WorkOrderRepository(_userDatabase.Database).ReplaceAllAsync([_workOrder]);
        await _writer.StartWorkAsync(_workOrder.Id);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Should_Queue_Completion_Without_Signature_When_Pad_Is_Empty()
    {
        await AddEntryAsync();
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        await _viewModel.CompleteCommand.ExecuteAsync(Signature(isEmpty: true));

        var completion = (await _outbox.GetAllAsync())[^1];
        completion.Kind.ShouldBe(OutboxOperationKind.CompleteWorkOrder);
        OutboxPayload.ClientSignaturePhotoId(completion).ShouldBeNull();
        await _navigation.Received(1).GoBackAsync();
    }

    [Fact]
    public async Task Should_Store_Signature_And_Queue_It_Before_Completion_When_Client_Signed()
    {
        await AddEntryAsync();
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        await _viewModel.CompleteCommand.ExecuteAsync(Signature(isEmpty: false, Jpeg));

        var queued = await _outbox.GetAllAsync();
        queued.TakeLast(2).Select(operation => operation.Kind).ShouldBe([OutboxOperationKind.UploadPhoto, OutboxOperationKind.CompleteWorkOrder]);
        var signatureId = OutboxPayload.ClientSignaturePhotoId(queued[^1]).ShouldNotBeNull();
        (await _photos.ReadAsync(signatureId)).ShouldBe(Jpeg);
        await _navigation.Received(1).GoBackAsync();
    }

    [Fact]
    public async Task Should_Stay_On_Screen_Without_Queuing_When_Signature_Is_Not_Jpeg()
    {
        await AddEntryAsync();
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        await _viewModel.CompleteCommand.ExecuteAsync(Signature(isEmpty: false, [0x89, 0x50, 0x4E, 0x47]));

        _viewModel.ValidationMessage.ShouldBe(AppResources.SignatureInvalid);
        (await _outbox.GetAllAsync()).ShouldNotContain(operation => operation.Kind == OutboxOperationKind.CompleteWorkOrder);
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public async Task Should_Ask_Before_Leaving_Only_When_Client_Signed(bool isEmpty, bool discard, bool expected)
    {
        await AddEntryAsync();
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Signature = Signature(isEmpty);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(discard);

        (await _viewModel.CanLeaveAsync()).ShouldBe(expected);

        await _dialogs.ReceivedWithAnyArgs(isEmpty ? 0 : 1).ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Leave_Without_Asking_When_Work_Order_Was_Completed_With_Signature()
    {
        await AddEntryAsync();
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);
        _viewModel.Signature = Signature(isEmpty: false, Jpeg);
        await _viewModel.CompleteCommand.ExecuteAsync(_viewModel.Signature);

        (await _viewModel.CanLeaveAsync()).ShouldBeTrue();

        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Should_Show_Error_When_Work_Order_Has_No_Entry()
    {
        await _viewModel.OnNavigatedToAsync(_workOrder.Id);

        _viewModel.Screen.ErrorMessage.ShouldBe(AppResources.CompleteRequiresEntry);
    }

    public async ValueTask DisposeAsync() => await _userDatabase.DisposeAsync();

    private static ISignatureSource Signature(bool isEmpty, byte[]? jpeg = null)
    {
        var signature = Substitute.For<ISignatureSource>();
        signature.IsEmpty.Returns(isEmpty);
        signature.ExportJpegAsync(Arg.Any<CancellationToken>()).Returns(jpeg ?? []);
        return signature;
    }

    private async Task AddEntryAsync()
    {
        await _writer.AddServiceEntryAsync(_workOrder.Id, new PendingServiceEntry(Guid.CreateVersion7(), "Przegląd", false, Now, Now.AddMinutes(1), null, null, []));
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
    }
}
