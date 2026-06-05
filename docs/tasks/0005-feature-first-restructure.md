# 0005 — Feature-first restructure

**Status:** ✅ Done

Reorganize both projects from layer-first (`Views/`+`ViewModels/`, `Services/`+`Models/`) to **feature-first
vertical slices**, so a feature's views, view models, and services live together. Done **before** the emulator
view/edit/create work (0006–0008) so those files are born in the right place. The app is small and early-stage —
cheapest time to set the structure.

This is a pure move/rename: no behavior changes. A green build + full test run proves it.

## Why now

The emulator detail/edit/create work decomposes each screen into several small `UserControl`s plus extra view
models and Core services. Under layer-first folders, `Views/` and `ViewModels/` (and `Services/`) would bloat and
scatter one feature across multiple trees. Feature folders keep a feature cohesive and the solution scannable.

## Target structure (namespaces follow folders)

```
Remote.Adb.Desktop/
  App.axaml(.cs), Program.cs, ViewLocator.cs        (root)
  Shell/      MainWindow, MainWindowViewModel, NavigationDestination
  Common/     ViewModelBase, IActivatable, Converters/  (+ future reusable controls: BusyOverlay, PropertyRow)
  Emulators/  EmulatorView, EmulatorViewModel, EmulatorDeviceViewModel   (+ 0006's added views/VMs)
  Devices/    DevicesView, DevicesViewModel
  Tunnel/     TunnelView, TunnelViewModel
  Settings/   SettingsView, SettingsViewModel
  Theming/    IThemeApplier, ThemeApplier
  Themes/, Assets/

Remote.Adb.Core/
  Emulators/  EmulatorService, AvdCatalog, AvdConfigParser, AdbOutputParser, EmulatorOutputParser,
              AndroidVirtualDevice, AvdMetadata   (+ 0006's AvdConfiguration/reader/writer/store)
  Common/     IProcessRunner/ProcessRunner, IAndroidSdk/AndroidSdk, ProcessResult, ProcessLaunchException
  Settings/   ISettingsService/SettingsService          (or Common/ — settings is cross-feature)
  ServiceCollectionExtensions.cs                          (root)

Remote.Adb.Core.UnitTests/
  Emulators/  parser + service tests          Common/  process/sdk tests          Fakes/  LoggerFake
```

**Rules:** `Common/` holds genuinely cross-feature pieces (base classes, process/SDK plumbing, reusable
controls, converters); a piece used by exactly one feature lives *in* that feature. `Shell/` owns the app frame
and navigation; features don't reference each other.

## ViewLocator — no change needed

`ViewLocator.cs` does `param.GetType().FullName!.Replace("ViewModel", "View", Ordinal)`. With a feature
namespace like `Remote.Adb.Desktop.Emulators`, `…Emulators.EmulatorViewModel` → `…Emulators.EmulatorView`
(only the class-name `ViewModel` matches — feature names contain no `ViewModel` substring). So co-locating VM +
View in one namespace resolves with **zero locator changes**. (Avoid any future feature/folder named with a
`ViewModel`/`View` substring.)

## Migration checklist

- [ ] Desktop: create `Shell/`, `Common/`, `Emulators/`, `Devices/`, `Tunnel/`, `Settings/`; move each
      `*.axaml(.cs)` + `*ViewModel.cs` (and `Converters/`, base classes) into its slice.
- [ ] Core: create `Emulators/`, `Common/` (and `Settings/`); move services/models/parsers accordingly.
- [ ] Tests: mirror the Core feature folders.
- [ ] Update every `namespace` to match the new folder; update `using` directives.
- [ ] Update XAML: `x:Class`, and the `xmlns:vm/...="using:..."` + `x:DataType` references to the new namespaces.
- [ ] Confirm `App.axaml`/`ViewLocator` still resolve (no code change expected) and `AddRemoteAdbCore()` compiles.
- [ ] `src/Remote.Adb.slnx`: project paths are unchanged (folders are inside the csproj dirs), so no slnx edit
      needed for source; only the docs solution-folder list changes (handled in this task's roadmap step).

## Verification

- [x] `dotnet build src/Remote.Adb.slnx -c Release` — clean (0 warnings).
- [x] `dotnet test src/Remote.Adb.slnx` — all 14 green (the C# moves didn't break references).
- [x] `dotnet run --project src/Remote.Adb.Desktop` — app launches and navigates (ViewLocator resolves
      each screen view in its new namespace); confirmed.
