using System.Diagnostics;
using System.Text;

namespace AppManagerForJava;

public sealed class LaunchResult
{
    public int? ExitCode { get; init; }
    public string? Error { get; init; }
    public bool Failed => Error != null || ExitCode != 0;
}

/// <summary>Owns a single Java process and drains both pipes independently of any viewer window.</summary>
public sealed class LaunchSession
{
    private const int MaxPreviewCharacters = 262144;
    private readonly object gate = new();
    private readonly StringBuilder preview = new();
    private StreamWriter? writer;
    private bool fileFailed;
    private bool trimmed;
    private string? logPath;
    public Guid AppId { get; }
    public string AppName { get; }
    public string ProfileName { get; }
    public string? LogPath { get { lock (gate) return logPath; } }
    public Task<LaunchResult> Completion { get; }
    public bool IsRunning => !Completion.IsCompleted;
    public string Snapshot { get { lock (gate) return (trimmed ? "[Earlier output omitted from this preview; see the full log file if enabled.]\r\n" : "") + preview; } }

    public LaunchSession(Guid appId, string appName, string profileName, Func<ProcessStartInfo> startInfo,
        bool writeLogFile, string logDirectory)
    {
        AppId = appId; AppName = appName; ProfileName = profileName;
        Completion = Task.Run(() => RunAsync(startInfo, writeLogFile, logDirectory));
    }
    private void Append(string text)
    {
        lock (gate)
        {
            preview.Append(text);
            if (preview.Length > MaxPreviewCharacters) { preview.Remove(0, preview.Length - MaxPreviewCharacters); trimmed = true; }
            if (writer == null) return;
            try { writer.Write(text); writer.Flush(); }
            catch (Exception ex)
            {
                fileFailed = true;
                preview.Append("\r\n[manager] Cannot write log file: " + ex.Message + "\r\n");
                try { writer.Dispose(); } catch { }
                writer = null;
            }
        }
    }
    private async Task PumpAsync(StreamReader reader, string streamName, CancellationToken token)
    {
        var buffer = new char[4096];
        try
        {
            int length;
            while ((length = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
                Append("[" + streamName + "] " + new string(buffer, 0, length));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Append("\r\n[manager] Error reading " + streamName + ": " + ex.Message + "\r\n"); }
    }
    private async Task<LaunchResult> RunAsync(Func<ProcessStartInfo> startInfo, bool writeLogFile, string logDirectory)
    {
        try
        {
            if (writeLogFile)
            {
                try
                {
                    Directory.CreateDirectory(logDirectory);
                    string path = Path.Combine(logDirectory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffffff") + "_" + Guid.NewGuid().ToString("N") + ".log");
                    var output = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                    lock (gate) { logPath = path; writer = output; }
                }
                catch (Exception ex)
                {
                    fileFailed = true;
                    Append("[manager] Cannot create log file: " + ex.Message + "\r\n");
                    // Do not start Java silently without the file logging the user requested.
                    return new LaunchResult { Error = "Cannot create the requested log file. " + ex.Message };
                }
            }
            Append($"[manager] Application: {AppName}\r\n[manager] Profile: {ProfileName}\r\n[manager] Started UTC: {DateTime.UtcNow:O}\r\n");
            var info = startInfo();
            info.UseShellExecute = false; info.RedirectStandardOutput = true; info.RedirectStandardError = true; info.CreateNoWindow = true;
            Append($"[manager] Java: {info.FileName}\r\n[manager] Working folder: {info.WorkingDirectory}\r\n");
            using var process = new Process { StartInfo = info };
            if (!process.Start()) throw new IOException("Java did not start.");
            using var drain = new CancellationTokenSource();
            Task stdout = PumpAsync(process.StandardOutput, "stdout", drain.Token);
            Task stderr = PumpAsync(process.StandardError, "stderr", drain.Token);
            await process.WaitForExitAsync().ConfigureAwait(false);
            // Descendants may inherit the pipes; do not wait forever after Java has exited.
            drain.CancelAfter(TimeSpan.FromSeconds(3));
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            if (drain.IsCancellationRequested) Append("\r\n[manager] Output drain timed out after Java exited; a child may still hold a pipe.\r\n");
            int code = process.ExitCode;
            Append($"\r\n[manager] Java exited with code {code} at {DateTime.UtcNow:O}\r\n");
            return new LaunchResult { ExitCode = code, Error = fileFailed ? "Output log could not be fully written. See the output window." : null };
        }
        catch (Exception ex)
        {
            Append("\r\n[manager] Launch failed: " + ex + "\r\n");
            return new LaunchResult { Error = ex.Message };
        }
        finally
        {
            lock (gate)
            {
                try { writer?.Dispose(); } catch { }
                writer = null;
            }
        }
    }
    public void RelocateLogPath(string oldRoot, string newRoot)
    {
        if (IsRunning) throw new InvalidOperationException("Cannot relocate an active log.");
        lock (gate)
        {
            if (logPath == null) return;
            string oldPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(oldRoot)) + Path.DirectorySeparatorChar;
            if (logPath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                logPath = Path.Combine(newRoot, Path.GetRelativePath(oldRoot, logPath));
        }
    }
    public static LaunchSession ForApp(AppStore store, JarApp app, LaunchProfile profile)
    {
        string directory = string.IsNullOrWhiteSpace(profile.LogDirectory) ? "logs" : profile.LogDirectory;
        try
        {
            string resolved = Path.GetFullPath(Path.Combine(store.AppDirectory(app), directory));
            return new LaunchSession(app.Id, app.Name, profile.Name, () => store.LaunchInfo(app, profile), profile.RedirectOutputToFile, resolved);
        }
        catch (Exception ex)
        {
            return new LaunchSession(app.Id, app.Name, profile.Name, () => throw new IOException("Invalid log folder: " + ex.Message, ex), false, "");
        }
    }
}
