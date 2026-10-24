namespace AppManagerForJava;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\AppManagerForJava-" + Environment.UserName, out bool first);
        if (!first) { MessageBox.Show("App Manager for Java is already open."); return; }
        try
        {
            string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppManagerForJava", "settings.json");
            Application.Run(new MainForm(new AppStore(config)));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to open App Manager for Java", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
