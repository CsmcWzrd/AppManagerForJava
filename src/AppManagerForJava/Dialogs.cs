namespace AppManagerForJava;
internal static class Dialogs
{
    public static string? Text(IWin32Window owner, string title, string label, string initial)
    {
        using var form = new Form { Text = title, Width = 570, Height = 185, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog };
        var caption = new Label { Text = label, Left = 16, Top = 16, Width = 520 };
        var input = new TextBox { Text = initial, Left = 16, Top = 44, Width = 520 };
        var ok = new Button { Text = "OK", Left = 350, Top = 86, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 440, Top = 86, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { caption, input, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel;
        VisualTheme.Apply(form);
        return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }
}
internal sealed class ProfileDialog : Form
{
    private readonly TextBox name = new() { Dock = DockStyle.Fill };
    private readonly TextBox args = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
    private readonly TextBox java = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
    private readonly CheckBox console = new() { Text = "Open live output window when launching", AutoSize = true };
    private readonly CheckBox log = new() { Text = "Redirect standard output and error to a log file", AutoSize = true };
    private readonly TextBox logDirectory = new() { Dock = DockStyle.Fill, PlaceholderText = "Default: logs inside the managed application folder" };
    private readonly Guid id;
    public LaunchProfile Value => new() { Id = id, Name = name.Text, Arguments = Lines(args.Text), JavaOptions = Lines(java.Text), ShowOutputWindow = console.Checked, RedirectOutputToFile = log.Checked, LogDirectory = logDirectory.Text };
    // One line is one argument. Do not strip whitespace or add shell quotes.
    private static string[] Lines(string text) => text.Length == 0 ? Array.Empty<string>() : text.Replace("\r\n", "\n").Split('\n');
    public ProfileDialog(LaunchProfile source, bool temporary)
    {
        id = source.Id; Text = temporary ? "Run with parameters once" : "Named launch profile";
        Width = 660; Height = 740; MinimumSize = new Size(600, 650); StartPosition = FormStartPosition.CenterParent;
        name.Text = source.Name; name.ReadOnly = temporary;
        args.Text = string.Join(Environment.NewLine, source.Arguments); java.Text = string.Join(Environment.NewLine, source.JavaOptions); console.Checked = source.ShowOutputWindow; log.Checked = source.RedirectOutputToFile; logDirectory.Text = source.LogDirectory;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 12 };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.Controls.Add(new Label { Text = "Profile name", AutoSize = true }); grid.Controls.Add(name);
        grid.Controls.Add(new Label { Text = "Application arguments: one argument per line. Spaces stay within an argument.\nDo not add surrounding quotes; use a blank line for an empty argument.", AutoSize = true }); grid.Controls.Add(args);
        grid.Controls.Add(new Label { Text = "JVM options: one option per line (for example -Xmx512m)", AutoSize = true }); grid.Controls.Add(java);
        grid.Controls.Add(console); grid.Controls.Add(log);
        grid.Controls.Add(new Label { Text = "Log folder (absolute or relative to the app folder)", AutoSize = true });
        var logRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        logRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); logRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var browseLogs = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browseLogs.Click += (_, _) => { using var picker = new FolderBrowserDialog { Description = "Select a log folder", UseDescriptionForTitle = true }; if (picker.ShowDialog(this) == DialogResult.OK) logDirectory.Text = picker.SelectedPath; };
        logRow.Controls.Add(logDirectory); logRow.Controls.Add(browseLogs); grid.Controls.Add(logRow);
        grid.Controls.Add(new Label { Text = "Working folder: the managed application's folder.", AutoSize = true });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = temporary ? "Run" : "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { cancel, ok }); grid.Controls.Add(buttons); Controls.Add(grid); CancelButton = cancel; VisualTheme.Apply(this);
    }
}

internal sealed class JavaSettingsDialog : Form
{
    private readonly TextBox folder = new() { Dock = DockStyle.Fill };
    private readonly TextBox executable = new() { Dock = DockStyle.Fill };
    public string JreFolder => folder.Text;
    public string JavaExecutable => executable.Text;
    public JavaSettingsDialog(Preferences settings)
    {
        Text = "Java runtime (JRE or JDK)"; Width = 720; Height = 310; MinimumSize = new Size(620, 310);
        StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
        folder.Text = settings.JreDirectory; executable.Text = settings.JavaExecutable;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        foreach (int height in new[] { 44, 36, 30, 36, 44, 42 }) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var info = new Label { Text = "Choose the JRE/JDK installation folder containing bin\\java.exe. This setting applies to all managed applications.", Dock = DockStyle.Fill }; grid.Controls.Add(info, 0, 0); grid.SetColumnSpan(info, 2);
        var browse = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browse.Click += (_, _) => { using var picker = new FolderBrowserDialog { Description = "Select the JRE or JDK folder containing bin\\java.exe", UseDescriptionForTitle = true }; if (picker.ShowDialog(this) == DialogResult.OK) folder.Text = picker.SelectedPath; };
        grid.Controls.Add(folder, 0, 1); grid.Controls.Add(browse, 1, 1);
        var label = new Label { Text = "Executable fallback (used only when the JRE folder is empty)", AutoSize = true }; grid.Controls.Add(label, 0, 2); grid.SetColumnSpan(label, 2);
        grid.Controls.Add(executable, 0, 3);
        var browseExe = new Button { Text = "Browse EXE…", Dock = DockStyle.Fill };
        browseExe.Click += (_, _) => { using var picker = new OpenFileDialog { Filter = "Java executable (java.exe)|java.exe|Executables (*.exe)|*.exe", CheckFileExists = true }; if (picker.ShowDialog(this) == DialogResult.OK) { executable.Text = picker.FileName; folder.Clear(); } }; grid.Controls.Add(browseExe, 1, 3);
        var hint = new Label { Text = "Leave the folder empty and use java.exe to resolve Java through PATH. Output capture uses java.exe rather than javaw.exe.", Dock = DockStyle.Fill }; grid.Controls.Add(hint, 0, 4); grid.SetColumnSpan(hint, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true }; var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { cancel, save }); grid.Controls.Add(buttons, 0, 5); grid.SetColumnSpan(buttons, 2);
        Controls.Add(grid); AcceptButton = save; CancelButton = cancel; VisualTheme.Apply(this);
    }
}
