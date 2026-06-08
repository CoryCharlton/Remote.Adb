# Remote.Adb

A cross-platform tool for managing ADB (Android Debug Bridge) connections to a remote development server, with both a desktop GUI and a console front-end over a shared core library.

The goal is to unify the workflows currently scattered across shell scripts (notably [`src/adb-tunnel.bat`](src/adb-tunnel.bat)):

1. **Emulator management** — list, start, stop, create, view/edit, and delete Android emulators.
2. **SSH port forwarding** — open a reverse tunnel so a Windows-side `adb` server is reachable from the remote dev host (the remote's `adb` talks to `127.0.0.1:5037`, forwarded back to the local server).
3. **Remote device connection** — connect to Android devices over the network (e.g. Wi-Fi).

> **Status:** early development. The multi-head structure is in place and **emulator management** (list / start / stop / create / view / edit / delete) has landed; SSH tunneling and network device connection are next. See [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Tech stack

- **.NET 10** / C# (`net10.0`), SDK pinned to `10.0.0` via `global.json` (`rollForward: latestMinor`)
- **[Avalonia 12](https://avaloniaui.net/)** for the cross-platform desktop UI
- **MVVM** via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) (`[ObservableProperty]`, `[RelayCommand]` source generators)
- **[.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host)** — the desktop head boots through **CCSWE.Avalonia.Hosting**, which wraps Avalonia's `AppBuilder` in the host so DI, configuration, and lifetime are wired the standard way
- **CCSWE.Avalonia.ViewLocator** — a source generator builds the view-model → view map at compile time (no reflection); **CCSWE.Avalonia.Material** supplies the Material 3 theme, type scale, and controls
- **[Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)** wiring the shared services into both front-ends
- Solution-wide `LangVersion=preview`, `ImplicitUsings=enable`, `Nullable=enable`
- [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) — versions live in `src/Directory.Packages.props`
- Versioning derived from git history via [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)

## Projects

The solution is `src/Remote.Adb.slnx`:

- **`Remote.Adb.Core`** — class library with all domain logic (models, services, DI registration). Both front-ends depend on it; no UI dependency.
- **`Remote.Adb.Desktop`** — the Avalonia desktop GUI (`WinExe`).
- **`Remote.Adb.Console`** — the command-line front-end.
- **`Remote.Adb.Core.UnitTests`** — NUnit 4 tests for the core library.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- The Android SDK (for emulator management — `emulator` and `adb` from platform-tools), and an `ssh` client for the tunnel workflows

### Build & run

```bash
# Build (Release)
dotnet build src/Remote.Adb.slnx --configuration Release

# Run the desktop app
dotnet run --project src/Remote.Adb.Desktop

# Run the console app
dotnet run --project src/Remote.Adb.Console -- emulator list

# Run tests
dotnet test src/Remote.Adb.slnx
```

## Architecture

All functionality lives in **`Remote.Adb.Core`**; the desktop GUI and console are thin front-ends that compose a DI service provider (`AddRemoteAdbCore()`) and drive the same services.

The desktop app follows Avalonia's MVVM conventions:

- `Program.Main` builds a `DesktopApplication` host (`DesktopApplication.CreateBuilder<App>`), registers services, and runs it — the host owns DI and the classic desktop lifetime. `BuildAvaloniaApp()` mirrors the same configuration (minus the host) for the XAML previewer; developer tools are added only in `DEBUG`.
- `App.OnFrameworkInitializationCompleted` is the composition root — the host injects the service provider, then the app applies the persisted theme/density and resolves the main view model from DI.
- **View resolution is convention-based** via a source-generated `ViewLocator`: a `[GenerateViewLocator(typeof(ViewModelBase))]` partial class is filled in at compile time, mapping each `XxxViewModel` to the `XxxView` in the **same namespace** (matching the feature-first layout) and resolving the view from DI. View models must derive from `ViewModelBase`.
- **Compiled bindings are on by default** — XAML bindings need a declared `x:DataType`, and binding errors surface at compile time.

To add a screen: in the relevant feature folder of `Remote.Adb.Desktop`, create `XxxView.axaml` (+ `.axaml.cs`) and `XxxViewModel.cs` (deriving from `ViewModelBase`), and register the view in DI; the generated `ViewLocator` wires them together by name. Domain models and services belong in the matching feature folder of `Remote.Adb.Core`.

## The tunnel workflow being replaced

`src/adb-tunnel.bat` is the battle-tested script this app is meant to supersede. It encodes hard-won knowledge that carries over to the C# implementation:

- The reverse tunnel is `ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 <host>`. `ExitOnForwardFailure` turns a silent bind failure into a visible non-zero exit.
- Kill the remote `adb` with `pkill -x adb`, **not** `adb kill-server` — `kill-server` does a localhost network round-trip that can hang on a stale/forwarded `127.0.0.1:5037`.
- Use the literal `127.0.0.1` (not `localhost`) for the forward target — Windows OpenSSH may resolve `localhost` to IPv6 `::1`, but the Windows `adb` server binds only IPv4, causing refused connections.
- A remote IntelliJ Android plugin respawns `adb` on Gradle sync and races the bind — hence the kill-then-bind-then-retry loop.

## License

[MIT](LICENSE.md) © 2026 Cory Charlton
