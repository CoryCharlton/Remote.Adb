# 0001 — Project restructure

**Status:** ✅ Done

Split the template scaffold into a shared core library with two thin front-ends, so the GUI and CLI
expose the same functionality.

## Tasks

- [x] Rename library `Remote.Adb.Shared` → `Remote.Adb.Core`; drop the `Class1.cs` placeholder.
- [x] Rename GUI `CCSWE.Remote.Adb` → `Remote.Adb.Desktop` (folder, csproj, namespaces, XAML).
- [x] Reference `Remote.Adb.Core` from `Remote.Adb.Desktop` and `Remote.Adb.Console`.
- [x] Delete the orphaned `Playground` project.
- [x] Update `src/Remote.Adb.slnx` project paths.
- [x] Update `CLAUDE.md` and `README.md` for the new structure.
- [x] `dotnet build src/Remote.Adb.slnx -c Release` is clean.

## Outcome

Solution layout: `Remote.Adb.Core` (logic) ← `Remote.Adb.Desktop` (Avalonia GUI) +
`Remote.Adb.Console` (CLI); `Remote.Adb.Core.UnitTests` covers Core.
