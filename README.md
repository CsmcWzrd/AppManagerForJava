# App Manager for Java

A Microsoft Visual C# Windows Forms application for managing and launching Java JAR applications. Open `AppManagerForJava.sln` in Visual Studio with the **.NET desktop development** workload. Set **AppManagerForJava** as the startup project and build/run it.

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

Or run `build.cmd` to build and run the core checks. The tests are an ordinary console program with a nonzero exit code on failure, not a `dotnet test` test-adapter project.

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
6. **Java…** sets an explicit `java.exe` path or the executable name resolved through `PATH`. For normal console output, use `java.exe` and keep **Show Java console** enabled. Using `javaw.exe` intentionally suppresses the Java console regardless of that checkbox.
7. **Open app folder** opens the application's working directory. If an application requires sibling libraries or resources, copy them here manually in the structure its manifest expects. Importing a JAR does not automatically discover or copy external dependencies.
8. **Storage…** selects a new or empty folder. This relocates the entire managed storage tree, including JARs, profiles, catalog and application-created files. It works across drives using copy-and-verify instead of a same-drive rename. All Java applications launched here must exit first. The copy runs in the background while editing controls are disabled.

Running applications continue if the manager closes. The manager tracks only processes started during its current session; close any Java processes started in earlier sessions or outside this manager before updating, deleting, or moving their files. It does not terminate applications automatically.

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
- Test missing Java, a missing managed JAR, and a non-executable JAR.
- Close Java, relocate to another drive, restart the manager, then launch again.
- Confirm updating or deleting an app is blocked while its tracked Java process runs.
- Check the icon grid, context menu, search, and layout at your preferred Windows display scaling.

The core tests simulate relocation between directories. A real cross-drive Windows move, desktop rendering, Java console behavior, and Visual Studio interaction need Windows validation.

## Design references

- https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo
- https://learn.microsoft.com/dotnet/desktop/winforms/advanced/walkthrough-performing-a-drag-and-drop-operation-in-windows-forms
