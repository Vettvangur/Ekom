using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class PreConsentTrackingSessionServiceTests
{
    [Fact]
    public void Read_Without_Session_Returns_Null()
    {
        var sut = new PreConsentTrackingSessionService();

        var result = sut.Read(new DefaultHttpContext());

        Assert.Null(result);
    }

    [Fact]
    public void WriteFirstTouch_Without_Session_Does_Not_Throw()
    {
        var sut = new PreConsentTrackingSessionService();
        var tracking = new OrderTracking
        {
            Source = "google"
        };

        var exception = Record.Exception(() => sut.WriteFirstTouch(new DefaultHttpContext(), tracking));

        Assert.Null(exception);
    }

    [Fact]
    public void Clear_Without_Session_Does_Not_Throw()
    {
        var sut = new PreConsentTrackingSessionService();

        var exception = Record.Exception(() => sut.Clear(new DefaultHttpContext()));

        Assert.Null(exception);
    }
}
