using Ekom.Printer.Agent;
using PDFtoImage;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Ekom.Printer.Agent.Tests;

public sealed class HtmlRendererTests
{
    [RendererFact]
    public async Task RendersPrintCssWithoutExecutingScriptsOrRequestingExternalResources()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var forbidden = $"http://127.0.0.1:{port}/customer-data";
        var html = $$"""
            <!doctype html><html><head><style>
              @page { size: A4; margin: 15mm; }
              .next { break-before: page; }
              @font-face { font-family: forbidden; src: url('{{forbidden}}'); }
              body { font-family: forbidden, sans-serif; }
            </style></head><body><h1>Picking slip</h1>
              <script>document.body.innerHTML = 'SCRIPT RAN'; fetch('{{forbidden}}');</script>
              <img src="{{forbidden}}"><iframe src="{{forbidden}}"></iframe>
              <iframe src="file:///etc/passwd"></iframe>
              <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=">
              <div class="next">Second page</div>
            </body></html>
            """;
        var pdf = await HtmlDocumentRenderer.RenderAsync(html, 8.27, 11.69);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        using var stream = new MemoryStream(pdf);
        Assert.Equal(2, Conversion.GetPageCount(stream));
        Assert.False(listener.Pending());
    }

    [RendererFact]
    public async Task CancelledRenderNeverStartsBrowser()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlDocumentRenderer.RenderAsync("<p>Test</p>", 8.27, 11.69, cancellation.Token));
    }

    public sealed class RendererFactAttribute : FactAttribute
    {
        public RendererFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("EKOM_RENDER_TESTS") != "1")
                Skip = "Set EKOM_RENDER_TESTS=1 and install the pinned Playwright Chromium browser to run renderer acceptance tests.";
        }
    }
}
