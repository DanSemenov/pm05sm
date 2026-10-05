namespace pm05sm;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Database.Initialize();
        if (args.Contains("--capture", StringComparer.OrdinalIgnoreCase))
        {
            EvidenceCapture.Create();
            return;
        }
        Application.Run(new LoginForm());
    }
}
