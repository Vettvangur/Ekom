using System.Runtime.InteropServices;

namespace Ekom.Printer.Agent;

internal static class NativePrintMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DocInfo
    {
        public int Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string Name;
        public IntPtr Output;
        public IntPtr DataType;
        public int Type;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr mode);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", EntryPoint = "StartDocW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int StartDoc(IntPtr dc, ref DocInfo info);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern int StartPage(IntPtr dc);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern int EndPage(IntPtr dc);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern int EndDoc(IntPtr dc);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll")] internal static extern int AbortDoc(IntPtr dc);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll")] internal static extern int GetDeviceCaps(IntPtr dc, int index);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalLock(IntPtr memory);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] internal static extern bool GlobalUnlock(IntPtr memory);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")] internal static extern IntPtr GlobalFree(IntPtr memory);
}
