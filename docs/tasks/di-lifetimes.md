# DI: own view-model lifetimes and stop manual construction

Several types are constructed by hand instead of resolved from DI, and the page view models are app-lifetime
transients held by the shell rather than scoped to the window/view that owns them. Do this as one focused pass
rather than piecemeal.

## View-model lifetimes (the main item)

The page view models (Emulator / Devices / Tunnel / Settings) are registered `AddTransient` but are constructed
once by `MainWindowViewModel` and held in its `Destinations` for the whole app run — so they behave like
singletons tied to the shell. Scope each VM to the window/view that owns it (e.g. a per-window DI scope), so a
view model's lifetime matches its view and it can be recreated/disposed with the view rather than living forever.

## Stop manual construction (from the DI review pass)

- **`CreateAvdViewModel`** — its ctor takes only services, yet `AvdCreateDialog` `new`s it while injecting
  `_provisioning`/`_store` purely to forward them. Register `AddTransient<CreateAvdViewModel>()` and inject a
  `Func<CreateAvdViewModel>` into `AvdCreateDialog` (so a fresh VM per dialog), dropping the forwarded services.
- **Dialog windows** — `DialogHost.ShowAsync<TWindow>` uses a `where TWindow : Window, new()` constraint and
  `new TWindow()`, so `ConfirmDialogWindow` / `CreateAvdWizardWindow` bypass DI and the source-generated
  ViewLocator (unlike the four page views). Resolve `TWindow` from `IServiceProvider`, register the windows, and
  drop the `new()` constraint — consistent with the page views and future-proof if a dialog needs a dependency.
- **`EmulatorDetailsViewModel`** — mixes a service (`IAvdConfigStore`) with runtime args (an `AvdConfiguration`
  and a back callback). Use a registered factory delegate so the service is DI-resolved and the runtime args stay
  explicit, removing the service-forwarding through `EmulatorViewModel`.
- **`ConfirmDialogViewModel`** (title/message/label) and **`EmulatorDeviceViewModel`** (device + command) take
  only per-call runtime args — keep manual `new` (or a small factory); not pure DI candidates.

## Related lifetime smells (same pass)

- `MainWindow` and `MainWindowViewModel` are `AddTransient` but are the single app root, resolved once in
  `App.OnFrameworkInitializationCompleted`. Transient is semantically wrong for a one-instance root and offers no
  guard against an accidental second resolution (which would re-run the diagnostics and build a second
  notification manager). Consider singleton, or resolve-once-and-store.
- `MainWindowViewModel` raises the startup diagnostics as a **constructor side effect** (`NotifyToolDiagnostics`),
  so correctness depends on the implicit ordering "VM constructed before the window opens and attaches the
  notification sink." Move that out of the ctor (e.g. an explicit `OnLoaded`/startup hook the window calls) so it
  isn't a surprising side effect during DI graph construction and is unit-testable without firing toasts.

## Notes

- Microsoft.Extensions.DependencyInjection has no built-in `Func<T>` — register the factory delegates explicitly
  (`AddTransient<Func<T>>(sp => () => sp.GetRequiredService<T>())`) or inject `IServiceProvider` /
  `IServiceScopeFactory` for per-instance creation.
- `MainWindow` is already DI-registered (`AddTransient<MainWindow>()`); this pass extends the same treatment to
  the dialog windows and the manually-built view models.
