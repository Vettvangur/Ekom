namespace Ekom.Printer.Agent.Setup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form = new SetupForm();
        Application.Run(form);
    }
}
