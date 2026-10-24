using AppManagerForJava;
using System.IO.Compression;

string temp = Path.Combine(Path.GetTempPath(), "JarManagerTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
int passed = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAILED: " + label); Console.WriteLine("PASS: " + label); passed++; }
void Throws(Action action, string label) { try { action(); } catch { Check(true, label); return; } throw new Exception("Expected failure: " + label); }
string MakeJar(string name, string content)
{
    string path = Path.Combine(temp, name);
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    using (var w = new StreamWriter(zip.CreateEntry("META-INF/MANIFEST.MF").Open())) w.Write("Manifest-Version: 1.0\r\nMain-Class: Main\r\n\r\n");
    using (var w = new StreamWriter(zip.CreateEntry("payload.txt").Open())) w.Write(content);
    return path;
}
try
{
    string config = Path.Combine(temp, "config", "settings.json"), storage = Path.Combine(temp, "store");
    JsonFile.Write(config, new Preferences { StorageRoot = storage, JavaExecutable = "java.exe" });
    var store = new AppStore(config);
    string jar = MakeJar("sample.jar", "original"); var app = store.Import(jar);
    Check(File.Exists(jar) && File.Exists(store.JarPath(app)) && store.Apps.Count == 1, "Import copies the source");
    Check(store.Profiles(app).Single().Name == "Default", "Default profile persisted");
    var p = new LaunchProfile { Name = "Unicode 名称", Arguments = new[] { "hello world", "", "C:\\path with spaces\\", "\"quoted\"", "a&b" }, JavaOptions = new[] { "-Xmx256m" } };
    store.SaveProfile(app, p); var read = store.Profiles(app).Single(x => x.Id == p.Id);
    Check(read.Arguments.SequenceEqual(p.Arguments), "Profile preserves exact arguments");
    Check(Directory.GetFiles(store.ProfileDirectory(app), "*.json").Length == 2, "Profiles saved in separate files");
    Throws(() => store.SaveProfile(app, new LaunchProfile { Name = p.Name }), "Duplicate profile names rejected");
    var info = store.LaunchInfo(app, read);
    Check(!info.UseShellExecute && info.ArgumentList.SequenceEqual(new[] { "-Xmx256m", "-jar", store.JarPath(app) }.Concat(p.Arguments)), "Launch arguments have correct order and boundaries");
    store.Rename(app, "New name"); store = new AppStore(config); app = store.Apps.Single();
    Check(app.Name == "New name", "Rename survives restart");
    string update = MakeJar("update.jar", "replacement"); store.Update(app, update);
    Check(File.ReadAllBytes(store.JarPath(app)).SequenceEqual(File.ReadAllBytes(update)), "Update replaces JAR");
    Check(store.Profiles(app).Count == 2, "Update preserves profiles");
    string bad = Path.Combine(temp, "bad.jar"); File.WriteAllText(bad, "bad");
    Throws(() => store.Update(app, bad), "Invalid update rejected");
    Check(File.ReadAllBytes(store.JarPath(app)).SequenceEqual(File.ReadAllBytes(update)), "Invalid update preserves previous JAR");
    Throws(() => store.MoveStorage(Path.Combine(storage, "nested")), "Nested destination rejected");
    string occupied = Path.Combine(temp, "occupied"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "keep"), "important");
    Throws(() => store.MoveStorage(occupied), "Occupied destination rejected");
    Check(File.ReadAllText(Path.Combine(occupied, "keep")) == "important", "Destination data preserved");
    File.WriteAllText(Path.Combine(store.AppDirectory(app), "app-data.txt"), "user data");
    string next = Path.Combine(temp, "second-drive-simulation"); store.MoveStorage(next);
    Check(!Directory.Exists(storage) && File.Exists(store.JarPath(app)), "Move commits and removes source");
    Check(File.ReadAllText(Path.Combine(store.AppDirectory(app), "app-data.txt")) == "user data", "Move preserves app data");
    store = new AppStore(config); app = store.Apps.Single();
    Check(store.Root == next && store.Profiles(app).Count == 2, "Moved storage survives restart");
    store.DeleteProfile(app, store.Profiles(app).Single(x => x.Id == p.Id));
    Check(store.Profiles(app).Count == 1, "Delete profile");
    store.Delete(app); Check(store.Apps.Count == 0 && !Directory.Exists(store.AppDirectory(app)) && File.Exists(jar), "Delete removes only managed copy");
    File.WriteAllText(config, "{ broken json"); Throws(() => new AppStore(config), "Corrupt settings are not silently overwritten");
    Console.WriteLine($"All {passed} checks passed."); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
finally { Directory.Delete(temp, true); }
