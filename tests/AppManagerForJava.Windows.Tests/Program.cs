using AppManagerForJava;
using System.IO.Compression;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string temp = Path.Combine(Path.GetTempPath(), "JarManagerWindowsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        int passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAILED: " + label);
            Console.WriteLine("PASS: " + label); passed++;
        }
        try
        {
            // Reproduce delayed native handle creation: the source must remain alive
            // after Add returns, until ImageList.CreateBitmap uses it.
            using (var icons = new OwnedImageList())
            {
                var original = new Bitmap(72, 72);
                icons.Add("first", original);
                GC.Collect(); GC.WaitForPendingFinalizers();
                Check(icons.Value.Handle != IntPtr.Zero, "ImageList realizes its native handle after Add");
                using (var copied = icons.Value.Images[0]) Check(copied.Width == 72, "Deferred bitmap remains readable");
                icons.Clear();
                bool released = false;
                try { _ = original.Width; } catch (ArgumentException) { released = true; } catch (ObjectDisposedException) { released = true; }
                Check(released && icons.Value.Images.Count == 0, "Clear releases original bitmaps after removing references");
                for (int i = 0; i < 50; i++)
                {
                    icons.Add("refresh", new Bitmap(72, 72));
                    _ = icons.Value.Handle;
                    icons.Clear();
                }
                Check(icons.Value.Images.Count == 0, "Repeated rebuilds release icon collections");
            }
            string config = Path.Combine(temp, "settings.json");
            JsonFile.Write(config, new Preferences { StorageRoot = Path.Combine(temp, "storage") });
            var store = new AppStore(config);
            for (int i = 0; i < 3; i++)
            {
                string jar = Path.Combine(temp, "app" + i + ".jar");
                using (var zip = ZipFile.Open(jar, ZipArchiveMode.Create))
                using (var text = new StreamWriter(zip.CreateEntry("META-INF/MANIFEST.MF").Open()))
                    text.Write("Manifest-Version: 1.0\r\nMain-Class: Main\r\n\r\n");
                store.Import(jar);
            }
            using (var form = new MainForm(store))
            {
                form.Show(); Application.DoEvents();
                IEnumerable<Control> Descendants(Control parent)
                {
                    foreach(Control child in parent.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; }
                }
                var all=Descendants(form).ToList();
                var list = all.OfType<AppGridView>().Single();
                Check(list.IsHandleCreated && list.Items.Count == 3 && list.LargeImageList!.Handle != IntPtr.Zero,
                    "Real main window opens with a populated catalog");
                var search = all.OfType<TextBox>().Single(c => c.Name == "LibrarySearch");
                search.Text = "app1"; Application.DoEvents();
                Check(list.Items.Count == 1, "Search rebuilds images safely");
                search.Clear(); Application.DoEvents();
                Check(list.Items.Count == 3, "Clearing search restores all icons");
                list.View = View.Details; list.View = View.Tile; Application.DoEvents();
                Check(list.LargeImageList!.Handle != IntPtr.Zero, "List view can realize images again");
                Check(list.OwnerDraw && list.View == View.Tile, "Custom card grid is active");
                Check(!all.Any(c => c is MenuStrip || c is ToolStrip), "No conventional menu or toolbar remains");
                var strip = all.OfType<HoverInfoStrip>().Single();
                list.Items[0].Selected=true;
                list.SetHoveredItem(list.Items[1]);
                Check(strip.AppTitle==((JarApp)list.Items[1].Tag!).Name && list.SelectedItems[0]==list.Items[0], "Hover updates information without changing selection");
                list.SetHoveredItem(null);
                Check(strip.AppTitle==((JarApp)list.Items[0].Tag!).Name, "Leaving a card restores selected application information");
                var orb = all.OfType<StartOrb>().Single(); orb.PerformClick();
                var drawer=all.Single(c=>c.Name=="CommandDrawer");
                Check(drawer.Visible, "Circular button opens command panel");
                foreach(string text in new[] { "＋  Add JAR files…", "↻  Update JAR…", "✎  Rename app…", "×  Delete app", "▶  Run application", "↗  Run with parameters…", "▤  Open app folder", "≡  View output", "＋  New profile…", "✎  Edit profile…", "×  Delete selected profile", "◉  JRE location…", "▣  Move storage…" })
                    Check(all.OfType<Button>().Any(b=>b.Text==text), "Command retained: "+text);
                orb.PerformClick(); Check(!drawer.Visible,"Circular button toggles command panel closed");
                form.Close();
            }
            Console.WriteLine($"All {passed} Windows checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(temp, true); }
    }
}
