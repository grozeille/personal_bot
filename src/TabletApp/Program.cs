namespace TabletApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.Exit(SelfTest.Run() ? 0 : 1);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
