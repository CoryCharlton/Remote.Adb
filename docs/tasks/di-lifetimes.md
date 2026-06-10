# DI: view-scoped lifetimes and DI-resolved dialog windows

Deferred until there's a concrete driver. For a single-window app whose `MainWindow` lives the whole process,
scoping view models to the window and DI-resolving the dialog windows is a **no-op today** — the page VMs already
live as long as the app, and disposal-on-close would fire at shutdown. Don't build speculative infrastructure for it.

## Trigger — do this when any of these becomes true

- A **second or recreatable window** exists (so a VM's lifetime should end with its window, not the app).
- **Navigation recreates page VMs per visit** (page-level scoping), so disposing them on leave actually matters.
- A **dialog window grows an injected dependency**, making the `new TWindow()` bypass of DI a real problem.

## Deferred work

- **`CCSWE.Avalonia.DependencyInjection` library** (own repo, modeled on `ccswe-avalonia-hosting`, referenced via
  cross-repo `ProjectReference` until published): a base package with an `AddFactory<T>()` `IServiceCollection`
  helper (MEDI has no built-in `Func<T>`), and a `.Desktop` package with a **view-level scope** primitive —
  `CreateScopedWindow<TWindow>(this IServiceProvider)` that resolves a window (and its VM graph) from a child
  `IServiceScope` and disposes the scope on `Closed`. Window granularity is the common case; the same primitive
  applies to a page `UserControl` for per-visit scoping later.
- **Scope the page VMs** (Emulator / Devices / Tunnel / Settings) and `MainWindowViewModel` to the shell window's
  scope (`AddScoped`), resolved via `CreateScopedWindow<MainWindow>()` in `App`, so they dispose with the window.
- **DI-resolve dialog windows**: drop `DialogHost.ShowAsync<TWindow>`'s `where TWindow : Window, new()` constraint
  and resolve `ConfirmDialogWindow` / `CreateAvdWizardWindow` from DI (a child scope), consistent with the page
  views and future-proof if a dialog needs a dependency.

## Already done (in-app, no library)

- `CreateAvdViewModel` and `EmulatorDetailsViewModel` are created via registered factories (`Func<CreateAvdViewModel>`,
  `EmulatorDetailsViewModelFactory`) instead of hand-`new`ed with forwarded services.
- `MainWindowViewModel` no longer fires the startup diagnostics as a constructor side effect — the shell calls
  `RaiseStartupDiagnostics()` from `MainWindow.OnLoaded` once its notification sink is attached.

## Notes

- Microsoft.Extensions.DependencyInjection has no built-in `Func<T>` — register factory delegates explicitly
  (`AddTransient<Func<T>>(sp => () => sp.GetRequiredService<T>())`) or inject `IServiceProvider` /
  `IServiceScopeFactory` for per-instance creation.
- `EmulatorDeviceViewModel` and `ConfirmDialogViewModel` take only per-call runtime args — keep manual `new`.
