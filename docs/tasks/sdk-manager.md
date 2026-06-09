# SDK Manager UI

A GUI over `sdkmanager` to list, **download/install**, and remove Android SDK packages — chiefly **system
images**, so a user can create an emulator from an image they don't have yet. Today the create wizard
(0008) lists only *installed* images (`SystemImageScanner` over the SDK root); this milestone removes that
limitation.

## Scope

- **List packages** — `sdkmanager --list` (installed + available), parsed into a typed model
  (`SdkManagerOutputParser` — the deferred parser noted in 0008). Filter to system images for the create
  flow; show the broader catalog in a dedicated SDK Manager screen.
- **Install** — `sdkmanager "<package>"` with **progress** (it streams percentage lines) and a license
  prompt (`--licenses` / piping `y`). Needs a long-running, cancellable operation with live progress — the
  first feature to need that, so it likely extends `IProcessRunner` (streamed stdout) or adds a streaming
  variant.
- **Remove** — `sdkmanager --uninstall "<package>"`.
- **Wire into create** — the wizard's system-image step offers "download" for not-installed images, then
  refreshes `ListInstalledImagesAsync`.

## Notes

- `IAndroidSdk.SdkManagerPath` already resolves (`cmdline-tools/latest/bin/sdkmanager[.bat]`); on Windows it
  runs via `cmd /c` like `avdmanager` (see `ProcessRunner`).
- Downloads are large (hundreds of MB to GB) — progress + cancellation are required, not optional.

## Verification

- List shows installed vs available; install a small package end-to-end with visible progress; the new image
  then appears in the create wizard.
