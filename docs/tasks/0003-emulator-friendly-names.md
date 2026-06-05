# 0003 — Emulator friendly names + row polish

**Status:** ✅ Done

The emulator list currently shows the raw AVD **id** (e.g. `Television_1080p_16.0`). Each AVD also has a
**display name** — the one Android Studio's Device Manager shows — plus a device-category tag. Surface
those, and tidy up the row presentation.

## Background — where the friendly name lives

- `emulator -list-avds` and `adb -s <serial> emu avd name` only return the **AvdId** (the underscore-y
  identifier). No flag exposes the friendly name.
- Each AVD's `config.ini` holds:
  - `AvdId=...` — the id returned above
  - `avd.ini.displayname=...` — the **friendly name** (e.g. `Television (1080p) 16.0`)
  - `tag.displaynames=...` — device category (e.g. `Google TV`, `Wear OS 6.0`) → candidate for a row icon
- Configs live under the AVD home: `ANDROID_AVD_HOME`, else `%USERPROFILE%\.android\avd` (`~/.android/avd`).
  The `.avd` folder name is **not** necessarily the AvdId — map via the `<AvdId>.ini` `path=` entry, or
  just read `AvdId` out of each `config.ini`.

## Tasks

### Core
- [x] `IAvdCatalog`/`AvdCatalog` in Core: resolve the AVD home (`ANDROID_AVD_HOME` →
      `ANDROID_SDK_HOME/.android/avd` → `~/.android/avd`), parse every `*.avd/config.ini`,
      build a map `AvdId → AvdMetadata(DisplayName, Tag)`. Pure parsing split into `AvdConfigParser`.
      Tolerates missing keys (older AVDs) and a missing AVD home (empty map).
- [x] Add `DisplayName` (fallback to id) and `Tag` to `AndroidVirtualDevice`.
- [x] `EmulatorService.ListAsync` joins `emulator -list-avds` ids to the catalog. `StartAsync` still
      takes the **id**, `StopAsync` the **serial** — only the displayed label changes.
- [x] Unit tests: `AvdConfigParser` (displayname present / absent / no AvdId) + the catalog join in
      `ListAsync` (14 tests total, all passing).

### Desktop row polish
- [x] Row title bound to `DisplayName`; id (+ tag + serial) as secondary text.
- [x] Replaced `Running: True/False` with a status badge (`PrimaryContainer` "Running" / neutral "Stopped").
- [x] Consistent row height (`MinHeight=56`, always-present subtitle line).
- [x] Device tag (`Google TV` / `Wear OS …`) shown as subtitle text.
- [x] Device-type **glyph** — Phosphor (regular) geometry vendored per-app in `Themes/Icons.axaml`
      (MIT, attributed in `Assets/Icons/PHOSPHOR-LICENSE.txt`), `Tag` → glyph via
      `DeviceTagToGeometryConverter`, rendered with built-in `PathIcon`. No icon library / no theme-lib
      coupling (decision: a generic Phosphor control is deferred unless duotone is needed).

### Console
- [x] `emulator list` prints `DisplayName (AvdId) — Tag  [status]`.

## Verification
- [x] `dotnet test` — 14 tests pass.
- [x] `emulator list` shows `Television (1080p) 16.0 (Television_1080p_16.0) — Google TV [running (...)]`;
      verified against a fake SDK + fake AVD home where the `.avd` dir names differ from the `AvdId`.
- [x] Desktop rows — launched and visually confirmed.
