using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AppManagerForJava;

public sealed class JarApp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
public sealed class LaunchProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public string[] Arguments { get; set; } = Array.Empty<string>();
    public string[] JavaOptions { get; set; } = Array.Empty<string>();
    public bool ShowConsole { get; set; } = true;
    public override string ToString() => Name;
}
public sealed class Preferences
{
    public string StorageRoot { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppManagerForJava", "Library");
    public string JavaExecutable { get; set; } = "java.exe";
}
public static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static T Read<T>(string path) where T : new() => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Empty JSON: " + path) : new T();
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(true);
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
public sealed class AppStore
{
    private readonly string preferencesPath;
    public Preferences Settings { get; private set; }
    public List<JarApp> Apps { get; private set; }
    public string Root => Settings.StorageRoot;
    private string Catalog => Path.Combine(Root, "catalog.json");
    public AppStore(string preferencesPath)
    {
        this.preferencesPath = preferencesPath;
        Settings = JsonFile.Read<Preferences>(preferencesPath);
        Settings.StorageRoot = Path.GetFullPath(Settings.StorageRoot);
        Directory.CreateDirectory(Root);
        Apps = JsonFile.Read<List<JarApp>>(Catalog);
        if (Apps.Any(a => a.Id == Guid.Empty) || Apps.Select(a => a.Id).Distinct().Count() != Apps.Count)
            throw new InvalidDataException("The catalog has invalid or duplicate application IDs.");
        JsonFile.Write(this.preferencesPath, Settings);
    }
    public string AppDirectory(JarApp app) => Path.Combine(Root, "apps", app.Id.ToString("N"));
    public string JarPath(JarApp app) => Path.Combine(AppDirectory(app), "application.jar");
    public string ProfileDirectory(JarApp app) => Path.Combine(AppDirectory(app), "profiles");
    public static void ValidateJar(string source)
    {
        if (!string.Equals(Path.GetExtension(source), ".jar", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a .jar file.");
        using var zip = ZipFile.OpenRead(source);
        if (zip.GetEntry("META-INF/MANIFEST.MF") == null)
            throw new InvalidDataException("The JAR has no META-INF/MANIFEST.MF. Use an executable JAR.");
    }
    public JarApp Import(string source)
    {
        ValidateJar(source);
        var app = new JarApp { Name = Path.GetFileNameWithoutExtension(source) };
        Directory.CreateDirectory(AppDirectory(app));
        try
        {
            File.Copy(source, JarPath(app));
            SaveProfile(app, new LaunchProfile());
            Apps.Add(app);
            try { JsonFile.Write(Catalog, Apps); } catch { Apps.Remove(app); throw; }
            return app;
        }
        catch { Directory.Delete(AppDirectory(app), true); throw; }
    }
    public void Update(JarApp app, string source)
    {
        ValidateJar(source);
        if (string.Equals(Path.GetFullPath(source), JarPath(app), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose an external replacement JAR.");
        string temp = JarPath(app) + ".new";
        try
        {
            File.Copy(source, temp, true);
            File.Move(temp, JarPath(app), true);
            app.UpdatedUtc = DateTime.UtcNow;
            JsonFile.Write(Catalog, Apps);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Rename(JarApp app, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter an application name.");
        var previous = app.Name; app.Name = name.Trim();
        try { JsonFile.Write(Catalog, Apps); } catch { app.Name = previous; throw; }
    }
    public void Delete(JarApp app)
    {
        // Commit removal first; remaining files are recoverable if cleanup fails.
        int index = Apps.IndexOf(app);
        Apps.Remove(app);
        try { JsonFile.Write(Catalog, Apps); } catch { Apps.Insert(index, app); throw; }
        if (Directory.Exists(AppDirectory(app))) Directory.Delete(AppDirectory(app), true);
    }
    public List<LaunchProfile> Profiles(JarApp app) => Directory.Exists(ProfileDirectory(app))
        ? Directory.GetFiles(ProfileDirectory(app), "*.json").Select(JsonFile.Read<LaunchProfile>).OrderBy(p => p.Name).ToList()
        : new List<LaunchProfile>();
    public void SaveProfile(JarApp app, LaunchProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name)) throw new ArgumentException("Enter a profile name.");
        if (Profiles(app).Any(p => p.Id != profile.Id && string.Equals(p.Name, profile.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("That profile name already exists.");
        profile.Name = profile.Name.Trim();
        JsonFile.Write(Path.Combine(ProfileDirectory(app), profile.Id.ToString("N") + ".json"), profile);
    }
    public void DeleteProfile(JarApp app, LaunchProfile profile) => File.Delete(Path.Combine(ProfileDirectory(app), profile.Id.ToString("N") + ".json"));
    public void SetJava(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new ArgumentException("Enter java.exe or an executable path.");
        var previous = Settings.JavaExecutable;
        Settings.JavaExecutable = executable.Trim().Trim('"');
        try { JsonFile.Write(preferencesPath, Settings); } catch { Settings.JavaExecutable = previous; throw; }
    }
    public ProcessStartInfo LaunchInfo(JarApp app, LaunchProfile profile)
    {
        if (!File.Exists(JarPath(app))) throw new FileNotFoundException("Managed JAR is missing.", JarPath(app));
        var info = new ProcessStartInfo(Settings.JavaExecutable)
        {
            UseShellExecute = false, WorkingDirectory = AppDirectory(app), CreateNoWindow = !profile.ShowConsole
        };
        foreach (var arg in profile.JavaOptions) info.ArgumentList.Add(arg);
        info.ArgumentList.Add("-jar"); info.ArgumentList.Add(JarPath(app));
        foreach (var arg in profile.Arguments) info.ArgumentList.Add(arg);
        return info;
    }
    private static string Normal(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p));
    private static bool Within(string child, string parent) => child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static byte[] Hash(string path) { using var s = File.OpenRead(path); using var hash = SHA256.Create(); return hash.ComputeHash(s); }
    public string MoveStorage(string destination)
    {
        string old = Normal(Root), next = Normal(destination);
        if (string.Equals(old, next, StringComparison.OrdinalIgnoreCase)) return "Storage is already at this location.";
        if (Within(next, old) || Within(old, next)) throw new IOException("Storage folders must not contain each other.");
        if (Within(Normal(preferencesPath), next) || Within(Normal(preferencesPath), old))
            throw new IOException("Choose a separate folder from the manager's settings file.");
        if (File.Exists(next) || Directory.Exists(next) && Directory.EnumerateFileSystemEntries(next).Any())
            throw new IOException("Choose a new or empty destination folder.");
        // Reject links/junctions before recursive traversal; never follow them.
        var directories = new List<string>(); var files = new List<string>();
        void Scan(string dir)
        {
            if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) throw new IOException("Storage contains a linked folder: " + dir);
            directories.Add(dir);
            foreach (string file in Directory.GetFiles(dir))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Storage contains a linked file: " + file);
                files.Add(file);
            }
            foreach (string sub in Directory.GetDirectories(dir)) Scan(sub);
        }
        Scan(old);
        Directory.CreateDirectory(next);
        // Resolve existing ancestor links as well by rejecting them.
        for (DirectoryInfo? d = new DirectoryInfo(next); d != null; d = d.Parent)
            if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Destination must not use linked folders.");
        try
        {
            foreach (var dir in directories) Directory.CreateDirectory(Path.Combine(next, Path.GetRelativePath(old, dir)));
            foreach (var file in files)
            {
                string target = Path.Combine(next, Path.GetRelativePath(old, file));
                File.Copy(file, target, false);
                if (!Hash(file).SequenceEqual(Hash(target))) throw new IOException("Copy verification failed: " + file);
            }
            Settings.StorageRoot = next;
            try { JsonFile.Write(preferencesPath, Settings); } catch { Settings.StorageRoot = old; throw; }
        }
        catch (Exception ex)
        {
            // Keep the source authoritative. Leave the partial destination for inspection.
            throw new IOException("Move was not committed. Original storage remains active; remove the partial destination before retrying.\n" + ex.Message, ex);
        }
        try { Directory.Delete(old, true); return "Storage moved and verified."; }
        catch (Exception ex) { return "New storage is active. Old copies could not all be removed: " + old + "\n" + ex.Message; }
    }
}
