# Remote.Adb

A cross-platform desktop app (with a matching console) that bridges Android development between your
local workstation and a remote dev machine: it opens the SSH reverse tunnel that lets a remote `adb`
reach your locally-attached devices and emulators, and manages those emulators along the way.

## Why this exists

I moved my day-to-day development to a remote Linux machine, but my Android phones (plugged in over
USB) and emulators still live on my local Windows workstation. The remote machine's `adb` — and the
IDE running on it — can't see those devices.

The fix is to run the `adb` server **locally** (where the devices are) and open an SSH **reverse**
tunnel so the remote's `adb` connects back through it to the local server. That worked as a pile of
shell scripts, but it was fiddly to start, easy to get wrong, and didn't help with emulators.
Remote.Adb turns that workflow into one app — and folds in emulator management and device listing
while it's there.

## What it does

1. **SSH reverse tunnel** — opens `ssh -N -R <remote>:127.0.0.1:<local> <host>` so the remote dev
   host's `adb` reaches your local `adb` server, with the kill-then-bind-then-retry handling the old
   script learned the hard way. Detects when the local server dies underneath the tunnel and faults
   instead of looking healthy. Can reconnect automatically on launch.
2. **Emulator management** — list, start, stop, create, view/edit, and delete Android Virtual Devices.
3. **Devices** — lists the devices `adb` currently sees (`adb devices -l`). Connecting to a device
   over the network (Wi-Fi) is still on the [roadmap](docs/ROADMAP.md).

> **Status:** early but usable. The SSH tunnel and emulator management are working; the Devices page
> lists devices but can't yet *connect* over the network. See [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Requirements

Run Remote.Adb on the machine that has your Android devices and emulators and the `adb` server —
typically your **local workstation** (Windows in my setup, though the app is cross-platform):

- **[.NET 10 runtime](https://dotnet.microsoft.com/download)** to run it (the **SDK** if you're building from source).
- The **Android SDK** — `adb` and `emulator` are found via `ANDROID_HOME`/`ANDROID_SDK_ROOT`, the
  platform default (e.g. `%LOCALAPPDATA%\Android\Sdk`), or an override you set in the app. Emulator
  creation also uses the `cmdline-tools` (`avdmanager`/`sdkmanager`).
- An **OpenSSH client** (`ssh`) on your `PATH` (the built-in Windows OpenSSH is fine).
- **Key-based SSH access to the remote host that works non-interactively.** The tunnel runs `ssh`
  in `BatchMode` — it will not answer a password or passphrase prompt — so load your key into an
  ssh-agent (or use a passphraseless key). Verify with `ssh <host> echo ok` returning without a
  prompt. A first-seen host key is trusted automatically (`StrictHostKeyChecking=accept-new`).
- On the **remote host**: `ssh`, `pkill`, and the dev-side `adb` client (the side that connects
  through the tunnel).

## Install & run

There are no published binaries yet — build from source:

```bash
# Build (Release)
dotnet build src/Remote.Adb.slnx --configuration Release

# Run the desktop app
dotnet run --project src/Remote.Adb.Desktop

# Run the console
dotnet run --project src/Remote.Adb.Console -- tunnel
```

## Using it

### Desktop

- **Tunnel** — enter the remote host (and, if needed, the remote/local ports — both default to
  `5037`), then **Connect**. Toggle *Connect automatically at launch* to have the tunnel come up
  with the app. The status card shows the live state (and the real `ssh` error if it can't connect),
  and a **Restart adb** action bounces the local `adb` server without dropping the tunnel.
- **Emulators** — your AVDs with their running state; start/stop/create/edit/delete from here.
- **Devices** — what `adb` currently sees, auto-refreshed while the page is open.

App settings (the remote host and ports, SDK/JDK overrides, theme) are persisted under your
app-data folder.

### Console

```bash
# Open the tunnel (host falls back to the saved setting); Ctrl+C closes it
dotnet run --project src/Remote.Adb.Console -- tunnel [host]

# Emulators
dotnet run --project src/Remote.Adb.Console -- emulator list
dotnet run --project src/Remote.Adb.Console -- emulator start <avd-name>
```

Run the console with no arguments for the full command list.

---

## Development

Contributions and local hacking start here.

### Tech stack

- **.NET 10** / C# (`net10.0`), SDK pinned to `10.0.0` via `global.json` (`rollForward: latestMinor`)
- **[Avalonia 12](https://avaloniaui.net/)** for the cross-platform desktop UI
- **MVVM** via [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) (`[ObservableProperty]`, `[RelayCommand]` source generators)
- **[.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host)** — the desktop head boots through **CCSWE.Avalonia.Hosting**, which wraps Avalonia's `AppBuilder` in the host so DI, configuration, and lifetime are wired the standard way
- **CCSWE.Avalonia.ViewLocator** — a source generator builds the view-model → view map at compile time (no reflection); **CCSWE.Avalonia.Material** supplies the Material 3 theme, type scale, and controls
- **[Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)** wiring the shared services into both front-ends
- Solution-wide `ImplicitUsings=enable`, `Nullable=enable`
- [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) — versions live in `src/Directory.Packages.props`
- Versioning derived from git history via [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)

### Projects

The solution is `src/Remote.Adb.slnx`:

- **`Remote.Adb.Core`** — class library with all domain logic (models, services, DI registration). Both front-ends depend on it; no UI dependency.
- **`Remote.Adb.Desktop`** — the Avalonia desktop GUI (`WinExe`).
- **`Remote.Adb.Console`** — the command-line front-end.
- **`Remote.Adb.Core.UnitTests`** / **`Remote.Adb.Desktop.UnitTests`** — NUnit 4 tests (plain NUnit + Moq; UI types sit behind seams so no Avalonia.Headless is needed).

```bash
dotnet test src/Remote.Adb.slnx
```

### Architecture

All functionality lives in **`Remote.Adb.Core`**; the desktop GUI and console are thin front-ends that
compose a DI service provider (`AddRemoteAdbCore()`) and drive the same services. Both projects are
organized **feature-first** (`Adb/`, `Emulators/`, `Devices/`, `Tunnel/`, `Settings/`), with `Common/`
for cross-feature plumbing.

The desktop app follows Avalonia's MVVM conventions:

- `Program.Main` builds a `DesktopApplication` host (`DesktopApplication.CreateBuilder<App>`), registers services, and runs it — the host owns DI and the classic desktop lifetime. `BuildAvaloniaApp()` mirrors the same configuration (minus the host) for the XAML previewer; developer tools are added only in `DEBUG`.
- `App.OnFrameworkInitializationCompleted` is the composition root — the host injects the service provider, then the app applies the persisted theme/density and resolves the main view model from DI.
- **View resolution is convention-based** via a source-generated `ViewLocator`: a `[GenerateViewLocator(typeof(ViewModelBase))]` partial class is filled in at compile time, mapping each `FooViewModel` to the `FooView` in the **same namespace** (matching the feature-first layout) and resolving the view from DI. View models must derive from `ViewModelBase`.
- **Compiled bindings are on by default** — XAML bindings need a declared `x:DataType`, and binding errors surface at compile time.
- View models and services stay UI-free: anything that would touch an Avalonia control or threading primitive goes behind a `Common/` abstraction with a UI-side adapter (e.g. `ITimerFactory`, `INotificationSink`, `IUiDispatcher`), keeping them unit-testable with plain NUnit + Moq.

To add a screen: in the relevant feature folder of `Remote.Adb.Desktop`, create `FooView.axaml` (+ `.axaml.cs`) and `FooViewModel.cs` (deriving from `ViewModelBase`), and register the view in DI; the generated `ViewLocator` wires them together by name. Domain models and services belong in the matching feature folder of `Remote.Adb.Core`.

### SSH/adb gotchas the tunnel handles

The reverse-tunnel workflow has a few non-obvious requirements the implementation bakes in:

- The reverse tunnel is `ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 <host>`. `ExitOnForwardFailure` turns a silent bind failure into a visible non-zero exit.
- Kill the remote `adb` with `pkill -x adb`, **not** `adb kill-server` — `kill-server` does a localhost network round-trip that can hang on a stale/forwarded `127.0.0.1:5037`.
- Use the literal `127.0.0.1` (not `localhost`) for the forward target — Windows OpenSSH may resolve `localhost` to IPv6 `::1`, but the Windows `adb` server binds only IPv4, causing refused connections.
- A remote IntelliJ Android plugin respawns `adb` on Gradle sync and races the bind — hence the kill-then-bind-then-retry loop.

The app adds non-interactive `ssh` options (`BatchMode`, `ConnectTimeout`, `GSSAPIAuthentication=no`,
`StrictHostKeyChecking=accept-new`) so it never hangs on a prompt from a windowless GUI process.

## License

[MIT](LICENSE.md) © 2026 Cory Charlton
