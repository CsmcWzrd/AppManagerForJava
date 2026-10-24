using AppManagerForJava;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;

if (args.Length > 0 && args[0] == "--log-child")
{
    if (args.Length > 1 && args[1] == "no-newline") { Console.Write("unterminated stdout"); Console.Error.Write("unterminated stderr"); return 0; }
    for (int i = 0; i < 5000; i++) { Console.WriteLine("stdout-" + i + new string('o', 80)); Console.Error.WriteLine("stderr-" + i + new string('e', 80)); }
    Console.WriteLine("Unicode output: 名称");
    return 7;
}


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
    // Runtime location persistence and compatibility with existing executable settings.
    JsonFile.Write(config, new Preferences { StorageRoot = next, JavaExecutable = "java.exe" });
    store = new AppStore(config); app = store.Import(jar);
    string jre = Path.Combine(temp, "JRE with spaces"); Directory.CreateDirectory(Path.Combine(jre, "bin"));
    File.WriteAllText(Path.Combine(jre, "bin", "java.exe"), "test placeholder");
    store.SetJreLocation(jre); store = new AppStore(config); app = store.Apps.Single();
    Check(store.JavaPath == Path.Combine(jre, "bin", "java.exe") && store.Settings.JreDirectory == jre, "JRE folder persists and resolves bin/java.exe");
    Throws(() => store.SetJreLocation(Path.Combine(temp, "missing-jre")), "Invalid JRE location rejected");
    Check(store.Settings.JreDirectory == jre, "Invalid JRE keeps previous setting");
    store.SetJava("java.exe"); Check(store.Settings.JreDirectory == "" && store.JavaPath == "java.exe", "PATH fallback clears folder override");
    store.SetJava(Path.Combine(jre, "bin", "javaw.exe"));
    Check(store.LaunchInfo(app, new LaunchProfile()).FileName == Path.Combine(jre, "bin", "java.exe"), "javaw switches to java for capture");
    Check(store.LaunchInfo(app, new LaunchProfile()).RedirectStandardOutput && store.LaunchInfo(app, new LaunchProfile()).RedirectStandardError, "Both output streams redirected");
    var logging = new LaunchProfile { Name = "Logging", LogDirectory = "custom logs", ShowOutputWindow = true, RedirectOutputToFile = false };
    store.SaveProfile(app, logging); var savedLogging = store.Profiles(app).Single(x => x.Id == logging.Id);
    Check(!savedLogging.RedirectOutputToFile && savedLogging.ShowOutputWindow && savedLogging.LogDirectory == "custom logs", "Log settings persist per profile");
    var legacy = System.Text.Json.JsonSerializer.Deserialize<LaunchProfile>("{\"Name\":\"Old profile\",\"ShowConsole\":true}")!;
    Check(legacy.RedirectOutputToFile && !legacy.ShowOutputWindow, "Legacy profiles receive safe logging defaults");

    ProcessStartInfo Child(string mode = "burst")
    {
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath ?? throw new Exception("No process host path");
        var child = new ProcessStartInfo(host);
        if (string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase)) child.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        child.ArgumentList.Add("--log-child"); child.ArgumentList.Add(mode); return child;
    }
    async Task<LaunchResult> Finish(LaunchSession session)
    {
        var winner = await Task.WhenAny(session.Completion, Task.Delay(TimeSpan.FromSeconds(30)));
        if (winner != session.Completion) throw new Exception("Output capture timed out (possible deadlock)");
        return await session.Completion;
    }
    string logs = Path.Combine(temp, "captured logs");
    var burst = new LaunchSession(app.Id, app.Name, "burst", () => Child(), true, logs);
    var failed = await Finish(burst); string contents = File.ReadAllText(burst.LogPath!);
    Check(failed.Failed && failed.ExitCode == 7, "Nonzero exit recorded as failure");
    Check(contents.Contains("stdout-0") && contents.Contains("stdout-4999") && contents.Contains("stderr-0") && contents.Contains("stderr-4999"), "Large simultaneous stdout/stderr captured without deadlock");
    Check(contents.Contains("Unicode output: 名称") && contents.Contains("Java exited with code 7"), "Unicode and exit diagnostics saved");
    Check(burst.Snapshot.Length < 264000 && burst.Snapshot.Contains("Earlier output omitted"), "Preview memory bounded while full file retained");
    var partial = new LaunchSession(app.Id, app.Name, "partial", () => Child("no-newline"), true, logs);
    Check(!(await Finish(partial)).Failed, "Exit zero reported as success");
    string partialText = File.ReadAllText(partial.LogPath!);
    Check(partialText.Contains("unterminated stdout") && partialText.Contains("unterminated stderr"), "Output without newline preserved");
    Check(partial.LogPath != burst.LogPath, "Separate unique log files per launch");
    var missing = new LaunchSession(app.Id, app.Name, "missing", () => new ProcessStartInfo(Path.Combine(temp, "absent-java.exe")), true, logs);
    Check((await Finish(missing)).Failed && missing.Snapshot.Contains("Launch failed"), "Missing Java creates failure diagnostics");
    Check(File.ReadAllText(missing.LogPath!).Contains("Launch failed"), "Startup exception saved to log file");
    var missingJar = new LaunchSession(app.Id, app.Name, "missing JAR", () => throw new FileNotFoundException("Managed JAR is missing"), true, logs);
    Check((await Finish(missingJar)).Failed && File.ReadAllText(missingJar.LogPath!).Contains("Managed JAR is missing"), "Prelaunch errors captured");
    var memory = new LaunchSession(app.Id, app.Name, "memory only", () => Child("no-newline"), false, logs);
    Check(!(await Finish(memory)).Failed && memory.LogPath == null && memory.Snapshot.Contains("unterminated stderr"), "Logging can be disabled with diagnostics retained in memory");
    string blocked = Path.Combine(temp, "blocked-log-folder"); File.WriteAllText(blocked, "not a directory"); bool started = false;
    var blockedSession = new LaunchSession(app.Id, app.Name, "blocked", () => { started = true; return Child(); }, true, blocked);
    Check((await Finish(blockedSession)).Failed && !started && blockedSession.Snapshot.Contains("Cannot create log file"), "Invalid log destination reports failure without launching silently");
    store.SetJava(Path.Combine(temp, "missing-java-forapp.exe"));
    var managedLog = LaunchSession.ForApp(store, app, new LaunchProfile());
    Check((await Finish(managedLog)).Failed && managedLog.LogPath!.StartsWith(Path.Combine(store.AppDirectory(app), "logs")), "Application launches use managed log folder");
    string oldRoot = store.Root, movedRoot = Path.Combine(temp, "third-storage");
    store.MoveStorage(movedRoot); managedLog.RelocateLogPath(oldRoot, movedRoot);
    Check(File.Exists(managedLog.LogPath!) && File.ReadAllText(managedLog.LogPath!).Contains("Launch failed"), "Completed log follows storage move");
    string? testJava = Environment.GetEnvironmentVariable("JAVA_MANAGER_TEST_JAVA");
    if (!string.IsNullOrWhiteSpace(testJava))
    {
        store.SetJava(testJava);
        var realJava = LaunchSession.ForApp(store, app, new LaunchProfile());
        var realResult = await Finish(realJava);
        Check(realResult.Failed && realResult.ExitCode != null && realResult.ExitCode != 0, "Real JVM launch of JAR with missing Main class reports failure");
        Check(File.ReadAllText(realJava.LogPath!).Contains("Main") && realJava.Snapshot.Contains("[stderr]"), "Real JVM startup diagnostics captured in file and preview");
    }
    Console.WriteLine($"All {passed} checks passed."); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
finally { Directory.Delete(temp, true); }
