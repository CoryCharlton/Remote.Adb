# Remote.Adb — Roadmap

Future work only. Completed work is evidenced in git history and the code itself, so it isn't tracked here — and
a milestone's task doc is deleted when the milestone lands. Order in the milestone table is the priority.

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

| Milestone | Notes |
|-----------|-------|
| **SDK Manager UI** (list / download / remove system images & packages via `sdkmanager`) | [tasks/sdk-manager.md](tasks/sdk-manager.md); unblocks creating from a not-yet-installed image. |
| **DI lifetimes / stop manual construction** | [tasks/di-lifetimes.md](tasks/di-lifetimes.md); deferred until a concrete driver — scope view models to their owning window/view and resolve currently-`new`'d dialog windows/VMs from DI. |
| **SSH reverse tunnel** for the adb server | Pillar 1. |
| **Remote device connection** over network | Pillar 3. |

## Working notes

- All domain logic lives in `Remote.Adb.Core`; the GUI and CLI are thin shells that resolve services
  from DI (`AddRemoteAdbCore()`). New capability lands in Core first, then is surfaced in both heads.
- See `src/adb-tunnel.bat` and the "tunnel workflow being replaced" section of `CLAUDE.md` for the
  hard-won SSH/adb details that must carry into Pillar 1.
- To debug the desktop UI (catch runtime layout/XAML bugs a clean build misses), drive it under WSLg
  with screenshots — see [wslg-gui-debugging.md](wslg-gui-debugging.md) and the reproducible data
  harness at [tools/setup-fake-avd-harness.sh](tools/setup-fake-avd-harness.sh).
