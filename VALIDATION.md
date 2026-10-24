# Validation results

Validated on 2026-10-24.

- All application C# source compiled with Roslyn against .NET 6 Windows Forms reference assemblies: passed, no compiler diagnostics.
- All application C# source compiled with Roslyn against .NET 8 Windows Forms reference assemblies: passed, no compiler diagnostics.
- Core console test executable compiled and executed on .NET 8: all 44 assertions passed, including real JVM startup-failure capture.
- Both stdout/stderr were drained under high simultaneous output, preserving Unicode and unterminated output; missing-runtime, invalid-log-directory, per-profile persistence and log relocation checks passed.
- A real Java runtime was invoked with an imported JAR whose Main class is missing; its nonzero exit and stderr were captured in both the file and preview.
- Project/manifest XML and solution project paths: checked.

The compiler checks supplied the SDK-generated global usings and an equivalent ApplicationConfiguration initializer outside the deliverable. The source solution itself retains the normal WinForms SDK generation of those items.

The environment could not run the normal .NET CLI/MSBuild build because process metadata access fails inside this execution container. Direct Roslyn compilation and test execution were used instead; this is not a claim of a completed Visual Studio or MSBuild solution build.

Windows desktop rendering, drag/drop events, notification-area behavior, modeless window interaction, actual Java process windows, .NET 6 runtime execution, published executables, and a physical cross-drive move were not exercised here. Follow the Windows validation checklist in README.md. No precompiled Windows executable is included.
