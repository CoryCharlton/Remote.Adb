# Debugging the desktop UI under WSLg (screenshot-driven)

The Avalonia desktop app runs under **WSLg** (`DISPLAY=:0`), so a headless agent can launch it,
drive it, and screenshot it. This is the only reliable way to catch **runtime layout/XAML bugs** —
a clean `dotnet build` does *not* catch them:

- bad bindings, missing animators, and layout overflow surface only at runtime;
- "can't scroll to the bottom", "scrollbar inset off the edge", clipped content, wrong alignment
  are **visual** — you have to *see* the rendered window.

> **Methodology — isolate, don't guess.** When a layout bug appears, do not stack speculative fixes.
> Reduce to the smallest repro, then change **one** property at a time and screenshot after each.
> The scroll-to-bottom bug was finally pinned by taking a *working* view and adding back **only**
> `ScrollViewer Padding` — it broke immediately, proving the cause. One variable per screenshot.

---

## 1. Prerequisites

WSLg is live when `DISPLAY=:0` and these tools exist (all present on this machine):

- `xdotool` — find/activate windows, warp the pointer, synthesize clicks + scroll-wheel events
- ImageMagick `import` — capture a specific X window to PNG (read it back with the Read tool)
- `dotnet` — build/run the app

Quick check: `DISPLAY=:0 xdotool getdisplaygeometry` should print the screen size.

---

## 2. Deterministic data harness (no real Android SDK needed)

The app lists AVDs by shelling out to `emulator -list-avds` and reads each `config.ini` from
`ANDROID_AVD_HOME`. To populate the UI without a real SDK, generate a throwaway fake-SDK + AVD set:

```bash
bash docs/tools/setup-fake-avd-harness.sh          # → /tmp/fakesdk, /tmp/avdhome
# or pick paths: bash docs/tools/setup-fake-avd-harness.sh /tmp/fakesdk /tmp/avdhome
```

This builds a **structured fake SDK root** (`$SDK/emulator`, `platform-tools`,
`cmdline-tools/latest/bin/{avdmanager,sdkmanager}`, `system-images/…`) plus 15 AVDs under `$AVDS`: one
**rich** `Television_1080p_16.0` (every detail group populated, with a sibling `.ini` for the Location group)
and 14 **minimal** `Pixel_*`. The fake `emulator -list-avds` reflects whatever is in `$AVDS`, and the fake
`avdmanager` actually creates/deletes `.avd` folders — so the **create** wizard and console verbs work too.

Why these env vars (see `AndroidSdk.cs` / `AvdHome.cs`):

- `ANDROID_HOME=/tmp/fakesdk` — the SDK root, so `IAndroidSdk` resolves `emulator`/`adb`/`avdmanager`/
  `sdkmanager` and `SystemImageScanner` finds the installed images.
- `ANDROID_AVD_HOME=/tmp/avdhome` — where `AvdHome`/`avdmanager` look for `<name>.avd/config.ini`.

> The fake `config.ini` keys must match the typed accessors in `AvdConfiguration.cs`
> (e.g. the Network group reads `runtime.network.*`, **not** `hw.network.*`). The generator is kept
> in sync with that file — if a property group renders blank, check the key there first.

---

## 3. Build & launch

```bash
dotnet build src/Remote.Adb.Desktop/Remote.Adb.Desktop.csproj -c Release -v q --nologo

# Launch in the BACKGROUND (run_in_background) so the agent keeps control:
ANDROID_HOME=/tmp/fakesdk ANDROID_AVD_HOME=/tmp/avdhome DISPLAY=:0 \
  dotnet src/Remote.Adb.Desktop/bin/Release/net10.0/Remote.Adb.Desktop.dll
```

> **WSLg flakiness (signal 16):** in some display states the GUI process is killed with exit 144 (SIGSTKFLT)
> shortly after a *backgrounded* launch, while a **foreground** launch stays alive. Workaround: run `dotnet`
> in the foreground under `timeout`, and capture from a **concurrent screenshot subshell** started just
> before it:
> ```bash
> ( for i in $(seq 6); do sleep 2; wid=$(…find window…); [ -n "$wid" ] && import -window "$wid" /tmp/shot.png && break; done ) &
> ANDROID_HOME=/tmp/fakesdk ANDROID_AVD_HOME=/tmp/avdhome DISPLAY=:0 timeout 16 dotnet …/Remote.Adb.Desktop.dll
> ```
> Also confirm the window you grabbed is actually ours — a stale/other window named similarly (e.g. the CCSWE
> Material demo) can match the search if our process already died.

Shut it down between runs with `pkill -f '[R]emote.Adb.Desktop.dll'`
(the `[R]` keeps the pattern from matching the `pkill` process itself; this command "fails" with
exit 144 when it kills the background task — that's expected, not an error).

---

## 4. Find the window (the reliable way)

`wmctrl` and `_NET_CLIENT_LIST` are unreliable under WSLg ("Cannot get client list properties").
Use `xdotool search` and filter by the **exact** window name — multiple windows can match the
substring, and ids change between runs:

```bash
export DISPLAY=:0
timeout 30 xdotool search --sync --name "Remote ADB" >/dev/null 2>&1   # wait for it to appear
wid=""
for w in $(xdotool search --name "Remote ADB" 2>/dev/null); do
  [ "$(xdotool getwindowname "$w" 2>/dev/null)" = "Remote ADB" ] && wid=$w && break
done
echo "wid=$wid"
xdotool windowactivate "$wid"; sleep 0.5
```

---

## 5. Click and scroll

The window is 900×560. The drawer occupies the left ~360px; content is on the right.

- **Click a card body** (avoid the play button at the far right): `xdotool mousemove 550 63; xdotool click 1`
- **Scroll** — the pointer must be **physically warped** over the scrollable area first; targeting by
  `--window` is not enough for wheel events. Button 5 = wheel down, button 4 = wheel up:

```bash
xdotool mousemove 630 300            # warp into the content area
xdotool click --repeat 200 --delay 5 5   # 200 wheel-down ticks → guaranteed bottom
# ...to scroll back up, use button 4 instead of 5.
```

Use a high repeat count (200+) so you reach the true extent — that's how you tell "the content ends
here" from "scrolling is stuck". A gap below the scrollbar thumb after a big scroll = a real bug.

> After a **manual** scroll (e.g. the user scrolled for you), re-warp and re-issue the wheel ticks —
> don't trust the prior scroll position.

---

## 6. Capture and read back

```bash
import -window "$wid" /tmp/shot.png
```

Then read `/tmp/shot.png` with the Read tool to *see* the rendered UI. Capture at each step of an
isolation experiment (top of list, bottom of list, details top, details bottom) and compare.

---

## 7. End-to-end recipe (copy-paste)

```bash
# build + launch (launch via run_in_background)
dotnet build src/Remote.Adb.Desktop/Remote.Adb.Desktop.csproj -c Release -v q --nologo
ANDROID_HOME=/tmp/fakesdk ANDROID_AVD_HOME=/tmp/avdhome DISPLAY=:0 \
  dotnet src/Remote.Adb.Desktop/bin/Release/net10.0/Remote.Adb.Desktop.dll   # background

# drive + capture
export DISPLAY=:0
timeout 30 xdotool search --sync --name "Remote ADB" >/dev/null 2>&1
wid=""; for w in $(xdotool search --name "Remote ADB"); do
  [ "$(xdotool getwindowname "$w")" = "Remote ADB" ] && wid=$w && break; done
xdotool windowactivate "$wid"; sleep 0.5
xdotool mousemove 550 63; xdotool click 1; sleep 1     # open the rich Television AVD
xdotool mousemove 630 300; xdotool click --repeat 250 --delay 5 5   # scroll details to bottom
import -window "$wid" /tmp/details-bottom.png           # → read with Read tool

# teardown
pkill -f '[R]emote.Adb.Desktop.dll'
```

---

## 8. Footguns (all hit at least once)

| Symptom | Cause / fix |
|---|---|
| `pkill -f Remote.Adb.Desktop` exits 144 / kills itself | the pattern matches the `pkill` process; use `pkill -f '[R]emote.Adb.Desktop.dll'` |
| `Cannot get client list properties (_NET_CLIENT_LIST)` | `wmctrl` fails under WSLg; use `xdotool search --name` |
| window id `wid=` comes back empty / wrong window | filter by exact `getwindowname` == `"Remote ADB"`, not the substring |
| wheel ticks do nothing | the pointer wasn't warped onto the content — `xdotool mousemove X Y` first, then `click 5` |
| `env PATH=$PATH ...` breaks | Windows PATH has spaces; set an explicit minimal `PATH=/tmp/fakesdk:/usr/bin:/bin` |
| heredocs / `sleep` blocked in foreground | use the Write tool / `printf`; gate waits with `timeout` + `xdotool --sync` |
| a property group renders blank | the fake `config.ini` key doesn't match `AvdConfiguration.cs` (e.g. `runtime.network.*`) |

---

## 9. Known layout rules these tools proved

- **Page inset goes on the scrolled content's `Margin`, never `ScrollViewer.Padding`.** Avalonia's
  `ScrollContentPresenter` leaves the bottom padding *outside* the scrollable extent, so padding makes
  the last items permanently unreachable. Put the inset on the inner `ItemsControl`/`StackPanel`
  `Margin`; the `ScrollViewer` itself stays padding-free and the scrollbar stays at the window edge.
- **A scrollable screen in the CCSWE `DrawerPage` must host its `ScrollViewer` in a `Panel`**, not a
  `DockPanel` — a `Panel` force-fills the `ScrollViewer` to a bounded viewport height; a `DockPanel`
  doesn't, so the scroll area runs taller than the window.
