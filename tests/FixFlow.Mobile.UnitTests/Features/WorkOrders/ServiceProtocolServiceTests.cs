using System.Net.Http.Headers;
using System.Net;
using System.Text;
using FixFlow.Mobile.Core.Api.Errors;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.WorkOrders.Details;
using FixFlow.Mobile.Core.Features.WorkOrders;
using FixFlow.Mobile.Core.Launching;
using FixFlow.Mobile.UnitTests.Api;
using NSubstitute;
using Refit;

namespace FixFlow.Mobile.UnitTests.Features.WorkOrders;

public sealed class ServiceProtocolServiceTests : IDisposable
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 protocol");

    private readonly IDocumentLauncher _documents = Substitute.For<IDocumentLauncher>();
    private readonly StubHttpMessageHandler _handler;
    private readonly ServiceProtocolService _service;
    private HttpResponseMessage _response = new(HttpStatusCode.OK);
    private byte[]? _openedContent;

    public ServiceProtocolServiceTests()
    {
        _handler = new StubHttpMessageHandler(_ => _response);
        _documents.OpenAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>()).Returns(call =>
        {
            using var copy = new MemoryStream();
            call.Arg<Stream>().CopyTo(copy);
            _openedContent = copy.ToArray();
            return Task.CompletedTask;
        });
        var api = RestService.ForGenerated<IFixFlowApi>(new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") });
        _service = new ServiceProtocolService(api, _documents);
    }

    [Fact]
    public async Task Should_Open_Pdf_With_File_Name_From_Api_When_Protocol_Is_Available()
    {
        var content = new ByteArrayContent(Pdf);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "protokol-ZL-2026-0042.pdf" };
        _response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

        var error = await _service.OpenAsync(Guid.NewGuid(), "ZL-2026-0042", TestContext.Current.CancellationToken);

        error.ShouldBeNull();
        await _documents.Received(1).OpenAsync(Arg.Any<Stream>(), "protokol-ZL-2026-0042.pdf", "application/pdf");
        _openedContent.ShouldBe(Pdf);
    }

    [Fact]
    public async Task Should_Return_Business_Error_Without_Opening_When_Work_Order_Is_Not_Completed()
    {
        _response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"status":409,"errorCode":"WorkOrder.ProtocolNotAvailable"}""", Encoding.UTF8, "application/problem+json"),
        };

        var error = await _service.OpenAsync(Guid.NewGuid(), "ZL-2026-0042", TestContext.Current.CancellationToken);

        error.ShouldNotBeNull().Kind.ShouldBe(ApiErrorKind.Business);
        await _documents.DidNotReceiveWithAnyArgs().OpenAsync(default!, default!, default!);
    }

    public void Dispose() => _handler.Dispose();
}
