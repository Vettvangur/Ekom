using PDFtoImage;
using SkiaSharp;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Runtime.InteropServices;
using static Ekom.Printer.Agent.NativePrintMethods;

namespace Ekom.Printer.Agent;

internal static class GdiSpoolPrinter
{
    public static string Print(byte[] pdf, PrinterSettings settings, int copies, CancellationToken ct)
    {
        using (var validation = new MemoryStream(pdf, false))
        {
            var pages = Conversion.GetPageCount(validation);
            if (pages is < 1 or > 200) throw new InvalidOperationException("A picking slip must contain between 1 and 200 pages.");
        }
        var mode = settings.GetHdevmode();
        var pointer = GlobalLock(mode);
        if (pointer == IntPtr.Zero) { GlobalFree(mode); throw new Win32Exception(); }
        IntPtr dc;
        try { dc = CreateDC("WINSPOOL", settings.PrinterName, null, pointer); }
        finally { GlobalUnlock(mode); GlobalFree(mode); }
        if (dc == IntPtr.Zero) throw new Win32Exception();
        var started = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            var info = new DocInfo { Size = Marshal.SizeOf<DocInfo>(), Name = "Ekom picking slip" };
            var jobId = StartDoc(dc, ref info);
            if (jobId <= 0) throw new Win32Exception();
            started = true;
            for (var copy = 0; copy < copies; copy++)
            {
                using var stream = new MemoryStream(pdf, false);
                var pages = 0;
                foreach (var bitmap in Conversion.ToImages(stream, options: new RenderOptions(Dpi: 200)))
                {
                    using (bitmap)
                    {
                        if (++pages > 200) throw new InvalidOperationException("A picking slip cannot exceed 200 pages.");
                        ct.ThrowIfCancellationRequested();
                        if (StartPage(dc) <= 0) throw new Win32Exception();
                        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                        using var imageStream = png.AsStream();
                        using var image = new Bitmap(imageStream);
                        using (var graphics = Graphics.FromHdc(dc))
                        {
                            graphics.PageUnit = GraphicsUnit.Pixel;
                            var width = GetDeviceCaps(dc, 8); // HORZRES: printable width in device pixels.
                            var height = GetDeviceCaps(dc, 10); // VERTRES.
                            var scale = Math.Min((double)width / image.Width, (double)height / image.Height);
                            graphics.DrawImage(image, new Rectangle(0, 0, (int)(image.Width * scale), (int)(image.Height * scale)));
                        }
                        if (EndPage(dc) <= 0) throw new Win32Exception();
                    }
                }
                if (pages == 0) throw new InvalidOperationException("The renderer returned no pages.");
            }
            if (EndDoc(dc) <= 0) throw new Win32Exception();
            started = false;
            return jobId.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            // Aborting does not prove that no paper was printed. The journal keeps this outcome uncertain.
            if (started) AbortDoc(dc);
            DeleteDC(dc);
        }
    }

}
