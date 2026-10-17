namespace native_browser;

static class Program
{
    /// <summary>Ponto de entrada. Aceita uma URL inicial opcional por linha de comando.</summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new BrowserForm(args.FirstOrDefault() ?? "about:home"));
    }
}
