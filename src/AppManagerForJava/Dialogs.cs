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
        return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }
}
internal sealed class ProfileDialog : Form
{
    private readonly TextBox name = new() { Dock = DockStyle.Fill };
    private readonly TextBox args = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
    private readonly TextBox java = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsReturn = true };
    private readonly CheckBox console = new() { Text = "Show Java console", AutoSize = true };
    private readonly Guid id;
    public LaunchProfile Value => new() { Id = id, Name = name.Text, Arguments = Lines(args.Text), JavaOptions = Lines(java.Text), ShowConsole = console.Checked };
    // One line is one argument. Do not strip whitespace or add shell quotes.
    private static string[] Lines(string text) => text.Length == 0 ? Array.Empty<string>() : text.Replace("\r\n", "\n").Split('\n');
    public ProfileDialog(LaunchProfile source, bool temporary)
    {
        id = source.Id; Text = temporary ? "Run with parameters once" : "Named launch profile";
        Width = 660; Height = 590; MinimumSize = new Size(520, 450); StartPosition = FormStartPosition.CenterParent;
        name.Text = source.Name; name.ReadOnly = temporary;
        args.Text = string.Join(Environment.NewLine, source.Arguments); java.Text = string.Join(Environment.NewLine, source.JavaOptions); console.Checked = source.ShowConsole;
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 9 };
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.Controls.Add(new Label { Text = "Profile name", AutoSize = true }); grid.Controls.Add(name);
        grid.Controls.Add(new Label { Text = "Application arguments: one argument per line. Spaces stay within an argument.\nDo not add surrounding quotes; use a blank line for an empty argument.", AutoSize = true }); grid.Controls.Add(args);
        grid.Controls.Add(new Label { Text = "JVM options: one option per line (for example -Xmx512m)", AutoSize = true }); grid.Controls.Add(java);
        grid.Controls.Add(console); grid.Controls.Add(new Label { Text = "Working folder: the managed application's folder.", AutoSize = true });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = temporary ? "Run" : "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { cancel, ok }); grid.Controls.Add(buttons); Controls.Add(grid); CancelButton = cancel;
    }
}
