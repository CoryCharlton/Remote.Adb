# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project

A cross-platform desktop app to manage ADB (Android Debug Bridge) connections to a remote development server. The end goal is to unify, in one GUI, the workflows currently scattered across scripts (notably `adb-tunnel.bat`):

1. **SSH port forwarding** — open a reverse tunnel so a Windows-side `adb` server is reachable from the remote dev host (the remote's `adb` talks to `127.0.0.1:5037`, forwarded back to the local server).
2. **Emulator management** — start, manage, and create Android emulators.
3. **Remote device connection** — connect to Android devices over the network (e.g. Wi-Fi).

The app is .NET 10 / C# targeting `net10.0`. All functionality lives in a shared **`Remote.Adb.Core`** library exposed through two front-ends: a desktop GUI (**`Remote.Adb.Desktop`**, **Avalonia 12** + **MVVM** via CommunityToolkit.Mvvm) and a CLI (**`Remote.Adb.Console`**). It is early-stage; emulator management landed first.

## Roadmap & tasks

Planning lives in `docs/`, not here — consult it before starting work, and keep it current as milestones land. **`docs/` tracks future work only**: completed work is evidenced in git history and the code itself, so it is *not* archived here.

- `docs/ROADMAP.md` — vision, the three pillars, and a milestone table of **planned** work in priority order (the source of truth for what's next).
- `docs/tasks/<name>.md` — per-milestone breakdowns with checklists and verification notes. Named by topic, **not numbered** (priority/order lives in the ROADMAP, which links to them).

When a milestone lands, **delete its task doc and remove its ROADMAP row** — don't leave a ✅. Where a task doc and the code disagree, the code wins and the doc needs reconciling.

## Reference: the tunnel workflow being replaced

`adb-tunnel.bat` is the battle-tested script this app is meant to supersede. It encodes hard-won knowledge that should carry over to the C# implementation:

- The reverse tunnel is `ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 <host>`. `ExitOnForwardFailure` turns a silent bind failure into a visible non-zero exit.
- Kill the remote `adb` with `pkill -x adb`, **not** `adb kill-server` — `kill-server` does a localhost network round-trip that can hang on a stale/forwarded `127.0.0.1:5037`.
- Use the literal `127.0.0.1` (not `localhost`) for the forward target — Windows OpenSSH may resolve `localhost` to IPv6 `::1`, but the Windows `adb` server binds only IPv4, causing refused connections.
- A remote IntelliJ Android plugin respawns `adb` on Gradle sync and races the bind — hence the kill-then-bind-then-retry loop.

## Git Commits

Do not include co-author trailers or any mention of Claude in commit messages.

## Build & Run Commands

All projects live under `src/`; the solution is `src/Remote.Adb.slnx`.

```bash
# Build
dotnet build src/Remote.Adb.slnx --configuration Release

# Run the desktop app
dotnet run --project src/Remote.Adb.Desktop

# Run the console app
dotnet run --project src/Remote.Adb.Console -- <args>

# Run all tests
dotnet test src/Remote.Adb.slnx

# Run a specific test
dotnet test src/Remote.Adb.slnx --filter "FullyQualifiedName~ClassName"
```

The SDK is pinned to `10.0.0` (`rollForward: latestMinor`) via `src/global.json`. `src/Directory.Build.props` applies `LangVersion=preview`, `ImplicitUsings=enable`, and `Nullable=enable` solution-wide, and references JetBrains.Annotations and Nerdbank.GitVersioning (version derived from git history).

## Package management

Package versions are centrally managed via **Central Package Management** (`src/Directory.Packages.props`, `ManagePackageVersionsCentrally=true`). To add or update a dependency, add a `<PackageVersion Include="X" Version="N" />` in `Directory.Packages.props` and a version-less `<PackageReference Include="X" />` in the project (or `Directory.Build.props` for solution-wide refs). Never put `Version=` on a `<PackageReference>` — it errors with NU1008.

## Architecture

Projects in `src/Remote.Adb.slnx`:

- **`Remote.Adb.Core`** — class library holding all domain logic: models, services (process execution, Android SDK location, emulator management, later SSH/devices), and the `AddRemoteAdbCore()` DI registration extension. Both front-ends depend on it; it has no UI dependency.
- **`Remote.Adb.Desktop`** — the Avalonia desktop GUI (`WinExe`), MVVM.
- **`Remote.Adb.Console`** — the CLI front-end exposing the same Core functionality.
- **`Remote.Adb.Core.UnitTests`** — NUnit 4 tests for Core.

Both front-ends compose a `Microsoft.Extensions.DependencyInjection` service provider and call `AddRemoteAdbCore()` to register the shared services. Keep logic in Core; the GUI and CLI are thin shells over it.

The desktop app follows Avalonia's **MVVM** conventions:

- `Program.Main` builds a **.NET Generic Host** via `DesktopApplication.CreateBuilder<App>` (from the **CCSWE.Avalonia.Hosting** package), registers services, and runs it — the host owns DI and the classic desktop lifetime. `BuildAvaloniaApp()` mirrors the same Avalonia configuration (minus the host) for the XAML previewer; `WithDeveloperTools()` is added only in `DEBUG`. Fonts/type scale come from the CCSWE.Avalonia.Material theme, not a base theme.
- `App.OnFrameworkInitializationCompleted` is the composition root — the host injects the service provider (`IServiceProviderAccessor`), then the app applies the persisted theme/density and resolves the main view model from DI.
- **View resolution is source-generated** via the **CCSWE.Avalonia.ViewLocator** package: `ViewLocator` is an empty `[GenerateViewLocator(typeof(ViewModelBase))]` partial that the generator fills in at compile time, mapping each `XxxViewModel` to the `XxxView` in the **same namespace** (e.g. `EmulatorViewModel` → `EmulatorView` — so a VM and its view live together in one feature folder) and resolving the view from DI. View models must derive from `ViewModelBase` for the locator to match.
- `ViewModelBase` derives from CommunityToolkit.Mvvm's `ObservableObject`. Use the toolkit's source generators (`[ObservableProperty]`, `[RelayCommand]`) for bindable state and commands.
- **Compiled bindings are on by default** (`AvaloniaUseCompiledBindingsByDefault=true`) — XAML bindings need a declared `x:DataType`, and binding errors surface at compile time.

### Project organization — feature-first vertical slices

Both `Remote.Adb.Desktop` and `Remote.Adb.Core` are organized **feature-first**: a feature's views, view models, and services live together in a feature folder (`Emulators/`, `Devices/`, `Tunnel/`, `Settings/`), with `Common/` for genuinely cross-feature pieces (base classes, process/SDK plumbing, reusable controls, converters) and Desktop's `Shell/` for the app frame + navigation. Namespaces follow folders; a piece used by exactly one feature lives *in* that feature, not in `Common/`.

**Decompose views into small, single-purpose `UserControl`s.** A page is a thin shell that composes smaller pieces (a list view, a row card, a reusable overlay, a detail pane) — never one giant XAML file. Extract a piece even if it isn't reused yet.

When adding a screen: create `XxxView.axaml` (+ `.axaml.cs`) and `XxxViewModel.cs` (deriving from `ViewModelBase`) in the relevant feature folder of `Remote.Adb.Desktop`, and register the view in DI; the source-generated `ViewLocator` wires them by name. Domain models and services belong in the matching feature folder of `Remote.Adb.Core`.

### Scrollable views — inset the content, not the scroll container

The page inset for a scrollable view goes on the **scrolled content's `Margin`** (the inner `ItemsControl`/`StackPanel`), never on the `ScrollViewer` or an outer container that wraps it:

- **Padding on the `ScrollViewer`** leaves the bottom padding *outside* the scrollable extent (Avalonia's `ScrollContentPresenter`), so the last items are unreachable.
- **Margin on an outer container** (or on the `ScrollViewer`/`TabControl` itself) insets the scrollbar too, so it floats off the panel edge instead of sitting against it.

Pattern: a full-bleed scroll container (no margin/padding) → scrolled content carries the inset as its `Margin`. The scrollbar reaches the edge and every item is reachable. Also, a scrollable screen hosted in the CCSWE `DrawerPage` must bound its `ScrollViewer` via a `Grid` `*` row (or a `Panel`), not a `DockPanel`, or it won't get a bounded viewport height.

# Testing

Tests use **NUnit 4**. (No test project exists yet; follow these conventions when adding one.)

## Class organization

- Outer class name: `<ClassUnderTest>Tests`, decorated with `[SuppressMessage("ReSharper", "InconsistentNaming")]`. The outer class is NOT `sealed` — nested classes inherit from it.
- Nested classes group tests by method or scenario: `When_<MethodName>_Is_Called`, inheriting the outer class.
- Test methods describe expected behavior: `It_<expected_behavior>` (e.g., `It_Adds_UserAgent_Header`)

```csharp
public class SomeServiceTests
{
    public class When_GetAsync_Is_Called : SomeServiceTests
    {
        [Test]
        public async Task It_returns_expected_result() { ... }
    }
}
```

## Arrange-Act-Assert

Follow the AAA pattern. Use blank lines to separate sections — do **not** use `// Arrange`, `// Act`, `// Assert` comments.

## Mocking

- Use **Moq** for mocking
- `ILogger` should be mocked using the `LoggerFake` class, not `new Mock<ILogger>()`
- Prefer `ReturnsAsync(...)` and `ThrowsAsync(...)` over manually setting up async mock methods

# Coding Standards

Follow standard C# conventions ([source](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)).

## Formatting

- 4-space indentation (no tabs)
- Allman brace style — opening and closing braces on their own lines
- Always use curly braces for control flow (`if`, `for`, `foreach`, `while`, etc.) — never omit them for single-line bodies
- One statement per line; one declaration per line
- One blank line between members; no consecutive blank lines
- Space after flow-control keywords (`if (`, `for (`); no space after method names (`Method(`)

## Naming

- PascalCase: classes, methods, properties, constants, namespaces, public fields, record primary constructor parameters
- camelCase: local variables, parameters
- `_camelCase`: private fields (underscore prefix)
- `I` prefix for interfaces (e.g. `IEmulatorService`)
- Two-character acronyms are uppercase (`IO`, `UI`); longer acronyms use PascalCase (`Http`, `Json`)
- Use `nameof()` instead of string literals for member/property names

## File organization

- One type per file; file named `{TypeName}.cs`
- Partial classes use `{ClassName}.{Part}.cs`
- File-scoped namespaces (`namespace Foo;`), aligned with folder structure
- `using` directives outside namespace declarations, ordered `System` first, then third-party, then project namespaces
- A per-project `Usings.cs` holds global usings (e.g. a test project's global `using NUnit.Framework;`)

## Access modifiers

- Always explicit (no implicit `private` or `internal`)
- `internal` for implementation details
- `[PublicAPI]` (JetBrains.Annotations, referenced solution-wide) on intentionally public API surface
- `[ExcludeFromCodeCoverage]` on composition-only types (the `AddRemoteAdbCore()` registration, the Avalonia `App` composition root, and similar)

## Language style

- Use `var` for all local variables where the type can be inferred
- Use language keywords for built-in types (`string`, `int`, not `String`, `Int32`)
- Prefer string interpolation (`$"..."`) over concatenation
- Use `&&`/`||`, not `&`/`|`, for logical comparisons
- Use `async`/`await` for async code; avoid `.Result` or `.Wait()`
- Prefer expression-bodied members for single-line getters/methods
- Nullable reference types are enabled solution-wide (`Nullable=enable` in `Directory.Build.props`); respect nullability annotations

## Class member order

Within a class, group members by kind in this order, and alphabetize strictly by name within each group regardless of access modifier:

1. Constants / `static readonly` fields
2. Instance fields
3. Constructors
4. Properties
5. Methods

Access modifiers do not affect ordering — when you go looking for `GetFoo()` you don't care whether it's public, internal, or private, so `CreateFoo()` comes before `GetFoo()` regardless. Nested types go at the bottom of the file, after all members of the outer type.

## Frozen collections for static lookups

Any `static readonly` `HashSet<T>` or `Dictionary<TKey, TValue>` that is never mutated after construction should be a `FrozenSet<T>` / `FrozenDictionary<TKey, TValue>` (`System.Collections.Frozen`), built via `.ToFrozenSet(comparer)` / `.ToFrozenDictionary()`. Lookups are faster and the frozen type signals "immutable lookup" at the type level.

## XML documentation

In library projects that enable `GenerateDocumentationFile` (e.g. `Remote.Adb.Core`), document public/internal types and members; use `<inheritdoc />` for interface implementations where the interface doc suffices. Elsewhere, add docs only where they clarify intent.