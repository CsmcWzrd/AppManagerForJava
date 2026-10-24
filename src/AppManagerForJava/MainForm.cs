using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace AppManagerForJava;
internal sealed class MainForm : Form
{
    private readonly AppStore store;
    private readonly ListView apps = new() { Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false, HideSelection = false, AllowDrop = true, BackColor = Color.FromArgb(247, 249, 252) };
    private readonly ComboBox profiles = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TextBox search = new() { Width = 220, PlaceholderText = "Search applications" };
    private readonly ImageList images = new() { ImageSize = new Size(72, 72), ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<LaunchSession> running = new();
    private readonly Dictionary<Guid, LaunchSession> latest = new();
    private readonly Dictionary<LaunchSession, OutputWindow> viewers = new();
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "App Manager for Java" };
    private bool busy;
    private JarApp? Selected => apps.SelectedItems.Count == 0 ? null : apps.SelectedItems[0].Tag as JarApp;
    public MainForm(AppStore store)
    {
        this.store = store; Text = "App Manager for Java"; Width = 1120; Height = 740; MinimumSize = new Size(900, 560);
        Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8) };
        void ActionButton(string title, Action action) { var b = new ToolStripButton(title); b.Click += (_, _) => Guard(action); toolbar.Items.Add(b); }
        ActionButton("Add JAR…", Add); ActionButton("Update…", UpdateJar); ActionButton("Rename…", Rename); ActionButton("Delete", Delete);
        toolbar.Items.Add(new ToolStripSeparator()); ActionButton("Run", RunDefault); ActionButton("Run with parameters…", RunOnce);
        toolbar.Items.Add(new ToolStripSeparator()); ActionButton("Storage…", MoveStorage); ActionButton("JRE…", SetJava);
        var profileBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), WrapContents = true };
        profileBar.Controls.Add(new Label { Text = "Launch profile", AutoSize = true, Margin = new Padding(0, 6, 8, 0) }); profileBar.Controls.Add(profiles);
        void ProfileButton(string title, Action action) { var b = new Button { Text = title, AutoSize = true }; b.Click += (_, _) => Guard(action); profileBar.Controls.Add(b); }
        ProfileButton("New…", NewProfile); ProfileButton("Edit…", EditProfile); ProfileButton("Delete profile", DeleteProfile); ProfileButton("Open app folder", OpenFolder); ProfileButton("View output", ViewOutput);
        profileBar.Controls.Add(search);
        var hint = new Label { Text = "Drop JAR files here to add them • Double-click an app to run the selected profile", Dock = DockStyle.Top, Height = 38, Padding = new Padding(12, 8, 0, 0), ForeColor = Color.DimGray };
        var statusBar = new StatusStrip(); statusBar.Items.Add(status);
        Controls.Add(apps); Controls.Add(hint); Controls.Add(profileBar); Controls.Add(toolbar); Controls.Add(statusBar); toolbar.Dock = DockStyle.Top;
        apps.LargeImageList = images; apps.SelectedIndexChanged += (_, _) => Guard(RefreshProfiles);
        apps.DoubleClick += (_, _) => Guard(RunDefault); search.TextChanged += (_, _) => RefreshApps();
        DragEventHandler enter = (_, e) => { if (!busy && e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsJar)) e.Effect = DragDropEffects.Copy; };
        DragEventHandler drop = async (_, e) => { if (!busy && e.Data?.GetData(DataFormats.FileDrop) is string[] files) await ImportFiles(files); };
        AllowDrop = true; DragEnter += enter; DragDrop += drop; apps.DragEnter += enter; apps.DragDrop += drop;
        var menu = new ContextMenuStrip();
        foreach (var pair in new (string Title, Action Action)[] { ("Run", RunDefault), ("Run with parameters…", RunOnce), ("Update JAR…", UpdateJar), ("Rename…", Rename), ("Open folder", OpenFolder), ("View output", ViewOutput), ("Delete", Delete) })
            menu.Items.Add(pair.Title, null, (_, _) => Guard(pair.Action));
        apps.ContextMenuStrip = menu;
        apps.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { var item = apps.GetItemAt(e.X, e.Y); if (item != null) { apps.SelectedItems.Clear(); item.Selected = true; } } };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open manager", null, (_, _) => RestoreManager());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        tray.ContextMenuStrip = trayMenu; tray.DoubleClick += (_, _) => RestoreManager();
        FormClosing += (_, e) =>
        {
            if (busy) { e.Cancel = true; return; }
            if (running.Any(p => p.IsRunning))
            {
                e.Cancel = true; tray.Visible = true; Hide();
                tray.ShowBalloonTip(3000, "Java applications are running", "Output capture continues. Double-click this icon to reopen the manager. Exit after Java applications finish.", ToolTipIcon.Info);
                return;
            }
            foreach (var window in viewers.Values.ToArray()) window.Close();
            tray.Dispose(); trayMenu.Dispose(); images.Dispose();
        };
        RefreshApps();
    }
    private void Guard(Action action) { if (busy) return; try { action(); } catch (Exception ex) { RefreshApps(); MessageBox.Show(this, ex.Message, "App Manager for Java", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    private JarApp RequireApp() => Selected ?? throw new InvalidOperationException("Select an application first.");
    private static bool IsJar(string p) => File.Exists(p) && string.Equals(Path.GetExtension(p), ".jar", StringComparison.OrdinalIgnoreCase);
    private static string? PickJar(IWin32Window owner)
    {
        using var d = new OpenFileDialog { Filter = "Java archives (*.jar)|*.jar", CheckFileExists = true };
        return d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
    }
    private async void Add()
    {
        using var dialog = new OpenFileDialog { Filter = "Java archives (*.jar)|*.jar", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) await ImportFiles(dialog.FileNames);
    }
    private async Task ImportFiles(string[] paths)
    {
        if (busy) return;
        var errors = new List<string>(); busy = true; UseWaitCursor = true; SetControls(false);
        try
        {
            foreach (var path in paths)
            {
                try { await Task.Run(() => store.Import(path)); }
                catch (Exception ex) { errors.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }
            RefreshApps();
        }
        finally { busy = false; UseWaitCursor = false; SetControls(true); }
        if (errors.Count > 0) MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Some files were not imported");
    }
    private void SetControls(bool enabled) { foreach (Control c in Controls) if (c is not StatusStrip) c.Enabled = enabled; }
    private void RefreshApps()
    {
        var id = Selected?.Id; apps.BeginUpdate(); apps.Items.Clear(); images.Images.Clear();
        foreach (var app in store.Apps.Where(a => a.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Name))
        {
            using var bitmap = MakeIcon(app.Name); images.Images.Add(app.Id.ToString(), bitmap);
            var item = new ListViewItem(app.Name, app.Id.ToString()) { Tag = app, ToolTipText = $"{store.JarPath(app)}\nUpdated {app.UpdatedUtc.ToLocalTime():g}" }; apps.Items.Add(item);
            if (app.Id == id) item.Selected = true;
        }
        apps.ShowItemToolTips = true; apps.EndUpdate(); RefreshProfiles(); status.Text = $"{store.Apps.Count} applications  |  {store.Root}";
    }
    private static Bitmap MakeIcon(string name)
    {
        var bitmap = new Bitmap(72, 72); using var g = Graphics.FromImage(bitmap); g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
        using var background = new SolidBrush(Color.FromArgb(35, 84, 145)); g.FillRoundedRectangleCompat(background, new Rectangle(5, 4, 62, 62), 12);
        using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold); using var captionFont = new Font("Segoe UI", 8, FontStyle.Bold);
        string letter = string.IsNullOrWhiteSpace(name) ? "J" : System.Globalization.StringInfo.GetNextTextElement(name);
        g.DrawString(letter, titleFont, Brushes.White, new RectangleF(5, 6, 62, 37), new StringFormat { Alignment = StringAlignment.Center });
        g.DrawString("JAR", captionFont, Brushes.White, new RectangleF(5, 44, 62, 18), new StringFormat { Alignment = StringAlignment.Center }); return bitmap;
    }
    private void RefreshProfiles()
    {
        profiles.Items.Clear(); if (Selected is { } app) foreach (var p in store.Profiles(app)) profiles.Items.Add(p);
        if (profiles.Items.Count > 0) profiles.SelectedIndex = 0;
    }
    private LaunchProfile CurrentProfile() => profiles.SelectedItem as LaunchProfile ?? new LaunchProfile();
    private void CheckIdle(JarApp app)
    {
        if (running.Any(p => p.AppId == app.Id && p.IsRunning)) throw new IOException("Close this application's Java processes before updating or deleting it.");
    }
    private async void UpdateJar()
    {
        if (busy) return;
        try
        {
            var app = RequireApp(); CheckIdle(app); var path = PickJar(this); if (path == null) return;
            busy = true; SetControls(false); UseWaitCursor = true;
            await Task.Run(() => store.Update(app, path)); RefreshApps();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Update failed"); }
        finally { busy = false; SetControls(true); UseWaitCursor = false; }
    }
    private void Rename() { var app = RequireApp(); var name = Dialogs.Text(this, "Rename application", "Display name", app.Name); if (name != null) { store.Rename(app, name); RefreshApps(); } }
    private void Delete()
    {
        var app = RequireApp(); CheckIdle(app);
        if (MessageBox.Show(this, $"Delete {app.Name} and its managed files, data and profiles?\nYour originally imported file is unaffected.", "Delete application", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { store.Delete(app); } finally { RefreshApps(); }
    }
    private void RunDefault() => Launch(RequireApp(), CurrentProfile());
    private void RunOnce() { var app = RequireApp(); using var d = new ProfileDialog(CurrentProfile(), true); if (d.ShowDialog(this) == DialogResult.OK) Launch(app, d.Value); }
    private void Launch(JarApp app, LaunchProfile profile)
    {
        var session = LaunchSession.ForApp(store, app, profile);
        running.Add(session); latest[app.Id] = session;
        status.Text = $"Starting {app.Name}…";
        if (profile.ShowOutputWindow) ShowOutput(session);
        _ = ObserveLaunch(session);
    }
    private async Task ObserveLaunch(LaunchSession session)
    {
        var result = await session.Completion;
        running.Remove(session);
        if (IsDisposed || Disposing) return;
        status.Text = result.Failed ? $"{session.AppName}: launch failed or exited unsuccessfully. See output."
            : $"{session.AppName} exited with code {result.ExitCode}.";
        if (result.Failed) ShowOutput(session);
    }
    private void RestoreManager() { Show(); WindowState = FormWindowState.Normal; Activate(); tray.Visible = false; }
    private void ViewOutput()
    {
        var app = RequireApp();
        if (!latest.TryGetValue(app.Id, out var session)) throw new InvalidOperationException("Run this application first to view its current-session output. Saved logs are in the configured log folder.");
        ShowOutput(session);
    }
    private void ShowOutput(LaunchSession session)
    {
        if (viewers.TryGetValue(session, out var existing) && !existing.IsDisposed)
        { existing.WindowState = FormWindowState.Normal; existing.Show(); existing.Activate(); return; }
        var window = new OutputWindow(session); viewers[session] = window;
        window.FormClosed += (_, _) => viewers.Remove(session);
        window.Show(); // Deliberately modeless and unowned: never block the manager.
    }
    private void NewProfile()
    {
        var app = RequireApp(); using var d = new ProfileDialog(new LaunchProfile { Name = "New profile" }, false);
        if (d.ShowDialog(this) == DialogResult.OK) { store.SaveProfile(app, d.Value); RefreshProfiles(); profiles.SelectedItem = profiles.Items.Cast<LaunchProfile>().First(p => p.Id == d.Value.Id); }
    }
    private void EditProfile()
    {
        var app = RequireApp(); using var d = new ProfileDialog(CurrentProfile(), false);
        if (d.ShowDialog(this) == DialogResult.OK) { store.SaveProfile(app, d.Value); RefreshProfiles(); profiles.SelectedItem = profiles.Items.Cast<LaunchProfile>().First(p => p.Id == d.Value.Id); }
    }
    private void DeleteProfile()
    {
        var app = RequireApp(); if (profiles.SelectedItem is not LaunchProfile p) return;
        if (MessageBox.Show(this, $"Delete launch profile '{p.Name}'?", "Delete profile", MessageBoxButtons.YesNo) == DialogResult.Yes) { store.DeleteProfile(app, p); RefreshProfiles(); }
    }
    private void OpenFolder() { var app = RequireApp(); Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { store.AppDirectory(app) }, UseShellExecute = false })?.Dispose(); }
    private void SetJava()
    {
        using var dialog = new JavaSettingsDialog(store.Settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!string.IsNullOrWhiteSpace(dialog.JreFolder)) store.SetJreLocation(dialog.JreFolder);
        else store.SetJava(dialog.JavaExecutable);
    }
    private async void MoveStorage()
    {
        if (busy) return;
        try
        {
            if (running.Any(p => p.IsRunning)) throw new IOException("Close all Java applications launched here before moving storage.");
            using var dialog = new FolderBrowserDialog { Description = "Choose a new or empty storage folder. All managed files will be moved.", UseDescriptionForTitle = true, ShowNewFolderButton = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (MessageBox.Show(this, $"Move all managed files to:\n{dialog.SelectedPath}?\nThe old copies are removed after verification.", "Move storage", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            busy = true; SetControls(false); UseWaitCursor = true; status.Text = "Copying and verifying storage…";
            string oldRoot = store.Root;
            string result = await Task.Run(() => store.MoveStorage(dialog.SelectedPath));
            foreach (var session in latest.Values.Concat(viewers.Keys).Distinct()) session.RelocateLogPath(oldRoot, store.Root);
            RefreshApps(); MessageBox.Show(this, result, "Storage");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Storage move failed"); }
        finally { busy = false; SetControls(true); UseWaitCursor = false; }
    }
}
internal static class GraphicsExtensions
{
    public static void FillRoundedRectangleCompat(this Graphics g, Brush brush, Rectangle r, int radius)
    {
        using var path = new GraphicsPath(); int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); g.FillPath(brush, path);
    }
}
