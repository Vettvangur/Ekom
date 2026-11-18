#if !NET8_0
using Ekom.Models.Import;
using Ekom.Umb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportMediaDownloadTests
{
    public static TheoryData<Exception> DownloadFailures => new()
    {
        new HttpRequestException("connection refused"),
        // What HttpClient throws when the request takes longer than its timeout.
        new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException()),
        new IOException("The response ended prematurely."),
    };

    [Theory]
    [MemberData(nameof(DownloadFailures))]
    public void FailedDownloadLeavesTheMediaOutAndDoesNotThrow(Exception failure)
    {
        var service = Service(new FailingHandler(failure));

        var media = service.ImportMediaFromExternalUrl(Image("https://erp.example.com/mug.jpg"), "comparer", ImportMediaTypes.Image, "MUG-1");

        Assert.Null(media);
    }

    [Fact]
    public void AddressThatIsNotAnAbsoluteUrlLeavesTheMediaOut()
    {
        var service = Service(new FailingHandler(new InvalidOperationException("must not be called")));

        Assert.Null(service.ImportMediaFromExternalUrl(Image("mug.jpg"), "comparer", ImportMediaTypes.Image, "MUG-1"));
    }

    private static ImportMediaFromExternalUrl Image(string url) => new()
    {
        Url = url,
        FileName = "mug.jpg",
        NodeName = "Mug",
    };

    private static ImportMediaService Service(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new ImportMediaService(null!, null!, factory.Object, null!, null!, null!, null!,
            NullLogger<ImportMediaService>.Instance, null!);
    }

    private sealed class FailingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(failure);
    }
}
#endif
