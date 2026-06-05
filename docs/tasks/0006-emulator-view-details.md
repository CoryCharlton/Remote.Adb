# 0006 — Emulator view details

**Status:** ✅ Done — Core + console verified; desktop detail pane pending visual confirmation. (Built on [0005](0005-feature-first-restructure.md).)

Clicking an emulator card opens a **read-only details view** for that AVD. First step of the
**view → edit → create** progression — each builds on the prior (view renders read-only, edit makes the same
fields editable, create starts blank with an image/device picker). **The full config model lands here** so the
later tasks reuse it without model churn.

## Current state

- Rows are `theme:Card`s, already clickable: each binds a stub `ViewDetailsCommand(EmulatorDeviceViewModel)`
  on `EmulatorViewModel` (empty body) so the hover/pressed affordance works today.
- `EmulatorDeviceViewModel` carries `DisplayName`, `Name` (AvdId), `Tag`, `IsRunning`, `Serial`.
- `AvdConfigParser` parses only `AvdId` / `avd.ini.displayname` / `tag.displaynames`; `AvdCatalog` builds the
  `AvdId → AvdMetadata` map for the list.

## UX — in-page master–detail (decided)

Clicking a card swaps the Emulators list for a detail pane **on the same screen**, with a Back affordance.
Reuses `ViewModelBase` / `ViewLocator` / page-owns-child; **no modal or new-navigation infra**. Edit (0007) and
create (0008) become modes of this same pane.

_Alternatives considered & rejected:_ a modal dialog window (the app has no dialog infra — heavier, against the
drawer-only shell); a new top-level nav destination (details are per-AVD and transient — wrong as a permanent peer).

## Core — AVD config model + round-trip IO (under `Core/Emulators/`)

Mirror the existing static, I/O-free parser pattern (`AdbOutputParser`/`EmulatorOutputParser`); reuse
`IProcessRunner` and `AvdCatalog`'s AVD-home resolution.

- **`IniDocument`** + `IniLine { Kind: Pair|Comment|Blank, Key, Value, Raw }` — ordered; preserves comments,
  blanks, and unknown keys verbatim via `Raw`. The round-trip backbone.
- **`IniParser.Parse(text) → IniDocument`** — generic `key=value` engine (split on first `=`, `\r\n`-safe),
  order-preserving. (Semantic concerns like "no AvdId" live above it.)
- **`AvdConfiguration`** — typed, **grouped** accessors over the document(s); also carries the optional sibling
  `<AvdId>.ini` document (`path=` / `target=`). `ToMetadata()` projects the existing 3-field `AvdMetadata`.
- **`AvdConfigReader.Read(configIni, siblingIni?) → AvdConfiguration?`** — null when no `AvdId`.
- **`AvdConfigWriter.Write(document, changes) → string`** — replace touched keys in place, append new keys
  deterministically, re-emit everything else from `Raw` (the unknown-key-preservation guarantee). Introduced
  here (with round-trip tests) so the contract is locked before edit (0007) depends on it.
- **`AvdValueConventions`** — shared `2G↔2048`, `yes↔true` conversions used by parser, writer, and tests.
- **`IAvdConfigStore`** (+ impl; singleton in `AddRemoteAdbCore()`) — `Read(avdId) → AvdConfiguration?` now
  (resolve AVD home, read `<id>.avd/config.ini` + sibling `<id>.ini`); `Write(...)` added in 0007. Keeps the
  catalog list-only.
- **Keep** `AvdMetadata` + `AvdConfigParser` unchanged — the list hot path stays fast (3-field), full parse
  only when opening one AVD.

### Field groups (typed; every other key still preserved in the raw map)

| Group | config.ini keys |
|---|---|
| Identity | `AvdId`, `avd.ini.displayname`, `tag.displaynames`, `tag.id` |
| System image | `image.sysdir.1` (→ derive API level + variant), `abi.type`, `target` |
| Device | `hw.device.name`, `hw.device.manufacturer` |
| Display | `hw.lcd.width`, `hw.lcd.height`, `hw.lcd.density` |
| Memory & storage | `hw.ramSize`, `vm.heapSize`, `disk.dataPartition.size`, `hw.sdCard`, `sdcard.size` |
| CPU | `hw.cpu.ncore`, `hw.cpu.arch` |
| Graphics & boot | `hw.gpu.enabled`, `hw.gpu.mode` |
| Sensors & I/O | `hw.camera.front`, `hw.camera.back`, `hw.gps`, `hw.keyboard`, `hw.initialOrientation`, `hw.audioInput` |
| Skin | `skin.name`, `skin.path`, `showDeviceFrame` |
| Network | `runtime.network.speed`, `runtime.network.latency` |
| Location | `path`, `target` (from the sibling `<AvdId>.ini`) |

## Desktop — decomposed views (under `Desktop/Emulators/`, shared bits in `Common/`)

`EmulatorViewModel` gains `[ObservableProperty] EmulatorDetailsViewModel? SelectedDetail`. `ViewDetails(device)`
→ `IAvdConfigStore.Read(device.Name)` → `SelectedDetail = new EmulatorDetailsViewModel(config)`. Back =
`SelectedDetail = null`. The VM does no file I/O (calls the store via DI).

Each block is its own small `UserControl` (`x:DataType`), per the decomposition convention:

- `EmulatorView` — **thin shell**: shows `EmulatorListView` when `SelectedDetail == null`, else
  `ContentControl Content="{Binding SelectedDetail}"` (ViewLocator → `EmulatorDetailsView`).
- `EmulatorListView` (`EmulatorViewModel`) — the list UI extracted from today's `EmulatorView` (ScrollViewer +
  ItemsControl + FAB + status).
- `EmulatorCardView` (`EmulatorDeviceViewModel`) — the row card extracted from the inline `DataTemplate`.
  **Re-home the commands:** the `#Root`-reached Start/Stop/ViewDetails bindings become an ancestor binding
  (`$parent[ItemsControl]`/named list) or move onto the row VM.
- `BusyOverlay` (Common) — reusable scrim + `CircularProgressIndicator`, bound to an `IsActive` property.
- `EmulatorDetailsView` (`EmulatorDetailsViewModel`) — back bar (arrow + AVD display name) over grouped fields.
- `PropertyGroupView` + `PropertyRow` (Common) — reusable section header + label→value row (read-only now,
  reused editable by 0007); or a `DetailGroup` record + data-driven `ItemsControl`.

## Console

- `emulator info <avd>` — resolve via `IAvdConfigStore.Read`, print the grouped fields (mirror the
  `emulator list` formatting). Add a usage line.

## Tasks

- [x] Core: `IniDocument`/`IniParser`, `AvdConfiguration`, `AvdConfigReader`, `AvdConfigWriter`, `AvdHome`,
      `IAvdConfigStore` (+ DI). Kept `AvdMetadata`/`AvdConfigParser`. (`AvdValueConventions` — typed value
      conversion — deferred to 0007, where two-way edit binding needs it.)
- [x] Core tests: `IniParserTests`; `AvdConfigWriterTests` (round-trip + unknown-key preservation);
      `AvdConfigReaderTests` (full parse + API-level + null-without-AvdId + `ToMetadata`). Kept `AvdConfigParserTests` green.
- [x] Desktop: `SelectedDetail` on `EmulatorViewModel`; wired `ViewDetails`; `EmulatorDetailsViewModel` +
      the decomposed views (`EmulatorView` shell / `EmulatorListView` / `EmulatorCardView` /
      `EmulatorDetailsView`, `BusyOverlay`, `PropertyGroupView` / `PropertyRow` + `DetailGroup`/`DetailRow`).
- [x] Console: `emulator info <avd>`.

## Verification

- [x] `dotnet test` passes — 27 tests (incl. parser/writer/reader).
- [x] `dotnet build` green across the new small views (compiled bindings).
- [ ] Desktop: clicking a row opens its details; grouped values match `config.ini`; Back returns. _Pending — run to confirm._
- [x] Console: `emulator info <avd>` prints the grouped fields (verified against a fake AVD home).
- [x] Writer-level: changing one key rewrites only that line; unknown keys/comments preserved.
