# Emulator list — periodic auto-refresh

The emulator list (`EmulatorViewModel` / `EmulatorListView`) only refreshes on the manual `RefreshCommand`, on
first activation (`OnActivatedAsync`), and transiently while a start is in flight (`WaitUntilRunningAsync` polls
every 2s until the AVD comes up). So state changed outside the app — an emulator started/stopped from the command
line or Android Studio, or an AVD created/deleted elsewhere — doesn't show until the user hits refresh.

## Goal

While the emulator page is the active screen, periodically re-list in the background so the rows track reality
without manual refresh.

## Notes / constraints

- Poll only while the page is active (tie to `IActivatable`/visibility), and stop when navigated away — don't run
  the timer for a backgrounded page.
- Reuse the existing `Merge(...)` reconciliation so a refresh preserves selection and the transient "starting"
  state (it already updates rows in place rather than rebuilding the collection).
- Don't stack refreshes: skip a tick if one is already running (the `IsBusy` guard in `RefreshAsync` covers the
  user-triggered path; the timer must respect it too) and don't fight the start-poll loop.
- Pick a calm interval (e.g. ~5–10s) — listing shells out to `adb`/`emulator`, so it's not free.
- Consider pausing/backing off when the window is unfocused or minimized.
