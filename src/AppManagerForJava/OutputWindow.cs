using System.Diagnostics;
namespace AppManagerForJava;

/// <summary>A normal modeless window. Closing it never stops the process or its capture.</summary>
internal sealed class OutputWindow : Form
{
    private readonly LaunchSession session;
    private readonly TextBox output = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10), MaxLength = 0 };
    private readonly Label details = new() { Dock = DockStyle.Top, Height = 76, Padding = new Padding(10), AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 400 };
    private readonly CheckBox follow = new() { Text = "Follow output", Checked = true, AutoSize = true };
    private readonly Button open = new() { Text = "Open full log", AutoSize = true };
    public OutputWindow(LaunchSession session)
    {
        this.session = session;
        Text = session.AppName + " — Java output"; Width = 920; Height = 590; MinimumSize = new Size(600, 350);
        StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(6) };
        var copy = new Button { Text = "Copy output", AutoSize = true };
        var close = new Button { Text = "Close", AutoSize = true };
        copy.Click += (_, _) => { try { Clipboard.SetText(output.Text.Length == 0 ? "No output yet." : output.Text); } catch (Exception ex) { details.Text = "Cannot copy: " + ex.Message; } };
        open.Click += (_, _) => { try { if (session.LogPath is { } path) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(); } catch (Exception ex) { details.Text = "Cannot open log: " + ex.Message; } };
        close.Click += (_, _) => Close();
        bar.Controls.AddRange(new Control[] { follow, copy, open, close }); Controls.Add(output); Controls.Add(details); Controls.Add(bar);
        timer.Tick += (_, _) => RefreshOutput(); timer.Start(); RefreshOutput();
        Activated += (_, _) => RefreshOutput();
        FormClosed += (_, _) => { timer.Stop(); timer.Dispose(); };
    }
    private void RefreshOutput()
    {
        string text = session.Snapshot;
        if (text != output.Text)
        {
            int start = output.SelectionStart, length = output.SelectionLength;
            output.Text = text;
            if (follow.Checked) { output.SelectionStart = output.TextLength; output.ScrollToCaret(); }
            else { output.SelectionStart = Math.Min(start, output.TextLength); output.SelectionLength = Math.Min(length, output.TextLength - output.SelectionStart); }
        }
        string state = session.IsRunning ? "Running" : session.Completion.Result.Failed
            ? "Launch failed or Java exited unsuccessfully: " + (session.Completion.Result.Error ?? $"exit code {session.Completion.Result.ExitCode}")
            : "Finished successfully (exit code 0)";
        details.Text = state + "\r\nLog file: " + (session.LogPath ?? "Not written; output is available in this window.");
        open.Enabled = session.LogPath != null;
        if (!session.IsRunning) timer.Stop();
    }
}
