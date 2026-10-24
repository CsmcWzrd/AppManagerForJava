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
                var list = form.Controls.OfType<ListView>().Single();
                Check(list.IsHandleCreated && list.Items.Count == 3 && list.LargeImageList!.Handle != IntPtr.Zero,
                    "Real main window opens with a populated catalog");
                var search = form.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<TextBox>()).Single();
                search.Text = "app1"; Application.DoEvents();
                Check(list.Items.Count == 1, "Search rebuilds images safely");
                search.Clear(); Application.DoEvents();
                Check(list.Items.Count == 3, "Clearing search restores all icons");
                list.View = View.Details; list.View = View.LargeIcon; Application.DoEvents();
                Check(list.LargeImageList!.Handle != IntPtr.Zero, "List view can realize images again");
                form.Close();
            }
            Console.WriteLine($"All {passed} Windows checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(temp, true); }
    }
}
