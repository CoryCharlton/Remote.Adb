# 0011 — Settings persistence (theme + density)

**Status:** ✅ Done · prerequisite for [0010 SDK/JAVA_HOME overrides](0010-settings-sdk-overrides.md)

Application settings now persist across sessions. Previously `SettingsService` held the theme in memory with
`Load()`/`Save()` TODO no-ops, so the choice was lost on restart. This adds a real JSON-on-disk store and makes
the **theme** and the new CCSWE.Avalonia.Material **density** durable. The layer is designed so future settings
(SDK/JAVA_HOME overrides, tunnel connection details) drop in by adding a field.

## Core — the persistence layer (`Remote.Adb.Core/Settings/`)

- **`SettingsModel`** — the serializable POCO / on-disk schema and single source of defaults
  (`Theme = Dark`, `Density = Compact`). `[JsonExtensionData]` carries unknown keys so an older build won't
  drop a newer build's settings when it re-saves.
- **`AppDensity`** — UI-agnostic enum (`Normal`/`Compact`) mirroring the library's `DensityStyle` (Core has no
  UI dependency). The desktop maps it to `DensityStyle`.
- **`ISettingsStore` / `SettingsStore`** — tolerant `Load()` (missing/corrupt/IO → defaults, never throws) and
  atomic `Save()` (temp file + `File.Move(overwrite)`), under `{ApplicationData}/Remote.Adb/settings.json`.
  Enums serialize as **names** (camelCase, indented) so the file is human-readable and stable against enum
  reordering. Reflection-based (the app isn't trim/AOT-published); a `// NOTE:` marks where a source-generated
  context would go if that changes. A `filePath` ctor param (defaulting to `DefaultFilePath()`) is the test seam.
- **`SettingsService`** — now injects `ISettingsStore`, loads the model once on construction, and re-saves the
  whole model on each change. `Theme` and `Density` keep the change-detection guard (no write on a no-op set).
- `ISettingsService` gains `Density`; `ISettingsStore` is registered in `AddRemoteAdbCore()`.

## Desktop — density applier + UI

- **`IDensityApplier` / `DensityApplier`** (`Theming/`) — mirrors `IThemeApplier`: flips
  `Application.Current.Styles.OfType<MaterialTheme>().First().DensityStyle` live (no restart).
- **`App.axaml.cs`** applies the persisted **theme and density** at startup. **`SettingsView`** gains a
  *Compact density* toggle alongside *Light mode*, bound to `SettingsViewModel.IsCompactDensity`.

## Extending the layer (future settings)

Add a property + default to `SettingsModel`, mirror a member on `ISettingsService`/`SettingsService`, and (for
UI settings) surface it in the Desktop VM. No store/serializer changes. This is how 0010's nullable
`SdkRoot`/`AvdHome`/`JavaHome` and a future nested `TunnelSettings` plug in.

## Tasks

- [x] Core: `SettingsModel`, `AppDensity`, `ISettingsStore`/`SettingsStore`, rewired `SettingsService`, DI.
- [x] Desktop: `IDensityApplier`/`DensityApplier`, startup apply, `IsCompactDensity` + Settings toggle.
- [x] Core tests: `SettingsStoreTests` (defaults on missing/corrupt/null, round-trip, string enums, unknown-key
      preservation, dir creation, atomic overwrite), `SettingsServiceTests` (load on ctor, persist on change,
      no-save on unchanged).

## Verification

- [x] `dotnet build` + `dotnet test` green.
- [ ] **Pending visual check** (Desktop, WSLg/Windows): toggle Light mode + Compact density off → relaunch →
      both persisted (`%APPDATA%\Remote.Adb\settings.json` shows `"theme":"Light"`, `"density":"Normal"`);
      delete the file → relaunch → defaults (Dark + Compact); hand-corrupt the file → relaunch → defaults, no crash.
