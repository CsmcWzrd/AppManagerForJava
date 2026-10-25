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

## Startup icon fix

The attached Windows exception report identifies deferred ImageList bitmap creation as the crash site. Source review found `using var bitmap` inside `RefreshApps`, which disposed the bitmap immediately after `Images.Add`. The fix transfers ownership to `OwnedImageList` and retains the original bitmaps until refresh or shutdown.

A Windows regression console project is included in the solution and `build.cmd`. It tests delayed handle creation, bitmap cleanup, repeated image rebuilds, and opening/searching the actual main window with imported apps. Its native Windows checks cannot run in this Linux environment; compilation is checked separately. The preceding 44 core checks cover the earlier storage/launch changes and do not validate native Windows image rendering.

For this icon fix, the updated application compiled against both .NET 6 and .NET 8 Windows reference assemblies without compiler diagnostics. The new Windows regression program also compiled against .NET 8 Windows references without diagnostics. These are compile checks; the native regression program was not executed here.

## Launchpad visual redesign — 2026-10-25

The main window now uses custom painted application cards, a circular launcher button, a rounded command panel, and a live application-information strip. All former toolbar/profile commands have been retained in the launcher, alongside keyboard and context-menu access. The original bitmap lifetime fix remains in place.

Windows regression checks have been extended to cover card rendering configuration, absence of standard menu/toolbar controls, opening/closing the circular menu, retained command availability, and hover information updates without selection changes. Native Windows interaction and visual layout still need execution on Windows, including 100%, 150%, and 200% scaling. No Windows screenshot or native visual test is claimed from this Linux environment.

For the 2026-10-25 redesign, the application source compiled with no diagnostics against .NET 6 and .NET 8 Windows reference assemblies. The updated Windows regression project compiled against .NET 8 references. Native GUI execution was not available; the new Windows interaction checks are included for execution on Windows. Storage and launch implementation files were not changed in this redesign.
