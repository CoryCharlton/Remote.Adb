# Remote.Adb — Roadmap

## Vision

A single tool — desktop GUI and console, both over a shared `Remote.Adb.Core` library — to manage
ADB connections to a remote development server. It replaces the workflows currently encoded in
`src/adb-tunnel.bat` and related manual steps.

## Pillars

1. **SSH port forwarding** — open a reverse tunnel so a local `adb` server is reachable from the
   remote dev host (`ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 <host>`), with the
   kill-then-bind-then-retry handling from `adb-tunnel.bat`.
2. **Emulator management** — list, start, stop, create, view/edit, and delete Android emulators.
3. **Remote device connection** — connect to Android devices over the network (e.g. Wi-Fi).

## Milestones

| # | Milestone | Status | Notes |
|---|-----------|--------|-------|
| 0 | Project restructure (Core / Desktop / Console / tests) | ✅ Done | [tasks/0001-restructure.md](tasks/0001-restructure.md) |
| 1 | **Emulator management** (list / start / stop) | ✅ Done | [tasks/0002-emulator-management.md](tasks/0002-emulator-management.md) |
| 2 | Emulator friendly names + row polish | ✅ Done | [tasks/0003-emulator-friendly-names.md](tasks/0003-emulator-friendly-names.md) |
| 3 | Emulator **view details** (clickable card → read-only details) | ✅ Done | [tasks/0006-emulator-view-details.md](tasks/0006-emulator-view-details.md); detail pane visually verified |
| 4 | Emulator **edit details** (rename / reconfigure an AVD) | ✅ Done | [tasks/0007-emulator-edit-details.md](tasks/0007-emulator-edit-details.md); builds on #3 |
| 5 | Emulator **creation** (AVD via `avdmanager`/`sdkmanager`) | ✅ Done | [tasks/0008-emulator-create.md](tasks/0008-emulator-create.md); Android-Studio-style two-screen wizard (device-catalog picker → configure) |
| 6 | Emulator **delete** (remove an AVD via row ⋮ menu + confirm) | ✅ Done | [tasks/0008-emulator-create.md](tasks/0008-emulator-create.md); shares `IAvdProvisioningService.DeleteAsync` |
| 7 | **SDK Manager UI** (list / download / remove system images & packages via `sdkmanager`) | ⬜ Planned | [tasks/0009-sdk-manager.md](tasks/0009-sdk-manager.md); unblocks creating from a not-yet-installed image |
| 8 | SSH reverse tunnel for the adb server | ⬜ Planned | Pillar 1 |
| 9 | Remote device connection over network | ⬜ Planned | Pillar 3 |

Foundational: **feature-first restructure** — reorganize Desktop + Core into vertical slices (a feature's
views/view models/services together, with `Common/` + `Shell/`). Prerequisite for the emulator
view/edit/create work above. ✅ Done. See [tasks/0005-feature-first-restructure.md](tasks/0005-feature-first-restructure.md).

Cross-cutting: **Settings persistence (theme + density)** — settings survive across sessions via JSON under the
app-data folder; the theme and the Material density are durable, and the layer extends to future settings by
adding a field. ✅ Done. See [tasks/0011-settings-persistence.md](tasks/0011-settings-persistence.md).

Cross-cutting: **Settings — SDK / JAVA_HOME overrides** — let Settings override the env-var-based tool
resolution (Android SDK path, AVD home, Java home), so the app works without (or despite) `ANDROID_HOME`/
`JAVA_HOME`. ⬜ Planned; builds on 0011's persistence layer.
See [tasks/0010-settings-sdk-overrides.md](tasks/0010-settings-sdk-overrides.md).

Cross-cutting: **design system** — the desktop UI is skinned by the **CCSWE.Avalonia.Material** package, a
standalone Material 3 theme + control library (tokens, M3 type scale, and controls such as `Card`,
`FloatingActionButton`, `CircularProgressIndicator`), dark by default. ✅ Done.
See [tasks/0004-design-system-theme.md](tasks/0004-design-system-theme.md).

Legend: ✅ done · 🚧 in progress · ⬜ planned

## Working notes

- All domain logic lives in `Remote.Adb.Core`; the GUI and CLI are thin shells that resolve services
  from DI (`AddRemoteAdbCore()`). New capability lands in Core first, then is surfaced in both heads.
- See `src/adb-tunnel.bat` and the "tunnel workflow being replaced" section of `CLAUDE.md` for the
  hard-won SSH/adb details that must carry into Pillar 1.
- To debug the desktop UI (catch runtime layout/XAML bugs a clean build misses), drive it under WSLg
  with screenshots — see [wslg-gui-debugging.md](wslg-gui-debugging.md) and the reproducible data
  harness at [tools/setup-fake-avd-harness.sh](tools/setup-fake-avd-harness.sh).
