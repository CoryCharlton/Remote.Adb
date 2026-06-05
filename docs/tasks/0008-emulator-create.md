# 0008 — Emulator creation (and delete)

**Status:** ✅ Done · builds on [0007 edit details](0007-emulator-edit-details.md)

Create a new AVD via `avdmanager`, then apply the tuned configuration through the same writer/model used by
edit. Implemented as a modal, two-screen wizard modelled on Android Studio's **Add Device** flow so everything
is gathered up front and creation is **atomic on Finish** — no interim AVD if the user cancels. The same
provisioning backend also powers **deleting** an AVD from the list.

## Tooling additions (Core)

- **`IProcessRunner.RunAsync`** gained an optional `standardInput` — `avdmanager create` prompts *"create a
  custom hardware profile? [no]"*, so we pipe `"no\n"` to keep it from blocking.
- **`IAndroidSdk.SdkManagerPath`** added (same `ResolveTool` pattern as `AvdManagerPath`). `sdkmanager` isn't
  used yet — it's the future hook for downloading images.
- **`SystemImagePackage`** / **`DeviceProfile`** records; **`SystemImageScanner`** (lists installed images by
  scanning `$SDK/system-images/android-<n>/<tag>/<abi>/` — version-independent, no `--list` parsing) and
  **`AvdManagerOutputParser.ParseDevices`** (`avdmanager list device`).
- **Device catalog from the SDK jars.** `avdmanager list device` is slow and gives no screen specs, so the
  primary source is **`DeviceDefinitionReader`** + **`DeviceDefinitionParser`**: read the user's
  `~/.android/devices.xml`, then scan `cmdline-tools/*/lib/sdklib/*.jar` for the embedded
  `com/android/sdklib/devices/*.xml` resources (Pixel/Nexus/TV/Wear/automotive/desktop/xr), dedupe by id, and
  fall back to the loose `tools/lib/devices.xml` only if the jars yield nothing. The parser is
  namespace-agnostic (matches by local name) and pulls dimensions, density (bucket/`dpi`/computed from
  diagonal), RAM (with unit), screen size, min API, `playstore-enabled`, and the `deprecated` flag.
  **`AvdCategories`** maps a device tag → form factor and **`AndroidApiLevels`** maps an API level → friendly
  version name ("API 34 · Android 14"). `avdmanager list device` remains the last-resort fallback.
- **`IAvdProvisioningService`** (`AvdProvisioningService`, DI singleton): `ListInstalledImagesAsync`,
  `ListDevicesAsync` (catalog reader, **cached**, avdmanager fallback with a logged warning), `CreateAsync`
  (`avdmanager create avd -n -k -d`, stdin `"no\n"`), `DeleteAsync`.
- **`AvdValueConventions.IsValidAvdName`** for the `-n` argument.

## Desktop — the wizard

- A modal **`CreateAvdWizardWindow`** (`Title="Add Device"`, `ShowDialog`), fronted by the Desktop-layer
  **`IAvdCreateDialog`** service so view models never touch `Window`. CCSWE Material has no dialog host, and a
  real `Window` is the idiomatic desktop choice (inherits the theme automatically).
- **`CreateAvdViewModel`** drives two screens bound to `CurrentStep`, mirroring Android Studio's flow:
  - **Step 0 — Add Device (picker).** A form-factor selector rendered as a radio list (phone / tablet / Wear /
    desktop / TV / automotive / XR) filters a device table (Name / Play Store / API / width / height / density)
    populated from the device catalog above, with a search box and a **Show obsolete** toggle for deprecated
    profiles.
  - **Step 1 — Configure.** Name + display name, a **Device** / **Additional settings** `TabControl` (the
    tunable groups reuse the shared `AvdDetailFields`/`PropertyGroupView`), and a live **summary** pane with a
    proportional device-frame **wireframe** (sized to the selected profile's resolution, with the diagonal and
    a rotated height label). Selecting a device seeds defaults (e.g. `hw.ramSize` from the profile's RAM).
- **Image filters.** Once a device is picked, the system-image list is filtered by **API level** and
  **services** (Google Play / Google APIs / AOSP …) dropdowns, and the device's tag scopes which images apply.
- **Finish** validates → `CreateAsync` → `IAvdConfigStore.Write` overrides → closes.
- **Pre-warm + cache.** A `BackgroundService` (**`DeviceCatalogWarmup`**, registered as `IHostedService`) calls
  `ListDevicesAsync` on launch so the catalog (slow to build the first time) is ready and cached before the
  wizard opens.
- Entry point: the Emulators list's bottom-right FAB is now **Create** (`+`); **Refresh** is a top-right text
  button.

## Desktop — delete

- Each row's **⋮ overflow menu** (`EmulatorCardView`) carries a destructive **Delete** item (disabled while the
  AVD is running). It binds to a `DeleteCommand` exposed on the **row** view model — a `MenuFlyout`'s items live
  in a popup and can't reach the list's `ItemsControl` ancestor the way the start/stop buttons do.
- **`EmulatorViewModel.DeleteAsync`** confirms via a **reusable** modal **`IConfirmDialog`** (mirrors the
  `IAvdCreateDialog` pattern — `ConfirmDialog` + `ConfirmDialogViewModel` + `ConfirmDialogWindow` in `Common/`),
  then `IAvdProvisioningService.DeleteAsync`, clears any open detail, and refreshes the list.

## Field model regrouping (shared by view / edit / create)

The field groups were reworked to mirror Android Studio (Camera, Network, Startup, Storage, Emulated
performance, Sensors & input, …) and factored into a shared **`AvdDetailFields`** builder so view, edit, and
the wizard's settings step stay in sync.

## Scope notes

- **Installed images only.** Creating requires an already-installed system image; downloading via
  `sdkmanager --install` (large downloads + progress) is deferred — `SdkManagerPath` is the hook.

## Tasks

- [x] Core: stdin on `RunAsync`; `SdkManagerPath`; records; `AvdManagerOutputParser`; `SystemImageScanner`;
      device catalog (`DeviceDefinitionReader`/`DeviceDefinitionParser`, `AvdCategories`, `AndroidApiLevels`);
      `IAvdProvisioningService`; `IsValidAvdName`; DI.
- [x] Core tests: `AvdManagerOutputParserTests`, `SystemImageScannerTests`, `AvdProvisioningServiceTests`
      (stdin verified), `DeviceDefinitionParserTests`, `IsValidAvdName` cases. (80 tests total.)
- [x] Desktop: two-screen create wizard (device-catalog picker → configure tabs + wireframe summary), image
      filters, device-default seeding, `DeviceCatalogWarmup` hosted service, entry-point FAB/refresh.
- [x] Desktop: per-row ⋮ delete via reusable `IConfirmDialog`.
- [x] Console: `emulator images` / `devices` / `create -n -k -d [--<key> <value>…]` / `delete`.
- [x] Harness: `setup-fake-avd-harness.sh` builds a structured fake SDK + fake `avdmanager` (create/delete).

## Verification

- [x] `dotnet build` + `dotnet test` green (80 passing).
- [x] Console (fake harness): `images`/`devices` list; `create -n Test_AVD -k <pkg> -d pixel_6 --hw.ramSize
      4096` creates it with the override applied; `list` shows it; `delete` removes it from list + disk.
- [x] Catalog reader validated against a real SDK (mounted Windows `Sdk` from WSL): ~88 devices with specs,
      Pixel profiles resolved with screen size / density / RAM and min API.
- [x] Grouping visual refresh landed — the per-group expanders were replaced by a flush single-surface layout
      with M3 `Divider` section headers (`PropertyGroupView`), so view / edit / create share one render seam.
- [ ] **Pending visual check** of the wizard and delete confirm dialog under WSLg — the agent launches WSLg
      blind (GUI processes get SIGSTKFLT when backgrounded), so the on-screen render is the user's review.
