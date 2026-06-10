# Remote.Adb — Roadmap

Future work only. Completed work is evidenced in git history and the code itself, so it isn't tracked here — and
a milestone's task doc is deleted when the milestone lands. Order in the milestone table is the priority.

## Vision

A single tool — desktop GUI and console, both over a shared `Remote.Adb.Core` library — to manage
ADB connections to a remote development server. It replaces the workflows previously encoded in
manual scripts and steps.

## Pillars

1. **SSH port forwarding** — open a reverse tunnel so a local `adb` server is reachable from the
   remote dev host (`ssh -o ExitOnForwardFailure=yes -N -R 5037:127.0.0.1:5037 <host>`), with
   kill-then-bind-then-retry handling for the IntelliJ adb-respawn race.
2. **Emulator management** — list, start, stop, create, view/edit, and delete Android emulators.
3. **Remote device connection** — connect to Android devices over the network (e.g. Wi-Fi).

## Milestones

| Milestone | Notes |
|-----------|-------|
| **SDK Manager UI** (list / download / remove system images & packages via `sdkmanager`) | [tasks/sdk-manager.md](tasks/sdk-manager.md); unblocks creating from a not-yet-installed image. |
| **DI lifetimes / stop manual construction** | [tasks/di-lifetimes.md](tasks/di-lifetimes.md); deferred until a concrete driver — scope view models to their owning window/view and resolve currently-`new`'d dialog windows/VMs from DI. |
| **Remote device connection** over network | Pillar 3. The Devices page now *lists* attached devices (`adb devices -l`); the remaining work is connecting to a device over the network (e.g. `adb connect host:port`). |

## Working notes

- All domain logic lives in `Remote.Adb.Core`; the GUI and CLI are thin shells that resolve services
  from DI (`AddRemoteAdbCore()`). New capability lands in Core first, then is surfaced in both heads.
- Pillars 1 (SSH reverse tunnel) and 2 (emulator management) have landed; the Devices page lists
  attached devices, leaving network *connect* (Pillar 3) as the open pillar. See the "SSH/adb
  constraints the tunnel must preserve" section of `CLAUDE.md` for the hard-won SSH/adb details
  the tunnel implementation preserves.
- To debug the desktop UI (catch runtime layout/XAML bugs a clean build misses), drive it under WSLg
  with screenshots — see [wslg-gui-debugging.md](wslg-gui-debugging.md) and the reproducible data
  harness at [tools/setup-fake-avd-harness.sh](tools/setup-fake-avd-harness.sh).
- Possible enhancement: expose the list auto-refresh interval (currently a 5s constant shared by the
  Emulators and Devices pages) as a persisted setting.
