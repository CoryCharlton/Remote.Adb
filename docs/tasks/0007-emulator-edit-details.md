# 0007 — Emulator edit details

**Status:** ✅ Done · builds on [0006 view details](0006-emulator-view-details.md)

Make the details pane editable: the same surface from 0006 flips read-only → editable, persisting changes back
to the AVD's `config.ini` **without dropping any unknown keys** (the round-trip writer landed in 0006).

## Approach (as built)

- The detail pane gains an `IsEditing` flag on `EmulatorDetailsViewModel`; the same rows flip read ↔ editable
  in place — no new route/page. `Edit` / `Save` / `Cancel` live in the (now pinned) header.
- The shared `Common/DetailRow` record was replaced by `Common/AvdField` (an `ObservableValidator` field VM):
  it carries the raw `config.ini` value, an optional choice set (dropdown), an optional read-mode display
  override (e.g. `"2048 MB"`), and a per-instance validation delegate surfaced inline via
  `INotifyDataErrorInfo`. `Common/DetailGroup` became observable (`IsEditing`/`IsExpanded`/`IsVisible`).
- **Full curated editable set** (Android-Studio-style): edit shows inputs for every supported key, even ones
  the AVD lacks. A field left **blank** is *omitted* — its key is **removed** from `config.ini`, not written
  empty (so minimal AVDs don't accrue empty keys).
- Save splits the dirty fields into **changes** (non-blank → set/append) and **removals** (now-blank → drop),
  then calls `IAvdConfigStore.Write(avdId, changes, removals)` → `AvdConfigWriter.Write`. Unknown keys,
  comments, ordering, and trailing newline are preserved by construction.
- Validation via CommunityToolkit `ObservableValidator` on `AvdField` (sizes via `AvdValueConventions.IsValidSize`,
  counts via `IsValidCount`, required-not-blank for display name); blank optional fields are allowed (= omit).
  Core stays pure (predicates only).

## Layout — collapsible sections (shared by view/edit/create)

Each config group renders as a collapsible **`Expander`** on the standard Elevated-card surface, built once in
`Common/PropertyGroupView` so the view, edit, and (future) create panes share one layout. Primary groups
(Identity, System image, Device, Memory & storage) default expanded; advanced groups (Processor & graphics,
Input & sensors, Skin, Network, Location) default collapsed. The 0006 read pane was updated to this layout too.

The details header is **pinned** (`Panel` → `Grid RowDefinitions="Auto,*"`): only the body scrolls. This
retired the old "header inside the ScrollViewer" workaround now that the real scroll cause (`ScrollViewer.Padding`)
is fixed — see [wslg-gui-debugging.md](../wslg-gui-debugging.md).

## Editable vs read-only-in-place

- **Editable:** display name, density, memory & storage (RAM, heap, data partition, SD card + size), CPU cores,
  graphics (GPU enabled/mode), input & sensors (cameras, GPS, keyboard, orientation, audio), skin + device
  frame, network speed/latency.
- **Read-only in place:** AvdId, system image (`image.sysdir.1`), `abi.type`, `target`, device profile
  identity, CPU arch, derived API level / resolution, sibling `path`. Surfaced, never editable — changing them
  is effectively a recreate.

## Rename caveat

- **DisplayName rename is free** — it's just the `avd.ini.displayname` value.
- **AvdId rename is deferred** — the id is the `<id>.avd/` folder name, the sibling `<id>.ini` filename, the
  `path=` inside both, the catalog dict key, and what `emulator -list-avds` returns. A real rename touches all of
  those (or routes through `avdmanager`). Not attempted in 0007.

## Tasks

- [x] Core: `AvdConfigWriter.Write` gains an optional `removals` parameter (drop keys, preserve the rest);
      `AvdValueConventions` (`IsValidSize` / `IsValidCount` / `BooleanValues`);
      `IAvdConfigStore.Write(avdId, changes, removals)` (locate → write → re-read) with a shared `Locate`.
- [x] Desktop: `AvdField` editable field VM, observable `DetailGroup`, `PropertyGroupView` expander,
      three-state `PropertyRow`, edit mode + pinned header/actions in `EmulatorDetailsViewModel`/`…View`.
- [x] Console: `emulator edit <avd> --<key> <value>…` (bare `--<key>` clears) → `store.Write`.
- [x] Tests: writer removal cases, `AvdValueConventionsTests`, `AvdConfigStoreTests` (temp `ANDROID_AVD_HOME`).

## Verification

- [x] Edit a field, save, reopen → value persisted in place; unrelated keys/comments/order intact (verified on
      disk: `hw.gps=no` updated at its original line, everything else unchanged).
- [x] Invalid size/count blocked inline; required display name enforced.
- [x] `dotnet test` (53 passing) + `dotnet build` green.
- [x] WSLg: pinned header, primary expanded / advanced collapsed, edit flips rows to text boxes + dropdowns.
