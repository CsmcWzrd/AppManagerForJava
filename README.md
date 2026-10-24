# App Manager for Java

A Microsoft Visual C# Windows Forms application for managing and launching Java JAR applications. Open `AppManagerForJava.sln` in Visual Studio with the **.NET desktop development** workload. Set **AppManagerForJava** as the startup project and build/run it.

## Startup icon crash fix

This revision fixes the startup exception `System.ArgumentException: Parameter is not valid` in `System.Drawing.Image.get_Width` / `ImageList.CreateBitmap`.

Previously, `RefreshApps` disposed each generated icon immediately after adding it to the image list. WinForms can defer reading that image until its native handle is created, including when the main window first appears. The image list now owns and retains the original bitmaps, clears image references before disposing old icons on refresh, and releases all originals at shutdown.

Extract this updated package, open the solution, then **Clean Solution** and **Rebuild Solution** before running. Existing application storage, JRE settings and saved profiles use the same formats.

## Requirements and build

- Windows desktop; this WinForms application is Windows-only.
- Visual Studio with the .NET 8 SDK and desktop targeting components, or the .NET 8 SDK command-line tools.
- A separately installed Java runtime compatible with your JARs. Java is not bundled.
- No third-party NuGet packages are used.

The default target is .NET 8, which meets the requested .NET 6-or-above baseline. The source also supports targeting .NET 6 and newer through `RuntimeVersion`. Install the corresponding SDK/targeting packs before changing the target. The application permits rolling forward to a higher installed major .NET desktop runtime. It does not roll backward: a default .NET 8 build still needs .NET 8 or later, not .NET 6.

From a developer terminal in this directory:

```bat
dotnet build AppManagerForJava.sln -c Release
dotnet run --project tests\AppManagerForJava.Tests -c Release
dotnet run --project src\AppManagerForJava -c Release
```

Or run `build.cmd` on Windows to build and run both the core checks and Windows UI regression checks. Both test projects are ordinary console programs with nonzero exit codes on failure, not `dotnet test` test-adapter projects. The Windows regression program briefly opens the real main window with a populated test catalog, checks deferred image-handle creation, refreshes the search/grid, and verifies bitmap cleanup. Run it on Windows with:

```bat
dotnet run --project tests\AppManagerForJava.Windows.Tests -c Release
```

To target .NET 6 explicitly:

```bat
dotnet build AppManagerForJava.sln -c Release -p:RuntimeVersion=6.0
dotnet run --project tests\AppManagerForJava.Tests -c Release -p:RuntimeVersion=6.0
```

`RuntimeVersion=10.0` similarly selects .NET 10 with a matching SDK. To create a Windows x64 executable with its own .NET runtime, run `publish-win-x64.cmd`. Distribute the entire `publish\win-x64` directory. Java is still required. For ARM64, change `win-x64` to `win-arm64` in the publish command.

## Using the application

1. Click **Add JAR…** to open one or several JARs, or drop JAR files onto the window. Files are copied; imported originals remain in place. A manifest is required, and the JAR must be executable by `java -jar` to launch successfully.
2. Select an app in the icon grid. **Update…** replaces its managed JAR while preserving its launch profiles and files. **Rename…** changes its display name. **Delete** removes its managed directory, including profiles and any app-created files, after confirmation.
3. Double-click an app or click **Run** to launch the selected profile. **Run with parameters…** edits parameters for one launch without saving them.
4. Click **New…** beside the profile selector to save parameters under a new name. **Edit…** overwrites the selected profile; **Delete profile** removes that profile. Each profile is a separate JSON file. The default profile is initially selected on app selection; choose another before running it.
5. Enter one application argument per line. A value such as `C:\My Files\report.txt` stays one argument, without surrounding quotes. Quotes entered into the editor are literal characters. Empty lines between arguments represent empty arguments. A completely empty editor means no arguments. JVM options use their own editor and appear before `-jar`; application arguments appear afterward.
6. **JRE…** lets you browse to a JRE or JDK installation folder containing `bin\java.exe`. This setting is saved and applies to all applications. Clear the folder to use the executable fallback, which can be a specific file selected with **Browse EXE…** or `java.exe` resolved through `PATH`. A selected JRE also sets `JAVA_HOME` for the launched process. Captured output is shown through the manager's output window. A `javaw.exe` fallback is mapped to the sibling `java.exe` so its standard output and error can be captured.
7. **Open app folder** opens the application's working directory. If an application requires sibling libraries or resources, copy them here manually in the structure its manifest expects. Importing a JAR does not automatically discover or copy external dependencies.
8. **Storage…** selects a new or empty folder. This relocates the entire managed storage tree, including JARs, profiles, catalog and application-created files. It works across drives using copy-and-verify instead of a same-drive rename. All Java applications launched here must exit first. The copy runs in the background while editing controls are disabled.

When Java applications are running, closing the main window hides the manager in the Windows notification area so output capture continues. Double-click its notification icon or choose **Open manager** to restore it. After the applications finish, use **Exit** from that icon or close the restored manager to quit. The manager tracks only processes started during its current session; close any Java processes started in earlier sessions or outside this manager before updating, deleting, or moving their files. It does not terminate applications automatically.


## Java output, log files, and failure windows

Named launch profiles and the one-time parameter editor now provide:

- **Redirect standard output and error to a log file** (enabled by default).
- **Log folder**, with a folder picker. Blank means `logs` within the managed application folder; relative paths are resolved from the app folder. Absolute paths support a separate folder or drive. Each launch creates a unique timestamped UTF-8 `.log` file instead of overwriting previous logs.
- **Open live output window when launching**, for viewing captured output immediately.

Java standard output and standard error are captured concurrently. They are tagged `[stdout]` and `[stderr]` in the combined log; tags mark chunks, not necessarily whole lines. Ordering between separate streams reflects receipt order. Application identity, profile, runtime path, start time and exit code are included. Output without a terminating newline is retained. Files are flushed as output arrives. If Java uses a different text encoding, configure its output for UTF-8 (for compatible runtimes, use JVM options such as `-Dfile.encoding=UTF-8`). Output that the JAR writes directly to its own application-managed files is separate from stdout/stderr and is not intercepted.

If Java cannot start, the managed JAR is missing, the requested log file cannot be created, or Java exits with a nonzero code, a separate **modeless output window** opens automatically. This is a normal resizable taskbar window created using `Show()`, not a modal dialog. You can keep using the manager, select/copy output, open the complete log in your associated editor, or close the output window. Closing that window does not terminate Java or interrupt its logging. If the application later exits unsuccessfully, the failure window reopens. **View output** reopens the most recent run for the selected app during the current manager session.

Even when writing a file is disabled, output remains available in a bounded in-memory preview, including failure diagnostics. The preview retains approximately the latest 256 KiB of text; the full file retains all captured output when enabled. If the requested file cannot be created, Java is not launched silently without logging; the failure window explains the problem. If a later disk write fails, the process continues, the preview remains available, and the failure is reported. Capture uses a hidden `java.exe` process instead of a separate console window. Interactive console input is not provided by this version; use noninteractive JARs or GUI JARs.

Default managed log folders move with application storage. Custom log folders outside storage remain at their configured location. Existing finished output windows follow moved log paths. Previously saved profiles are compatible and receive the new logging defaults; the old `ShowConsole` field is retained for file compatibility but is superseded by **Open live output window**.

For Java, a nonzero exit is treated as failure at any stage, including immediately after startup. Exit code zero is treated as success; the manager cannot infer application-specific failures that deliberately return zero. Windows notification-area behavior and output windows require desktop validation on Windows.

## File layout and recovery

Manager configuration: `%LOCALAPPDATA%\AppManagerForJava\settings.json`

Default storage: `%LOCALAPPDATA%\AppManagerForJava\Library`

```text
Library/
  catalog.json
  apps/
    <application-guid>/
      application.jar
      profiles/
        <profile-guid>.json
      logs/
        <UTC-timestamp>_<run-guid>.log
      <application files and data>
```

Catalog and profile writes use a temporary file and a same-directory replacement. Invalid JSON is reported rather than silently replaced with an empty catalog. A per-user single-instance guard prevents this manager from concurrently editing its catalog.

For relocation, files are copied to the empty destination and checked with SHA-256 before the configuration points to the new location. Only then are the old files removed. Nested destinations, occupied destinations, and linked files/folders encountered during traversal are rejected. If copying or configuration saving fails, original storage remains authoritative and a partial destination may remain; inspect/remove that partial copy before retrying. If cleanup fails after the configuration switches, the new storage remains active and a message identifies the old copies to remove manually. Abrupt process termination can leave temporary files or duplicated data; retain a backup of valuable application data.

Deletion commits catalog removal before removing files. If a locked file prevents cleanup, an error is shown and the remaining GUID folder can be removed manually from storage after the application exits. The manager never deletes the original imported JAR unless that original was manually placed inside the managed directory.

## Tests and manual validation

`tests/AppManagerForJava.Tests` exercises the actual storage and launch code: copying imports, separate named profile files, Unicode profile names, exact argument boundaries, JVM/application argument ordering, persistence after restart, valid/invalid updates, profile preservation, nested and occupied destination rejection, storage relocation with app data, deletion, and corrupt settings handling.

Before deployment on your Windows machine:

- Build the solution and run the console checks.
- Import and drag/drop an executable GUI JAR and a console JAR.
- Launch with a spaced path, a Unicode argument, and two differently named profiles; inspect the Java application's received arguments.
- Choose a JRE folder, restart the manager, and confirm the resolved Java runtime in a log.
- Enable file logging and a live output window. Close/reopen that window while the app is running and verify logging continues.
- Launch a JAR that exits with a nonzero code. Verify its failure window is modeless, closable, and contains stdout/stderr.
- With a JAR running, close the main window and restore it using the notification icon; confirm the process and file capture continue.
- Test missing Java, a missing managed JAR, and a non-executable JAR.
- Close Java, relocate to another drive, restart the manager, then launch again.
- Confirm updating or deleting an app is blocked while its tracked Java process runs.
- Check the icon grid, context menu, search, and layout at your preferred Windows display scaling.

The core tests also exercise JRE folder persistence, invalid-folder rejection, legacy profiles, log settings, real child-process output flooding on both streams, Unicode text, unterminated output, nonzero exits, unique log paths, missing executables, prelaunch errors, memory-only capture and invalid log destinations. The output-flood checks use a small C# child mode in the test executable. To additionally check real Java startup failure, set `JAVA_MANAGER_TEST_JAVA` to the path of a Java executable before running the tests; this invokes an imported JAR with a deliberately missing Main class and checks its captured stderr.

The core tests simulate relocation between directories. A real cross-drive Windows move, desktop rendering, Java console behavior, and Visual Studio interaction need Windows validation.

## Design references

- https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo
- https://learn.microsoft.com/dotnet/desktop/winforms/advanced/walkthrough-performing-a-drag-and-drop-operation-in-windows-forms
