using Ekom.Printer.Agent.Client;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Ekom.Printer.Agent.Setup;

internal static class ServicePipeIdentity
{
    public static void Verify(NamedPipeClientStream pipe)
    {
        using var service = new ServiceController(AgentSetupProtocol.ServiceName);
        if (service.Status != ServiceControllerStatus.Running)
            throw new IOException("The printer service is not running.");
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var pipeProcess) ||
            !QueryServiceStatusEx(service.ServiceHandle, 0, out var status, Marshal.SizeOf<ServiceStatusProcess>(), out _))
            throw new Win32Exception();
        if (status.ProcessId == 0 || status.ProcessId != pipeProcess)
            throw new UnauthorizedAccessException("The pipe server is not the installed printer service.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode,
            CheckPoint, WaitHint, ProcessId, ServiceFlags;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(SafeHandle service, int level, out ServiceStatusProcess status, int size, out int needed);
}
