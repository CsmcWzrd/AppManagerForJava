using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace AppManagerForJava;
internal sealed class MainForm : Form
{
    private readonly AppStore store;
    private readonly AppGridView apps = new() { Dock = DockStyle.Fill, AllowDrop = true, Name = "ApplicationGrid" };
    private readonly ComboBox profiles = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(30, 9, 18, 0), ForeColor = VisualTheme.Muted, BackColor = VisualTheme.Canvas, AutoEllipsis = true };
    private readonly HoverInfoStrip information = new() { Dock = DockStyle.Fill, Name = "HoverInformation" };
    private readonly StartOrb start = new() { Name = "StartMenuButton", Size = new Size(62, 62) };
    private readonly RoundPanel commands = new() { Name = "CommandDrawer", Visible = false, Size = new Size(440, 530), Padding = new Padding(5) };
    private readonly Label menuTitle = new() { Font = new Font("Segoe UI Semibold", 13), ForeColor = VisualTheme.Ink, AutoEllipsis = true, Dock = DockStyle.Fill };
    private readonly Label libraryCount = new() { ForeColor = VisualTheme.Muted, AutoSize = true };
    private readonly Label empty = new() { Text = "Your next favorite app belongs here.\n\nOpen the circular menu to add JAR files, or drop them here.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = VisualTheme.Muted, Font = new Font("Segoe UI", 12), AllowDrop = true };
    private readonly List<Button> appCommands = new();
    private JarApp? hoveredApp;
    private readonly TextBox search = new() { Name = "LibrarySearch", BorderStyle = BorderStyle.None, Width = 230, PlaceholderText = "Search your library…", BackColor = Color.White, ForeColor = VisualTheme.Ink };
    private readonly OwnedImageList images = new();
    private readonly List<LaunchSession> running = new();
    private readonly Dictionary<Guid, LaunchSession> latest = new();
    private readonly Dictionary<LaunchSession, OutputWindow> viewers = new();
    private readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "App Manager for Java" };
    private bool busy;
    private JarApp? Selected => apps.SelectedItems.Count == 0 ? null : apps.SelectedItems[0].Tag as JarApp;
    public MainForm(AppStore store)
    {
        this.store = store; Text = "App Manager for Java"; Width = 1220; Height = 820; MinimumSize = new Size(920, 660);
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        Font = new Font("Segoe UI", 10); BackColor = VisualTheme.Canvas; ForeColor = VisualTheme.Ink; StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 106, BackColor = Color.White, Padding = new Padding(108, 0, 24, 0) };
        top.Controls.Add(information); start.Location = new Point(25, 22); top.Controls.Add(start); start.BringToFront();
        start.Click += (_, _) => ToggleCommands();
        var heading = new Panel { Dock = DockStyle.Top, Height = 140, Padding = new Padding(30, 0, 30, 0) };
        var eyebrow = new Label { Text = "YOUR PERSONAL LAUNCHPAD", Location = new Point(32, 23), AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = VisualTheme.Accent };
        var title = new Label { Text = "A little Java. A lot of possibility.", Location = new Point(28, 47), AutoSize = true, Font = new Font("Segoe UI Semibold", 23), ForeColor = VisualTheme.Ink };
        libraryCount.Location = new Point(32, 101);
        var searchBox = new RoundPanel { Size = new Size(282, 43), Location = new Point(Width - 346, 81), Anchor = AnchorStyles.Top | AnchorStyles.Right, Padding = new Padding(18, 11, 12, 8) };
        search.Dock = DockStyle.Fill; searchBox.Controls.Add(search); heading.Controls.AddRange(new Control[] { eyebrow, title, libraryCount, searchBox });
        // Reflow the heading/search at smaller window widths without overlaps.
        heading.Resize += (_, _) => { title.MaximumSize = new Size(Math.Max(200, heading.Width - 365*DeviceDpi/96), 42*DeviceDpi/96); searchBox.Left = heading.ClientSize.Width - searchBox.Width - 30*DeviceDpi/96; };
        var library = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 0, 22, 0) };
        library.Controls.Add(apps); library.Controls.Add(empty);
        Controls.Add(library); Controls.Add(heading); Controls.Add(top); Controls.Add(status); Controls.Add(commands); commands.BringToFront();
        BuildCommandDrawer(); Resize += (_, _) => PositionCommands();
        apps.IsAppRunning = id => running.Any(p => p.AppId == id && p.IsRunning);
        apps.AppHovered += app => { hoveredApp = app; UpdateInformation(); };
        apps.SelectedIndexChanged += (_, _) => { if (!busy) { Guard(RefreshProfiles); UpdateInformation(); UpdateCommandState(); } };
        apps.MouseDown += (_, _) => commands.Hide();
        library.MouseDown += (_, _) => commands.Hide(); heading.MouseDown += (_, _) => commands.Hide();
        search.Enter += (_, _) => commands.Hide();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { commands.Hide(); apps.Focus(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.O) { Guard(Add); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.F10) { ToggleCommands(); e.SuppressKeyPress = true; }
        };
        apps.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Guard(RunDefault); e.SuppressKeyPress = true; } };
        apps.LargeImageList = images.Value;
        apps.DoubleClick += (_, _) => Guard(RunDefault); search.TextChanged += (_, _) => RefreshApps();
        DragEventHandler enter = (_, e) => { if (!busy && e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsJar)) e.Effect = DragDropEffects.Copy; };
        DragEventHandler drop = async (_, e) => { if (!busy && e.Data?.GetData(DataFormats.FileDrop) is string[] files) await ImportFiles(files); };
        AllowDrop = true; DragEnter += enter; DragDrop += drop; apps.DragEnter += enter; apps.DragDrop += drop; empty.DragEnter += enter; empty.DragDrop += drop;
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
    private void SetControls(bool enabled) { commands.Hide(); foreach (Control c in Controls) if (c != status) c.Enabled = enabled; }
    private void RefreshApps()
    {
        var id = Selected?.Id; hoveredApp = null; apps.ResetHover(); apps.BeginUpdate();
        try
        {
            apps.Items.Clear(); images.Clear();
            foreach (var app in store.Apps.Where(a => a.Name.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Name))
            {
                images.Add(app.Id.ToString(), MakeIcon(app.Name));
                var item = new ListViewItem(app.Name, app.Id.ToString()) { Tag = app, ToolTipText = $"{store.JarPath(app)}\nUpdated {app.UpdatedUtc.ToLocalTime():g}" }; apps.Items.Add(item);
                if (app.Id == id) item.Selected = true;
            }
        }
        finally { apps.EndUpdate(); }
        apps.ShowItemToolTips = true;
        empty.Visible = apps.Items.Count == 0; if (empty.Visible) empty.BringToFront();
        empty.Text = store.Apps.Count == 0 ? "Your next favorite app belongs here.\n\nOpen the circular menu to add JAR files, or drop them here." : "No apps match your search.\n\nTry a different name.";
        RefreshProfiles(); UpdateInformation(); UpdateCommandState();
        libraryCount.Text = $"{store.Apps.Count} applications  ·  Double-click to launch  ·  Drop JARs to add";
        status.Text = $"●  Library ready     ·     {store.Root}";
    }
    internal void UpdateInformation(JarApp? hovered = null)
    {
        var app = hovered ?? hoveredApp ?? Selected;
        if (app == null)
        {
            information.SetInfo("App Manager for Java", "A beautiful home for your Java applications", $"{store.Apps.Count} apps in your library  ·  {running.Count(p => p.IsRunning)} running  ·  Hover over a card to explore");
            return;
        }
        string size = "JAR unavailable";
        try { var file = new FileInfo(store.JarPath(app)); if (file.Exists) size = file.Length < 1048576 ? $"{file.Length / 1024d:N0} KB" : $"{file.Length / 1048576d:N1} MB"; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        bool active = running.Any(p => p.AppId == app.Id && p.IsRunning);
        information.SetInfo(app.Name, $"{(active ? "Running now" : "Ready to launch")}  ·  {size}  ·  Updated {app.UpdatedUtc.ToLocalTime():g}", store.JarPath(app));
    }
    private void PositionCommands()
    {
        int s = DeviceDpi; commands.Location = new Point(18*s/96, 112*s/96);
        commands.Size = new Size(Math.Min(440*s/96, ClientSize.Width-36*s/96), Math.Max(240*s/96, Math.Min(570*s/96,ClientSize.Height-commands.Top-42*s/96)));
    }
    private void ToggleCommands()
    {
        if (busy) return;
        if (commands.Visible) { commands.Hide(); apps.Focus(); return; }
        PositionCommands(); UpdateCommandState(); commands.Show(); commands.BringToFront(); commands.SelectNextControl(null, true, true, true, false);
    }
    private void UpdateCommandState()
    {
        menuTitle.Text = Selected?.Name ?? "Make yourself at home";
        foreach (var button in appCommands) button.Enabled = Selected != null;
        profiles.Enabled = Selected != null;
    }
    private void BuildCommandDrawer()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14), BackColor = Color.White };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Color.White };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        int row = 0;
        void Span(Control control, int height)
        {
            table.RowCount = row+1; table.RowStyles.Add(new RowStyle(SizeType.Absolute,height)); table.Controls.Add(control,0,row++); table.SetColumnSpan(control,2);
        }
        void Section(string name) => Span(new Label { Text=name, ForeColor=VisualTheme.Muted, Font=new Font("Segoe UI Semibold",8), Dock=DockStyle.Fill, Padding=new Padding(3,9,0,0) },32);
        Button Command(string title, Action action, bool requiresApp)
        {
            var button=new Button { Text=title, AccessibleName=title, FlatStyle=FlatStyle.Flat, Dock=DockStyle.Fill, Margin=new Padding(3), BackColor=Color.FromArgb(247,245,253), ForeColor=VisualTheme.Ink, TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(9,0,0,0), Cursor=Cursors.Hand, Font=new Font("Segoe UI",9) };
            button.FlatAppearance.BorderSize=0; button.FlatAppearance.MouseOverBackColor=Color.FromArgb(235,226,252); button.FlatAppearance.MouseDownBackColor=Color.FromArgb(224,211,248);
            button.Click+=(_,_)=>{ commands.Hide(); Guard(action); }; if(requiresApp)appCommands.Add(button); return button;
        }
        void Pair(string one,Action first,bool firstApp,string two,Action second,bool secondApp)
        {
            table.RowCount=row+1;table.RowStyles.Add(new RowStyle(SizeType.Absolute,45));table.Controls.Add(Command(one,first,firstApp),0,row);table.Controls.Add(Command(two,second,secondApp),1,row++);
        }
        Span(new Label { Text="THE LAUNCH MENU", ForeColor=VisualTheme.Accent, Font=new Font("Segoe UI Semibold",8), Dock=DockStyle.Fill },23); Span(menuTitle,38);
        Section("APPLICATIONS");
        Pair("＋  Add JAR files…",Add,false,"↻  Update JAR…",UpdateJar,true);
        Pair("✎  Rename app…",Rename,true,"×  Delete app",Delete,true);
        Pair("▶  Run application",RunDefault,true,"↗  Run with parameters…",RunOnce,true);
        Pair("▤  Open app folder",OpenFolder,true,"≡  View output",ViewOutput,true);
        Section("LAUNCH PROFILES");
        profiles.Dock=DockStyle.Fill;profiles.FlatStyle=FlatStyle.Flat;profiles.BackColor=Color.FromArgb(247,245,253);profiles.ForeColor=VisualTheme.Ink;Span(profiles,35);
        Pair("＋  New profile…",NewProfile,true,"✎  Edit profile…",EditProfile,true);
        Span(Command("×  Delete selected profile",DeleteProfile,true),40);
        Section("YOUR ENVIRONMENT");Pair("◉  JRE location…",SetJava,false,"▣  Move storage…",MoveStorage,false);
        Span(Command("Exit manager",Close,false),40);
        table.Margin=Padding.Empty; scroll.Controls.Add(table);commands.Controls.Add(scroll);
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
        running.Add(session); latest[app.Id] = session; apps.Invalidate(); UpdateInformation();
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
        apps.Invalidate(); UpdateInformation();
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
