# Desktop UI tests (Avalonia headless)

There's no test project for `Remote.Adb.Desktop`, so view models and view/binding behavior go untested — which is
how the notification-render timing, the create wizard's partial-failure flow, and the missing list sort all
slipped through. Add `Remote.Adb.Desktop.UnitTests` and use Avalonia's headless test platform to drive the UI
without a display server.

## Scope

- New `Remote.Adb.Desktop.UnitTests` project (NUnit 4, same conventions as `Remote.Adb.Core.UnitTests`), listed in
  the `.slnx` `/tests/` folder.
- Use **Avalonia.Headless** (`Avalonia.Headless.NUnit`, `[AvaloniaTest]`) to host controls/app without a window
  server, so view models, bindings, commands, and control lifecycle can be exercised and the dispatcher pumped.
  `CCSWE.Avalonia.Material`'s own `src/CCSWE.Avalonia.Material.UnitTests` project (e.g. `CardTests.cs`,
  `DensityTests.cs`) is a working headless setup to copy (project SDK, `[AvaloniaTest]`, app-builder fixture).
- First targets — the things that just bit us: `INotificationService` buffer/flush + `MainWindow` sink wiring,
  `MainWindowViewModel` diagnostics → notifications, `CreateAvdViewModel` create + partial-failure (close + toast)
  flow, `EmulatorViewModel` merge **and sort**.

## Notes

- Headless asserts on logical/rendered state without WSLg/X; pairs with the screenshot-based WSLg debugging
  ([wslg-gui-debugging.md](../wslg-gui-debugging.md)) for true visual checks.
- Pairs with [di-lifetimes.md](di-lifetimes.md): DI-resolving the dialog VMs/windows makes them straightforward to
  construct under test.
